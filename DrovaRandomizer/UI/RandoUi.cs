using System.Globalization;
using DrovaRandomizer.Systems;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using DrovaRandomizer.UI.Patches;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
#if DEBUG
using Drova_Modding_API.Systems.Testing;
#endif
using Il2CppCommandTerminal;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace DrovaRandomizer.UI
{
    /// <summary>
    /// The F3 window to start a run and see its state. Rect-based IMGUI only, because GUI.Window
    /// and GUILayout are stripped from this game's IMGUI module.
    /// </summary>
    public sealed class RandoUi : IRandoSystem
    {
        private const KeyCode ToggleKey = KeyCode.F3;
        private const string CharacterCreationScene = "Scene_CharacterCreation";

        private const float Margin = 16f;
        private const float Width = 520f;
        private const float Padding = 16f;
        private const float RowHeight = 26f;
        private const float RowGap = 6f;
        private const float SectionGap = 16f;
        private const float LabelWidth = 250f;
        private const float ValueWidth = 72f;
        private const float ButtonWidth = 120f;
        private const float SmallButtonWidth = 80f;
        private const float TabWidth = 92f;

        private static readonly Color Panel = new(0.102f, 0.078f, 0.063f, 0.95f);
        private static readonly Color Frame = new(0.549f, 0.353f, 0.227f, 1f);
        private static readonly Color Bone = new(0.910f, 0.875f, 0.800f, 1f);
        private static readonly Color Ash = new(0.620f, 0.580f, 0.514f, 1f);
        private static readonly Color Lichen = new(0.561f, 0.639f, 0.353f, 1f);
        private static readonly Color ButtonColor = new(0.165f, 0.129f, 0.102f, 1f);
        private static readonly Color ButtonHover = new(0.227f, 0.176f, 0.133f, 1f);
        private static readonly Color FieldColor = new(0.059f, 0.047f, 0.035f, 1f);

        private enum Tab : byte { Locations, Milestones, Items, World, Talents, Run, Options, Progress }

        private static readonly (Tab Tab, string[] Keys)[] Groups =
        [
            (Tab.Locations, [RandoOptions.Faction, RandoOptions.Chests, RandoOptions.Containers, RandoOptions.Quests,
                RandoOptions.Critters, RandoOptions.Resources, RandoOptions.Caches, RandoOptions.Pickups, RandoOptions.Traders,
                RandoOptions.Muggings]),
            (Tab.Milestones, [RandoOptions.KillChecks, RandoOptions.KillInterval, RandoOptions.AttributeChecks,
                RandoOptions.AttributeInterval, RandoOptions.TalentChecks]),
            (Tab.Items, [RandoOptions.KeepVanillaLoot, RandoOptions.StackSize, RandoOptions.QuestItemSupply, RandoOptions.HerbSupply,
                RandoOptions.MandragoraSupply, RandoOptions.StartWithTools, RandoOptions.AllItems]),
            (Tab.World, [RandoOptions.Teleporters, RandoOptions.Runes, RandoOptions.MandragoraPlacement, RandoOptions.MonsterShuffle,
                RandoOptions.Carnage]),
            (Tab.Talents, [RandoOptions.TalentItems, RandoOptions.TeacherShuffle, RandoOptions.StartingTalents, RandoOptions.RandomLearning]),
        ];

        private readonly ProgressPanel _progress = new();

        private bool _visible;
        private bool _gameplayInputOff;
        private bool _cheatRegistered;
        private bool _inCharacterCreation;
        private bool _newGameRun;
        private Tab _tab;
        private RandoOptions _options = new();
        private string _seedField = RandomSeed();
        private string _message = "";

        private float _height;

        private Texture2D? _panelTexture;
        private Texture2D? _frameTexture;
        private Texture2D? _lichenTexture;
        private GUIStyle? _title;
        private GUIStyle? _label;
        private GUIStyle? _hint;
        private GUIStyle? _on;
        private GUIStyle? _field;
        private GUIStyle? _button;
        private GUIStyle? _primary;
        private GUIStyle? _tabStyle;
        private GUIStyle? _slider;
        private GUIStyle? _thumb;

        /// <summary>True from character creation until it finishes.</summary>
        internal bool InCharacterCreation => _inCharacterCreation;

        /// <summary>Picks the run character creation starts and switches Randomize on. For tests.</summary>
        internal void ChooseNewGameRun(string seed, RandoOptions options)
        {
            _seedField = seed;
            _options = options;
            _newGameRun = true;
        }

        /// <summary>Opens the window on a tab, or closes it for "off". For tests.</summary>
        internal string Open(string tab)
        {
            _visible = tab.Trim() != "off";
            if (_visible && !Enum.TryParse(tab.Trim(), ignoreCase: true, out _tab))
            {
                return "unknown tab " + tab;
            }
            return _visible ? "open on " + _tab : "closed";
        }

        void IRandoSystem.Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
            }
            if (!_visible && _gameplayInputOff)
            {
                _gameplayInputOff = false;
                try
                {
                    InputAccess.ToggleGameplayActionMaps(true);
                }
                catch (Exception e)
                {
                    MelonLogger.Error("[Rando] could not re-enable the gameplay input maps: " + e);
                }
            }
        }

        /// <summary>Draws the window while it is open. Called from OnGUI.</summary>
        public void Draw()
        {
            if (!_visible)
            {
                return;
            }
            EnsureStyles();
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                _visible = false;
                current.Use();
                return;
            }

            if (current != null && current.type == EventType.Repaint && Core.InGameplayScene)
            {
                InputAccess.ToggleGameplayActionMaps(false);
                _gameplayInputOff = true;
            }

            bool active = Randomizer.Active;
            if (active != _tab >= Tab.Run)
            {
                _tab = active ? Tab.Run : Tab.Locations;
            }

            float width = _tab == Tab.Progress ? Math.Max(Width, _progress.Width + 2f * Padding) : Width;
            Rect box = new(Margin, Margin, width, _height);
            Framed(box);

            float x = box.x + Padding;
            float inner = width - 2f * Padding;
            float y = box.y + Padding;
            GUI.Label(Row(x, y, inner), "Drova Randomizer", _title);
            if (GUI.Button(new Rect(x + inner - SmallButtonWidth, y, SmallButtonWidth, RowHeight), "Close", _button))
            {
                _visible = false;
            }
            y += RowHeight + RowGap;

            y = active ? DrawRun(Randomizer.Seed!, x, y, inner) : DrawNewRun(x, y, inner);
            if (_message.Length > 0)
            {
                y = Hint(x, y + RowGap, inner, _message);
            }
            _height = y + Padding - box.y;
        }

        private float DrawRun(RandoSeed seed, float x, float y, float width)
        {
            y = Tabs(x, y, width, Tab.Run, Tab.Options, Tab.Progress);
            if (_tab == Tab.Options)
            {
                return DrawRunOptions(seed.Options, x, y, width);
            }
            if (_tab == Tab.Progress)
            {
                return _progress.Draw(x, y);
            }

            y = Line(x, y, "Seed", seed.Number.ToString(CultureInfo.InvariantCulture));
            int total = seed.Placement.Count;
            y = Line(x, y, "Locations checked", Randomizer.CheckedCount + " of " + total);
            GUI.DrawTexture(new Rect(x, y + 4f, width, 6f), _frameTexture);
            GUI.DrawTexture(new Rect(x, y + 4f, total == 0 ? 0f : width * Randomizer.CheckedCount / total, 6f), _lichenTexture);
            y += SectionGap;

            if (CoopAccess.IsMultiplayer)
            {
                y = Line(x, y, "Co-op", SharedLocations.Enabled ? "Shared loot" : "Each their own");
                y = Hint(x, y, width, SharedLocations.Enabled
                    ? "A location one player loots is done for everyone. The finder gets its item."
                    : "Every player checks every location for themselves.") + RowGap;
            }
            return Hint(x, y, width, "Spoiler log: UserData/DrovaRandomizer/" + seed.SpoilerFileName);
        }

        private float DrawRunOptions(RandoOptions options, float x, float y, float width)
        {
            float half = (width - RowGap) / 2f;
            foreach (Tab tab in new[] { Tab.Locations, Tab.Milestones, Tab.Items, Tab.World, Tab.Talents })
            {
                GUI.Label(Row(x, y, width), tab.ToString(), _hint);
                y += RowHeight - 6f;
                int column = 0;
                foreach (OptionDef def in RandoOptions.Defs.Where(def => !def.Retired && TabOf(def) == tab))
                {
                    GUI.Label(new Rect(x + column * (half + RowGap), y, half, RowHeight), def.Label + ": " + options.Describe(def),
                        options[def.Key] == def.Default ? _label : _on);
                    column = 1 - column;
                    y += column == 0 ? RowHeight - 4f : 0f;
                }
                y += (column == 1 ? RowHeight - 4f : 0f) + RowGap;
            }
            return y;
        }

        private float DrawNewRun(float x, float y, float width)
        {
            GUI.Label(Row(x, y, 70f), "Seed", _label);
            _seedField = GUI.TextField(new Rect(x + 70f, y, 180f, RowHeight), _seedField, 20, _field);
            if (GUI.Button(new Rect(x + 258f, y, SmallButtonWidth, RowHeight), "Random", _button))
            {
                _seedField = RandomSeed();
            }
            y += RowHeight + RowGap;

            GUI.Label(Row(x, y, 70f), "Preset", _label);
            float presetX = x + 70f;
            foreach (string preset in RandoOptions.Presets.Keys)
            {
                if (GUI.Button(new Rect(presetX, y, 128f, RowHeight), char.ToUpperInvariant(preset[0]) + preset[1..], _button))
                {
                    _options = RandoOptions.FromPreset(preset)!;
                }
                presetX += 128f + RowGap;
            }
            y += RowHeight + SectionGap;

            y = Tabs(x, y, width, Tab.Locations, Tab.Milestones, Tab.Items, Tab.World, Tab.Talents);
            foreach (OptionDef def in RandoOptions.Defs.Where(def => !def.Retired && TabOf(def) == _tab))
            {
                y = DrawOption(def, x, y, width);
            }

            y += SectionGap;
            float hintX = x + ButtonWidth + RowGap * 2f;
            float hintWidth = width - ButtonWidth - RowGap * 2f;
            if (_inCharacterCreation)
            {
                if (GUI.Button(new Rect(x, y, ButtonWidth, RowHeight), _newGameRun ? "Randomize: On" : "Randomize: Off", _newGameRun ? _primary : _button))
                {
                    _newGameRun = !_newGameRun;
                }
                return Hint(hintX, y + 4f, hintWidth, _newGameRun
                    ? "Starts when you finish character creation. The intro is skipped."
                    : "This game plays vanilla.");
            }
            if (GUI.Button(new Rect(x, y, ButtonWidth, RowHeight), "Start run", _primary))
            {
                _message = TryStart(_seedField, _options);
            }
            return Hint(hintX, y + 4f, hintWidth, "Starts on the loaded save.");
        }

        /// <summary>
        /// Character creation finishes: queues the run chosen here and forces the intro skip, since the run
        /// starts in the open world. False holds the game back for a seed that is no number.
        /// </summary>
        internal bool OnFinishCharacterCreation(Toggle? skipIntro)
        {
            if (_newGameRun)
            {
                if (!TryParseSeed(_seedField, out ulong seed))
                {
                    _message = "The seed must be a whole number.";
                    _visible = true;
                    return false;
                }
                Randomizer.StartWhenReady(seed, _options);
                if (skipIntro != null)
                {
                    skipIntro.isOn = true;
                }
            }
            _inCharacterCreation = false;
            _visible = false;
            return true;
        }

        private float DrawOption(OptionDef def, float x, float y, float width)
        {
            GUI.Label(Row(x, y, LabelWidth), def.Label, _label);
            float controlX = x + LabelWidth;
            float controlWidth = width - LabelWidth;
            if (def.IsToggle)
            {
                bool on = _options.On(def.Key);
                if (GUI.Button(new Rect(controlX, y, ValueWidth, RowHeight), on ? "On" : "Off", on ? _primary : _button))
                {
                    _options[def.Key] = on ? 0 : 1;
                }
            }
            else if (def.Choices != null)
            {
                if (GUI.Button(new Rect(controlX, y, controlWidth, RowHeight), _options.Describe(def), _button))
                {
                    _options[def.Key] = (_options[def.Key] + 1) % def.Choices.Length;
                }
            }
            else
            {
                float sliderWidth = controlWidth - ValueWidth - RowGap;
                float moved = GUI.HorizontalSlider(new Rect(controlX, y + 8f, sliderWidth, RowHeight - 12f), _options[def.Key], def.Min, def.Max, _slider, _thumb);
                _options[def.Key] = (int)MathF.Round(moved);
                GUI.Label(new Rect(controlX + sliderWidth + RowGap, y, ValueWidth, RowHeight), _options.Describe(def), _label);
            }
            return y + RowHeight + RowGap;
        }

        private float Tabs(float x, float y, float width, params Tab[] tabs)
        {
            float tabX = x;
            foreach (Tab tab in tabs)
            {
                bool selected = _tab == tab;
                if (GUI.Toggle(new Rect(tabX, y, TabWidth, RowHeight), selected, tab.ToString(), _tabStyle) && !selected)
                {
                    _tab = tab;
                }
                if (selected)
                {
                    GUI.DrawTexture(new Rect(tabX + 8f, y + RowHeight - 1f, TabWidth - 16f, 3f), _lichenTexture);
                }
                tabX += TabWidth + 4f;
            }
            y += RowHeight + 2f;
            GUI.DrawTexture(new Rect(x, y, width, 1f), _frameTexture);
            return y + SectionGap;
        }

        private static Tab TabOf(OptionDef def)
        {
            return Groups.FirstOrDefault(group => group.Keys.Contains(def.Key), (Tab.World, [])).Tab;
        }

        private float Line(float x, float y, string label, string value)
        {
            GUI.Label(Row(x, y, LabelWidth), label, _label);
            GUI.Label(Row(x + LabelWidth, y, Width - 2f * Padding - LabelWidth), value, _label);
            return y + RowHeight;
        }

        private float Hint(float x, float y, float width, string text)
        {
            float height = _hint!.CalcHeight(new GUIContent(text), width);
            GUI.Label(new Rect(x, y, width, height), text, _hint);
            return y + height;
        }

        private void Framed(Rect rect)
        {
            GUI.DrawTexture(rect, _frameTexture);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), _panelTexture);
        }

        private static Rect Row(float x, float y, float width)
        {
            return new Rect(x, y, width, RowHeight);
        }

        private static string TryStart(string seedText, RandoOptions options)
        {
            if (!TryParseSeed(seedText, out ulong seed))
            {
                return "The seed must be a whole number.";
            }
            try
            {
                Randomizer.Start(seed, options);
                return "Run " + seed + " started.";
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
        }

        private static bool TryParseSeed(string text, out ulong seed)
        {
            return ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out seed);
        }

        private static string RandomSeed()
        {
            return new Random().Next(1, int.MaxValue).ToString(CultureInfo.InvariantCulture);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static GUIStyle Text(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle style = new(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle Filled(Color background, Color hover, Color text)
        {
            GUIStyle style = new(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            style.normal.background = Solid(background);
            style.hover.background = Solid(hover);
            style.active.background = Solid(hover);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
        }

        private void EnsureStyles()
        {
            if (_title != null && _panelTexture != null)
            {
                return;
            }

            _panelTexture = Solid(Panel);
            _frameTexture = Solid(Frame);
            _lichenTexture = Solid(Lichen);

            _title = Text(18, Bone, FontStyle.Bold);
            _label = Text(14, Bone);
            _on = Text(14, Lichen, FontStyle.Bold);
            _hint = Text(12, Ash);
            _hint.alignment = TextAnchor.UpperLeft;
            _hint.wordWrap = true;

            _field = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleLeft, border = new RectOffset(0, 0, 0, 0) };
            _field.normal.background = Solid(FieldColor);
            _field.focused.background = Solid(FieldColor);
            _field.hover.background = Solid(FieldColor);
            _field.normal.textColor = Bone;
            _field.focused.textColor = Bone;
            _field.hover.textColor = Bone;

            _button = Filled(ButtonColor, ButtonHover, Bone);
            _primary = Filled(Lichen, new Color(0.635f, 0.718f, 0.416f, 1f), Panel);
            _primary.fontStyle = FontStyle.Bold;

            _tabStyle = Filled(Panel, ButtonHover, Ash);
            _tabStyle.onNormal.background = _tabStyle.normal.background;
            _tabStyle.onHover.background = _tabStyle.hover.background;
            _tabStyle.onActive.background = _tabStyle.hover.background;
            _tabStyle.onNormal.textColor = Bone;
            _tabStyle.onHover.textColor = Bone;

            _slider = new GUIStyle(GUI.skin.horizontalSlider) { border = new RectOffset(0, 0, 0, 0) };
            _slider.normal.background = Solid(FieldColor);
            _thumb = Filled(Lichen, Bone, Bone);
            _thumb.fixedWidth = 10f;
        }

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            CharacterCreationPatches.Apply(harmony);
        }

        void IRandoSystem.OnSceneLoaded(string sceneName)
        {
            if (sceneName == CharacterCreationScene)
            {
                _inCharacterCreation = true;
                _visible = true;
            }
            else if (_inCharacterCreation && sceneName == SceneNames.MainMenu)
            {
                _inCharacterCreation = false;
                _visible = false;
            }

            if (_cheatRegistered)
            {
                return;
            }
            try
            {
                CheatMenuAccess.RegisterCheat("rando_start", OnStartCommand, 0, -1,
                    "rando_start [seed] [default|minimal|completionist] [option=value ...]", "Start a randomizer run on this save");
                _cheatRegistered = true;
            }
            catch (Exception e)
            {
                MelonLogger.Msg("rando_start not available yet, retrying on the next scene: " + e.Message);
            }
        }

        private void OnStartCommand(Il2CppReferenceArray<CommandArg> args)
        {
            try
            {
                string seed = args.Length > 0 ? args[0].String : RandomSeed();
                RandoOptions? options = RandoOptions.FromPreset(args.Length > 1 ? args[1].String : "default");
                string? message = options == null ? "Unknown preset." : null;
                for (int i = 2; i < args.Length && message == null; i++)
                {
                    string[] pair = args[i].String.Split('=');
                    if (pair.Length != 2 || !int.TryParse(pair[1], out int value) || Array.Find(RandoOptions.Defs, d => d.Key == pair[0]) == null)
                    {
                        message = "Bad option '" + args[i].String + "'.";
                    }
                    else
                    {
                        options![pair[0]] = value;
                    }
                }
                message ??= TryStart(seed, options!);
                MelonLogger.Msg("rando_start: " + message);
#if DEBUG
                TestAgent.Report(message);
#endif
                ToastAccess.Show(message);
            }
            catch (Exception e)
            {
                MelonLogger.Error("rando_start failed: " + e);
            }
        }
    }
}
