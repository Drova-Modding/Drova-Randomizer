# Drova Randomizer

A randomizer for *Drova - Forsaken Kin*. It shuffles what chests, containers, quest rewards and shops hand out,
and optionally critters, ore veins, caches, loose items, muggings and kill/learning milestones. Cave entrances,
rune riddles, Mandragora plants, monsters and teachers can be shuffled too. No server needed.

## Install

1. Install MelonLoader and the [Drova Modding API](https://github.com/Drova-Modding/Drova-Modding-API) 0.7.0 or newer.
2. Extract `DrovaRandomizer.zip` into the game folder.

Don't play a randomizer run and an ArchipelagoDrova seed on the same save.

## Play

- **New game:** the randomizer window opens in character creation. Pick a seed, a preset and options, switch
  **Randomize** on and finish your character. The intro is skipped.
- **Existing save:** press **F3**, set up the run and press **Start run**.

The run is saved with your game. The spoiler log is in `UserData/DrovaRandomizer/`. Same seed and options give
the same run.

- Keys, quest items and energy crystals stay where they are (except in Carnage mode), so a seed can't lock itself.
- With shops on, traders sell the randomized items at their normal price. Keys, quest items, experience and
  talents never end up in a shop.
- Pick the faction you will join. Quests of the other faction never hold progression.

**Co-op** (with the co-op mod): everyone plays the host's run. Your own run comes back when you leave. All players
need the same randomizer version. With shared loot on, a location one player loots or buys from is done for
everyone.

**Console:** `rando_start [seed] [default|minimal|completionist] [option=value ...]`, for example
`rando_start 4242 default traders=1`. The option keys are in `Systems/Generation/RandoOptions.cs`.

## Develop

- `dotnet build DrovaRandomizer` builds and copies the DLL to the game (`GamePath` in `Directory.Build.props`).
  A Release build also writes `dist/DrovaRandomizer.zip`.
- `dotnet test DrovaRandomizer.Tests` checks the generator over many seeds and options.
- In-game tests need Debug builds of the mod and the API:
  `dotnet run --project "../Drova Modding API/tools/Drova.TestRunner" -c Debug -- tests/scenarios/smoke.scn`.
- `python tools/gen_data.py` regenerates the item and location tables after a game update. The scripts in
  `tools/extract_locations` produce the other tables.

## License

Code: MIT (`LICENSE`). The rune art in `assets/` may only be used in connection with Drova (`assets/LICENSE`).
