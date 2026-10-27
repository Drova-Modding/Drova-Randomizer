using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using DrovaRandomizer.Systems.Talents.Patches;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Talents;
using Il2Cpp;
using Il2CppDrova;
using Il2CppDrova.GUI;
using Il2CppDrova.GUI.LearnGUI;
using Il2CppDrova.Talent;
using MelonLoader;
using UnityEngine;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;

namespace DrovaRandomizer.Systems.Talents
{
    /// <summary>
    /// The "Random talent" entry of random learning. Every teacher who teaches talents also offers it, at
    /// the median learning point cost of that teacher's list. Choosing it teaches a random talent the player
    /// can learn right now. The entry is a runtime clone of one of the teacher's talents with its own guid,
    /// cost and name, so the learn window, its point bookkeeping and the teach feedback all work unchanged.
    /// </summary>
    public sealed class RandomTalent : IRandoSystem
    {
        /// <summary>Loca keys under <see cref="ItemLocaTable.LocaPath"/>, written with the item names.</summary>
        public const string NameKey = "rando_random_talent_name";
        public const string DescriptionKey = "rando_random_talent_desc";

        private const string GuidPrefix = "drova-randomizer-random-talent-";

        private static readonly Dictionary<int, TalentContainer> EntryByCost = new();
        private static readonly Dictionary<int, (float Row, float Frame, float Step)> BaseWidths = new();
        private static RandoSeed? _seed;
        private static bool _learning;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            RandomTalentPatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _seed = seed != null && seed.Options.On(RandoOptions.RandomLearning) ? seed : null;
        }

        /// <summary>Appends the entry to what a teacher offers, for a teacher who teaches talents at all.</summary>
        public static void AddEntry(TeacherConfig teacher, Il2CppCollections.List<TeachableTalent>? offered)
        {
            if (_seed == null || offered == null || teacher._talents == null || teacher._talents.Count == 0)
            {
                return;
            }

            List<int> costs = [];
            TalentContainer? template = null;
            foreach (TeachableTalent slot in teacher._talents)
            {
                if (slot?._talent != null)
                {
                    costs.Add(slot._talent.TalentCostLearnPoints);
                    template ??= slot._talent;
                }
            }
            if (template == null)
            {
                return;
            }
            costs.Sort();
            offered.Add(new TeachableTalent { _talent = Entry(costs[(costs.Count - 1) / 2], template), _teachExplicit = true });
        }

        private static TalentContainer Entry(int cost, TalentContainer template)
        {
            if (EntryByCost.TryGetValue(cost, out TalentContainer? entry) && entry != null && entry)
            {
                return entry;
            }

            entry = Object.Instantiate(template);
            entry.name = "Rando_RandomTalent_" + cost;
            entry._guid = GuidPrefix + cost;
            entry._talentCostLearnPoints = cost;
            entry._teachers = new Il2CppCollections.List<EntityInfo>();
            TalentLoca loca = new()
            {
                _name = new LocalizedString(ItemLocaTable.LocaPath, NameKey),
                _description = new LocalizedString(ItemLocaTable.LocaPath, DescriptionKey),
            };
            entry._loca = loca.Cast<ITalentLoca>();
            Object.DontDestroyOnLoad(entry);
            EntryByCost[cost] = entry;
            return entry;
        }

        internal static bool IsEntry(string? guid)
        {
            return guid != null && guid.StartsWith(GuidPrefix, StringComparison.Ordinal);
        }

        private static List<TalentContainer> Candidates(TalentActorModule module)
        {
            List<TalentContainer> candidates = [];
            foreach (string name in TalentTable.Teachable)
            {
                if (TalentContainerDatabase.TryGetTalent(name, out TalentContainer? talent) && module.CanLearnTalent(talent.GUID))
                {
                    candidates.Add(talent);
                }
            }
            return candidates;
        }

        internal static void OnCanLearnTalent(TalentActorModule module, string guid, ref bool learnable)
        {
            if (IsEntry(guid))
            {
                learnable = Candidates(module).Count > 0;
            }
        }

        /// <summary>
        /// Learning the entry learns a seeded random candidate instead and skips the original. The window has
        /// already charged the entry's cost. A direct call with lpCost pays it here.
        /// </summary>
        internal static bool OnLearnTalent(TalentActorModule module, TalentContainer? container, bool lpCost)
        {
            try
            {
                if (_learning || container == null || !IsEntry(container.GUID))
                {
                    return true;
                }

                List<TalentContainer> candidates = Candidates(module);
                if (_seed == null || candidates.Count == 0)
                {
                    return false;
                }

                RandoState state = Randomizer.State;
                TalentContainer chosen = candidates[Generator.Stream(_seed.Number, "learn" + state.RandomLearns).Next(candidates.Count)];
                state.RandomLearns++;
                Randomizer.MarkDirty();

                _learning = true;
                try
                {
                    module.LearnTalent(chosen, false);
                    module.SetNextTalentsIsKnown(chosen);
                }
                finally
                {
                    _learning = false;
                }

                PlayerAttributeStats? stats = lpCost ? module.GetActor().GetStats().TryCast<PlayerAttributeStats>() : null;
                if (stats != null)
                {
                    stats.DecreaseLearningPoint(container.TalentCostLearnPoints);
                }
                MelonLogger.Msg("[Rando talents] random talent: " + chosen.name + ".");
                ToastAccess.Show("You learn " + chosen.GetTalentName() + ".");
                return false;
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando talents] LearnTalent prefix failed: " + e);
                return false;
            }
        }

        /// <summary>False for the entry: it is never remembered as known or asked for its follow-ups.</summary>
        internal static bool AllowsTalent(TalentContainer? talent)
        {
            return talent == null || !IsEntry(talent.GUID);
        }

        /// <summary>
        /// The window has four talent buttons. A teacher with four talents plus the entry needs a fifth. The
        /// talent row and its frame are widened by one button step for it, and back for four.
        /// </summary>
        internal static void OnSetLearnableTalents(GUI_LearnWindow window, Il2CppCollections.List<TeachableTalent>? talents)
        {
            try
            {
                Il2CppCollections.List<GUI_Talent_LearnButton> buttons = window._talentButtons;
                if (talents == null || buttons == null || buttons.Count == 0)
                {
                    return;
                }

                RectTransform row = buttons[0].transform.parent.TryCast<RectTransform>()!;
                RectTransform frame = row.parent.TryCast<RectTransform>()!;
                if (!BaseWidths.ContainsKey(row.GetInstanceID()))
                {
                    BaseWidths[row.GetInstanceID()] = (row.sizeDelta.x, frame.sizeDelta.x, row.rect.width / buttons.Count);
                }
                (float rowWidth, float frameWidth, float step) = BaseWidths[row.GetInstanceID()];

                while (buttons.Count < talents.Count)
                {
                    GUI_Talent_LearnButton last = buttons[^1];
                    buttons.Add(Object.Instantiate(last.gameObject, last.transform.parent).GetComponent<GUI_Talent_LearnButton>());
                }

                float extra = Math.Max(0, talents.Count - 4) * step;
                row.sizeDelta = new Vector2(rowWidth + extra, row.sizeDelta.y);
                frame.sizeDelta = new Vector2(frameWidth + extra, frame.sizeDelta.y);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando talents] adding a learn button failed: " + e);
            }
        }
    }
}
