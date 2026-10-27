using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Systems.SaveGame;
using MelonLoader;
using MelonLoader.Utils;

namespace DrovaRandomizer.Systems.Run
{
    /// <summary>The loaded save's run, its checks and the item hand-over.</summary>
    public sealed class Randomizer : IRandoSystem
    {
        private const string OpenWorldScene = "Scene_OpenWorldRoot";

        private static readonly RandoStore Store = new();
        private static readonly ItemGranter Granter = new();

        private static readonly RandoState NoRun = new();

        private static bool _persistDirty;

        private static (ulong Seed, RandoOptions Options)? _queued;

        private static string? _hostRun;

        private static string? _hostKey;
        private static string _hostHash = "";

        private static string DataDirectory => Path.Combine(MelonEnvironment.UserDataDirectory, "DrovaRandomizer");

        /// <summary>The loaded save's run, or null when the save has none.</summary>
        public static RandoSeed? Seed { get; private set; }

        /// <summary>True while the loaded save is a randomizer run.</summary>
        [MemberNotNullWhen(true, nameof(Seed))]
        public static bool Active => Seed != null;

        /// <summary>
        /// The progress the active run writes to. Under a co-op host's run that is a guest state, so the
        /// save's own run is never rewritten.
        /// </summary>
        public static RandoState State
        {
            get
            {
                RandoState own = Store.State;
                if (_hostKey == null || _hostKey == RunOf(own))
                {
                    return own;
                }
                if (_hostKey == "")
                {
                    return NoRun;
                }
                if (!own.Guests.TryGetValue(_hostKey, out RandoState guest))
                {
                    string[] parts = _hostKey.Split('|');
                    guest = new RandoState { Seed = parts[0], Options = parts[1] };
                    own.Guests[_hostKey] = guest;
                }
                guest.DataHash = _hostHash;
                return guest;
            }
        }

        /// <summary>The run in effect as "seed|options|data hash", "" without one. What a co-op host shares.</summary>
        public static string RunValue => _hostRun ?? (Active ? RunOf(State) + "|" + Generator.DataHash() : "");

        /// <summary>The active run's "seed|options", the same on every machine of a co-op session.</summary>
        public static string RunKey => RunOf(State);

        /// <summary>Checks done in this run.</summary>
        public static int CheckedCount => Active ? State.Checked.Count : 0;

        /// <summary>
        /// Starts a run on the loaded save. Throws <see cref="InvalidOperationException"/> when that is not possible.
        /// </summary>
        public static void Start(ulong seed, RandoOptions options)
        {
            if (!Core.InGameplayScene)
            {
                throw new InvalidOperationException("Load or start a game first.");
            }
            if (Active)
            {
                throw new InvalidOperationException("This save already is randomizer run " + Seed.Number + ".");
            }
            if (_hostRun != null)
            {
                throw new InvalidOperationException("In a co-op session the host's run applies.");
            }

            RandoSeed generated = Generator.Generate(seed, options);
            Dictionary<string, RandoState> guests = Store.State.Guests;
            Store.Reset();
            State.Guests = guests;
            State.Seed = seed.ToString(CultureInfo.InvariantCulture);
            State.Options = options.ToString();
            State.DataHash = Generator.DataHash();
            WriteSpoiler(generated);
            Activate(generated);
            MelonLogger.Msg("Started randomizer run " + seed + " with " + generated.Placement.Count + " locations.");
        }

        /// <summary>Starts this run once the new game's world is ready. For character creation.</summary>
        public static void StartWhenReady(ulong seed, RandoOptions options)
        {
            _queued = (seed, options);
        }

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            SaveGameSystem.Instance.AddStore(Store);
            Store.OnStateLoaded += OnStateLoaded;
            SaveGameSystem.BeforeSaveGameLoaded += _ => EndRun();
            PlayerAccess.OnPlayerFound += _ => OnWorldReady();
            CoopAccess.ShareHostSetting("DrovaRandomizer.Run", () => RunValue, EnforceHostRun, needsReload: true);
        }

        void IRandoSystem.OnSceneLoaded(string sceneName)
        {
            if (sceneName == SceneNames.MainMenu)
            {
                _queued = null;
                EndRun();
            }
            else if (sceneName == OpenWorldScene && _queued != null)
            {
                PlayerAccess.StartWaitForPlayerCoroutine();
            }
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            Granter.StackSize = seed == null ? ConsumableStackSize.Full : (ConsumableStackSize)seed.Options[RandoOptions.StackSize];
        }

        void IRandoSystem.Update()
        {
            Pump();
        }

        private static void EnforceHostRun(string? run)
        {
            if (run == _hostRun)
            {
                return;
            }
            _hostRun = run;
            string[] parts = (run ?? "").Split('|');
            _hostKey = run == null ? null : parts[0] == "" ? "" : parts[0] + "|" + (parts.Length > 1 ? parts[1] : "");
            _hostHash = parts.Length > 2 ? parts[2] : "";
            if (Core.InGameplayScene)
            {
                OnStateLoaded();
            }
        }

        private static void OnStateLoaded()
        {
            if (!ulong.TryParse(State.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
            {
                Deactivate();
                return;
            }

            try
            {
                Activate(Generator.Generate(seed, RandoOptions.Parse(State.Options)));
                MelonLogger.Msg("Loaded randomizer run " + seed + ": " + State.Checked.Count + " of "
                    + Seed.Placement.Count + " locations checked.");
                if (_hostKey != null && _hostHash != "" && _hostHash != Generator.DataHash())
                {
                    Warn("The co-op host runs another randomizer version: placements will differ between your machines.");
                }
                else if (_hostKey == null && State.DataHash != Generator.DataHash())
                {
                    Warn("This run was generated with other data; unchecked locations may have moved.");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Regenerating randomizer run " + seed + " failed, the save plays vanilla: " + e);
                Deactivate();
            }
        }

        private static void EndRun()
        {
            Store.Reset();
            Deactivate();
        }

        private static void Deactivate()
        {
            if (Seed == null)
            {
                return;
            }
            Seed = null;
            ConfigureWorld();
        }

        private static void OnWorldReady()
        {
            if (_queued is { } queued && Core.InGameplayScene)
            {
                _queued = null;
                try
                {
                    Start(queued.Seed, queued.Options);
                    return;
                }
                catch (InvalidOperationException e)
                {
                    Warn(e.Message);
                }
            }
            if (!Active && (_hostKey ?? RunOf(Store.State)) != "")
            {
                OnStateLoaded();
                return;
            }
            ConfigureWorld();
        }

        private static void ConfigureWorld()
        {
            foreach (IRandoSystem system in Core.Systems)
            {
                system.Configure(Seed);
            }
        }

        /// <summary>True when this location holds an item in the current run.</summary>
        [MemberNotNullWhen(true, nameof(Seed))]
        public static bool IsLocation([NotNullWhen(true)] LocationDef? location)
        {
            return Active && location != null && Seed.Placement.ContainsKey(location.Key);
        }

        /// <summary>True once this location was checked in the current run.</summary>
        public static bool IsChecked([NotNullWhen(true)] LocationDef? location)
        {
            return Active && location != null && State.Checked.Contains(location.Key);
        }

        /// <summary>
        /// Checks a location and hands over its placed item. <paramref name="grant"/> is false when the trade
        /// already gave it. Repeats and non-locations are ignored.
        /// </summary>
        public static void Check(LocationDef location, string source, bool grant = true)
        {
            if (!IsLocation(location) || !State.Checked.Add(location.Key))
            {
                return;
            }

            ItemDef item = ItemTable.Get(Seed.Placement[location.Key])!;
            if (grant)
            {
                State.PendingGrants.Add(item.Id);
            }
            _persistDirty = true;
            MelonLogger.Msg("[Rando] " + source + ": '" + location.Name + "' holds '" + item.Name + "'.");
            ToastAccess.Show(item.Name + "  (" + location.Name + ")");
            SharedLocations.Send(location);
        }

        /// <summary>
        /// Marks a location a co-op partner checked as checked here, without the item. False for repeats and
        /// non-locations.
        /// </summary>
        public static bool CheckForPartner(string key)
        {
            LocationDef? location = LocationTable.Get(key);
            if (!IsLocation(location) || !State.Checked.Add(location.Key))
            {
                return false;
            }
            State.FoundByPartner.Add(location.Key);
            _persistDirty = true;
            MelonLogger.Msg("[Rando] co-op partner checked '" + location.Name + "'.");
            ToastAccess.Show(location.Name + "  (found by your partner)");
            return true;
        }

        /// <summary>True when a co-op partner, not this player, checked this location.</summary>
        public static bool IsFoundByPartner([NotNullWhen(true)] LocationDef? location)
        {
            return Active && location != null && State.FoundByPartner.Contains(location.Key);
        }

        /// <summary>Tells the player in a toast and the log.</summary>
        public static void Warn(string warning)
        {
            MelonLogger.Warning(warning);
            ToastAccess.Show(warning);
        }

        /// <summary>Flags the state for the next push into the live savegame.</summary>
        public static void MarkDirty()
        {
            _persistDirty = true;
        }

        /// <summary>Records that an ending was reached.</summary>
        public static void ReachGoal()
        {
            if (!Active || State.GoalReached)
            {
                return;
            }
            State.GoalReached = true;
            _persistDirty = true;
            MelonLogger.Msg("[Rando] goal reached in run " + Seed.Number + ".");
        }

        private static void Pump()
        {
            while (Active && State.PendingGrants.Count > 0 && Granter.TryGrant(State.PendingGrants[0]))
            {
                State.PendingGrants.RemoveAt(0);
                _persistDirty = true;
            }

            if (_persistDirty)
            {
                _persistDirty = false;
                Store.PushToLiveSavegame();
            }
        }

        [MemberNotNull(nameof(Seed))]
        private static void Activate(RandoSeed seed)
        {
            Seed = seed;
            if (!State.StartGranted)
            {
                State.StartGranted = true;
                State.PendingGrants.AddRange(seed.StartItems);
            }
            ConfigureWorld();
            _persistDirty = true;

            foreach (IRandoSystem system in Core.Systems)
            {
                system.CatchUp();
            }
        }

        private static string RunOf(RandoState state)
        {
            return state.Seed == "" ? "" : state.Seed + "|" + state.Options;
        }

        private static void WriteSpoiler(RandoSeed seed)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string path = Path.Combine(DataDirectory, seed.SpoilerFileName);
                File.WriteAllText(path, seed.Spoiler());
                MelonLogger.Msg("Spoiler log written to " + path + ".");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Could not write the spoiler log: " + e.Message);
            }
        }
    }
}
