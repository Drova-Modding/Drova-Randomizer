using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova.GlobalVarSystem;
using UnityEngine;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>Warns once per run when the player joins the faction the run was not made for.</summary>
    public sealed class FactionWatch : IRandoSystem
    {
        private const float PollInterval = 2f;
        private const string FactionList = "QuestVars_OneFaction";

        private const string JoinedNemeton = "JoinedMoorlager";
        private const string JoinedRemnants = "JoinedRuinenlager";

        private static float _nextPoll;

        void IRandoSystem.Update()
        {
            if (!Randomizer.Active || Time.realtimeSinceStartup < _nextPoll || Randomizer.State.FactionWarned)
            {
                return;
            }
            _nextPoll = Time.realtimeSinceStartup + PollInterval;

            bool runIsNemeton = Randomizer.Seed.Options[RandoOptions.Faction] == 0;
            if (!IsSet(runIsNemeton ? JoinedRemnants : JoinedNemeton))
            {
                return;
            }

            Randomizer.State.FactionWarned = true;
            Randomizer.MarkDirty();
            string planned = runIsNemeton ? "Nemeton" : "the Remnants";
            string joined = runIsNemeton ? "the Remnants" : "Nemeton";
            Randomizer.Warn("This run was made for " + planned + ", but you joined " + joined
                + ": items placed in " + planned + " quests and shops may now be out of reach.");
        }

        private static bool IsSet(string varName)
        {
            SubDatabase_GVars? database = ProviderAccess.GVarDatabase;
            if (database == null)
            {
                return false;
            }
            foreach (GVarList list in database.AllGVars)
            {
                if (list == null || list.name != FactionList)
                {
                    continue;
                }
                AGVarBase gvar = list.GetGVarByName(varName);
                AGVar<bool>? flag = gvar == null ? null : gvar.TryCast<AGVar<bool>>();
                return flag != null && flag.GetValue();
            }
            return false;
        }
    }
}
