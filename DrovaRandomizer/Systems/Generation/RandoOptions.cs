using System.Globalization;

namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>The consumable stack size option, in its choice order.</summary>
    public enum ConsumableStackSize
    {
        Full,
        Small,
        Single,
    }

    /// <summary>
    /// One option. A toggle has range 0..1 and no <see cref="Choices"/>. A choice stores the index
    /// into <see cref="Choices"/>. A <see cref="Retired"/> option is no longer offered.
    /// </summary>
    public sealed record OptionDef(string Key, string Label, int Default, int Min, int Max, string[]? Choices = null, bool Retired = false)
    {
        /// <summary>True for an on/off option.</summary>
        public bool IsToggle => Choices == null && Min == 0 && Max == 1;
    }

    /// <summary>The settings a run is generated from. Persisted as the values in <see cref="Defs"/> order, "1.0.1...".</summary>
    public sealed class RandoOptions
    {
        public const string Faction = "faction";
        public const string Chests = "chests";
        public const string Containers = "containers";
        public const string Quests = "quests";
        public const string Critters = "critters";
        public const string Resources = "resources";
        public const string Caches = "caches";
        public const string Pickups = "pickups";
        public const string Traders = "traders";
        /// <summary>Retired. Kept so the options after it keep their position.</summary>
        public const string TraderStackSize = "trader_stack_size";
        public const string Muggings = "muggings";
        public const string KillChecks = "kill_checks";
        public const string KillInterval = "kill_interval";
        public const string AttributeChecks = "attribute_checks";
        public const string AttributeInterval = "attribute_interval";
        public const string TalentChecks = "talent_checks";
        public const string KeepVanillaLoot = "keep_vanilla_loot";
        public const string StackSize = "stack_size";
        public const string QuestItemSupply = "quest_item_supply";
        public const string HerbSupply = "herb_supply";
        public const string MandragoraSupply = "mandragora_supply";
        public const string StartWithTools = "start_with_tools";
        public const string AllItems = "all_items";
        public const string Teleporters = "teleporters";
        public const string Runes = "runes";
        public const string Carnage = "carnage";
        public const string MandragoraPlacement = "mandragora_placement";
        public const string MonsterShuffle = "monster_shuffle";
        public const string TalentItems = "talent_items";
        public const string TeacherShuffle = "teacher_shuffle";
        public const string StartingTalents = "starting_talents";
        public const string RandomLearning = "random_learning";

        /// <summary>
        /// Every option in display and storage order. Append new options at the end and never remove or
        /// reorder one: saves and the co-op host's run (at most 256 characters on the wire) store values by position.
        /// </summary>
        public static readonly OptionDef[] Defs =
        [
            new(Faction, "Faction you will join", 0, 0, 1, ["Nemeton", "Remnants"]),
            new(Chests, "Chests", 1, 0, 1),
            new(Containers, "Barrels, crates, sacks", 1, 0, 1),
            new(Quests, "Quest rewards", 1, 0, 1),
            new(Critters, "Critters and carcasses", 0, 0, 1),
            new(Resources, "Ore veins and herbs", 0, 0, 1),
            new(Caches, "Hidden caches", 0, 0, 1),
            new(Pickups, "Loose items (very long)", 0, 0, 1),
            new(Traders, "Shops sell random items", 0, 0, 1),
            new(TraderStackSize, "Checks per trader stack", 1, 1, 1, Retired: true),
            new(Muggings, "Mugging NPCs", 0, 0, 1),
            new(KillChecks, "Enemy kill milestones", 0, 0, 50),
            new(KillInterval, "Kills per milestone", 10, 1, 100),
            new(AttributeChecks, "Attribute milestones", 0, 0, 80),
            new(AttributeInterval, "Points per milestone", 5, 1, 20),
            new(TalentChecks, "Talent milestones", 0, 0, 10),
            new(KeepVanillaLoot, "Keep vanilla loot too", 0, 0, 1),
            new(StackSize, "Consumable stack size", 0, 0, 2, ["Full", "Small", "Single"]),
            new(QuestItemSupply, "Quest item supply", 8, 0, 20),
            new(HerbSupply, "Permanent herb supply", 5, 0, 20),
            new(MandragoraSupply, "Mandragora supply", 7, 0, 20),
            new(StartWithTools, "Start with pickaxe and spear", 1, 0, 1),
            new(AllItems, "Allow quest-property items", 0, 0, 1),
            new(Teleporters, "Shuffle cave entrances", 0, 0, 1),
            new(Runes, "Shuffle rune riddles", 0, 0, 1),
            new(MandragoraPlacement, "Mandragora plants", 0, 0, 1, ["Vanilla", "Shuffled"]),
            new(MonsterShuffle, "Monster shuffle", 0, 0, 2, ["Off", "Same tier", "Chaos"]),
            new(TalentItems, "Talents as found items", 0, 0, 1),
            new(TeacherShuffle, "Shuffle what teachers teach", 0, 0, 1),
            new(StartingTalents, "Starting talents", 0, 0, 3),
            new(Carnage, "Carnage (may soft-lock)", 0, 0, 1),
            new(RandomLearning, "Random talent at teachers", 0, 0, 1),
        ];

        /// <summary>Preset name -> the location toggles it sets. Everything else keeps its default.</summary>
        public static readonly Dictionary<string, string[]> Presets = new()
        {
            { "default", [] },
            { "minimal", [Chests] },
            { "completionist", [Chests, Containers, Quests, Critters, Resources, Caches, Pickups] },
        };

        private static readonly string[] PresetToggles = [Chests, Containers, Quests, Critters, Resources, Caches, Pickups];

        private readonly Dictionary<string, int> _values = new(StringComparer.Ordinal);

        /// <summary>The defaults.</summary>
        public RandoOptions()
        {
            foreach (OptionDef def in Defs)
            {
                _values[def.Key] = def.Default;
            }
        }

        /// <summary>Value of an option, clamped to its range on write. Unknown keys throw.</summary>
        public int this[string key]
        {
            get => _values[key];
            set
            {
                OptionDef def = Array.Find(Defs, d => d.Key == key) ?? throw new KeyNotFoundException(key);
                _values[key] = Math.Clamp(value, def.Min, def.Max);
            }
        }

        /// <summary>True when a toggle is on.</summary>
        public bool On(string key) => _values[key] != 0;

        /// <summary>A preset's options, or null for an unknown name.</summary>
        public static RandoOptions? FromPreset(string? name)
        {
            if (!Presets.TryGetValue(name ?? "", out string[] toggles))
            {
                return null;
            }

            RandoOptions options = new();
            if (name != "default")
            {
                foreach (string key in PresetToggles)
                {
                    options[key] = Array.IndexOf(toggles, key) >= 0 ? 1 : 0;
                }
            }
            return options;
        }

        /// <summary>Parses <see cref="ToString"/>. Missing or unreadable values keep their default.</summary>
        public static RandoOptions Parse(string? text)
        {
            RandoOptions options = new();
            string[] values = (text ?? "").Split('.');
            for (int i = 0; i < values.Length && i < Defs.Length; i++)
            {
                if (int.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    options[Defs[i].Key] = value;
                }
            }
            return options;
        }

        /// <summary>The values in <see cref="Defs"/> order, dot-separated.</summary>
        public override string ToString()
        {
            return string.Join(".", Defs.Select(def => _values[def.Key].ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>Six hex digits identifying these options.</summary>
        public string Hash()
        {
            return (Generator.Fnv(ToString()) & 0xFFFFFF).ToString("x6");
        }

        /// <summary>The value as the player reads it.</summary>
        public string Describe(OptionDef def)
        {
            int value = _values[def.Key];
            if (def.Choices != null)
            {
                return def.Choices[value];
            }
            return def.IsToggle ? (value != 0 ? "on" : "off") : value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
