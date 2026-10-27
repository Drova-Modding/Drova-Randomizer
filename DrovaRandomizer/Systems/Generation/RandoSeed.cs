using System.Globalization;
using System.Text;
using DrovaRandomizer.Data;

namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>A generated run: what every location holds, plus the world shuffles.</summary>
    public sealed class RandoSeed
    {
        /// <summary>The seed number the run was generated from.</summary>
        public ulong Number { get; init; }

        /// <summary>The options the run was generated from.</summary>
        public RandoOptions Options { get; init; } = new();

        /// <summary>Location key -> item id, for every location of this run.</summary>
        public Dictionary<string, string> Placement { get; } = new(StringComparer.Ordinal);

        /// <summary>Item ids granted when the run starts.</summary>
        public List<string> StartItems { get; } = [];

        /// <summary>Mouth gate -> interior gate. Empty when cave entrances are vanilla.</summary>
        public Dictionary<string, string> Teleporters { get; } = new(StringComparer.Ordinal);

        /// <summary>Original rune pattern -> the pattern its door now wants. Empty when runes are vanilla.</summary>
        public Dictionary<string, string> Runes { get; } = new(StringComparer.Ordinal);

        /// <summary>The library floor riddle's stairs sequence. Empty when vanilla.</summary>
        public List<string> FloorRunes { get; } = [];

        /// <summary>Creature spawner guid -> the prefab it builds instead of its own. Only changed spawners.</summary>
        public Dictionary<string, string> Monsters { get; } = new(StringComparer.Ordinal);

        /// <summary>Teachable talent -> the talent its teachers teach instead. Empty when teachers are vanilla.</summary>
        public Dictionary<string, string> Teachers { get; } = new(StringComparer.Ordinal);

        /// <summary>Talents placed as items. Empty unless talents are items.</summary>
        public HashSet<string> TalentItems { get; } = new(StringComparer.Ordinal);

        /// <summary>Mandragora plant guid -> its spot. Empty when the plants stay where the game put them.</summary>
        public Dictionary<string, (float X, float Y)> Alraunes { get; } = new(StringComparer.Ordinal);

        /// <summary>"spoiler_&lt;seed&gt;_&lt;options hash&gt;.txt", so runs of one seed with other options do not overwrite each other.</summary>
        public string SpoilerFileName => "spoiler_" + Number.ToString(CultureInfo.InvariantCulture) + "_" + Options.Hash() + ".txt";

        /// <summary>Human-readable log of the whole run.</summary>
        public string Spoiler()
        {
            StringBuilder text = new();
            text.AppendLine("Drova Randomizer seed " + Number.ToString(CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("Options:");
            foreach (OptionDef def in RandoOptions.Defs)
            {
                text.AppendLine("    " + def.Label + ": " + Options.Describe(def));
            }

            text.AppendLine();
            text.AppendLine("Starting items:");
            foreach (string id in StartItems)
            {
                text.AppendLine("    " + ItemTable.Get(id)!.Name);
            }

            if (Teleporters.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Teleporter shuffle:");
                foreach (KeyValuePair<string, string> pair in Teleporters.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.AppendLine("    " + pair.Key + " <-> " + pair.Value);
                }
            }

            if (Runes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Rune shuffle:");
                foreach (KeyValuePair<string, string> pair in Runes.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.AppendLine("    " + pair.Key + " now requires the pattern of " + pair.Value);
                }
            }

            if (FloorRunes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Library floor riddle:");
                text.AppendLine("    stairs sequence: " + string.Join(" -> ", FloorRunes));
            }

            if (Alraunes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Mandragora plants:");
                foreach (KeyValuePair<string, (float X, float Y)> pair in Alraunes.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.AppendLine("    " + pair.Key + " at " + pair.Value.X.ToString(CultureInfo.InvariantCulture) + ","
                        + pair.Value.Y.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (Teachers.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Teacher shuffle:");
                foreach (KeyValuePair<string, string> pair in Teachers.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.AppendLine("    teachers of " + pair.Key + " teach " + pair.Value);
                }
            }

            if (Monsters.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Monster shuffle: " + Monsters.Count + " spawners build another creature (spawner guid -> prefab guid).");
                foreach (KeyValuePair<string, string> pair in Monsters.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.AppendLine("    " + pair.Key + " -> " + pair.Value);
                }
            }

            text.AppendLine();
            text.AppendLine("Locations:");
            text.AppendLine();
            foreach (KeyValuePair<string, string> pair in Placement
                .Select(p => new KeyValuePair<string, string>(LocationTable.Get(p.Key)!.Name, ItemTable.Get(p.Value)!.Name))
                .OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.AppendLine(pair.Key + ": " + pair.Value);
            }
            return text.ToString();
        }
    }
}
