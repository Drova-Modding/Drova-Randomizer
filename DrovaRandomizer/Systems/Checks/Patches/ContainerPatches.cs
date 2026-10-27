using Drova_Modding_API;
using Il2CppDrova;
using Il2CppDrova.InteractionSystem;
using Il2CppDrova.InventorySystem;

namespace DrovaRandomizer.Systems.Checks.Patches
{
    /// <summary>The looting hooks of <see cref="ContainerTracker"/>.</summary>
    internal static class ContainerPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(Interact_Bhvr_LootInventory),
                nameof(Interact_Bhvr_LootInventory.InventoryChangedEventListener),
                typeof(ContainerPatches), nameof(InventoryChangedPostfix));
            Hooking.TryPostfix(harmony, typeof(SpawnFromLootTable), nameof(SpawnFromLootTable.SpawnLoot),
                typeof(ContainerPatches), nameof(SpawnLootPostfix));
            Hooking.TryPostfix(harmony, typeof(Interact_Bhvr_ResourceSpot),
                nameof(Interact_Bhvr_ResourceSpot.GetItems),
                typeof(ContainerPatches), nameof(GetItemsPostfix));
        }

        private static void InventoryChangedPostfix(Interact_Bhvr_LootInventory __instance, InventoryChangedEventArgs arg0)
        {
            ContainerTracker.OnInventoryChanged(__instance, arg0);
        }

        private static void SpawnLootPostfix(SpawnFromLootTable __instance)
        {
            ContainerTracker.OnCacheSpawnLoot(__instance);
        }

        private static void GetItemsPostfix(Interact_Bhvr_ResourceSpot __instance)
        {
            ContainerTracker.OnResourceGetItems(__instance);
        }
    }
}
