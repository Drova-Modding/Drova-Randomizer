using Drova_Modding_API;
using Il2CppDrova.GUI;
using Il2CppDrova.InteractionSystem;
using Il2CppDrova.InventorySystem;

namespace DrovaRandomizer.Systems.Loot.Patches
{
    /// <summary>The hooks of <see cref="LootSuppressor"/>.</summary>
    internal static class LootPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPrefix(harmony, typeof(GUI_LootInventory), nameof(GUI_LootInventory.ShowWindow),
                typeof(LootPatches), nameof(ShowWindowPrefix));

            Hooking.TryPrefix(harmony, typeof(Interact_Bhvr_LootInventory),
                nameof(Interact_Bhvr_LootInventory.CreateLootInventory),
                typeof(LootPatches), nameof(CreateLootInventoryPrefix));
            Hooking.TryPrefix(harmony, typeof(Interact_Bhvr_LootKnockout),
                nameof(Interact_Bhvr_LootKnockout.CreateLootInventory),
                typeof(LootPatches), nameof(CreateLootInventoryPrefix));

            Hooking.TryPrefix(harmony, typeof(Interact_Bhvr_LootAll), nameof(Interact_Bhvr_LootAll.LootAll),
                typeof(LootPatches), nameof(LootAllPrefix));

            Hooking.TryPrefix(harmony, typeof(Interact_Bhvr_ResourceSpot),
                nameof(Interact_Bhvr_ResourceSpot.GetItems),
                typeof(LootPatches), nameof(GetItemsPrefix));
        }

        private static void ShowWindowPrefix(IInventoryContainer inventory)
        {
            LootSuppressor.OnShowWindow(inventory);
        }

        private static void CreateLootInventoryPrefix(Interact_Bhvr_LootInventory __instance)
        {
            LootSuppressor.OnCreateLootInventory(__instance);
        }

        private static void LootAllPrefix(Interact_Bhvr_LootAll __instance)
        {
            LootSuppressor.OnLootAll(__instance);
        }

        private static bool GetItemsPrefix(Interact_Bhvr_ResourceSpot __instance)
        {
            return LootSuppressor.OnResourceGetItems(__instance);
        }
    }
}
