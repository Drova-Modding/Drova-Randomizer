using DrovaRandomizer.Systems.Generation;

namespace DrovaRandomizer.Systems.Run
{
    /// <summary>Everything a run persists inside the Drova save. The placement itself is regenerated from seed and options.</summary>
    public class RandoState
    {
        /// <summary>Seed number of the run, or "" when this save has none.</summary>
        public string Seed { get; set; } = "";

        /// <summary>The run's options in <see cref="RandoOptions.ToString"/> form.</summary>
        public string Options { get; set; } = "";

        /// <summary><see cref="Generator.DataHash"/> of the build that started the run.</summary>
        public string DataHash { get; set; } = "";

        /// <summary>Location keys already checked.</summary>
        public HashSet<string> Checked { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Locations a co-op partner checked under shared loot. Their vanilla loot stays suppressed.</summary>
        public HashSet<string> FoundByPartner { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Item ids found but not yet handed over, granted front first.</summary>
        public List<string> PendingGrants { get; set; } = [];

        /// <summary>Enemies defeated during the run. Drives "kills#k".</summary>
        public int EnemyKills { get; set; }

        /// <summary>Attribute points bought at teachers. Drives "attributes#k".</summary>
        public int AttributesLearned { get; set; }

        /// <summary>Talents learned from teachers. Drives "talents#k".</summary>
        public int TalentsLearned { get; set; }

        /// <summary>Units of its placed item bought per shop slot ("traderGuid:itemGuid" -> count).</summary>
        public Dictionary<string, int> TraderUnitsBought { get; set; } = new();

        /// <summary>Talents learned under random learning. Picks the next random talent.</summary>
        public int RandomLearns { get; set; }

        /// <summary>True once an outro was reached.</summary>
        public bool GoalReached { get; set; }

        /// <summary>True once the run's starting items were queued.</summary>
        public bool StartGranted { get; set; }

        /// <summary>True once the player was warned about joining the faction the run was not made for.</summary>
        public bool FactionWarned { get; set; }

        /// <summary>This player's progress in co-op hosts' runs, keyed by "seed|options".</summary>
        public Dictionary<string, RandoState> Guests { get; set; } = new(StringComparer.Ordinal);
    }
}
