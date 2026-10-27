using Drova_Modding_API;
using Il2CppDrova.Utilities.LazyLoading;

namespace DrovaRandomizer.Systems.World.Patches
{
    /// <summary>The spawner hook of <see cref="MonsterShuffler"/>.</summary>
    internal static class MonsterPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(LazyActor), "Awake", typeof(MonsterPatches), nameof(AwakePostfix));
        }

        private static void AwakePostfix(LazyActor __instance)
        {
            MonsterShuffler.OnAwake(__instance);
        }
    }
}
