using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova;
using MelonLoader;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>
    /// Turns enemy kills into "kills#k" milestone checks at k * interval kills. Kills by summons or damage
    /// over time carry a non-player source and do not count.
    /// </summary>
    public sealed class KillTracker : IRandoSystem
    {
        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            GameEvents.OnPlayerKilledActor += OnPlayerKilledActor;
        }

        void IRandoSystem.CatchUp()
        {
            SyncMilestones();
        }

        private static void SyncMilestones()
        {
            CheckUpTo("kills#", Randomizer.State.EnemyKills / Interval(RandoOptions.KillInterval));
        }

        /// <summary>Checks "prefix1" .. "prefixN". The run's option decides how many of them exist.</summary>
        internal static void CheckUpTo(string prefix, int reached)
        {
            for (int k = 1; k <= reached; k++)
            {
                LocationDef? location = LocationTable.Get(prefix + k);
                if (!Randomizer.IsLocation(location))
                {
                    return;
                }
                Randomizer.Check(location, "milestone");
            }
        }

        internal static int Interval(string option)
        {
            return Randomizer.Active ? Math.Max(1, Randomizer.Seed.Options[option]) : 1;
        }

        private static void OnPlayerKilledActor(Actor victim)
        {
            try
            {
                if (!Randomizer.Active)
                {
                    return;
                }

                Randomizer.State.EnemyKills++;
                Randomizer.MarkDirty();
                SyncMilestones();
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando kill] OnPlayerKilledActor failed: " + e);
            }
        }
    }
}
