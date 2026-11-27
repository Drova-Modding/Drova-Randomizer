using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Il2CppDrova;
using Il2CppDrova.GUI;
using Il2CppDrova.Items;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>The trait labels above the elites the player fought, in their rarity's colour, and their options.</summary>
    internal static class EliteLabels
    {
        private const string LabelsShownKey = "DrovaRandomizer_EliteLabels";
        private const string LabelSizeKey = "DrovaRandomizer_EliteLabelSize";
        private const float DefaultLabelSize = 0.7f;

        /// <summary>How far above an elite's health bar its label appears, in world units.</summary>
        private const float LabelGap = 8f;

        /// <summary>Where a label goes above an elite without a health bar, from its centre in world units.</summary>
        private const float LabelHeight = 28f;

        /// <summary>Elite bodies the player fought here, so an elite shows its label once per load and drops loot when it dies.</summary>
        private static readonly HashSet<int> LabelledBodies = [];

        /// <summary>The labels that follow their elite, above its health bar, until it dies or unloads.</summary>
        private static readonly List<(Actor Actor, FloatingText Label, Transform? Bar)> Labels = [];

        private static bool _labelsShown = true;
        private static float _labelSize = DefaultLabelSize;

#if DEBUG
        internal static int LabelCount => LabelledBodies.Count;
#endif

        internal static void Initialize()
        {
            OptionMenuAccess.Instance.OnOptionMenuOpen += BuildOptions;
            OptionMenuAccess.Instance.OnOptionMenuClose += ReadOptions;
        }

        internal static void Clear()
        {
            LabelledBodies.Clear();
            Labels.Clear();
        }

        /// <summary>The player fought an elite: it shows its label, once per load.</summary>
        internal static void OnFought(Actor elite, EnemyAffix affix)
        {
            if (LabelledBodies.Add(elite.GetInstanceID()))
            {
                ShowLabel(elite, affix);
            }
        }

        internal static bool WasFought(Actor elite)
        {
            return LabelledBodies.Contains(elite.GetInstanceID());
        }

        /// <summary>An elite's trait words, under its own name for a legendary and its leader's for a legendary's minion.</summary>
        internal static string LabelText(Actor actor, EnemyAffix affix)
        {
            string text = string.Join(", ", Affixes.Traits(affix));
            if (affix.Rarity == Affixes.MaxRarity && Rarities.Seed is { } seed)
            {
                return Affixes.LegendaryName(seed, actor.GetGuidComponent().GuidString) + "\n" + text;
            }
            if (affix.Leader != "" && Rarities.Seed is { } packSeed && Affixes.RollEnemy(packSeed, affix.Leader, Rarities.Frequency).Rarity == Affixes.MaxRarity)
            {
                return "Minion of " + Affixes.LegendaryName(packSeed, affix.Leader) + "\n" + text;
            }
            return text;
        }

        /// <summary>Puts an elite's traits above it in the game's rarity colour, under its own name for a legendary.</summary>
        private static void ShowLabel(Actor actor, EnemyAffix affix)
        {
            GameObject? prefab = Resources.Load<GameObject>("FloatingText");
            if (prefab == null)
            {
                return;
            }

            string text = LabelText(actor, affix);
            Color colour = GUIGameHandler.TryGet(out GUIGameHandler? gui) && gui != null
                ? gui.GetRarityToColorMapping().GetColorFromRarity((Item.ItemRarity)affix.Rarity)
                : Color.white;
            ShowNPCBar? bar = actor.GetComponentInChildren<ShowNPCBar>(true);
            FloatingText label = Object.Instantiate(prefab).GetComponent<FloatingText>();
            // The game's floating text rises and fades out on its own. A label stays put.
            label.enabled = false;
            label.SetText(text, colour);
            label._text.ForceMeshUpdate();
            Labels.Add((actor, label, bar != null ? bar.transform : null));
            MoveLabels();
        }

        /// <summary>Keeps each label above its elite and removes it once the elite is dead or gone.</summary>
        internal static void MoveLabels()
        {
            for (int i = Labels.Count - 1; i >= 0; i--)
            {
                (Actor actor, FloatingText label, Transform? bar) = Labels[i];
                if (label && actor && actor.GetHealth() != null && !actor.GetHealth().IsDead)
                {
                    bool shown = _labelsShown && actor.gameObject.activeInHierarchy;
                    label.gameObject.SetActive(shown);
                    label.transform.localScale = Vector3.one * _labelSize;
                    // The text is centred on its position, so every line past the first would reach half a line further down.
                    TMP_TextInfo? info = label._text.textInfo;
                    float raise = info != null && info.lineCount > 1
                        ? (info.lineCount - 1) * info.lineInfo[0].lineHeight / 2f * label._text.transform.lossyScale.y
                        : 0f;
                    Vector3 anchor = bar != null ? bar.position + Vector3.up * LabelGap : actor.GetCenterPosition() + Vector2.up * LabelHeight;
                    label.transform.position = anchor + Vector3.up * raise;
                    continue;
                }

                if (label)
                {
                    Object.Destroy(label.gameObject);
                }
                Labels.RemoveAt(i);
            }
        }

        private static void BuildOptions()
        {
            OptionMenuAccess.Instance.GetBuilder("DrovaRandomizer")?
                .CreateTitle(LocalizationAccess.GetLocalizedString(RarityItems.LocaPath, "OptionsTitle"))
                .CreateSwitch(LocalizationAccess.GetLocalizedString(RarityItems.LocaPath, "OptionLabels"), LocalizationAccess.GetLocalizedString(RarityItems.LocaPath, "OptionLabelsOn"),
                    LocalizationAccess.GetLocalizedString(RarityItems.LocaPath, "OptionLabelsOff"), LabelsShownKey, true)
                .CreateSlider(LocalizationAccess.GetLocalizedString(RarityItems.LocaPath, "OptionLabelSize"), LabelSizeKey, 0.3f, 2f, DefaultLabelSize)
                .Build();
        }

        internal static void ReadOptions()
        {
            _labelsShown = !ConfigAccessor.TryGetConfigValue(LabelsShownKey, out bool shown) || shown;
            _labelSize = ConfigAccessor.TryGetConfigValue(LabelSizeKey, out float size) ? size : DefaultLabelSize;
        }
    }
}
