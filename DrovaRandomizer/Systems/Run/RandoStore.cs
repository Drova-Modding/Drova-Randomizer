using Drova_Modding_API.Systems.SaveGame.Store;
using Il2CppDrova.Saveables;
using MelonLoader;
using Newtonsoft.Json;

namespace DrovaRandomizer.Systems.Run
{
    /// <summary>The run state, persisted inside the native .sav through the Modding API's save system.</summary>
    public class RandoStore : IStorable
    {
        private const string StoreKey = "DrovaRandomizer_State";

        /// <summary>The key the state is stored under in the save.</summary>
        public string SaveGameKey => StoreKey;

        /// <summary>State of the loaded save. Replaced on every load and reset.</summary>
        public RandoState State { get; private set; } = new();

        /// <summary>Raised on the main thread once <see cref="Load"/> populated the state.</summary>
        public event Action? OnStateLoaded;

        /// <summary>Serializes the state, "" on failure.</summary>
        public string Save()
        {
            try
            {
                return JsonConvert.SerializeObject(State);
            }
            catch (Exception e)
            {
                MelonLogger.Error("Failed to serialize the randomizer state: " + e);
                return "";
            }
        }

        /// <summary>
        /// Writes the state into the live savegame's data bag so the next disk write carries it.
        /// Drova's autosave never fires the API's BeforeSaveGameSaving, so <see cref="Save"/> alone would
        /// miss every autosave. Main thread only.
        /// </summary>
        public void PushToLiveSavegame()
        {
            try
            {
                SavegameData? data = Savegame.Current?.Data;
                string json = Save();
                if (data != null && !string.IsNullOrEmpty(json))
                {
                    data.SetString(StoreKey, json);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Could not push the randomizer state into the live savegame: " + e.Message);
            }
        }

        /// <summary>Reads the state from the save, empty when it does not parse.</summary>
        public void Load(string result)
        {
            try
            {
                State = JsonConvert.DeserializeObject<RandoState>(result) ?? new RandoState();
            }
            catch (Exception e)
            {
                MelonLogger.Error("Failed to read the randomizer state, starting empty: " + e);
                State = new RandoState();
            }

            try
            {
                OnStateLoaded?.Invoke();
            }
            catch (Exception e)
            {
                MelonLogger.Error("OnStateLoaded handler failed: " + e);
            }
        }

        /// <summary>Wipes the state. A new game fires no load, so the main menu resets it too.</summary>
        public void Reset()
        {
            State = new RandoState();
        }
    }
}
