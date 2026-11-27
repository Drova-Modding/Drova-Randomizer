# Drova Randomizer

A randomizer for *Drova - Forsaken Kin*. It shuffles what chests, containers, quest rewards and shops hand out,
and optionally critters, ore veins, caches, loose items, muggings and kill/learning milestones. Cave entrances,
rune riddles, Mandragora plants, monsters and teachers can be shuffled too. No server needed.

## Install

1. Install MelonLoader and the [Drova Modding API](https://github.com/Drova-Modding/Drova-Modding-API) 0.9.3 or newer.
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
- With **Item rarities and elite enemies** on, enemies and NPCs can come up as elites with more health, damage,
  armour, magic resistance or speed, or an effect on their hits (some poison on every hit). They are a bit larger,
  give two to five times the experience, and once you fight them show their traits above them in their rarity's colour
  (Tough, Swift, Venomous, ...); legendary elites carry a name of their own. Some elites have a special trait: Vampiric,
  Thorned, Volatile (bursts when it dies), Jailer (roots you), Summoner or Regenerating. **Elite frequency** sets how
  many there are, from Low to Hell. Options > Modding hides these names or changes their size. Elite humans wear and
  wield gear of their rarity. Weapons and armour you find can be a rarity above their own, up to legendary: stronger,
  as their tooltip shows, and from rare upwards with a chance to poison, cause bleeding or cast one of the game's
  attack, summoning or battle cry spells on a hit. Most also carry a bonus (life leech, thorns, an attribute, maximum
  health or critical hit chance) and are named after what they do. Killed elites drop gold and sometimes items that
  suit their strength. Strong legendary elites can drop one of eight unique items. Every epic or legendary elite also
  drops a missing piece of one of three item sets, whose tooltips show how many pieces you wear. Epic and legendary
  creatures lead the common ones of their kind around them as minions. The variants are custom items in
  `Mods/Modding_API/Items/DrovaRandomizer_rarities.json`. Delete that file after removing the mod and saves fall back
  to the normal items.

**Co-op** (with the co-op mod): everyone plays the host's run. Your own run comes back when you leave. All players
need the same randomizer version. With shared loot on, a location one player loots or buys from is done for
everyone.

**Console:** `rando_start [seed] [default|minimal|completionist|chaos] [option=value ...]`, for example
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
