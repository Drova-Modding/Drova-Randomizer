using Drova_Modding_API;
using HarmonyLib;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.Talent;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.Talents.Patches
{
    /// <summary>The learning hooks of <see cref="RandomTalent"/>.</summary>
    internal static class RandomTalentPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(TalentActorModule), nameof(TalentActorModule.CanLearnTalent),
                typeof(RandomTalentPatches), nameof(CanLearnTalentPostfix));
            Hooking.TryPrefix(harmony, typeof(TalentActorModule), nameof(TalentActorModule.LearnTalent),
                typeof(RandomTalentPatches), nameof(LearnTalentPrefix));
            Hooking.TryPrefix(harmony, AccessTools.Method(typeof(TalentActorModule),
                nameof(TalentActorModule.AddKnownTalent), [typeof(TalentContainer)]), typeof(RandomTalentPatches), nameof(KnownTalentPrefix),
                "TalentActorModule.AddKnownTalent(TalentContainer)");
            Hooking.TryPrefix(harmony, typeof(TalentActorModule), nameof(TalentActorModule.SetNextTalentsIsKnown),
                typeof(RandomTalentPatches), nameof(KnownTalentPrefix));
            Hooking.TryPrefix(harmony, typeof(GUI_LearnWindow), nameof(GUI_LearnWindow.SetLearnableTalents),
                typeof(RandomTalentPatches), nameof(SetLearnableTalentsPrefix));
        }

        private static void CanLearnTalentPostfix(TalentActorModule __instance, string GUID, ref bool __result)
        {
            RandomTalent.OnCanLearnTalent(__instance, GUID, ref __result);
        }

        private static bool LearnTalentPrefix(TalentActorModule __instance, TalentContainer container, bool lpCost)
        {
            return RandomTalent.OnLearnTalent(__instance, container, lpCost);
        }

        private static bool KnownTalentPrefix(TalentContainer talent)
        {
            return RandomTalent.AllowsTalent(talent);
        }

        private static void SetLearnableTalentsPrefix(GUI_LearnWindow __instance, Il2CppCollections.List<TeachableTalent> talents)
        {
            RandomTalent.OnSetLearnableTalents(__instance, talents);
        }
    }
}
