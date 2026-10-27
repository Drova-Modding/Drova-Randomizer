using Drova_Modding_API;
using Il2CppDrova.TradingSystem;

namespace DrovaRandomizer.Systems.Checks.Patches
{
    /// <summary>The shelf hooks of <see cref="TraderTracker"/>.</summary>
    internal static class TraderPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(TraderActorAdapter), nameof(TraderActorAdapter.RefreshTraderItems),
                typeof(TraderPatches), nameof(StockPostfix));
            Hooking.TryPostfix(harmony, typeof(TraderActorAdapter), nameof(TraderActorAdapter.UpdateInventory),
                typeof(TraderPatches), nameof(StockPostfix));
        }

        private static void StockPostfix(TraderActorAdapter __instance)
        {
            TraderTracker.Stock(__instance);
        }
    }
}
