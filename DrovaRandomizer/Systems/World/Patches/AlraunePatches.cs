using Drova_Modding_API;
using Il2CppDrova.Saveables;

namespace DrovaRandomizer.Systems.World.Patches
{
    /// <summary>The plant hook of <see cref="AlrauneShuffler"/>.</summary>
    internal static class AlraunePatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(Saveable_Alraune), "Start",
                typeof(AlraunePatches), nameof(StartPostfix));
        }

        private static void StartPostfix(Saveable_Alraune __instance)
        {
            AlrauneShuffler.OnStart(__instance);
        }
    }
}
