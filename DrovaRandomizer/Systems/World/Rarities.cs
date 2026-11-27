using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.World.Patches;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Il2Cpp;
using Il2CppDrova;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.Items;
using Il2CppDrova.Utilities.LazyLoading;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// The rarities option. Enemies roll modifiers from the seed and their spawner guid, so an elite stays one across
    /// loads without save state, and elite humans are equipped with rarity variants. <see cref="RarityItems"/> holds
    /// the variants. <see cref="EliteCombat"/>, <see cref="EliteDrops"/> and <see cref="EliteLabels"/> do the rest.
    /// </summary>
    public sealed class Rarities : IRandoSystem
    {
        private const float ScalePerRarity = 0.05f;

        /// <summary>The rolls by spawner guid for the current run. A pack roll reads its neighbours too, and it runs on every hit.</summary>
        private static readonly Dictionary<string, EnemyAffix> RollByGuid = new(StringComparer.Ordinal);

        /// <summary>The same rolls by body instance id, so a hit reads no guid.</summary>
        private static readonly Dictionary<int, EnemyAffix> RollByBody = [];

        /// <summary>The modifiers each living body was built with, by instance id.</summary>
        private static readonly Dictionary<int, EnemyAffix> AppliedByBody = [];

        /// <summary>Rolls found items and drops, different every session.</summary>
        internal static readonly SeededRandom LootRandom = new((ulong)DateTime.UtcNow.Ticks);

        /// <summary>The preset whose equipment is swapped for the actor starting up, and its own items.</summary>
        private static ActorEquipPreset? _swappedPreset;
        private static Item?[] _swappedItems = [];

        internal static ulong? Seed { get; private set; }

        internal static int Frequency { get; private set; } = Affixes.NormalFrequency;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            RarityPatches.Apply(harmony);
            GameEvents.OnEntityDied += EliteDrops.OnEntityDied;
            GameEvents.OnEquipmentChanged += (actor, _) => RarityItems.OnEquipEffectsChanged(actor);
            EliteLabels.Initialize();
        }

        void IRandoSystem.OnSceneLoaded(string sceneName)
        {
            if (sceneName != SceneNames.MainMenu)
            {
                return;
            }

            AppliedByBody.Clear();
            EliteLabels.Clear();
            EliteCombat.Clear();

            try
            {
                RarityItems.RegisterVariants();
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] registering the item variants failed: " + e);
            }
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            Seed = seed != null && seed.Options.On(RandoOptions.Rarities) ? seed.Number : null;
            Frequency = seed != null ? seed.Options[RandoOptions.EliteFrequency] : Affixes.NormalFrequency;
            RollByGuid.Clear();
            RollByBody.Clear();
#if DEBUG
            EliteCombat.ProcLog.Clear();
            EliteCombat.EliteHitLog.Clear();
            EliteDrops.LastDrop = "none";
#endif
            EliteLabels.ReadOptions();

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
            EliteLabels.MoveLabels();
            EliteCombat.Update();
        }

        /// <summary>A rarity variant of a found item, or the item itself for a common roll.</summary>
        internal static Item Upgrade(Item item)
        {
            if (Seed == null)
            {
                return item;
            }

            int rarity = Affixes.RollItemRarity(LootRandom);
            string id = Affixes.VariantId(item.ReadableId, rarity, LootRandom.Next(Affixes.Variants));
            return rarity > 0 && RarityItems.TryGetVariant(id, out Item? variant) ? variant : item;
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

        internal static bool TryGetElite(Actor actor, out EnemyAffix affix)
        {
            affix = default;
            // Only spawner bodies have a guid that is the same on every load. The player and summons are not spawned that way.
            if (Seed is not { } seed || actor == null || !actor.IsControlledViaLazy)
            {
                return false;
            }
            int body = actor.GetInstanceID();
            if (RollByBody.TryGetValue(body, out affix))
            {
                return affix.Rarity > 0;
            }

            GuidComponent? guid = actor.GetGuidComponent();
            if (guid == null || string.IsNullOrEmpty(guid.GuidString))
            {
                return false;
            }

            string id = guid.GuidString;
            if (!RollByGuid.TryGetValue(id, out affix))
            {
                affix = Affixes.RollWithPack(seed, id, Frequency, PackTable.Neighbours.TryGetValue(id, out string[]? pack) ? pack : null);
                RollByGuid[id] = affix;
            }
            RollByBody[body] = affix;
            return affix.Rarity > 0;
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
                    if (equipment[i] != null && RarityItems.TryGetVariant(Affixes.VariantId(equipment[i].ReadableId, affix.Rarity, variant), out Item? rare))
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
    }
}
