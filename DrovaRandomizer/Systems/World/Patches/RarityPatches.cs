using Drova_Modding_API;
using Il2CppDrova;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.Items.Stats;

namespace DrovaRandomizer.Systems.World.Patches
{
    /// <summary>The actor and damage hooks of <see cref="Rarities"/>.</summary>
    internal static class RarityPatches
    {
        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            // FinishInit runs once per body, after its equip preset set health and attributes.
            Hooking.TryPostfix(harmony, typeof(Actor), "FinishInit", typeof(RarityPatches), nameof(FinishInitPostfix));
            // Every hit of every actor ends here, after armour, and nothing else calls it.
            Hooking.TryPostfix(harmony, typeof(HealthChange), nameof(HealthChange.Intern_CalculateResultingValue),
                typeof(RarityPatches), nameof(CalculatePostfix));
            // The one place a spawned human gets its gear and hands it to its AI.
            Hooking.TryPrefix(harmony, typeof(ActorEquipPreset), nameof(ActorEquipPreset.SetStartupSettings),
                typeof(RarityPatches), nameof(StartupSettingsPrefix));
            Hooking.TryPostfix(harmony, typeof(ActorEquipPreset), nameof(ActorEquipPreset.SetStartupSettings),
                typeof(RarityPatches), nameof(StartupSettingsPostfix));
            // Feeds the weapon tooltip and the spells that scale with the held weapon.
            Hooking.TryPostfix(harmony, typeof(ActionStats), nameof(ActionStats.BaseDamage),
                typeof(RarityPatches), nameof(BaseDamagePostfix));
        }

        private static void FinishInitPostfix(Actor __instance)
        {
            Rarities.OnActorInitialised(__instance);
        }

        private static void StartupSettingsPrefix(ActorEquipPreset __instance, Actor actor)
        {
            Rarities.OnEquipPresetStarting(__instance, actor);
        }

        private static void StartupSettingsPostfix()
        {
            Rarities.RestoreEquipPreset();
        }

        private static void BaseDamagePostfix(ActionStats __instance, ref float __result)
        {
            RarityItems.OnBaseDamage(__instance, ref __result);
        }

        private static void CalculatePostfix(HealthChange __instance, Health defenderHealth, ref HealthChange.HealthChangeCalc __result)
        {
            EliteCombat.OnHit(__instance, defenderHealth, ref __result);
        }
    }
}
