using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Rendering;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Keeps world-art rune hints (riddle plates, clue stones) truthful under the rune shuffle by
    /// laying a replacement pattern over the art's grid region. Only the scenes in
    /// RuneHintTable.HintScenes are swept, because sweeping every streamed chunk costs frames.
    /// </summary>
    public sealed class RuneHintOverlay : IRandoSystem
    {
        private const string OverlayName = "Rando_RuneHint";

        private static readonly Dictionary<int, GameObject> OverlayByRenderer = new();
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        private static Dictionary<string, string>? _map;
        private static bool _anyPieceRemapped;

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            foreach (string sceneName in RuneHintTable.HintScenes)
            {
                SceneStreamAccess.AddSceneListener(sceneName, OnHintSceneLoaded);
            }
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _map = seed is { Runes.Count: > 0 } ? seed.Runes : null;
            _anyPieceRemapped = false;
            if (_map != null)
            {
                foreach (RuneHintPiece piece in RuneHintTable.Generated.Values)
                {
                    if (_map.TryGetValue(piece.Pattern, out string? replacement) && replacement != piece.Pattern)
                    {
                        _anyPieceRemapped = true;
                        break;
                    }
                }
            }
            SpriteCache.Clear();

            DestroyOverlays();

            if (_anyPieceRemapped)
            {
                int created = 0;
                foreach (string sceneName in RuneHintTable.HintScenes)
                {
                    Scene scene = SceneManager.GetSceneByName(sceneName);
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        created += SweepScene(scene);
                    }
                }
                MelonLogger.Msg("Rune hint overlays applied to " + created + " art piece(s).");
            }
        }

        private static void OnHintSceneLoaded(Scene scene)
        {
            try
            {
                if (!_anyPieceRemapped)
                {
                    return;
                }
                int created = SweepScene(scene);
                if (created > 0)
                {
                    MelonLogger.Msg("Rune hint overlays applied to " + created + " art piece(s) in " + scene.name + ".");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("RuneHintOverlay scene sweep failed: " + e);
            }
        }

        private static int SweepScene(Scene scene)
        {
            int created = 0;
            foreach (SpriteRenderer renderer in SceneStreamAccess.GetComponentsInScene<SpriteRenderer>(scene))
            {
                if (TryOverlay(renderer))
                {
                    created++;
                }
            }
            return created;
        }

        private static bool TryOverlay(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return false;
            }
            if (!RuneHintTable.Generated.TryGetValue(renderer.sprite.name, out RuneHintPiece piece))
            {
                return false;
            }
            if (_map == null || !_map.TryGetValue(piece.Pattern, out string replacement) || replacement == piece.Pattern)
            {
                return false;
            }
            int rendererId = renderer.GetInstanceID();
            if (OverlayByRenderer.TryGetValue(rendererId, out GameObject? existing) && existing != null)
            {
                return false;
            }
            return CreateOverlay(renderer, rendererId, piece, replacement);
        }

        private static bool CreateOverlay(SpriteRenderer parent, int rendererId, RuneHintPiece piece, string replacement)
        {
            Sprite? sprite = GetOverlaySprite(parent.sprite.name, piece, replacement, parent.sprite.pixelsPerUnit);
            if (sprite == null)
            {
                return false;
            }

            Vector2 pivot = parent.sprite.pivot;
            float ppu = parent.sprite.pixelsPerUnit;
            Vector2 localOffset = new(
                (piece.X + piece.OffsetX + piece.W / 2f - pivot.x) / ppu,
                (piece.Y + piece.OffsetY + piece.H / 2f - pivot.y) / ppu);

            GameObject? overlay = SpriteOverlay.Create(parent, sprite, localOffset, OverlayName);
            if (overlay == null)
            {
                return false;
            }
            OverlayByRenderer[rendererId] = overlay;
            return true;
        }

        private static Sprite? GetOverlaySprite(string pieceName, RuneHintPiece piece, string replacement, float ppu)
        {
            string key = pieceName + "->" + replacement;
            if (SpriteCache.TryGetValue(key, out Sprite? cached) && cached != null)
            {
                return cached;
            }

            bool[][]? pattern = DecodeTopDown(replacement);
            if (pattern == null)
            {
                MelonLogger.Warning("Rune hint overlay: unknown pattern '" + replacement + "'.");
                return null;
            }

            int rowOffset = piece.Quad is "SW" or "SE" ? 4 : 0;
            int colOffset = piece.Quad is "NE" or "SE" ? 4 : 0;

            Color32[] pixels = new Color32[piece.W * piece.H];
            Color32 clear = new(0, 0, 0, 0);
            for (int y = 0; y < piece.H; y++)
            {
                int rowTopDown = Mathf.Clamp((int)((piece.H - 1 - y) / piece.CellSize), 0, piece.Cells - 1);
                for (int x = 0; x < piece.W; x++)
                {
                    if (!piece.MaskAt(x, y))
                    {
                        pixels[y * piece.W + x] = clear;
                        continue;
                    }
                    int col = Mathf.Clamp((int)(x / piece.CellSize), 0, piece.Cells - 1);
                    bool on = piece.Quad == "full"
                        ? pattern[rowTopDown][col]
                        : pattern[rowOffset + rowTopDown][colOffset + col];
                    pixels[y * piece.W + x] = on ? piece.OnColor : piece.OffColor;
                }
            }

            Sprite? sprite = SpriteOverlay.CreateRuntimeSprite(pixels, piece.W, piece.H, ppu, "Rando_" + key);
            if (sprite != null)
            {
                SpriteCache[key] = sprite;
            }
            return sprite;
        }

        private static bool[][]? DecodeTopDown(string patternName)
        {
            Color32[]? pixels = RuneShuffler.DecodeHex(RuneTable.Generated.GetValueOrDefault(patternName), 64);
            if (pixels == null)
            {
                return null;
            }
            bool[][] rows = new bool[8][];
            for (int rowTopDown = 0; rowTopDown < 8; rowTopDown++)
            {
                int rowBottomUp = 7 - rowTopDown;
                rows[rowTopDown] = new bool[8];
                for (int col = 0; col < 8; col++)
                {
                    rows[rowTopDown][col] = pixels[rowBottomUp * 8 + col].a > 127;
                }
            }
            return rows;
        }

        private static void DestroyOverlays()
        {
            foreach (GameObject overlay in OverlayByRenderer.Values)
            {
                SpriteOverlay.Destroy(overlay);
            }
            OverlayByRenderer.Clear();
        }
    }
}
