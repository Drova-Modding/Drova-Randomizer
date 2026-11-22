using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.World.Patches;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Systems.Items;
using Il2Cpp;
using Il2CppCustomFramework.Localization;
using Il2CppDrova;
using Il2CppDrova.ABI;
using Il2CppDrova.ActorActions;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.Items;
using Il2CppDrova.Items.Stats;
using Il2CppDrova.StatusEffects;
using Il2CppDrova.Utilities.LazyLoading;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using static Il2CppCustomFramework.Localization.LocalizationDB;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// The rarities option. Enemies roll modifiers from the seed and their spawner guid, so an elite stays one
    /// across loads without save state. Weapons and armour exist as rarity variants, custom items the Modding
    /// API registers from a file this system writes. An armour variant carries its power in its own stat
    /// modifiers. A weapon variant's power and every proc act in the damage funnel, so they work for whoever
    /// wields it, and elite humans are equipped with variants.
    /// </summary>
    public sealed class Rarities : IRandoSystem
    {
        private const string LocaPath = "DrovaRandomizerRarities";
        private const string FileName = "DrovaRandomizer_rarities.json";
        private const float ScalePerRarity = 0.05f;
        private const int MaxComboSteps = 6;

        /// <summary>Chance by rarity that a killed elite drops a variant of its rarity.</summary>
        private static readonly float[] DropChance = [0f, 0.15f, 0.35f, 0.6f, 1f];

        private static readonly Dictionary<string, ItemAffix> AffixById = new(StringComparer.Ordinal);

        /// <summary>Procs run a frame late, because applying an effect inside the damage funnel re-enters it.</summary>
        private static readonly List<(Actor Source, Actor Target, string Proc)> PendingProcs = [];

        /// <summary>What each proc spell does: an ability spawned at the target, or else an effect on its caster.</summary>
        private static readonly Dictionary<string, (ABI_Ability? Ability, StatusEffect? Effect)> SpellEffects = new(StringComparer.Ordinal);

        private static readonly HashSet<int> SpellAbilityIds = [];

        /// <summary>The modifiers each living body was built with, by instance id.</summary>
        private static readonly Dictionary<int, EnemyAffix> AppliedByBody = [];

        private static readonly SeededRandom LootRandom = new((ulong)DateTime.UtcNow.Ticks);

        private static ulong? _seed;
        private static bool _locaWritten;

        /// <summary>The preset whose equipment is swapped for the actor starting up, and its own items.</summary>
        private static ActorEquipPreset? _swappedPreset;
        private static Item?[] _swappedItems = [];

#if DEBUG
        /// <summary>The last hit the damage hook handled, as "attacker>defender weapon factor". For the test commands.</summary>
        internal static string LastHit { get; private set; } = "none";
#endif

        /// <summary>How many item variants are registered.</summary>
        internal static int VariantCount => AffixById.Count;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            RarityPatches.Apply(harmony);
            GameEvents.OnPlayerKilledActor += OnPlayerKilledActor;
        }

        void IRandoSystem.OnSceneLoaded(string sceneName)
        {
            if (sceneName != SceneNames.MainMenu)
            {
                return;
            }

            AppliedByBody.Clear();

            try
            {
                RegisterVariants();
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] registering the item variants failed: " + e);
            }
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _seed = seed != null && seed.Options.On(RandoOptions.Rarities) ? seed.Number : null;

            // A body keeps what it was built with. One built under another run, or none, is rebuilt by its spawner.
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LazyActor>()))
            {
                LazyActor? lazy = found.TryCast<LazyActor>();
                Actor? body = lazy != null && lazy.gameObject.scene.IsValid() && lazy.IsLoaded ? lazy.Actor : null;
                if (body == null || !body.IsInitialized)
                {
                    continue;
                }

                EnemyAffix? wanted = TryGetElite(body, out EnemyAffix affix) ? affix : null;
                EnemyAffix? applied = AppliedByBody.TryGetValue(body.GetInstanceID(), out EnemyAffix built) ? built : null;
                if (wanted != applied)
                {
                    lazy!.Unload();
                }
            }
        }

        void IRandoSystem.Update()
        {
            if (PendingProcs.Count == 0)
            {
                return;
            }

            (Actor Source, Actor Target, string Proc)[] procs = [.. PendingProcs];
            PendingProcs.Clear();
            foreach ((Actor source, Actor target, string proc) in procs)
            {
                if (source && target)
                {
                    RunProc(source, target, proc);
                }
            }
        }

        /// <summary>A rarity variant of a found item, or the item itself for a common roll.</summary>
        internal static Item Upgrade(Item item)
        {
            if (_seed == null)
            {
                return item;
            }

            int rarity = Affixes.RollItemRarity(LootRandom);
            string id = Affixes.VariantId(item.ReadableId, rarity, LootRandom.Next(Affixes.Variants));
            return rarity > 0 && CustomItemSystem.Items.TryGetValue(id, out Item? variant) ? variant : item;
        }

        internal static void OnActorInitialised(Actor actor)
        {
            try
            {
                if (!TryGetElite(actor, out EnemyAffix affix) || actor.GetHealth() == null || actor.GetHealth().IsDead)
                {
                    return;
                }

                AppliedByBody[actor.GetInstanceID()] = affix;
                ActorBuffAccess.TryApply(actor, new ActorBuff(affix.Health, affix.Damage, affix.Armour, affix.MagicResistance));
                if (affix.Speed != 1f)
                {
                    GenericStatDesc speed = actor.GetMovementModule().GetMovementSpeedStat();
                    actor.GetStats().GetGenericStat(speed).CreateModifier(affix.Speed, ModifiableFloat.Mode.Mult);
                }
                actor.transform.localScale *= 1f + ScalePerRarity * affix.Rarity;
                actor.GetStats().SetExpOnDead(actor.GetStats().ExpOnDead * Affixes.ExperienceFactor(affix.Rarity));
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] Actor.FinishInit postfix failed: " + e);
            }
        }

        internal static void OnHit(HealthChange change, Health defenderHealth, ref HealthChange.HealthChangeCalc result)
        {
            try
            {
                if (result.ResultingDamage >= 0 || defenderHealth == null)
                {
                    return;
                }

                // Only a hit of an ability carries this, damage over time does not. A proc's own spell has no action
                // and must not proc again. The swing itself does not read the scaled base damage, so it is scaled here.
                ABI_InstanceInfo? info = change.AbiInfo;
                if (info == null || (info.RelatedAction == null && info.Ability != null && SpellAbilityIds.Contains(info.Ability.GetInstanceID())))
                {
                    return;
                }

                Actor? attacker = change.Source != null ? change.Source.TryCast<Actor>() : null;
                Actor? defender = defenderHealth.OwnerEntity != null ? defenderHealth.OwnerEntity.TryCast<Actor>() : null;
                if (attacker == null || defender == null || attacker == defender || InBrawl(attacker) || InBrawl(defender))
                {
                    return;
                }

                float factor = 1f;
                Item? weapon = info.RelatedAction != null ? info.RelatedAction.SourceItem : null;
                if (weapon != null && AffixById.TryGetValue(weapon.ReadableId, out ItemAffix weaponAffix))
                {
                    factor *= weaponAffix.Power;
                    QueueProc(attacker, defender, weaponAffix.Proc, Affixes.ProcChance);
                }
                else if (TryGetElite(attacker, out EnemyAffix elite))
                {
                    QueueProc(attacker, defender, elite.Proc, elite.ProcChance);
                }

                Actor.EquipmentModule? equipment = defender.GetEquipmentModule();
                Il2CppCollections.List<ActorEquipSlot>? slots = equipment != null ? equipment.AllItemSlotsRef : null;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    Item? worn = slots[i].IsInActiveSet() ? slots[i].GetItemOnSlot() : null;
                    if (worn != null && worn.Category.MainCategory == ItemMainCategory.Armor
                        && AffixById.TryGetValue(worn.ReadableId, out ItemAffix armourAffix))
                    {
                        QueueProc(defender, attacker, armourAffix.Proc, Affixes.ProcChance);
                    }
                }

#if DEBUG
                LastHit = attacker.name + ">" + defender.name + " " + (weapon != null ? weapon.ReadableId : "-") + " " + factor;
#endif
                if (factor != 1f)
                {
                    result.ResultingDamage = -Math.Max(1, (int)MathF.Round(-result.ResultingDamage * factor));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] damage postfix failed: " + e);
            }
        }

        internal static bool TryGetElite(Actor actor, out EnemyAffix affix)
        {
            affix = default;
            // Only spawner bodies have a guid that is the same on every load. The player and summons are not spawned that way.
            if (_seed is not ulong seed || actor == null || !actor.IsControlledViaLazy)
            {
                return false;
            }

            GuidComponent? guid = actor.GetGuidComponent();
            if (guid == null || string.IsNullOrEmpty(guid.GuidString))
            {
                return false;
            }

            affix = Affixes.RollEnemy(seed, guid.GuidString);
            return affix.Rarity > 0;
        }

        private static bool InBrawl(Actor actor)
        {
            return actor.CustomStats != null && actor.CustomStats.IsInBrawl;
        }

        private static void QueueProc(Actor source, Actor target, string proc, float chance)
        {
            if (proc != "" && Random.value < chance)
            {
                PendingProcs.Add((source, target, proc));
            }
        }

        internal static void RunProc(Actor source, Actor target, string proc)
        {
            try
            {
                switch (proc)
                {
                    case Affixes.Poison:
                        if (StatusEffectAccess.TryGet(StatusEffectGuids.Other.Poison, out StatusEffect? poison))
                        {
                            StatusEffectAccess.TryApply(target, poison);
                        }
                        break;
                    case Affixes.Bleed:
                        Health? health = target.GetHealth();
                        if (health != null)
                        {
                            StatusEffectAccess.TryApplyBleeding(target, Math.Max(1, health.MaxHealth / 10));
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

        /// <summary>
        /// Before a preset equips an elite: its equipment becomes the elite's rarity variants. The preset is shared
        /// by every actor using it, so <see cref="RestoreEquipPreset"/> puts its own items back right after.
        /// </summary>
        internal static void OnEquipPresetStarting(ActorEquipPreset preset, Actor actor)
        {
            try
            {
                RestoreEquipPreset();
                if (!TryGetElite(actor, out EnemyAffix affix) || preset._equipment == null)
                {
                    return;
                }

                int variant = (int)(Generator.Fnv(actor.GetGuidComponent().GuidString) % Affixes.Variants);
                Il2CppCollections.List<Item> equipment = preset._equipment;
                _swappedItems = new Item?[equipment.Count];
                _swappedPreset = preset;
                for (int i = 0; i < equipment.Count; i++)
                {
                    _swappedItems[i] = equipment[i];
                    if (equipment[i] != null && CustomItemSystem.Items.TryGetValue(
                        Affixes.VariantId(equipment[i].ReadableId, affix.Rarity, variant), out Item? rare))
                    {
                        equipment[i] = rare;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] equipping an elite failed: " + e);
            }
        }

        internal static void RestoreEquipPreset()
        {
            if (_swappedPreset != null)
            {
                for (int i = 0; i < _swappedItems.Length && i < _swappedPreset._equipment.Count; i++)
                {
                    _swappedPreset._equipment[i] = _swappedItems[i];
                }
            }
            _swappedPreset = null;
        }

        /// <summary>The tooltip's and the weapon-scaled spells' base damage of a weapon variant.</summary>
        internal static void OnBaseDamage(ActionStats stats, ref float result)
        {
            Item? item = stats._item;
            if (item != null && item.Category.MainCategory == ItemMainCategory.Weapon
                && AffixById.TryGetValue(item.ReadableId, out ItemAffix affix))
            {
                result *= affix.Power;
            }
        }

        /// <summary>
        /// Sets a variant's own stat modifiers and health bonus from its base item's, so reapplying never compounds.
        /// </summary>
        private static void ScaleArmourStats(Item item, Item baseItem, float power)
        {
            for (int i = 0; i < item._itemBehaviours.Count && i < baseItem._itemBehaviours.Count; i++)
            {
                ItemBhvr_EquipEffect_StatModification? stats = item._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_StatModification>();
                ItemBhvr_EquipEffect_StatModification? baseStats = baseItem._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_StatModification>();
                if (stats != null && baseStats != null)
                {
                    ScaleModifications(stats._equipModifications, baseStats._equipModifications, power);
                    ScaleModifications(stats._equipNPCModifications, baseStats._equipNPCModifications, power);
                }

                ItemBhvr_EquipEffect_MaxHealth? health = item._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_MaxHealth>();
                ItemBhvr_EquipEffect_MaxHealth? baseHealth = baseItem._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_MaxHealth>();
                if (health != null && baseHealth != null)
                {
                    health._maxHealth = (int)MathF.Ceiling(baseHealth._maxHealth * power);
                }
            }
        }

        private static void ScaleModifications(GenericStatModificationSet? set, GenericStatModificationSet? baseSet, float power)
        {
            Il2CppCollections.List<GenericStatModification>? values = set != null ? set._modifications : null;
            Il2CppCollections.List<GenericStatModification>? baseValues = baseSet != null ? baseSet._modifications : null;
            for (int i = 0; values != null && baseValues != null && i < values.Count && i < baseValues.Count; i++)
            {
                float value = baseValues[i].Value;
                if (baseValues[i].Mode != ModifiableFloat.Mode.Add || value <= 0f)
                {
                    continue;
                }

                // Whole points round up, so every rarity shows. Fractions are chances and stay fractions.
                values[i]._useContainer = false;
                values[i]._valueProp = value == MathF.Round(value) ? MathF.Ceiling(value * power) : value * power;
            }
        }

        private static string SpellName(string spellId)
        {
            SubDatabase_Item? database = ProviderAccess.ItemDatabase;
            Item? spell = database != null ? database.GetItemByReadableId(spellId) : null;
            return spell != null ? spell.GetLocalizedItemName() : spellId;
        }

        private static void OnPlayerKilledActor(Actor victim)
        {
            try
            {
                Actor? player = Core.Player;
                if (player == null || !TryGetElite(victim, out EnemyAffix affix) || Random.value >= DropChance[affix.Rarity])
                {
                    return;
                }

                List<string> pool = AffixById.Where(variant => variant.Value.Rarity == affix.Rarity).Select(variant => variant.Key).ToList();
                if (pool.Count == 0 || !CustomItemSystem.Items.TryGetValue(pool[LootRandom.Next(pool.Count)], out Item? drop))
                {
                    return;
                }

                player.GetInventory().AddItem(new ItemStack(drop, 1), true);
                ToastAccess.Show(drop.GetLocalizedItemName() + "  (elite drop)");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] elite drop failed: " + e);
            }
        }

        /// <summary>
        /// Writes the variants as a custom item file. The API then registers them at every main menu before a
        /// save loads and updates them in place, which registering from code each time would not.
        /// </summary>
        private static void RegisterVariants()
        {
            SubDatabase_Item? database = ProviderAccess.ItemDatabase;
            if (database == null)
            {
                return;
            }

            AffixById.Clear();
            List<CustomItemDefinition> definitions = [];
            List<LocalizationAccess.LocalizationEntry> loca = [];
            // The game ships a folder only for the languages it has.
            ELanguage[] languages = Enum.GetValues<ELanguage>()
                .Where(language => Directory.Exists(LocalizationDB.Instance.GetLanguageFolderPath(language))).ToArray();
            for (int i = 0; i < database._entries.Count; i++)
            {
                Item? item = database._entries[i].Item;
                if (item == null || item.ReadableId != database._entries[i].ReadableId || !CanHaveRarity(item))
                {
                    continue;
                }

                bool weapon = item.Category.MainCategory == ItemMainCategory.Weapon;
                for (int rarity = 1; rarity <= Affixes.MaxRarity; rarity++)
                {
                    for (int variant = 0; variant < Affixes.Variants; variant++)
                    {
                        string id = Affixes.VariantId(item.ReadableId, rarity, variant);
                        ItemAffix affix = Affixes.RollItem(id, rarity);
                        AffixById[id] = affix;
                        definitions.Add(new CustomItemDefinition
                        {
                            ReadableId = id,
                            Base = item.ReadableId,
                            LocaPath = LocaPath,
                            BuyValue = item.BuyValue * (1 + rarity),
                            SellValue = item.SellValue * (1 + rarity),
                        });
                        foreach (ELanguage language in affix.Proc != "" ? languages : [])
                        {
                            loca.Add(new LocalizationAccess.LocalizationEntry(id + "_description", Describe(affix, weapon), language));
                        }
                    }
                }
            }

            string path = Path.Combine(CustomItemSystem.GetFolderPath(), FileName);
            string json = JsonSerializer.Serialize(new CustomItemConfigFile { Items = definitions });
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            {
                File.WriteAllText(path, json);
                CustomItemSystem.Reload();
            }

            foreach (KeyValuePair<string, ItemAffix> variant in AffixById)
            {
                if (CustomItemSystem.Items.TryGetValue(variant.Key, out Item? item))
                {
                    // The name stays the base item's in every language. The rarity colours it and the inventory border.
                    // The description is the proc's, or the base item's without one.
                    item._copyName = true;
                    item._copyDescription = variant.Value.Proc == "";
                    if (variant.Value.Proc != "")
                    {
                        // Some base items carry a description of their own that would win over the proc's.
                        item._customLocaDesc = null;
                    }
                    item._rarity = (Item.ItemRarity)Math.Max((int)item._rarity, variant.Value.Rarity);
                    if (item._copyLocaItem != null && item.Category.MainCategory == ItemMainCategory.Armor)
                    {
                        ScaleArmourStats(item, item._copyLocaItem, variant.Value.Power);
                    }
                }
            }

            // The game caches an item's tooltip stats by guid.
            ItemStatsFactory.ClearCache();
            if (!_locaWritten)
            {
                LocalizationAccess.CreateLocalizationEntries(loca, LocaPath);
                _locaWritten = true;
            }
            MelonLogger.Msg("[Rando rarities] " + AffixById.Count + " item variants.");
        }

        private static bool CanHaveRarity(Item item)
        {
            return item.Category.MainCategory is ItemMainCategory.Weapon or ItemMainCategory.Armor
                && item.Category.SubCategory is not (ItemSubCategory.Weapon_Bow_Ammo or ItemSubCategory.Weapon_Slingshot_Ammo)
                && item.BuyValue > 0 && item.SellValue > 0 && !item.IsInQuestCategory
                && !CustomItemSystem.Items.ContainsKey(item.ReadableId);
        }

        private static string Describe(ItemAffix affix, bool weapon)
        {
            string effect = affix.Proc switch
            {
                Affixes.Poison => "poisons",
                Affixes.Bleed => "causes bleeding",
                _ => "casts " + SpellName(affix.Proc),
            };
            return MathF.Round(Affixes.ProcChance * 100f) + "% chance " + (weapon ? "on hit" : "when hit") + ": " + effect + ".";
        }
    }
}
