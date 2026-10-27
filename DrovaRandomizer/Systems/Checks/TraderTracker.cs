using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Checks.Patches;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova;
using Il2CppDrova.Items;
using Il2CppDrova.TradingSystem;
using Il2CppTradingSystem.ItemContainers;
using MelonLoader;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>
    /// Shops sell what the run placed, and buying checks the slot. Only the window's shelf changes, so the
    /// saved trade inventory stays vanilla.
    /// </summary>
    public sealed class TraderTracker : IRandoSystem
    {
        private static readonly Dictionary<string, List<string>> ShelfSlots = new(StringComparer.Ordinal);

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            TraderPatches.Apply(harmony);
            GameEvents.OnTraderItemRemoved += OnItemRemoved;
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            ShelfSlots.Clear();
        }

        /// <summary>Puts the placed items on a freshly built shelf. A slot leaves it once its stack is bought out.</summary>
        internal static void Stock(TraderActorAdapter adapter)
        {
            if (!Randomizer.Active)
            {
                return;
            }
            try
            {
                Actor trader = adapter._actor;
                SubDatabase_Item? database = ProviderAccess.ItemDatabase;
                if (!trader || ContainerTracker.IsPlayer(trader) || database == null)
                {
                    return;
                }

                List<string> chain = ContainerTracker.GuidChain(trader);
                Il2CppCollections.List<IItemContainer> shelf = adapter._trader._items;
                for (int i = shelf.Count - 1; i >= 0; i--)
                {
                    string itemGuid = shelf[i].GetId();
                    foreach (string traderGuid in chain)
                    {
                        LocationDef? slot = LocationTable.GetTraderSlot(traderGuid, itemGuid);
                        if (!Randomizer.IsLocation(slot))
                        {
                            continue;
                        }

                        ItemDef placed = ItemTable.Get(Randomizer.Seed.Placement[slot.Key])!;
                        Item item = database.GetItemByReadableId(placed.Id);
                        int left = StackOf(placed) - Bought(slot.Key);
                        if (!item || left <= 0 || Randomizer.IsFoundByPartner(slot))
                        {
                            shelf.RemoveAt(i);
                            break;
                        }
                        shelf[i] = new ItemContainer(item.Cast<IItem>(), left, false).Cast<IItemContainer>();

                        string key = traderGuid + ":" + item.Guid;
                        if (!ShelfSlots.TryGetValue(key, out List<string>? slots))
                        {
                            ShelfSlots[key] = slots = [];
                        }
                        if (!slots.Contains(slot.Key))
                        {
                            slots.Add(slot.Key);
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando trader] stocking a shelf failed: " + e);
            }
        }

        private static int StackOf(ItemDef placed)
        {
            (int low, int high) = Generator.GrantRange(placed.Amount, (ConsumableStackSize)Randomizer.Seed!.Options[RandoOptions.StackSize]);
            return Math.Clamp(placed.Amount, low, high);
        }

        private static int Bought(string slotKey)
        {
            return Randomizer.State.TraderUnitsBought.TryGetValue(slotKey, out int bought) ? bought : 0;
        }

        private static void OnItemRemoved(TraderActorAdapter? adapter, ItemTraderStack? itemStack)
        {
            try
            {
                if (adapter == null || itemStack == null || !Randomizer.Active)
                {
                    return;
                }
                Actor trader = adapter._actor;
                if (!trader || ContainerTracker.IsPlayer(trader))
                {
                    return;
                }

                int amount = Math.Max(1, itemStack.Amount);
                foreach (string traderGuid in ContainerTracker.GuidChain(trader))
                {
                    if (!ShelfSlots.TryGetValue(traderGuid + ":" + itemStack.Id, out List<string>? slots))
                    {
                        continue;
                    }
                    foreach (string slotKey in slots)
                    {
                        LocationDef slot = LocationTable.Get(slotKey)!;
                        int left = StackOf(ItemTable.Get(Randomizer.Seed.Placement[slotKey])!) - Bought(slotKey);
                        if (amount <= 0 || left <= 0)
                        {
                            continue;
                        }
                        int taken = Math.Min(left, amount);
                        Randomizer.State.TraderUnitsBought[slotKey] = Bought(slotKey) + taken;
                        amount -= taken;
                        Randomizer.MarkDirty();
                        Randomizer.Check(slot, "bought", grant: false);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando trader] OnTraderItemRemoved handler failed: " + e);
            }
        }
    }
}
