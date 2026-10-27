using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Run;
using Drova_Modding_API.Access;
using Il2CppDrova.GlobalVarSystem;
using Il2CppDrova.QuestSystem;
using MelonLoader;
using UnityEngine;

namespace DrovaRandomizer.Systems.Checks
{
    /// <summary>
    /// Turns quest completion into location checks. Without the API's SetValue hook, its fallback
    /// misses direct typed writes, so quest states are also polled every <see cref="PollInterval"/> seconds.
    /// </summary>
    public sealed class QuestTracker : IRandoSystem
    {
        private const float PollInterval = 1f;

        private static bool _pollActive;
        private static bool _sweepPending;
        private static float _nextScan;

        private static readonly Dictionary<string, QuestState> LastState = new(StringComparer.Ordinal);

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            GameEvents.OnQuestCompleted += OnQuestCompletedEvent;

            _pollActive = !GameEvents.QuestSetValueHookActive;
            MelonLogger.Msg("[Rando quest] detection: GameEvents.OnQuestCompleted"
                + (_pollActive ? " + polling every " + PollInterval + "s (SetValue hook not active)" : "") + ".");
        }

        void IRandoSystem.CatchUp()
        {
            _sweepPending = true;
            LastState.Clear();
        }

        void IRandoSystem.Update()
        {
            try
            {
                if (!_pollActive && !_sweepPending)
                {
                    return;
                }

                if (Time.realtimeSinceStartup < _nextScan)
                {
                    return;
                }
                _nextScan = Time.realtimeSinceStartup + PollInterval;

                if (!Core.InGameplayScene || !Randomizer.Active)
                {
                    return;
                }

                Scan(_sweepPending);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando quest] Update failed: " + e);
            }
        }

        private static void Scan(bool catchUp)
        {
            SubDatabase_GVars? gvars = ProviderAccess.GVarDatabase;
            if (gvars == null || gvars.AllGVars == null)
            {
                return;
            }

            int quests = 0;
            int mapped = 0;
            int completed = 0;

            foreach (GVarList list in gvars.AllGVars)
            {
                if (!list || !list.IsQuestVarList)
                {
                    continue;
                }

                quests++;
                string name = list.name;
                if (!QuestStateReader.TryRead(list.GetQuestState(), out QuestState state))
                {
                    continue;
                }

                bool known = LastState.TryGetValue(name, out QuestState previous);
                LastState[name] = state;

                if (state != QuestState.IsCompleted)
                {
                    continue;
                }

                completed++;
                if (catchUp || !known || previous != QuestState.IsCompleted)
                {
                    if (Report(name, "scan"))
                    {
                        mapped++;
                    }
                }
            }

            if (catchUp && quests > 0)
            {
                _sweepPending = false;
                MelonLogger.Msg("[Rando quest] catch-up sweep: " + quests + " quest lists, " + completed +
                    " completed, " + mapped + " of them locations.");
            }
        }

        private static void OnQuestCompletedEvent(string gvarListName)
        {
            try
            {
                if (string.IsNullOrEmpty(gvarListName))
                {
                    return;
                }
                LastState[gvarListName] = QuestState.IsCompleted;
                Report(gvarListName, "api event");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando quest] OnQuestCompleted handler failed: " + e);
            }
        }

        private static bool Report(string gvarListName, string source)
        {
            LocationDef? location = LocationTable.Get(gvarListName);
            if (location == null)
            {
                return false;
            }

            Randomizer.Check(location, "quest " + source);
            return true;
        }
    }
}
