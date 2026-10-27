using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DrovaRandomizer.Data
{
    /// <summary>What kind of act checks a location. Each is its own option toggle or counter.</summary>
    public enum LocationCategory
    {
        Chest,
        Container,
        Critter,
        Resource,
        Cache,
        Pickup,
        Quest,
        Trader,
        Mugging,
        Kills,
        Attributes,
        Talents
    }

    /// <summary>A location's faction. Joining one faction locks the other's quests and merchants.</summary>
    public enum Faction
    {
        Neutral,
        Nemeton,
        Remnants
    }

    /// <summary>
    /// One location, keyed by the game's own identity: a GuidComponent guid (one per container, however
    /// many items it holds), "traderGuid:itemGuid" ("#unitN" for further stock units), a muggable
    /// NPC's LazyActor guid, a quest's GVarList name, or "kills#N" / "attributes#N" / "talents#N".
    /// <see cref="Number"/> is the unit or milestone number, 1 for a base location.
    /// </summary>
    public sealed record LocationDef(string Key, string Name, LocationCategory Category, string Area, Faction Faction, int Number);

    /// <summary>Lookups over the generated rows in LocationTable.g.cs (tools/gen_data.py).</summary>
    public static partial class LocationTable
    {
        private static Dictionary<string, LocationDef>? _byKey;

        private static string? NormalizeGuid(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            StringBuilder hex = new(32);
            foreach (char c in raw)
            {
                if (c is >= '0' and <= '9' or >= 'a' and <= 'f')
                {
                    hex.Append(c);
                }
                else if (c is >= 'A' and <= 'F')
                {
                    hex.Append((char)(c + 32));
                }
                else if (c is not ('-' or '{' or '}' or ' '))
                {
                    return null;
                }
            }

            if (hex.Length != 32)
            {
                return null;
            }

            hex.Insert(20, '-');
            hex.Insert(16, '-');
            hex.Insert(12, '-');
            hex.Insert(8, '-');
            return hex.ToString();
        }

        /// <summary>The location for a key in any guid spelling, or null.</summary>
        public static LocationDef? Get(string? key)
        {
            if (_byKey == null)
            {
                Dictionary<string, LocationDef> built = new(All.Length, StringComparer.Ordinal);
                foreach (LocationDef location in All)
                {
                    built[Canonical(location.Key)] = location;
                }
                _byKey = built;
            }
            return key != null && _byKey.TryGetValue(Canonical(key), out LocationDef? found) ? found : null;
        }

        /// <summary>The trader slot location for a bought item, or null. Unit n of it is "&lt;key&gt;#unitn".</summary>
        public static LocationDef? GetTraderSlot(string rawTraderGuid, string rawItemGuid)
        {
            return Get(rawTraderGuid + ":" + rawItemGuid);
        }

        /// <summary>The authored ("_fixLoot") contents of a randomized container as "readable_id:amount" entries.</summary>
        public static bool TryGetAuthoredLoot(string? rawGuid, [NotNullWhen(true)] out string[]? entries)
        {
            entries = null;
            string? key = NormalizeGuid(rawGuid);
            return key != null && AuthoredLoot.TryGetValue(key, out entries);
        }

        private static string Canonical(string key)
        {
            int hash = key.IndexOf('#');
            string suffix = hash < 0 ? "" : key.Substring(hash);
            string body = hash < 0 ? key : key.Substring(0, hash);
            int colon = body.IndexOf(':');
            if (colon > 0)
            {
                string? trader = NormalizeGuid(body.Substring(0, colon));
                string? item = NormalizeGuid(body.Substring(colon + 1));
                return trader != null && item != null ? trader + ":" + item + suffix : key;
            }
            return (NormalizeGuid(body) ?? body) + suffix;
        }
    }
}
