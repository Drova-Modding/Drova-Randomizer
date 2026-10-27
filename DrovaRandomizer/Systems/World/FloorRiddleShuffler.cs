using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Il2CppDrova;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Shuffles the library floor riddle's stairs sequence (vanilla Energy, Root, Fire) and swaps the
    /// clue images of the books "Geschichte der Runenmagie" I-III to match. The secret sequence stays,
    /// because its hint is localized prose.
    /// </summary>
    public sealed class FloorRiddleShuffler : IRandoSystem
    {
        private const string LibraryScene = "Scene_Overworld_Interaction_-2_23";

        private const string StairsResultVar = "FloorRuneSolved_Stairs";

        private const string BuiltPrefix = "Rando_";

        private static readonly Dictionary<string, int> ClueSlotBySprite = new()
        {
            { "GUI_Notes_Library_RuneClue_2", 0 },
            { "GUI_Notes_Library_RuneClue_0", 1 },
            { "GUI_Notes_Library_RuneClue_1", 2 },
        };

        private static readonly Dictionary<string, Sprite> BuiltSprites = new();

        private static string[]? _sequence;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            SceneStreamAccess.AddSceneListener(LibraryScene, OnLibrarySceneLoaded);
            WindowContentAccess.OnLetterShown += SwapClueImages;
            WindowContentAccess.OnJournalLetterShown += controller => SwapClueImages(controller._letterRoot);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _sequence = seed is { FloorRunes.Count: > 0 } ? [.. seed.FloorRunes] : null;
            if (_sequence != null)
            {
                MelonLogger.Msg("Floor riddle shuffle configured: " + string.Join(" -> ", _sequence) + ".");
            }

            Scene scene = SceneManager.GetSceneByName(LibraryScene);
            if (scene.IsValid() && scene.isLoaded)
            {
                ApplyToScene(scene);
            }
        }

        private static void OnLibrarySceneLoaded(Scene scene)
        {
            try
            {
                if (_sequence != null)
                {
                    ApplyToScene(scene);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Floor riddle scene apply failed: " + e);
            }
        }

        private static void ApplyToScene(Scene scene)
        {
            string[] order = _sequence ?? FloorClueTable.VanillaSequence;
            foreach (FloorRiddleController controller in SceneStreamAccess.GetComponentsInScene<FloorRiddleController>(scene))
            {
                foreach (FloorRiddleController.Sequence sequence in controller._sequences)
                {
                    if (sequence.ResultVar == null || sequence.ResultVar.name != StairsResultVar)
                    {
                        continue;
                    }
                    sequence.Elements.Clear();
                    foreach (string rune in order)
                    {
                        if (!Enum.TryParse(rune, out FloorRiddleController.Element element))
                        {
                            MelonLogger.Error("Floor riddle: unknown element '" + rune + "'.");
                            return;
                        }
                        sequence.Elements.Add(element);
                    }
                    MelonLogger.Msg("Floor riddle stairs sequence set to "
                        + string.Join(" -> ", order) + ".");
                }
            }
        }

        private static void SwapClueImages(Component root)
        {
            try
            {
                if (_sequence == null || root == null)
                {
                    return;
                }
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    Sprite sprite = image.sprite;
                    if (sprite == null || sprite.name.StartsWith(BuiltPrefix, StringComparison.Ordinal)
                        || !ClueSlotBySprite.TryGetValue(sprite.name, out int slot))
                    {
                        continue;
                    }
                    string rune = _sequence[slot];
                    if (rune == FloorClueTable.VanillaSequence[slot])
                    {
                        continue;
                    }
                    Sprite? built = GetSprite(rune, sprite.pixelsPerUnit);
                    if (built != null)
                    {
                        image.sprite = built;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Floor riddle letter swap failed: " + e);
            }
        }

        private static Sprite? GetSprite(string runeName, float pixelsPerUnit)
        {
            if (BuiltSprites.TryGetValue(runeName, out Sprite? cached) && cached != null)
            {
                return cached;
            }

            FloorClueArt art = FloorClueTable.Generated.GetValueOrDefault(runeName);
            int width = art.W;
            int height = art.H;
            Color32[]? pixels = RuneShuffler.DecodeHex(art.Hex, width * height);
            if (pixels == null)
            {
                MelonLogger.Warning("Floor riddle: no clue art for '" + runeName + "'.");
                return null;
            }

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = BuiltPrefix + "FloorClue_" + runeName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = BuiltPrefix + "FloorClue_" + runeName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            BuiltSprites[runeName] = sprite;
            return sprite;
        }
    }
}
