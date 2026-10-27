using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Shuffles the rune-drawing riddles per the run's map of original -> replacement pattern name.
    /// A rune door and its hint note show the same DrawRune_* art, so remapping both by that name
    /// keeps every riddle solvable. Textures are rebuilt from RuneTable pixels, because the other
    /// puzzle's bundle may not be loaded.
    /// </summary>
    public sealed class RuneShuffler : IRandoSystem
    {
        private const string BuiltPrefix = "Rando_";

        private static readonly Dictionary<string, Texture2D> BuiltTextures = new();
        private static readonly Dictionary<string, Sprite> BuiltSprites = new();

        private static Dictionary<string, string>? _map;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            WindowContentAccess.RegisterDrawingTargetTransformer(TransformDrawingTarget);
            WindowContentAccess.OnLetterShown += SwapRuneImages;
            WindowContentAccess.OnJournalLetterShown += SwapRuneImages;
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _map = seed is { Runes.Count: > 0 } ? seed.Runes : null;
            if (_map != null)
            {
                MelonLogger.Msg("Rune shuffle configured: " + _map.Count + " patterns.");
            }
        }

        private static Texture2D? TransformDrawingTarget(Texture2D targetTexture)
        {
            try
            {
                if (_map == null || targetTexture == null || targetTexture.name.StartsWith(BuiltPrefix, StringComparison.Ordinal))
                {
                    return null;
                }
                if (_map.TryGetValue(targetTexture.name, out string replacement)
                    && replacement != targetTexture.name)
                {
                    return GetTexture(replacement);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Rune target swap failed: " + e);
            }
            return null;
        }

        private static void SwapRuneImages(Component root)
        {
            try
            {
                if (_map == null || root == null)
                {
                    return;
                }
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    Sprite sprite = image.sprite;
                    if (sprite == null || sprite.name.StartsWith(BuiltPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (_map.TryGetValue(sprite.name, out string replacement) && replacement != sprite.name)
                    {
                        Sprite? built = GetSprite(replacement, sprite.pixelsPerUnit);
                        if (built != null)
                        {
                            image.sprite = built;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Rune letter swap failed: " + e);
            }
        }

        /// <summary>
        /// RRGGBBAA hex to RGBA32 pixels in Unity's bottom-up SetPixels32 order, or null when the hex
        /// does not hold exactly <paramref name="count"/> pixels.
        /// </summary>
        internal static Color32[]? DecodeHex(string? hex, int count)
        {
            if (hex == null || hex.Length != count * 8)
            {
                return null;
            }

            Color32[] pixels = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * 8;
                pixels[i] = new Color32(Convert.ToByte(hex.Substring(o, 2), 16), Convert.ToByte(hex.Substring(o + 2, 2), 16),
                    Convert.ToByte(hex.Substring(o + 4, 2), 16), Convert.ToByte(hex.Substring(o + 6, 2), 16));
            }
            return pixels;
        }

        private static Texture2D? GetTexture(string runeName)
        {
            if (BuiltTextures.TryGetValue(runeName, out Texture2D? cached) && cached != null)
            {
                return cached;
            }

            Color32[]? pixels = DecodeHex(RuneTable.Generated.GetValueOrDefault(runeName), 64);
            if (pixels == null)
            {
                MelonLogger.Warning("Rune map names unknown pattern '" + runeName + "'; keeping vanilla.");
                return null;
            }

            Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
            {
                name = BuiltPrefix + runeName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            BuiltTextures[runeName] = texture;
            return texture;
        }

        private static Sprite? GetSprite(string runeName, float pixelsPerUnit)
        {
            if (BuiltSprites.TryGetValue(runeName, out Sprite? cached) && cached != null)
            {
                return cached;
            }

            Texture2D? texture = GetTexture(runeName);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = BuiltPrefix + runeName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            BuiltSprites[runeName] = sprite;
            return sprite;
        }
    }
}
