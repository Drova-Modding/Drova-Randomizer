using System.Globalization;
using DrovaRandomizer.Data;
using DrovaRandomizer.Systems;
using DrovaRandomizer.Systems.Checks;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using DrovaRandomizer.Systems.Talents;
using DrovaRandomizer.Systems.World;
using Drova_Modding_API;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Talents;
using Drova_Modding_API.Systems.Testing;
using Il2Cpp;
using Il2CppDrova;
using Il2CppDrova.Brawl.Modules;
using Il2CppDrova.GUI;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.InteractionSystem;
using Il2CppDrova.Items;
using Il2CppDrova.Items.Stats;
using Il2CppDrova.Talent;
using Il2CppDrova.Utilities.LazyLoading;
using Il2CppInterop.Runtime;
using Il2CppOpenWorldSystem;
using Il2CppPathfinding;
using Il2CppTradingSystem.ItemContainers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;

namespace DrovaRandomizer.Access
{
    /// <summary>The randomizer's TestAgent commands and state. Inert without --agent-port.</summary>
    internal sealed class RandoTestCommands : IRandoSystem
    {
        private const float ReachTolerance = 40f;

        private static Actor Player => Core.Player ?? throw new InvalidOperationException("no player yet");

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            TestAgent.RegisterState(() => "rando=" + (Randomizer.Active ? Randomizer.Seed.Number.ToString() : "none")
                + " checked=" + Randomizer.CheckedCount + " pending=" + Randomizer.State.PendingGrants.Count
                + " options=" + (Randomizer.Active ? Randomizer.Seed.Options.Hash() : "-") + " monsters=" + (Randomizer.Seed?.Monsters.Count ?? 0)
                + " factionwarned=" + (Randomizer.State.FactionWarned ? 1 : 0) + " cc=" + (Core.Ui.InCharacterCreation ? 1 : 0));
            TestAgent.RegisterCommand("rando_gate", GateCommand);
            TestAgent.RegisterCommand("rando_f3", tab => Core.Ui.Open(tab));
            TestAgent.RegisterCommand("rando_hooks", HooksCommand);
            TestAgent.RegisterCommand("rando_actoritems", ActorItemsCommand);
            TestAgent.RegisterCommand("rando_brawl", BrawlCommand);
            TestAgent.RegisterCommand("rando_mug", MugCommand);

            TestAgent.RegisterCommand("rando_teach", TeachCommand);
            TestAgent.RegisterCommand("rando_learned", _ => string.Join(",", TalentTable.Teachable.Where(name =>
                TalentContainerDatabase.TryGetTalent(name, out TalentContainer? talent) && Player.TalentActorModule.HasTalentLearned(talent.GUID))));
            TestAgent.RegisterCommand("rando_learn", name =>
            {
                TalentContainerDatabase.TryGetTalent(name.Trim(), out TalentContainer? talent);
                PlayerAttributeStats stats = Player.GetStats().TryCast<PlayerAttributeStats>()!;
                stats.GiveLearningPoint((talent ?? throw new InvalidOperationException("no talent " + name)).TalentCostLearnPoints);
                Player.TalentActorModule.LearnTalent(talent);
                return "learned, " + stats.GetAvailableTalentPoints() + " LP left";
            });
            TestAgent.RegisterCommand("rando_taughtby", name =>
            {
                TalentContainerDatabase.TryGetTalent(name.Trim(), out TalentContainer? talent);
                List<string> teachers = [];
                foreach (EntityInfo teacher in (talent ?? throw new InvalidOperationException("no talent " + name))._teachers)
                {
                    teachers.Add(teacher.name);
                }
                return string.Join(",", teachers.OrderBy(t => t, StringComparer.Ordinal));
            });

            TestAgent.RegisterCommand("rando_window", WindowCommand);
            TestAgent.RegisterCommand("rando_teachers", _ => string.Join(",", LoadedTeachers().Select(teacher =>
                teacher.name + "=" + teacher.GetLearnableTalents(Player).Count)));
            TestAgent.RegisterCommand("rando_openlearn", OpenLearnCommand);
            TestAgent.RegisterCommand("rando_clicklearn", ClickLearnCommand);
            TestAgent.RegisterCommand("rando_ui", _ =>
            {
                List<string> parts = [];
                for (Transform node = GUIGameHandler.TryGet().GetLearnGUI(false, true)._talentButtons[0].transform; node; node = node.parent)
                {
                    RectTransform? rect = node.TryCast<RectTransform>();
                    parts.Add(node.name + ":" + (rect != null && rect ? rect.rect.width.ToString("F0") + "/" + rect.sizeDelta.x.ToString("F0") : "-")
                        + (node.GetComponent<LayoutGroup>() ? "[layout]" : "") + (node.GetComponent<ContentSizeFitter>() ? "[fitter]" : ""));
                }
                return string.Join(" < ", parts);
            });
            TestAgent.RegisterCommand("rando_hoverlearn", index =>
            {
                GUI_Talent_LearnButton button = VisibleButtons(GUIGameHandler.TryGet().GetLearnGUI(false, true))[int.Parse(index)];
                button._perkButton.OnPointerEnter(new PointerEventData(EventSystem.current));
                return button._perkButton.GetTalentContainer().GetTalentName();
            });
            TestAgent.RegisterCommand("rando_known", _ => string.Join(",", TalentTable.Teachable.Where(name =>
                TalentContainerDatabase.TryGetTalent(name, out TalentContainer? talent) && Player.TalentActorModule.IsTalentKnown(talent.GUID))));

            TestAgent.RegisterCommand("rando_prefab", guid => FindLoaded(guid)._actorReference.AssetGUID);
            TestAgent.RegisterCommand("rando_alraunes", _ => string.Join(";", AlrauneShuffler.Plants.Select(plant =>
                plant.Key + "@" + Format(plant.Value.position))));
            // "rando_pickup x,y": the started pickup within 40 units, as "icon <sprite>", "vanilla" or "none".
            TestAgent.RegisterCommand("rando_pickup", at =>
            {
                string[] xy = at.Trim().Split(',');
                Vector2 spot = new(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
                PickupSprites.Pickup? near = PickupSprites.Pickups.Values.FirstOrDefault(p => p.Visual
                    && Vector2.Distance(p.Visual.transform.position, spot) < 40f);
                return near == null ? "none" : near.Visual.sprite == near.Vanilla ? "vanilla" : "icon " + near.Visual.sprite.name;
            });
            TestAgent.RegisterCommand("rando_reach", ReachCommand);
            // "rando_proc <spawner guid> <poison|bleed|spell item id>": the player's proc on that spawner's body.
            TestAgent.RegisterCommand("rando_proc", args =>
            {
                string[] parts = args.Trim().Split(' ');
                EliteCombat.RunProc(Player, FindLoaded(parts[0]).Actor, parts[1]);
                return parts[1];
            });
            // "rando_spell <spell item id>": what a proc of that spell does.
            TestAgent.RegisterCommand("rando_spell", id =>
            {
                (Il2CppDrova.ABI.ABI_Ability? ability, Il2CppDrova.StatusEffects.StatusEffect? effect) = EliteCombat.GetSpellEffect(id.Trim());
                return ability != null ? "ability " + ability.name : effect != null ? "effect " + effect.name : "none";
            });
            // "rando_maxhealth <n>": n more maximum health at once, without a reward toast per point.
            TestAgent.RegisterCommand("rando_maxhealth", amount =>
            {
                Player.GetHealth().AddRuntimeMaxHealth(int.Parse(amount.Trim(), CultureInfo.InvariantCulture));
                return Player.GetHealth().MaxHealth.ToString(CultureInfo.InvariantCulture);
            });
            // "rando_grant <reward id> <n>": queues a reward n times, as a check would.
            TestAgent.RegisterCommand("rando_grant", args =>
            {
                string[] parts = args.Trim().Split(' ');
                Randomizer.State.PendingGrants.AddRange(Enumerable.Repeat(parts[0], int.Parse(parts[1])));
                return parts[1];
            });
            // "rando_tooltip <item id>": opens the item's tooltip and answers its name and description.
            TestAgent.RegisterCommand("rando_tooltip", id =>
            {
                Item item = ProviderAccess.ItemDatabase!.GetItemByReadableId(id.Trim());
                // The controller reuses an open tooltip window, which only fits an item of the same kind.
                if (GUIGameHandler.TryGet(out GUIGameHandler? gui) && gui != null)
                {
                    gui.GetToolTipController().HideToolTip();
                }
                Il2CppDrova.GUI.ToolTip.GUITooltipBuilder.BuildItemTooltip(item, true);
                return item.GetLocalizedItemName() + " | " + item.GetLocalizedItemDescription();
            });
            // "rando_eliteseed <guid> <rarity> [<guid> <least rarity> [always]]": the first seed that rolls those spawners so,
            // with "always" the second one poisoning its target on every hit.
            TestAgent.RegisterCommand("rando_eliteseed", args =>
            {
                string[] parts = args.Trim().Split(' ');
                EnemyAffix Roll(ulong seed, string guid) => Affixes.RollEnemy(seed, guid, Affixes.NormalFrequency);
                for (ulong seed = 1; seed < 1000000; seed++)
                {
                    if (Roll(seed, parts[0]).Rarity == int.Parse(parts[1])
                        && (parts.Length < 4 || Roll(seed, parts[2]).Rarity >= int.Parse(parts[3]))
                        && (parts.Length < 5 || Roll(seed, parts[2]) is { Proc: Affixes.Poison, ProcChance: 1f }))
                    {
                        return seed + " " + Roll(seed, parts[0]) + (parts.Length < 4 ? "" : " " + Roll(seed, parts[2]));
                    }
                }
                return "none";
            });
            // "rando_specialseed <guid> <special> [rarity]": the first seed that gives that spawner the special, at that rarity if given.
            TestAgent.RegisterCommand("rando_specialseed", args =>
            {
                string[] parts = args.Trim().Split(' ');
                for (ulong seed = 1; seed < 1000000; seed++)
                {
                    EnemyAffix affix = Affixes.RollEnemy(seed, parts[0], Affixes.NormalFrequency);
                    if (affix.Special == parts[1] && (parts.Length < 3 || affix.Rarity == int.Parse(parts[2])))
                    {
                        return seed + " " + affix;
                    }
                }
                return "none";
            });
            // "rando_elite <spawner guid>": the loaded body's modifiers and trait words, or "common".
            TestAgent.RegisterCommand("rando_elite", guid =>
            {
                Actor? body = FindLoaded(guid.Trim()).Actor;
                return body != null && Rarities.TryGetElite(body, out EnemyAffix affix) ? affix + " " + string.Join("+", Affixes.Traits(affix)) : "common";
            });
            // "rando_label <spawner guid>": the label text the loaded elite gets once fought, lines joined by " / ".
            TestAgent.RegisterCommand("rando_label", guid =>
            {
                Actor? body = FindLoaded(guid.Trim()).Actor;
                return body != null && Rarities.TryGetElite(body, out EnemyAffix affix) ? EliteLabels.LabelText(body, affix).Replace("\n", " / ") : "common";
            });
            // "rando_affix <item id>": the modifiers of a variant or unique.
            TestAgent.RegisterCommand("rando_affix", id => RarityItems.AffixOf(id.Trim())?.ToString() ?? "none");
            // "rando_sets": per set the pieces the player wears and the count its tooltips show, as worn=a,b,c shown=a,b,c.
            TestAgent.RegisterCommand("rando_sets", _ => RarityItems.SetState());
            // "rando_gear": what the player's gear does in a fight, as the hit hook reads it.
            TestAgent.RegisterCommand("rando_gear", _ =>
            {
                RarityItems.GearEffects gear = RarityItems.GearOf(Player);
                return "leech=" + gear.SetLeech + " thorns=" + gear.SetThorns + " armour=" + gear.Armour.Length;
            });
            // "rando_weaponset": switches to the player's other weapon set as its button does, and answers the active one.
            TestAgent.RegisterCommand("rando_weaponset", _ =>
            {
                Actor.EquipmentModule equipment = Player.GetEquipmentModule();
                equipment.TrySelectEquipmentSet(equipment.ActiveSetIndex == 0 ? 1 : 0);
                return equipment.ActiveSetIndex.ToString(CultureInfo.InvariantCulture);
            });
            // "rando_drop": the last elite drop as gold=N tier=N items=id+id.
            TestAgent.RegisterCommand("rando_drop", _ => EliteDrops.LastDrop);
            // "rando_pickdrop <rarity> <tier> <n>": n elite drop picks as "uniques=N rarities=r,r tiers=t,t".
            TestAgent.RegisterCommand("rando_pickdrop", args =>
            {
                int[] parts = args.Trim().Split(' ').Select(int.Parse).ToArray();
                List<Item> picks = Enumerable.Range(0, parts[2]).Select(_ => RarityItems.PickDrop(parts[0], parts[1])).OfType<Item>().ToList();
                List<ItemAffix> affixes = picks.Select(item => RarityItems.AffixOf(item.ReadableId)).OfType<ItemAffix>().ToList();
                return "uniques=" + picks.Count(item => item.ReadableId.EndsWith("_unique", StringComparison.Ordinal))
                    + " rarities=" + string.Join(",", affixes.Select(affix => affix.Rarity).Distinct().OrderBy(value => value))
                    + " tiers=" + string.Join(",", picks.Select(item => RarityItems.TierOf(item.ReadableId)).Distinct().OrderBy(value => value));
            });
            // "rando_procs": the last procs that ran as source>target:proc:what it did.
            TestAgent.RegisterCommand("rando_procs", _ => string.Join(",", EliteCombat.ProcLog));
            // "rando_armour": the player's armour and magic resistance.
            TestAgent.RegisterCommand("rando_armour", _ =>
            {
                StatContainer stats = GlobalStatsSettings.GetStatContainer();
                AttributeStats own = Player.GetStats();
                return "armour=" + own.GetGenericStat(stats.PhysicalResistanceStat).GetIntValue()
                    + " magic=" + own.GetGenericStat(stats.MagicalResistanceStat).GetIntValue()
                    + " strength=" + own.GetGenericStat(stats.StrengthStat).GetIntValue()
                    + " dexterity=" + own.GetGenericStat(stats.DexStat).GetIntValue()
                    + " mind=" + own.GetGenericStat(stats.MindStat).GetIntValue()
                    + " crit=" + own.GetGenericStat(stats.CritChanceStat).GetFloatValue().ToString("F2", CultureInfo.InvariantCulture)
                    + " maxhealth=" + Player.GetHealth().MaxHealth + " ";
            });
            // "rando_worn": each of the player's slots with an item, as id:main category:in active set:proc:effects applied.
            TestAgent.RegisterCommand("rando_worn", _ =>
            {
                List<string> worn = [];
                Il2CppCollections.List<ActorEquipSlot> slots = Player.GetEquipmentModule().AllItemSlotsRef;
                for (int i = 0; i < slots.Count; i++)
                {
                    Item? item = slots[i].GetItemOnSlot();
                    if (item != null)
                    {
                        worn.Add(item.ReadableId + ":" + item.Category.MainCategory + ":" + slots[i].IsInActiveSet() + ":" + (RarityItems.AffixOf(item.ReadableId)?.Proc ?? "-") + ":" + slots[i]._hasActiveEffectsApplied);
                    }
                }
                return string.Join(",", worn);
            });
            // "rando_elitehits": the last elite hits as weapon:proc:queued.
            TestAgent.RegisterCommand("rando_elitehits", _ => string.Join(",", EliteCombat.EliteHitLog));
            // "rando_itemstats <item id>": tooltip base damage and armour of an item.
            TestAgent.RegisterCommand("rando_itemstats", id =>
            {
                Item item = ProviderAccess.ItemDatabase!.GetItemByReadableId(id.Trim());
                Il2CppDrova.Items.Stats.ItemStats? stats = item.Category.MainCategory == ItemMainCategory.Weapon ? new(item) : null;
                Il2CppDrova.Items.Stats.ItemArmorStats armour = new(item);
                return "damage=" + (stats != null && stats._primaryStats != null ? stats._primaryStats.BaseDamage().ToString(CultureInfo.InvariantCulture) : "-")
                    + " armour=" + armour._physicalResistance + " magic=" + armour._magicalResistance + " rarity=" + item.Rarity;
            });
            // "lasthit=attacker>defender_weapon_factor variants=N elites=N unbuffed=N" plus name:rarity:health/base:xp of every loaded elite.
            TestAgent.RegisterCommand("rando_rarities", _ =>
            {
                List<string> elites = [];
                int unbuffed = 0;
                foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LazyActor>()))
                {
                    LazyActor? lazy = found.TryCast<LazyActor>();
                    Actor? body = lazy != null && lazy.gameObject.scene.IsValid() ? lazy.Actor : null;
                    if (body == null || !body.IsInitialized || body.GetHealth() == null || body.GetHealth().IsDead || !Rarities.TryGetElite(body, out EnemyAffix affix))
                    {
                        continue;
                    }
                    Health health = body.GetHealth();
                    unbuffed += health.MaxHealth > health.BaseMaxHealth ? 0 : 1;
                    Il2CppCollections.List<ActorEquipSlot>? slots = body.GetEquipmentModule() != null ? body.GetEquipmentModule().AllItemSlotsRef : null;
                    List<string> gear = [];
                    for (int i = 0; slots != null && i < slots.Count; i++)
                    {
                        Item? worn = slots[i].GetItemOnSlot();
                        if (worn != null && worn.ReadableId.Contains("_r" + affix.Rarity + "v"))
                        {
                            gear.Add(worn.ReadableId);
                        }
                    }
                    elites.Add(lazy!.name + ":" + affix.Rarity + ":" + health.MaxHealth + "/" + health.BaseMaxHealth + ":xp" + body.GetStats().ExpOnDead
                        + ":gear=" + string.Join("+", gear));
                }
                return "lasthit=" + EliteCombat.LastHit.Replace(' ', '_') + " variants=" + RarityItems.VariantCount + " elites=" + elites.Count + " unbuffed=" + unbuffed + " labels=" + EliteLabels.LabelCount + " engaged=" + EliteCombat.EngagedCount + " " + string.Join(",", elites);
            });
            TestAgent.RegisterCommand("rando_placed", key => Randomizer.Active && Randomizer.Seed.Placement.TryGetValue(key.Trim(), out string? id) ? id : "none");
            TestAgent.RegisterCommand("rando_run", _ => Randomizer.RunKey);
            TestAgent.RegisterCommand("rando_trade", TradeCommand);
            TestAgent.RegisterCommand("rando_newgame", hero =>
            {
                SavegameGameHandler.TryGet().CreateNewCurrentSavegame(int.Parse(hero.Trim(), CultureInfo.InvariantCulture));
                SceneGameHandler.TryGet().LoadNewGameStartScene();
                return "character creation loading";
            });
            TestAgent.RegisterCommand("rando_cc", CharacterCreationCommand);
            TestAgent.RegisterCommand("rando_money", amount =>
            {
                Player.GetInventory().AddItem(new ItemStack(ItemUtil.GetMoneyItem(), int.Parse(amount.Trim(), CultureInfo.InvariantCulture)), false);
                return "money added";
            });
            TestAgent.RegisterCommand("rando_tradestock", TradeStockCommand);
            TestAgent.RegisterCommand("rando_shelf", _ =>
            {
                GUI_Window_Merchant? window = Object.FindObjectOfType<GUI_Window_Merchant>();
                if (window == null || !window.IsWindowOpen)
                {
                    return "closed";
                }
                List<string> stacks = [];
                foreach (IItemContainer container in window._marketPlace.GetTraderLeft().GetAllItems())
                {
                    stacks.Add(container.GetItem().ReadableId + ":" + container.GetAmount());
                }
                return string.Join(";", stacks) + ";";
            });
            TestAgent.RegisterCommand("rando_finishcc", _ =>
            {
                Object.FindObjectOfType<GUI_ButtonClick_FinishCharacterCreation>()!.Finish(GameMode.Default);
                return "finished";
            });
        }

        private static string TradeCommand(string guid)
        {
            foreach (Actor npc in Resources.FindObjectsOfTypeAll<Actor>())
            {
                if (npc.gameObject.scene.IsValid() && ContainerTracker.GuidChain(npc).Contains(guid.Trim()))
                {
                    GUI_Window_Merchant window = ProviderAccess.GetGUIGameHandler()!.GetMerchantWindow();
                    window.Init(npc, true);
                    window.ShowWindow();
                    return "trading";
                }
            }
            throw new InvalidOperationException("no loaded actor " + guid);
        }

        private static string TradeStockCommand(string guid)
        {
            foreach (Actor npc in Resources.FindObjectsOfTypeAll<Actor>())
            {
                if (npc.gameObject.scene.IsValid() && ContainerTracker.GuidChain(npc).Contains(guid.Trim()))
                {
                    List<string> stacks = [];
                    foreach (ItemStack stack in npc.GetInventoryActorModule().GetTradeInventory().InventoryItems)
                    {
                        stacks.Add(stack.Item.ReadableId + ":" + stack.Amount);
                    }
                    return stacks.Count == 0 ? "none" : string.Join(";", stacks);
                }
            }
            throw new InvalidOperationException("no loaded actor " + guid);
        }

        private static string CharacterCreationCommand(string args)
        {
            string[] parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            RandoOptions options = RandoOptions.FromPreset("default")!;
            foreach (string pair in parts.Skip(1))
            {
                string[] keyValue = pair.Split('=');
                options[keyValue[0]] = int.Parse(keyValue[1], CultureInfo.InvariantCulture);
            }
            Core.Ui.ChooseNewGameRun(parts[0], options);
            return "randomize on";
        }

        private static string GateCommand(string name)
        {
            foreach (OW_Teleporter gate in TeleporterAccess.GetAllGates())
            {
                if (gate.name == name && gate.gameObject.scene.IsValid())
                {
                    Vector3 position = gate.transform.position;
                    return position.x.ToString("F0") + "," + position.y.ToString("F0") + " -> "
                        + gate._targetPos.x.ToString("F0") + "," + gate._targetPos.y.ToString("F0");
                }
            }
            throw new InvalidOperationException("no loaded gate " + name);
        }

        private static string HooksCommand(string fragment)
        {
            return string.Join(";", Hooking.FiredCounts()
                .Where(hook => hook.Label.Contains(fragment.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(hook => hook.Label + "=" + hook.Calls));
        }

        private static string ActorItemsCommand(string guid)
        {
            foreach (Actor actor in Resources.FindObjectsOfTypeAll<Actor>())
            {
                if (actor.gameObject.scene.IsValid() && ContainerTracker.GuidChain(actor).Contains(guid.Trim()))
                {
                    List<string> stacks = [];
                    foreach (ItemStack stack in actor.GetInventory().InventoryItems)
                    {
                        stacks.Add(stack.Item.ReadableId + ":" + stack.Amount);
                    }
                    return stacks.Count == 0 ? "none" : string.Join(";", stacks);
                }
            }
            throw new InvalidOperationException("no loaded actor " + guid);
        }

        private static string BrawlCommand(string guid)
        {
            foreach (Actor npc in Resources.FindObjectsOfTypeAll<Actor>())
            {
                if (npc.gameObject.scene.IsValid() && ContainerTracker.GuidChain(npc).Contains(guid.Trim()))
                {
                    BrawlUtil.StartBrawl(Player.GetEntityInfo(), npc.GetEntityInfo(),
                        new Il2CppCollections.List<EntityInfo>(), new Il2CppCollections.List<EntityInfo>(),
                        new Il2CppCollections.List<ItemStack>());
                    return "brawl started";
                }
            }
            throw new InvalidOperationException("no loaded actor " + guid);
        }

        private static string MugCommand(string guid)
        {
            foreach (Interact_Bhvr_LootKnockout loot in Resources.FindObjectsOfTypeAll<Interact_Bhvr_LootKnockout>())
            {
                Actor? owner = loot._ownerActor;
                if (!loot.gameObject.activeInHierarchy || owner == null || !ContainerTracker.GuidChain(owner).Contains(guid.Trim()))
                {
                    continue;
                }
                InteractableTrigger trigger = loot.GetComponentInParent<InteractableTrigger>();
                if (!trigger.IsActorInRange(Player))
                {
                    throw new InvalidOperationException("not in range of the loot");
                }
                return trigger.TryStartInteractAction(Player) ? "used" : throw new InvalidOperationException("the game refused");
            }
            throw new InvalidOperationException("no knockout loot of " + guid);
        }

        private static string TeachCommand(string configName)
        {
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TeacherConfig>()))
            {
                if (found.name == configName)
                {
                    List<string> names = [];
                    foreach (TeachableTalent offered in found.Cast<TeacherConfig>().GetLearnableTalents(Player))
                    {
                        names.Add(offered._talent.name);
                    }
                    return names.Count == 0 ? "nothing" : string.Join(",", names);
                }
            }
            throw new InvalidOperationException("no loaded " + configName);
        }

        private static List<TeacherConfig> LoadedTeachers()
        {
            List<TeacherConfig> teachers = [];
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TeacherConfig>()))
            {
                TeacherConfig teacher = found.Cast<TeacherConfig>();
                if (teacher._talents != null && teacher._talents.Count > 0 && teachers.All(t => t.name != teacher.name))
                {
                    teachers.Add(teacher);
                }
            }
            return teachers;
        }

        private static string OpenLearnCommand(string args)
        {
            string[] parts = args.Trim().Split(' ');
            TeacherConfig teacher = LoadedTeachers().FirstOrDefault(t => t.name == parts[0])
                ?? throw new InvalidOperationException("no loaded " + parts[0]);
            Player.GetStats().TryCast<PlayerAttributeStats>()!.GiveLearningPoint(int.Parse(parts[1]));

            GUI_LearnWindow window = GUIGameHandler.TryGet().GetLearnGUI(false, true);
            window.Init();
            window.SetLearnableTalents(teacher.GetLearnableTalents(Player));
            window.DisableLearnableStats();
            return string.Join(",", VisibleButtons(window).Select(button => button._perkButton.GetTalentContainer().name));
        }

        private static string ClickLearnCommand(string index)
        {
            GUI_LearnWindow window = GUIGameHandler.TryGet().GetLearnGUI(false, true);
            List<GUI_Talent_LearnButton> buttons = VisibleButtons(window);
            buttons[int.Parse(index)]._button.onClick.Invoke();
            LearnService service = window._learnService;
            window._okButton.onClick.Invoke();
            service.FinishLearnSelection();
            service.ApplyData();
            return Player.GetStats().TryCast<PlayerAttributeStats>()!.GetAvailableTalentPoints() + " LP left";
        }

        private static List<GUI_Talent_LearnButton> VisibleButtons(GUI_LearnWindow window)
        {
            List<GUI_Talent_LearnButton> visible = [];
            foreach (GUI_Talent_LearnButton button in window._talentButtons)
            {
                if (button.gameObject.activeSelf)
                {
                    visible.Add(button);
                }
            }
            return visible;
        }

        private static string WindowCommand(string args)
        {
            string[] parts = args.Trim().Split(' ');
            Actor player = Player;
            TeacherConfig? teacher = null;
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TeacherConfig>()))
            {
                if (found.name == parts[0])
                {
                    teacher = found.Cast<TeacherConfig>();
                }
            }
            if (teacher == null)
            {
                throw new InvalidOperationException("no loaded " + parts[0]);
            }

            Il2CppCollections.List<TeachableTalent> offered = teacher.GetLearnableTalents(player);
            TalentContainer? pick = null;
            foreach (TeachableTalent slot in offered)
            {
                bool random = parts[1] == "random" && RandomTalent.IsEntry(slot._talent.GUID);
                if (random || slot._talent.name == parts[1])
                {
                    pick = slot._talent;
                }
            }
            if (pick == null)
            {
                throw new InvalidOperationException(parts[1] + " is not offered");
            }

            PlayerAttributeStats stats = player.GetStats().TryCast<PlayerAttributeStats>()!;
            stats.GiveLearningPoint(pick.TalentCostLearnPoints);
            LearnService service = new(player);
            service.AddLearnableTalents(offered);
            service.ToggleTalentSelected(pick);
            service.ApplyData();
            return "cost " + pick.TalentCostLearnPoints + ", " + stats.GetAvailableTalentPoints() + " LP left";
        }

        private static LazyActor FindLoaded(string guid)
        {
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LazyActor>()))
            {
                LazyActor? lazy = found.TryCast<LazyActor>();
                if (lazy != null && lazy.gameObject.scene.IsValid() && lazy._guidstring == guid)
                {
                    return lazy;
                }
            }
            throw new InvalidOperationException("no loaded spawner " + guid);
        }

        private static string ReachCommand(string points)
        {
            Vector2 from = Player.GetFeetPosition();
            List<string> answers = [];
            foreach (string point in points.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = point.Split(',');
                Vector2 goal = new(float.Parse(xy[0], CultureInfo.InvariantCulture),
                    float.Parse(xy[1], CultureInfo.InvariantCulture));
                ABPath path = ABPath.Construct(new Vector3(from.x, from.y, 0f), new Vector3(goal.x, goal.y, 0f));
                AstarPath.StartPath(path);
                AstarPath.BlockUntilCalculated(path);
                bool reached = !path.error && path.vectorPath != null && path.vectorPath.Count > 0
                    && (new Vector2(path.vectorPath[path.vectorPath.Count - 1].x, path.vectorPath[path.vectorPath.Count - 1].y) - goal).magnitude <= ReachTolerance;
                answers.Add(reached ? "1" : "0");
            }
            return string.Join(";", answers);
        }

        private static string Format(Vector3 position)
        {
            return position.x.ToString("F0") + "," + position.y.ToString("F0");
        }
    }
}
