using Drova_Modding_API;
using Il2CppDrova;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.Talent;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.Talents.Patches
{
    /// <summary>The teacher hook of <see cref="TalentShuffler"/>.</summary>
    internal static class TalentShufflerPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(TeacherConfig), nameof(TeacherConfig.GetLearnableTalents),
                typeof(TalentShufflerPatches), nameof(GetLearnableTalentsPostfix));
        }

        private static void GetLearnableTalentsPostfix(TeacherConfig __instance, Actor target,
            ref Il2CppCollections.List<TeachableTalent> __result)
        {
            TalentShuffler.OnGetLearnableTalents(__instance, target, ref __result);
        }
    }
}
