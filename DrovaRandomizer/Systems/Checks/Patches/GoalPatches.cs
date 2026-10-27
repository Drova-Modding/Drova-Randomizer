using Drova_Modding_API;
using Il2CppDrova;

namespace DrovaRandomizer.Systems.Checks.Patches
{
    /// <summary>The outro hook of <see cref="GoalTracker"/>.</summary>
    internal static class GoalPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPrefix(harmony, typeof(SceneGameHandler), nameof(SceneGameHandler.ActuallyChangeScene),
                typeof(GoalPatches), nameof(ActuallyChangeScenePrefix));
        }

        private static void ActuallyChangeScenePrefix(string sceneName)
        {
            GoalTracker.OnChangeScene(sceneName);
        }
    }
}
