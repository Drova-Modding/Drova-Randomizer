# Changelog

## [0.4.0] - 2026-11-28
### Added
- Elites can have a special trait: Vampiric (heals from its hits), Thorned (hurts whoever hits it), Volatile (bursts when it dies), Jailer (roots its target), Summoner (calls a pack once or twice per fight) or Regenerating. Every legendary elite has one.
- Weapons and armour can carry a bonus: life leech (weapons), thorns (armour), Strength, Dexterity, Mind, maximum health or critical hit chance. Every rare or better item has one.
- A variant with a hit effect or bonus is named after it, such as "Venomous Short Sword of the Bear".
- Eight unique legendaries with their own names, such as Grimmaw's Fang and the Ring of the Undying. Strong legendary elites can drop them.
- New option "Elite frequency": Low, Normal, High or Hell.
- Killed elites drop gold. Epic elites drop up to two items and legendary elites two or three. A drop suits the strength of the elite that dropped it.
- Three item sets of three pieces each: Vorgath's Wrath, Nerith's Veil and The Hollow Grove. Two pieces worn give a bonus and three a second one. A piece's tooltip shows how many of its set you wear. Every epic or legendary elite you kill drops a piece you don't have yet, from the set you are closest to completing, so every piece can be had.
- Packs: common creatures near an epic or legendary one of their kind are its minions, with half its strength and the same hit effect.

### Changed
- Once you fight an elite, its traits (Tough, Brutal, Hardened, Warded, Swift, Venomous, Toxic, Savage, Arcane) stay above its health bar in its rarity's colour until it dies. Legendary elites also carry a name of their own. Options > Modding can hide these names or change their size.
- Rarity variants are only handed out above their item's own rarity, so the colour and rarity in the tooltip always tell a variant apart. Variants already in a save keep working.
- Elites give 2 to 5 times the experience instead of 2 to 17 times.
- Only poison triggers on every hit now. Procs no longer cast movement, vision or form spells. Items you already have may cast a different spell.

### Fixed
- An elite human with a rarity weapon now also triggers its own rolled hit effect.
- Co-op: a client's rarity weapon now hits as hard on the host as it does on the client, and its poison and bleeding count. Before, a client's weapon power was lost on creatures.
- Co-op: every player who fought an elite gets its drop, whoever dealt the last blow. Before, a client never got one.
- Co-op: summoning, regenerating and vampiric elites act once, on the host, whichever player fights them. Leech, thorns and hit effects apply once.
- Poison and bleeding from hit effects count as the attacker's damage, so a kill by them gives experience.
- Bernold's chest in the Ruins is no longer a location. Passing out after Eugen's shots puts all your belongings into it, and opening it as a location could take some of them away.

## [0.3.0] - 2026-11-22
### Added
- New option "Item rarities and elite enemies". Enemies and NPCs can be elites with more health, damage, armour, magic resistance or speed, or a hit effect (some trigger it on every hit). Elites are larger, give much more experience and can drop an item of their rarity, and elite humans carry gear of their rarity.
- Weapons and armour you find can be uncommon to legendary, with higher stats shown in the tooltip and, from rare upwards, a chance to poison, cause bleeding or cast any of the game's spells on a hit.

## [0.2.0] - 2026-11-03
### Added
- Loose items show the icon of the item placed there.
- New "Chaos" preset: every shuffle on and at its maximum (monsters shuffled across tiers), including quest-property items and Carnage.

### Changed
- "Completionist" also shuffles shops, muggings, cave entrances, rune riddles, Mandragora plants, monsters (same tier), talents as found items and what teachers teach, and adds the random talent at teachers.
- The random talent at a teacher uses the API's learn window, so teachers with many talents show them all.
- Needs Drova Modding API 0.8.0 or newer.

## [0.1.0] - 2026-10-27
First release.
