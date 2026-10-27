using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Talents.Patches;
using Drova_Modding_API.Systems.Talents;
using Il2CppDrova;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.Talent;
using MelonLoader;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.Talents
{
    /// <summary>
    /// Talents in a run. Teacher shuffle swaps the talents between teachers: every teacher slot teaches the
    /// run's replacement, so each teacher keeps a full list. With random learning every teacher also offers
    /// a "Random talent" entry (<see cref="RandomTalent"/>).
    /// </summary>
    public sealed class TalentShuffler : IRandoSystem
    {
        private static readonly Dictionary<string, List<EntityInfo>> VanillaTeachers = new(StringComparer.Ordinal);
        private static RandoSeed? _seed;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            TalentShufflerPatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _seed = seed;
            foreach (string name in TalentTable.Teachable)
            {
                if (!VanillaTeachers.ContainsKey(name) && TalentContainerDatabase.TryGetTalent(name, out TalentContainer? talent) && talent._teachers != null)
                {
                    List<EntityInfo> teachers = [];
                    foreach (EntityInfo teacher in talent._teachers)
                    {
                        teachers.Add(teacher);
                    }
                    VanillaTeachers[name] = teachers;
                }
            }

            foreach (KeyValuePair<string, List<EntityInfo>> vanilla in VanillaTeachers)
            {
                string taught = seed?.Teachers.GetValueOrDefault(vanilla.Key, vanilla.Key) ?? vanilla.Key;
                if (TalentContainerDatabase.TryGetTalent(taught, out TalentContainer? talent))
                {
                    Il2CppCollections.List<EntityInfo> teachers = new();
                    vanilla.Value.ForEach(teachers.Add);
                    talent._teachers = teachers;
                }
            }
        }

        internal static void OnGetLearnableTalents(TeacherConfig teacher, Actor? target,
            ref Il2CppCollections.List<TeachableTalent> offered)
        {
            try
            {
                if (_seed == null || target == null || teacher._talents == null)
                {
                    return;
                }

                if (_seed.Teachers.Count > 0)
                {
                    Il2CppCollections.List<TeachableTalent> learnable = new();
                    foreach (TeachableTalent slot in teacher._talents)
                    {
                        TalentContainer? vanilla = slot?._talent;
                        if (slot == null || vanilla == null
                            || !TalentContainerDatabase.TryGetTalent(_seed.Teachers.GetValueOrDefault(vanilla.name, vanilla.name), out TalentContainer? talent))
                        {
                            continue;
                        }

                        TeachableTalent taught = new() { _talent = talent, _teachExplicit = slot._teachExplicit };
                        if (!target.TalentActorModule.HasTalentLearned(LearnUtil.SelectTeachableTalent(target, taught.Cast<ITeachableTalent>())))
                        {
                            learnable.Add(taught);
                        }
                    }
                    offered = learnable;
                }

                RandomTalent.AddEntry(teacher, offered);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando talents] GetLearnableTalents postfix failed: " + e);
            }
        }
    }
}
