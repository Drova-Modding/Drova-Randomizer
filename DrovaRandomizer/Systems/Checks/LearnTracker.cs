using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.Talent;
using MelonLoader;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>
    /// Turns teacher learning into milestone checks, "attributes#k" at k * interval points bought and
    /// "talents#k" at the k-th talent learned.
    /// </summary>
    public sealed class LearnTracker : IRandoSystem
    {
        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            GameEvents.OnAttributePointsLearned += OnAttributePointsLearned;
            GameEvents.OnTalentLearned += OnTalentLearned;
        }

        void IRandoSystem.CatchUp()
        {
            SyncMilestones();
        }

        private static void SyncMilestones()
        {
            RandoState state = Randomizer.State;
            KillTracker.CheckUpTo("attributes#", state.AttributesLearned / KillTracker.Interval(RandoOptions.AttributeInterval));
            KillTracker.CheckUpTo("talents#", state.TalentsLearned);
        }

        private static void OnAttributePointsLearned(LearnService service, int learned)
        {
            try
            {
                if (!Randomizer.Active)
                {
                    return;
                }
                Randomizer.State.AttributesLearned += learned;
                Randomizer.MarkDirty();
                SyncMilestones();
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando learn] OnAttributePointsLearned failed: " + e);
            }
        }

        private static void OnTalentLearned(Actor? actor, TalentContainer container)
        {
            try
            {
                if (!Randomizer.Active || actor == null || !actor.IsPlayer)
                {
                    return;
                }
                Randomizer.State.TalentsLearned++;
                Randomizer.MarkDirty();
                SyncMilestones();
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando learn] OnTalentLearned failed: " + e);
            }
        }
    }
}
