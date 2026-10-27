using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Checks;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Loot.Patches;
using DrovaRandomizer.Systems.Run;
using Il2CppDrova.InteractionSystem;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.InventorySystem.Filters;
using Il2CppDrova.Items;
using MelonLoader;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.Loot
{
    /// <summary>
    /// Empties randomized containers of their vanilla loot, so a check gives only the placed item.
    /// Keys, quest items and energy crystals stay, since the generator has no logic to keep a key from
    /// landing behind its own door.
    /// </summary>
    public sealed class LootSuppressor : IRandoSystem
    {
        private static bool _hideGear;

        /// <summary>True while a run suppresses vanilla loot.</summary>
        public static bool Enabled { get; private set; }

        /// <summary>True under Carnage, which strips protected items too and can soft-lock the seed.</summary>
        public static bool StripProtected { get; private set; }

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            LootPatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            StripProtected = seed != null && seed.Options.On(RandoOptions.Carnage);
            Enabled = seed != null && (!seed.Options.On(RandoOptions.KeepVanillaLoot) || StripProtected);
        }

        /// <summary>Items the player must keep for progression to remain possible without logic.</summary>
        internal static bool IsProtected(Item? item)
        {
            if (item == null)
            {
                return true;
            }

            if (item.IsQuestItem || item.IsInQuestCategory)
            {
                return true;
            }

            ItemCategory category = item.Category;
            if (category != null && category.SubCategory == ItemSubCategory.Misc_Key)
            {
                return true;
            }

            string readableId = item.ReadableId;
            if (string.IsNullOrEmpty(readableId))
            {
                return true;
            }

            if (readableId.StartsWith("key_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (readableId.Equals("misc_key_locked_door", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (readableId.StartsWith("item_energycrystal_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (readableId.StartsWith("misc_riddle_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void Strip(IInventoryContainer? inventory, string locationName, string matchedGuid, string source,
            Inventory? lootSource = null, bool actorLoot = false)
        {
            if (inventory == null || inventory.IsEmpty)
            {
                return;
            }

            Dictionary<string, int>? authoredLeft = null;
            if (!GeneratesLoot(lootSource) && LocationTable.TryGetAuthoredLoot(matchedGuid, out string[]? authored))
            {
                authoredLeft = new Dictionary<string, int>(authored.Length, StringComparer.OrdinalIgnoreCase);
                foreach (string entry in authored)
                {
                    int split = entry.LastIndexOf(':');
                    if (split <= 0 || !int.TryParse(entry.Substring(split + 1), out int amount) || amount <= 0)
                    {
                        continue;
                    }
                    string readableId = entry.Substring(0, split);
                    authoredLeft[readableId] = authoredLeft.TryGetValue(readableId, out int have) ? have + amount : amount;
                }
            }

            List<ItemStack> snapshot = [];
            foreach (ItemStack stack in inventory.InventoryItems)
            {
                snapshot.Add(stack);
            }

            int removed = 0;
            int kept = 0;
            int foreign = 0;
            int worn = 0;
            foreach (ItemStack stack in snapshot)
            {
                if (stack == null || stack.Item == null)
                {
                    continue;
                }

                if (actorLoot && !IsMuggable(stack.Item))
                {
                    worn++;
                    continue;
                }
                if (!StripProtected && IsProtected(stack.Item))
                {
                    kept++;
                    continue;
                }

                if (authoredLeft != null)
                {
                    string readableId = stack.Item.ReadableId;
                    if (string.IsNullOrEmpty(readableId) ||
                        !authoredLeft.TryGetValue(readableId, out int left) || left <= 0)
                    {
                        foreign++;
                        continue;
                    }
                    int take = Math.Min(left, stack.Amount);
                    inventory.RemoveItem(stack.Item, take, false);
                    authoredLeft[readableId] = left - take;
                    removed++;
                    continue;
                }

                inventory.RemoveItem(stack.Item, stack.Amount, false);
                removed++;
            }

            if (removed > 0 || kept > 0 || foreign > 0 || worn > 0)
            {
                MelonLogger.Msg("[Rando loot] " + source + " '" + locationName + "': suppressed " + removed +
                    " vanilla item(s)" + (kept > 0 ? ", kept " + kept + " progression-critical" : "") +
                    (foreign > 0 ? ", left " + foreign + " non-authored item(s) untouched" : "") +
                    (worn > 0 ? ", left the NPC " + worn + " item(s) of gear" : "") + ".");
            }
        }

        private static bool IsMuggable(Item item)
        {
            if (item.IsMoney())
            {
                return true;
            }
            ItemCategory category = item.Category;
            return item.IsLootable && category != null && category.MainCategory is ItemMainCategory.Consumable or ItemMainCategory.Misc;
        }

        private static bool GeneratesLoot(Inventory? lootSource)
        {
            return lootSource != null && lootSource.GetComponent<Inventory_LootBhvr>() != null;
        }

        internal static void OnCreateLootInventory(Interact_Bhvr_LootInventory? container)
        {
            try
            {
                _hideGear = false;
                if (!Enabled || container == null)
                {
                    return;
                }

                if (!ContainerTracker.TryResolveLocation(container, null, out LocationDef? location, out string? guid)
                    || !Randomizer.IsLocation(location))
                {
                    return;
                }

                if (Randomizer.IsChecked(location) && !Randomizer.IsFoundByPartner(location))
                {
                    return;
                }

                _hideGear = container._isActorLoot;
                Strip(container.OwnerInventory, location.Name, guid, "opened", container._ownerInventory, container._isActorLoot);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] suppressing before the loot window failed: " + e);
            }
        }

        internal static void OnShowWindow(IInventoryContainer? inventory)
        {
            try
            {
                if (!_hideGear)
                {
                    return;
                }
                _hideGear = false;
                InventoryFilterAdapter? view = inventory?.TryCast<InventoryFilterAdapter>();
                if (view == null)
                {
                    return;
                }

                Il2CppCollections.List<ItemStack> shown = view.InventoryItems;
                for (int i = shown.Count - 1; i >= 0; i--)
                {
                    Item? item = shown[i]?.Item;
                    if (item != null && item.Category != null
                        && item.Category.MainCategory is ItemMainCategory.Weapon or ItemMainCategory.Armor)
                    {
                        view.AddFilterItem(item.Guid);
                        shown.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] hiding a knocked-out NPC's gear failed: " + e);
            }
        }

        internal static bool OnResourceGetItems(Interact_Bhvr_ResourceSpot? spot)
        {
            try
            {
                if (!Enabled || spot == null)
                {
                    return true;
                }

                if (!ContainerTracker.TryResolveLocation(spot, null, out LocationDef? location, out _)
                    || !Randomizer.IsLocation(location))
                {
                    return true;
                }

                MelonLogger.Msg("[Rando loot] resource '" + location.Name + "': suppressed the vanilla yield.");
                return false;
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] suppressing a resource yield failed: " + e);
                return true;
            }
        }

        internal static void OnLootAll(Interact_Bhvr_LootAll? interaction)
        {
            try
            {
                if (!Enabled || interaction == null)
                {
                    return;
                }

                if (!ContainerTracker.TryResolveLocation(interaction, null, out LocationDef? location, out string? guid)
                    || !Randomizer.IsLocation(location) || Randomizer.IsChecked(location) && !Randomizer.IsFoundByPartner(location))
                {
                    return;
                }

                Inventory? lootInventory = interaction.LootInventory;
                Strip(lootInventory == null ? null : lootInventory.Cast<IInventoryContainer>(), location.Name, guid,
                    "loot all", lootInventory);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando loot] suppressing a loot-all failed: " + e);
            }
        }
    }
}
