using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.World.Patches;
using Il2CppDrova.Utilities.LazyLoading;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Monster shuffle: gives creature spawners (LazyActor) another creature prefab. The prefab
    /// reference is swapped on the spawner before it builds a body, in LazyActor.Awake, which runs as
    /// its chunk scene streams in. The spawner keeps its guid, so its save state, kill credit and
    /// co-op identity stay the same.
    /// </summary>
    public sealed class MonsterShuffler : IRandoSystem
    {
        private static readonly Dictionary<string, string> VanillaPrefabs = new(StringComparer.Ordinal);
        private static Dictionary<string, string> _prefabs = new(StringComparer.Ordinal);

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            MonsterPatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            _prefabs = seed?.Monsters ?? new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Object found in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LazyActor>()))
            {
                LazyActor? lazy = found.TryCast<LazyActor>();
                if (lazy != null && lazy.gameObject.scene.IsValid() && Swap(lazy) && lazy.IsLoaded)
                {
                    lazy.Unload();
                }
            }
        }

        internal static void OnAwake(LazyActor spawner)
        {
            try
            {
                Swap(spawner);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando monsters] LazyActor.Awake postfix failed: " + e);
            }
        }

        private static bool Swap(LazyActor lazy)
        {
            string guid = lazy._guidstring;
            if (string.IsNullOrEmpty(guid) || lazy._actorReference == null)
            {
                return false;
            }

            string current = lazy._actorReference.AssetGUID;
            if (!VanillaPrefabs.TryGetValue(guid, out string? vanilla))
            {
                if (!_prefabs.ContainsKey(guid))
                {
                    return false;
                }
                vanilla = current;
                VanillaPrefabs[guid] = vanilla;
            }

            string wanted = _prefabs.GetValueOrDefault(guid, vanilla);
            if (wanted == current)
            {
                return false;
            }
            lazy._actorReference = new AssetReferenceGameObject(wanted);
            return true;
        }
    }
}
