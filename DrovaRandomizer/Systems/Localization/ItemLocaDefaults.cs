using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Talents;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Il2CppCustomFramework.Localization;
using Il2CppDrova.Items;
using MelonLoader;
using static Il2CppCustomFramework.Localization.LocalizationDB;

namespace DrovaRandomizer.Systems.Localization
{
    /// <summary>
    /// Names the items the game never named (NPC-only armor and the like), so a randomized drop does
    /// not arrive as "[LOCA] Key not found". The items are pointed at a loca path this mod owns,
    /// because a loca path is a whole file and the game's Items file cannot take one extra key.
    /// That path also mirrors the game's rarity and weapon-type text, which the redirect moves too.
    /// </summary>
    public sealed class ItemLocaDefaults : IRandoSystem
    {
        private static readonly HashSet<string> Redirected = [];

        private static bool _entriesWritten;

        void IRandoSystem.OnSceneLoaded(string sceneName)
        {
            if (sceneName == SceneNames.GameplayMain)
            {
                ApplyToItemDatabase();
            }
            else if (sceneName == SceneNames.MainMenu)
            {
                WriteLocalizationEntries();
            }
        }

        private static void WriteLocalizationEntries()
        {
            if (_entriesWritten) return;

            try
            {
                List<LocalizationAccess.LocalizationEntry> entries = [];
                foreach (KeyValuePair<ELanguage, Dictionary<string, string>> language in ItemLocaTable.Entries)
                {
                    foreach (KeyValuePair<string, string> entry in language.Value)
                    {
                        entries.Add(new LocalizationAccess.LocalizationEntry(entry.Key, entry.Value, language.Key));
                    }
                    bool german = language.Key == ELanguage.de;
                    entries.Add(new LocalizationAccess.LocalizationEntry(RandomTalent.NameKey,
                        german ? "Zuf\u00e4lliges Talent" : "Random talent", language.Key));
                    entries.Add(new LocalizationAccess.LocalizationEntry(RandomTalent.DescriptionKey, german
                        ? "Lehrt ein zuf\u00e4lliges Talent, das du jetzt lernen kannst."
                        : "Teaches a random talent you can learn right now.", language.Key));
                }

                LocalizationAccess.CreateLocalizationEntries(entries, ItemLocaTable.LocaPath);
                _entriesWritten = true;
            }
            catch (Exception e)
            {
                MelonLogger.Error("ItemLocaDefaults.WriteLocalizationEntries failed: " + e);
            }
        }

        private static void ApplyToItemDatabase()
        {
            try
            {
                if (Redirected.Count == ItemLocaTable.RedirectedItems.Length) return;

                if (!LocaPathIsReadable()) return;

                SubDatabase_Item? database = ProviderAccess.ItemDatabase;
                if (database == null) return;

                foreach (string readableId in ItemLocaTable.RedirectedItems)
                {
                    if (Redirected.Contains(readableId)) continue;

                    Item item = database.GetItemByReadableId(readableId);
                    if (!item) continue;

                    item.SetLocalization(ItemLocaTable.LocaPath);
                    Redirected.Add(readableId);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("ItemLocaDefaults.ApplyToItemDatabase failed: " + e);
            }
        }

        private static bool LocaPathIsReadable()
        {
            LocalizationDB database = LocalizationDB.Instance;
            if (!database)
            {
                return false;
            }

            return database.LocaPathExists(ItemLocaTable.LocaPath, "common", database);
        }
    }
}
