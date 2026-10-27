using System.Diagnostics.CodeAnalysis;
using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Checks.Patches;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova;
using Il2CppDrova.Brawl;
using Il2CppDrova.Crafting;
using Il2CppDrova.InteractionSystem;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.Saveables;
using MelonLoader;
using UnityEngine;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>Turns looting a chest, container, pickup, cache or resource spot into a location check.</summary>
    public sealed class ContainerTracker : IRandoSystem
    {
        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            GameEvents.OnLootInventoryOpened += InventoryOpenedEvent;
            GameEvents.OnPickupCollected += PickupUsedEvent;
            GameEvents.OnPickUpOnceLooted += PickUpOnceLootedEvent;
            GameEvents.OnLootedAll += LootAllEvent;
            GameEvents.OnCacheLooted += CacheLootedEvent;
            GameEvents.OnResourceMinigameFinished += ResourceMinigameFinishedEvent;
            ContainerPatches.Apply(harmony);
        }

        private static void InventoryOpenedEvent(Interact_Bhvr_LootInventory? container, bool openedByNpc)
        {
            try
            {
                if (openedByNpc || container == null)
                {
                    return;
                }
                Report(container, null, "opened");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] InventoryOpened handler failed: " + e);
            }
        }

        internal static void OnInventoryChanged(Interact_Bhvr_LootInventory? container, InventoryChangedEventArgs args)
        {
            try
            {
                if (container == null)
                {
                    return;
                }

                if (args.IsInLoadingStage || args.Action != InventoryChangedEventArgs.EAction.Removed
                    || !IsPlayer(container._interactionActor))
                {
                    return;
                }

                Report(container, null, "looted");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] InventoryChanged postfix failed: " + e);
            }
        }

        private static void PickUpOnceLootedEvent(Saveable_PickUp_Once? pickup)
        {
            try
            {
                if (pickup == null)
                {
                    return;
                }
                Report(pickup, null, "pickup once");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] Saveable_PickUp_Once handler failed: " + e);
            }
        }

        private static void LootAllEvent(Interact_Bhvr_LootAll? interaction)
        {
            try
            {
                if (interaction == null)
                {
                    return;
                }
                Report(interaction, null, "loot all");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] Interact_Bhvr_LootAll handler failed: " + e);
            }
        }

        private static void PickupUsedEvent(PickupInteraction? pickup)
        {
            try
            {
                if (pickup == null)
                {
                    return;
                }
                Report(pickup, pickup._guid, "pickup");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] PickupInteraction handler failed: " + e);
            }
        }

        internal static void OnCacheSpawnLoot(SpawnFromLootTable? cache)
        {
            try
            {
                if (cache == null)
                {
                    return;
                }
                Report(cache, null, "cache broken");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] SpawnLoot postfix failed: " + e);
            }
        }

        private static void CacheLootedEvent(SpawnFromLootTable? cache)
        {
            try
            {
                if (cache == null)
                {
                    return;
                }
                Report(cache, null, "cache looted");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] OnCacheLooted handler failed: " + e);
            }
        }

        private static void ResourceMinigameFinishedEvent(Interact_Bhvr_ResourceSpot? spot, MinigameFinishedArgs? args)
        {
            try
            {
                if (spot == null || args == null || !IsPlayer(args.Actor))
                {
                    return;
                }

                Report(spot, null, "resource worked");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] OnResourceMinigameFinished handler failed: " + e);
            }
        }

        internal static void OnResourceGetItems(Interact_Bhvr_ResourceSpot? spot)
        {
            try
            {
                if (spot == null)
                {
                    return;
                }
                Report(spot, null, "resource worked");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] GetItems postfix failed: " + e);
            }
        }

        /// <summary>True when the actor is the local player.</summary>
        public static bool IsPlayer(Actor? actor)
        {
            if (actor == null || !actor)
            {
                return false;
            }

            EntityGameHandler? handler = ProviderAccess.GetEntityGameHandler();
            return handler != null && handler.CheckIsPlayer(actor);
        }

        /// <summary>Resolves the location of a behavior's object from the first guid in its chain the table knows.</summary>
        public static bool TryResolveLocation(Component behaviour, string? fallbackGuid,
            [NotNullWhen(true)] out LocationDef? location, [NotNullWhen(true)] out string? matchedGuid)
        {
            location = null;
            matchedGuid = null;
            List<string> chain = GuidChain(KnockoutOwnerOrSelf(behaviour));
            if (!string.IsNullOrEmpty(fallbackGuid))
            {
                chain.Add(fallbackGuid);
            }

            foreach (string guid in chain)
            {
                location = LocationTable.Get(guid);
                if (location != null)
                {
                    matchedGuid = guid;
                    return true;
                }
            }
            return false;
        }

        private static Component KnockoutOwnerOrSelf(Component behaviour)
        {
            try
            {
                Interact_Bhvr_LootKnockout? knockout = behaviour.TryCast<Interact_Bhvr_LootKnockout>();
                if (knockout == null)
                {
                    return behaviour;
                }
                IBrawlActor? brawl = knockout._actor;
                if (brawl == null)
                {
                    return behaviour;
                }
                Actor? owner = brawl.Entity == null ? null : brawl.Entity.TryCast<Actor>();
                return owner != null ? owner : behaviour;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[Rando loot] resolving a knockout's owner failed: " + e.Message);
                return behaviour;
            }
        }

        /// <summary>
        /// Every GuidComponent guid from the behavior up to the scene root, nearest first. A runtime-spawned
        /// child carries a fresh guid, the scene-baked one sits further up.
        /// </summary>
        public static List<string> GuidChain(Component? behaviour)
        {
            List<string> chain = [];
            try
            {
                if (behaviour == null)
                {
                    return chain;
                }

                Transform node = behaviour.transform;
                while (node)
                {
                    GuidComponent owner = node.GetComponent<GuidComponent>();
                    if (owner)
                    {
                        string guid = owner._guidString;
                        if (!string.IsNullOrEmpty(guid))
                        {
                            chain.Add(guid);
                        }
                    }
                    node = node.parent;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[Rando loot] walking the GuidComponent chain failed: " + e.Message);
            }
            return chain;
        }

        private static void Report(Component behaviour, string? fallbackGuid, string source)
        {
            if (TryResolveLocation(behaviour, fallbackGuid, out LocationDef? location, out _))
            {
                Randomizer.Check(location, source);
            }
        }
    }
}
