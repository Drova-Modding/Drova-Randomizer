using Drova_Modding_API;
using Il2CppDrova.GUI;

namespace DrovaRandomizer.UI.Patches
{
    /// <summary>The character creation hook of <see cref="RandoUi"/>.</summary>
    internal static class CharacterCreationPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPrefix(harmony, typeof(GUI_ButtonClick_FinishCharacterCreation), nameof(GUI_ButtonClick_FinishCharacterCreation.Finish),
                typeof(CharacterCreationPatches), nameof(FinishPrefix));
        }

        private static bool FinishPrefix(GUI_ButtonClick_FinishCharacterCreation __instance)
        {
            return Core.Ui.OnFinishCharacterCreation(__instance._skipIntroToggle);
        }
    }
}
