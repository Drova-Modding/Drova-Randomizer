#if DEBUG
using DrovaRandomizer.Access;
#endif
using DrovaRandomizer.Systems;
using DrovaRandomizer.Systems.Checks;
using DrovaRandomizer.Systems.Localization;
using DrovaRandomizer.Systems.Loot;
using DrovaRandomizer.Systems.Run;
using DrovaRandomizer.Systems.Talents;
using DrovaRandomizer.Systems.World;
using DrovaRandomizer.UI;
using Drova_Modding_API.Access;
using Drova_Modding_API.GlobalFields;
using Drova_Modding_API.Systems.SaveGame;
using Il2CppDrova;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(DrovaRandomizer.Core), "DrovaRandomizer", "0.1.0", "TrustNoOneElse")]
[assembly: MelonGame("Just2D", "Drova")]
[assembly: MelonAdditionalDependencies("Drova_Modding_API")]

namespace DrovaRandomizer
{
    public class Core : MelonMod
    {
        private const int MaxGuiFailStreak = 15;

        private int _guiFailStreak;
        private int _lastGuiFailFrame = -1;

        /// <summary>The F3 window and toasts.</summary>
        public static RandoUi Ui { get; private set; } = null!;

        /// <summary>Every system in init and update order. The randomizer comes first because it starts and ends the run.</summary>
        internal static IReadOnlyList<IRandoSystem> Systems { get; private set; } = [];

        /// <summary>True while Scene_Gameplay_Main is loaded.</summary>
        public static bool InGameplayScene { get; private set; }

        /// <summary>The player. Null until PlayerAccess.OnPlayerFound fires.</summary>
        public static Actor? Player { get; private set; }

        public override void OnInitializeMelon()
        {
            PlayerAccess.OnPlayerFound += player => Player = player;
            SaveGameSystem.BeforeSaveGameLoaded += _ => Player = null;
            Ui = new RandoUi();

            Systems =
            [
                new Randomizer(),
                new SharedLocations(),
                new QuestTracker(),
                new ContainerTracker(),
                new TraderTracker(),
                new GoalTracker(),
                new KillTracker(),
                new LearnTracker(),
                new FactionWatch(),
                new LootSuppressor(),
                new TeleporterShuffler(),
                new RuneShuffler(),
                new RuneHintOverlay(),
                new FloorRiddleShuffler(),
                new MonsterShuffler(),
                new RandomTalent(),
                new TalentShuffler(),
                new AlrauneShuffler(),
                new ItemLocaDefaults(),
                Ui,
#if DEBUG
                new RandoTestCommands(),
#endif
            ];
            foreach (IRandoSystem system in Systems)
            {
                system.Initialize(HarmonyInstance);
            }

            LoggerInstance.Msg("Initialized. Press F3 for the randomizer window.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (sceneName == SceneNames.GameplayMain)
                {
                    InGameplayScene = true;
                }
                else if (sceneName == SceneNames.MainMenu)
                {
                    InGameplayScene = false;
                    Player = null;
                }

                foreach (IRandoSystem system in Systems)
                {
                    system.OnSceneLoaded(sceneName);
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error("OnSceneWasLoaded failed: " + e);
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (sceneName == SceneNames.GameplayMain)
            {
                InGameplayScene = false;
                Player = null;
            }
        }

        public override void OnUpdate()
        {
            try
            {
                foreach (IRandoSystem system in Systems)
                {
                    system.Update();
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error("OnUpdate failed: " + e);
            }
        }

        public override void OnGUI()
        {
            if (_guiFailStreak >= MaxGuiFailStreak)
            {
                return;
            }

            try
            {
                Ui.Draw();
            }
            catch (Exception e)
            {
                int frame = Time.frameCount;
                if (frame == _lastGuiFailFrame)
                {
                    return;
                }
                _guiFailStreak = frame == _lastGuiFailFrame + 1 ? _guiFailStreak + 1 : 1;
                _lastGuiFailFrame = frame;
                LoggerInstance.Error("OnGUI failed (frame streak " + _guiFailStreak + "/" + MaxGuiFailStreak + "): " + e);
            }
        }
    }
}
