using DrovaRandomizer.Systems.Generation;

namespace DrovaRandomizer.Systems
{
    /// <summary>One part of the randomizer. <see cref="Core.Systems"/> drives them all, in its order.</summary>
    internal interface IRandoSystem
    {
        /// <summary>Once, at melon init: hooks, event subscriptions, test commands.</summary>
        void Initialize(HarmonyLib.Harmony harmony)
        {
        }

        /// <summary>After every scene load.</summary>
        void OnSceneLoaded(string sceneName)
        {
        }

        /// <summary>
        /// Applies the run in effect to the loaded world, or vanilla for null. Runs on (de)activation and when
        /// the world is ready after a load.
        /// </summary>
        void Configure(RandoSeed? seed)
        {
        }

        /// <summary>A run was activated on the loaded save: catch up on what the save already did.</summary>
        void CatchUp()
        {
        }

        /// <summary>Main thread, every frame.</summary>
        void Update()
        {
        }
    }
}
