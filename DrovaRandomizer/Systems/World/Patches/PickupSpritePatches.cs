using Drova_Modding_API;
using Il2CppDrova.Saveables;

namespace DrovaRandomizer.Systems.World.Patches
{
    /// <summary>The pickup hook of <see cref="PickupSprites"/>.</summary>
    internal static class PickupSpritePatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Hooking.TryPostfix(harmony, typeof(Saveable_PickUp_Once), "Start",
                typeof(PickupSpritePatches), nameof(StartPostfix));
        }

        private static void StartPostfix(Saveable_PickUp_Once __instance)
        {
            PickupSprites.OnStart(__instance);
        }
    }
}
