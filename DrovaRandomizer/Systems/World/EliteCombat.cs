using System.Diagnostics;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Systems.Coop;
using Il2CppDrova;
using Il2CppDrova.ABI;
using Il2CppDrova.ActorActions;
using Il2CppDrova.Items;
using Il2CppDrova.StatusEffects;
using MelonLoader;
using UnityEngine;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Random = UnityEngine.Random;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>What elites and rarity items do in a fight: weapon power, hit effects, leech, thorns and the elite specials.</summary>
    internal static class EliteCombat
    {
        private const int MaxComboSteps = 6;

        private const float SummonInterval = 20f;

        /// <summary>A summoner only calls helpers while the player is this close, in world units.</summary>
        private const float SummonRange = 600f;

        /// <summary>Share of its maximum health a regenerating elite heals every second.</summary>
        private const float RegenShare = 0.015f;

        /// <summary>Share of the damage a vampiric elite heals.</summary>
        private const float VampiricShare = 0.3f;

        /// <summary>Share of the damage a thorned elite reflects.</summary>
        private const float EliteThornsShare = 0.25f;

        /// <summary>Hit effects run a frame late, because applying an effect inside the damage funnel re-enters it.</summary>
        private static readonly List<Action> PendingEffects = [];

        /// <summary>Summoners and regenerating elites that took damage, and when each acts next.</summary>
        private static readonly List<Engagement> Engaged = [];

        private static readonly List<CoopPlayer> PlayerBuffer = [];

        /// <summary>What each proc spell does: an ability spawned at the target, or else an effect on its caster.</summary>
        private static readonly Dictionary<string, (ABI_Ability? Ability, StatusEffect? Effect)> SpellEffects = new(StringComparer.Ordinal);

        private static readonly HashSet<int> SpellAbilityIds = [];

#if DEBUG
        /// <summary>The last hit the damage hook handled, as "attacker>defender weapon factor". For the test commands.</summary>
        internal static string LastHit { get; private set; } = "none";

        internal static int EngagedCount => Engaged.Count;

        /// <summary>The last hits of elites with a proc as "weapon:proc:queued", newest last. For the test commands.</summary>
        internal static readonly List<string> EliteHitLog = [];

        /// <summary>The last procs that ran as "source>target:proc:what it did", newest last. For the test commands.</summary>
        internal static readonly List<string> ProcLog = [];
#endif

        /// <summary>Runs the hit effects queued last frame and lets engaged elites act.</summary>
        internal static void Update()
        {
            TickEngaged();
            if (PendingEffects.Count == 0)
            {
                return;
            }

            Action[] effects = [.. PendingEffects];
            PendingEffects.Clear();
            foreach (Action effect in effects)
            {
                effect();
            }
        }

        internal static void Clear()
        {
            Engaged.Clear();
        }

        /// <summary>Runs an effect next frame, outside the damage funnel.</summary>
        internal static void Later(Action effect)
        {
            PendingEffects.Add(effect);
        }

        internal static void OnHit(HealthChange change, Health defenderHealth, ref HealthChange.HealthChangeCalc result)
        {
            try
            {
                if (result.ResultingDamage >= 0 || defenderHealth == null)
                {
                    return;
                }

                Actor? defender = defenderHealth.OwnerEntity != null ? defenderHealth.OwnerEntity.TryCast<Actor>() : null;
                // Before the ability check: a teammate's blow reaches the machine that owns the creature without one.
                if (defender != null && CoopAccess.IsWorldAuthority)
                {
                    Engage(defender);
                }

                // Only a hit of an ability carries this, damage over time does not. A proc's own spell has no action
                // and must not proc again. The swing itself does not read the scaled base damage, so it is scaled here.
                ABI_InstanceInfo? info = change.AbiInfo;
                if (info == null || (info.RelatedAction == null && info.Ability != null && SpellAbilityIds.Contains(info.Ability.GetInstanceID())))
                {
                    return;
                }

                Actor? attacker = change.Source != null ? change.Source.TryCast<Actor>() : null;
                if (attacker == null || defender == null || attacker == defender || InBrawl(attacker) || InBrawl(defender))
                {
                    return;
                }

                Item? weapon = info.RelatedAction != null ? info.RelatedAction.SourceItem : null;
                ItemAffix? weaponAffix = weapon != null && RarityItems.TryGetAffix(weapon.ReadableId, out ItemAffix found) ? found : null;
                float factor = weaponAffix?.Power ?? 1f;
#if DEBUG
                LastHit = attacker.name + ">" + defender.name + " " + (weapon != null ? weapon.ReadableId : "-") + " " + factor;
#endif
                if (factor != 1f)
                {
                    result.ResultingDamage = -Math.Max(1, (int)MathF.Round(-result.ResultingDamage * factor));
                    // Co-op reports a client's blow on a creature by this figure, and the host applies it without the weapon.
                    change._potentialChange = (int)MathF.Round(change._potentialChange * factor);
                }

                int dealt = -result.ResultingDamage;
                if (weaponAffix is { } wielded)
                {
                    QueueProc(attacker, defender, wielded.Proc, Affixes.ProcChance);
                    if (wielded.Bonus == Affixes.Leech)
                    {
                        QueueHeal(attacker, Share(dealt, wielded.BonusAmount / 100f), Affixes.Leech);
                    }
                }
                if (Rarities.TryGetElite(attacker, out EnemyAffix elite))
                {
                    bool queued = QueueProc(attacker, defender, elite.Proc, elite.ProcChance);
                    if (elite.Special == Affixes.Vampiric)
                    {
                        QueueHeal(attacker, Share(dealt, VampiricShare), Affixes.Vampiric);
                    }
                    else if (elite.Special == Affixes.Jailer)
                    {
                        QueueProc(attacker, defender, Affixes.JailerSpell, Affixes.ProcChance);
                    }
#if DEBUG
                    if (elite.Proc != "")
                    {
                        EliteHitLog.Add((weapon != null ? weapon.ReadableId : "-") + ":" + elite.Proc + ":" + (queued ? 1 : 0));
                        if (EliteHitLog.Count > 12)
                        {
                            EliteHitLog.RemoveAt(0);
                        }
                    }
#endif
                }

                int setLeech = RarityItems.GearOf(attacker).SetLeech;
                if (setLeech > 0)
                {
                    QueueHeal(attacker, Share(dealt, setLeech / 100f), Affixes.Leech);
                }

                int thorns = Rarities.TryGetElite(defender, out EnemyAffix hitElite) && hitElite.Special == Affixes.Thorns ? Share(dealt, EliteThornsShare) : 0;
                RarityItems.GearEffects gear = RarityItems.GearOf(defender);
                thorns += gear.SetThorns > 0 ? Share(dealt, gear.SetThorns / 100f) : 0;
                foreach (ItemAffix armourAffix in gear.Armour)
                {
                    QueueProc(defender, attacker, armourAffix.Proc, Affixes.ProcChance);
                    thorns += armourAffix.Bonus == Affixes.Thorns ? Share(dealt, armourAffix.BonusAmount / 100f) : 0;
                }
                if (thorns > 0)
                {
                    QueueThorns(defender, attacker, thorns);
                }

                Actor? player = Core.Player;
                Actor? opponent = player == null ? null : attacker == player ? defender : defender == player ? attacker : null;
                if (opponent != null && Rarities.TryGetElite(opponent, out EnemyAffix shown))
                {
                    EliteLabels.OnFought(opponent, shown);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] damage postfix failed: " + e);
            }
        }

        private static bool InBrawl(Actor actor)
        {
            return actor.CustomStats != null && actor.CustomStats.IsInBrawl;
        }

        /// <summary>
        /// Whether this machine decides the actor's health: the local player's own, or anything but a teammate's body on
        /// the machine that owns the world. Heals and thorns carry no source, so co-op never reports them.
        /// </summary>
        private static bool OwnsHealth(Actor actor)
        {
            return actor == Core.Player || (CoopAccess.IsWorldAuthority && !CoopAccess.IsRemotePlayer(actor));
        }

        /// <summary>
        /// Whether a proc runs here. Its damage counts as the source's, which co-op reports from a client, so it runs where
        /// either side is the local player, and on the world's owner when no teammate's body takes part.
        /// </summary>
        private static bool ProcRunsHere(Actor source, Actor target)
        {
            return source == Core.Player || target == Core.Player
                || (CoopAccess.IsWorldAuthority && !CoopAccess.IsRemotePlayer(source) && !CoopAccess.IsRemotePlayer(target));
        }

        private static bool QueueProc(Actor source, Actor target, string proc, float chance)
        {
            if (proc == "" || !ProcRunsHere(source, target) || Random.value >= chance)
            {
                return false;
            }
            PendingEffects.Add(() =>
            {
                if (source && target)
                {
                    RunProc(source, target, proc);
                }
            });
            return true;
        }

        private static void QueueHeal(Actor actor, int amount, string kind)
        {
            if (!OwnsHealth(actor))
            {
                return;
            }
            PendingEffects.Add(() =>
            {
                if (actor && DamageAccess.TryHeal(actor, amount))
                {
                    LogProc(actor.name + ">" + actor.name + ":" + kind + ":" + amount);
                }
            });
        }

        /// <summary>
        /// Pure damage back at the attacker. Without a source the game adds no offensive stats to the figure,
        /// so a kill by thorns gives no kill credit.
        /// </summary>
        private static void QueueThorns(Actor source, Actor attacker, int amount)
        {
            if (!OwnsHealth(attacker))
            {
                return;
            }
            PendingEffects.Add(() =>
            {
                if (attacker && DamageAccess.TryGetPureDamageType(out DamageStatDesc? pure) && DamageAccess.TryApplyDamage(attacker, amount, pure))
                {
                    LogProc((source ? source.name : "-") + ">" + attacker.name + ":" + Affixes.Thorns + ":" + amount);
                }
            });
        }

        /// <summary>A share of some damage, at least one point.</summary>
        private static int Share(int damage, float share)
        {
            return Math.Max(1, (int)MathF.Round(damage * share));
        }

        [Conditional("DEBUG")]
        private static void LogProc(string entry)
        {
#if DEBUG
            ProcLog.Add(entry);
            if (ProcLog.Count > 30)
            {
                ProcLog.RemoveAt(0);
            }
#endif
        }

        /// <summary>
        /// A summoner or regenerating elite starts acting once it takes damage. Called only where the creatures' health is
        /// decided, so summons and heals exist once in co-op.
        /// </summary>
        private static void Engage(Actor elite)
        {
            if (!Rarities.TryGetElite(elite, out EnemyAffix affix) || affix.Special is not (Affixes.Summoner or Affixes.Regenerating)
                || InBrawl(elite) || Engaged.Any(engaged => engaged.Actor == elite))
            {
                return;
            }

            Engaged.Add(affix.Special == Affixes.Summoner
                ? new Engagement(elite, affix.Special, Time.time + SummonInterval, Affixes.SummonCasts(affix.Rarity))
                : new Engagement(elite, affix.Special, Time.time + 1f, -1));
        }

        /// <summary>The living player body nearest to a point within a range, the local one or a teammate's.</summary>
        private static Actor? NearestPlayer(Vector2 at, float range)
        {
            Actor? nearest = null;
            float best = range;
            CoopAccess.GetPlayers(PlayerBuffer);
            foreach (CoopPlayer player in PlayerBuffer)
            {
                Health? health = CoopAccess.TryGetPlayerActor(player.Id, out Actor? body) ? body.GetHealth() : null;
                float distance = health != null && !health.IsDead ? Vector2.Distance(body!.GetCenterPosition(), at) : float.MaxValue;
                if (distance < best)
                {
                    nearest = body;
                    best = distance;
                }
            }
            return nearest;
        }

        /// <summary>Summoners call helpers and regenerating elites heal, until they die or unload.</summary>
        private static void TickEngaged()
        {
            for (int i = Engaged.Count - 1; i >= 0; i--)
            {
                Engagement engaged = Engaged[i];
                Health? health = engaged.Actor ? engaged.Actor.GetHealth() : null;
                if (health == null || health.IsDead || engaged.SummonsLeft == 0)
                {
                    Engaged.RemoveAt(i);
                    continue;
                }
                if (Time.time < engaged.Next)
                {
                    continue;
                }

                Actor actor = engaged.Actor;
                if (engaged.Special == Affixes.Summoner)
                {
                    Actor? target = NearestPlayer(actor.GetCenterPosition(), SummonRange);
                    Engaged[i] = engaged with { Next = Time.time + SummonInterval };
                    if (target != null)
                    {
                        Engaged[i] = Engaged[i] with { SummonsLeft = engaged.SummonsLeft - 1 };
                        RunProc(actor, target, Affixes.SummonSpell(actor.GetGuidComponent().GuidString));
                    }
                    continue;
                }

                Engaged[i] = engaged with { Next = Time.time + 1f };
                if (health.CurHealth < health.MaxHealth)
                {
                    QueueHeal(actor, Share(health.MaxHealth, RegenShare), Affixes.Regenerating);
                }
            }
        }

        internal static void RunProc(Actor source, Actor target, string proc)
        {
            LogProc(source.name + ">" + target.name + ":" + proc + ":" + (proc is Affixes.Poison or Affixes.Bleed ? "status"
                : GetSpellEffect(proc) is { Ability: not null } ? "ability" : GetSpellEffect(proc).Effect != null ? "effect" : "nothing"));
            try
            {
                switch (proc)
                {
                    // On the source's behalf: its ticks count as the source's damage, for kill credit and the co-op report.
                    case Affixes.Poison:
                        if (StatusEffectAccess.TryGet(StatusEffectGuids.Other.Poison, out StatusEffect? poison))
                        {
                            StatusEffectAccess.TryApplyFrom(target, poison, source);
                        }
                        break;
                    case Affixes.Bleed:
                        Health? health = target.GetHealth();
                        if (health != null)
                        {
                            StatusEffectAccess.TryApplyBleedingFrom(target, Math.Max(1, health.MaxHealth / 10), source);
                        }
                        break;
                    default:
                        (ABI_Ability? ability, StatusEffect? effect) = GetSpellEffect(proc);
                        if (ability != null)
                        {
                            Vector2 from = source.GetCenterPosition();
                            Vector2 to = target.GetCenterPosition();
                            ability.SpawnAbility(source, from, to, (to - from).normalized, null, null, target, null);
                        }
                        else if (effect != null)
                        {
                            StatusEffectAccess.TryApply(source, effect);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] proc " + proc + " failed: " + e);
            }
        }

        /// <summary>
        /// What casting a spell item does, read off its action prefab and the combo steps behind it. An ability
        /// wins over an effect, so a spell with both hits the target.
        /// </summary>
        internal static (ABI_Ability? Ability, StatusEffect? Effect) GetSpellEffect(string spellId)
        {
            if (SpellEffects.TryGetValue(spellId, out (ABI_Ability? Ability, StatusEffect? Effect) known))
            {
                return known;
            }

            ABI_Ability? ability = null;
            StatusEffect? effect = null;
            SubDatabase_Item? database = ProviderAccess.ItemDatabase;
            Item? spell = database != null ? database.GetItemByReadableId(spellId) : null;
            for (int i = 0; spell != null && i < spell._itemBehaviours.Count; i++)
            {
                ItemBhvr_ActorAction_FromPrefab? action = spell._itemBehaviours[i].TryCast<ItemBhvr_ActorAction_FromPrefab>();
                AA_ABase? prefab = action != null ? action._actionPrefab : null;
                for (int step = 0; prefab != null && step < MaxComboSteps; step++)
                {
                    ability ??= FindAbility(prefab);
                    effect ??= prefab.GetComponentsInChildren<AA_Trigger_StatusEffect>(true)
                        .Select(trigger => trigger._statusEffect).FirstOrDefault(found => found != null);
                    AA_Atk_BhvrChanges? changes = prefab.GetComponent<AA_Atk_BhvrChanges>();
                    prefab = changes != null && changes._hasCombo ? changes._comboPrefab : null;
                }
            }

            if (ability != null)
            {
                SpellAbilityIds.Add(ability.GetInstanceID());
            }
            SpellEffects[spellId] = (ability, effect);
            return (ability, effect);
        }

        /// <summary>The first ability of any component of an action prefab that spawns one.</summary>
        private static ABI_Ability? FindAbility(AA_ABase prefab)
        {
            return prefab.GetComponentsInChildren<AA_Trigger_Ability>(true)
                    .Select(trigger => FirstAbility(trigger._chargeLevels)).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Trigger_AbilityOnTarget>(true)
                    .Select(trigger => FirstAbility(trigger._chargeLevels)).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Trigger_AimedAbility>(true)
                    .Select(trigger => trigger._ability).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Trigger_RandAbility>(true)
                    .Select(trigger => trigger._chargeLevels != null && trigger._chargeLevels.Count > 0
                        ? FirstAbility(trigger._chargeLevels[0].Abilities) : null).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Trigger_FixPositions>(true)
                    .Select(trigger => trigger._ability).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Atk_Channel_AbilitySpawn>(true)
                    .Select(trigger => trigger._ability).FirstOrDefault(found => found != null)
                ?? prefab.GetComponentsInChildren<AA_Atk_Channel_TimeSpawnAbility>(true)
                    .Select(trigger => trigger._ability).FirstOrDefault(found => found != null);
        }

        private static ABI_Ability? FirstAbility(Il2CppCollections.List<ABI_Ability>? chargeLevels)
        {
            return chargeLevels != null && chargeLevels.Count > 0 && chargeLevels[0] != null ? chargeLevels[0] : null;
        }

        /// <summary>A summoner or regenerating elite the player fought. <c>SummonsLeft</c> is -1 for a regenerating one.</summary>
        private readonly record struct Engagement(Actor Actor, string Special, float Next, int SummonsLeft);
    }
}
