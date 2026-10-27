using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.World.Patches;
using Il2CppDrova;
using Il2CppDrova.Saveables;
using MelonLoader;
using UnityEngine;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Moves the Mandragora plants (PickUp_Alraune with <see cref="Saveable_Alraune"/>) to the run's
    /// spots. The plants live in the always-loaded gameplay scene and turn into the fleeing creature
    /// where they stand, so moving the plant moves the whole encounter and keeps its save state.
    /// </summary>
    public sealed class AlrauneShuffler : IRandoSystem
    {
        /// <summary>Plant guid -> its transform, for the plants started so far.</summary>
        internal static readonly Dictionary<string, Transform> Plants = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Vector3> VanillaPositions = new(StringComparer.Ordinal);
        private static Dictionary<string, Vector2> _spots = new(StringComparer.Ordinal);

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            AlraunePatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _spots = seed == null
                ? new Dictionary<string, Vector2>(StringComparer.Ordinal)
                : seed.Alraunes.ToDictionary(p => p.Key, p => new Vector2(p.Value.X, p.Value.Y));
            foreach (string guid in Plants.Keys.ToList())
            {
                Apply(guid);
            }
        }

        internal static void OnStart(Saveable_Alraune plant)
        {
            try
            {
                GuidComponent owner = plant.GetComponentInParent<GuidComponent>();
                if (!owner)
                {
                    return;
                }
                string guid = owner._guidString;
                Plants[guid] = owner.transform;
                VanillaPositions[guid] = owner.transform.position;
                Apply(guid);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando alraune] Start postfix failed: " + e);
            }
        }

        private static void Apply(string guid)
        {
            Transform plant = Plants[guid];
            if (!plant)
            {
                Plants.Remove(guid);
                return;
            }
            Vector3 vanilla = VanillaPositions[guid];
            plant.position = _spots.TryGetValue(guid, out Vector2 spot) ? new Vector3(spot.x, spot.y, vanilla.z) : vanilla;
        }
    }
}
