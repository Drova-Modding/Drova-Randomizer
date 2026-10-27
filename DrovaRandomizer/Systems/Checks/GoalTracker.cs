using DrovaRandomizer.Systems.Checks.Patches;
using DrovaRandomizer.Systems.Run;
using MelonLoader;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>Records the goal when the game loads any outro scene, so good and bad endings both count.</summary>
    public sealed class GoalTracker : IRandoSystem
    {
        private const string OutroScenePrefix = "Scene_Outro";

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            GoalPatches.Apply(harmony);
        }

        internal static void OnChangeScene(string? sceneName)
        {
            try
            {
                if (sceneName != null && sceneName.StartsWith(OutroScenePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    Randomizer.ReachGoal();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando goal] ActuallyChangeScene prefix failed: " + e);
            }
        }
    }
}
