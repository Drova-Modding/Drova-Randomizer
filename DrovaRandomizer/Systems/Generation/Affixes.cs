namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// The rolls of the rarities option. A rarity runs from 0 (common) to <see cref="MaxRarity"/> (legendary),
    /// the game's own item rarity scale.
    /// </summary>
    internal static class Affixes
    {
        public const int MaxRarity = 4;

        /// <summary>Differently rolled copies of each item per rarity.</summary>
        public const int Variants = 2;

        /// <summary>Chance per hit that a proc triggers.</summary>
        public const float ProcChance = 0.2f;

        /// <summary>The elite frequency choice whose weights the rolls of 0.3.0 used.</summary>
        public const int NormalFrequency = 1;

        public const string Poison = "poison";
        public const string Bleed = "bleed";

        public const string Vampiric = "vampiric";
        public const string Thorns = "thorns";
        public const string Explosive = "explosive";
        public const string Jailer = "jailer";
        public const string Summoner = "summoner";
        public const string Regenerating = "regenerating";

        public const string Leech = "leech";
        public const string Strength = "strength";
        public const string Dexterity = "dexterity";
        public const string Mind = "mind";
        public const string Vitality = "vitality";
        public const string Crit = "crit";

        /// <summary>The spell a jailer roots its target with.</summary>
        public const string JailerSpell = "flow_graspingRoots";

        /// <summary>The spell a volatile elite bursts with when it dies.</summary>
        public const string ExplosionSpell = "flow_blast";

        /// <summary>Set pieces are epic, below the uniques. An epic or legendary kill also drops a missing piece.</summary>
        public const int SetPieceRarity = 3;

        private const float SetPiecePower = 1.3f;

        /// <summary>The rarity an elite needs at least to lead the common creatures of its kind around it.</summary>
        private const int PackLeaderRarity = 3;

        private const int EnemyAffixKinds = 6;

        /// <summary>One in this many poisoning elites poisons on every hit.</summary>
        private const int AlwaysProcOneIn = 4;

        /// <summary>Chance a legendary drop is one of the <see cref="Uniques"/>.</summary>
        private const float UniqueChance = 0.25f;

        /// <summary>The rarity weights per elite frequency choice: low, normal, high and hell.</summary>
        private static readonly float[][] EnemyWeights =
        [
            [85f, 10f, 3.5f, 1f, 0.5f],
            [70f, 18f, 8f, 3f, 1f],
            [50f, 27f, 14f, 6f, 3f],
            [20f, 30f, 25f, 15f, 10f],
        ];

        private static readonly float[] ItemWeights = [55f, 25f, 12f, 6f, 2f];

        /// <summary>Chance by rarity that an elite has one of the <see cref="Specials"/> on top of its modifiers.</summary>
        private static readonly float[] SpecialChance = [0f, 0.2f, 0.35f, 0.5f, 1f];

        private static readonly string[] Specials = [Vampiric, Thorns, Explosive, Jailer, Summoner, Regenerating];

        private static readonly string[] WeaponBonuses = [Leech, Strength, Dexterity, Mind, Vitality, Crit];
        private static readonly string[] ArmourBonuses = [Thorns, Strength, Dexterity, Mind, Vitality, Crit];

        /// <summary>The bands of the monster shuffle's health tiers, as in tools/gen_monster_table.py.</summary>
        private static readonly int[] HealthTierLimits = [100, 400, 900, 1600];

        /// <summary>Gold a killed elite drops by rarity, before its health tier multiplies it.</summary>
        private static readonly int[] GoldByRarity = [0, 10, 25, 60, 150];

        /// <summary>The spells a summoner calls its helpers with.</summary>
        private static readonly string[] SummonSpells = ["flow_conjure_insects", "flow_conjure_pack", "flow_conjure_rats", "flow_conjure_spiritCall"];

        /// <summary>The spells a proc can cast, by item id. Movement, vision and form spells do nothing on a hit.</summary>
        private static readonly string[] Spells =
        [
            "flow_aggressiveCry", "flow_ballista", "flow_battleCry", "flow_blast", "flow_bloodRage", "flow_bruntShot",
            "flow_bullRush", "flow_conflagration", "flow_conjure_insects", "flow_conjure_pack", "flow_conjure_rats",
            "flow_conjure_spiritCall", "flow_cyclone", "flow_dashStab", "flow_energyProjectile", "flow_execute",
            "flow_ferventCry", "flow_fireCry", "flow_graspingRoots", "flow_heavyBlow", "flow_infection", "flow_magicNuke",
            "flow_natureTouch", "flow_obsidianSpear", "flow_pierce", "flow_poisonArrow", "flow_ricochet", "flow_root_aoe",
            "flow_root_cone", "flow_shockingBlow", "flow_spartanKick", "flow_spiritShot", "flow_sunder",
            "flow_thunderstorm", "flow_venomCry", "flow_whirlingEnergy", "flow_wound",
        ];

        /// <summary>First names and epithets a legendary elite's name is put together from.</summary>
        private static readonly string[] LegendaryNames =
        [
            "Grimmaw", "Vorgath", "Ashkara", "Mordrek", "Ulvhild", "Draegor", "Nerith", "Brakka", "Velmora", "Skarn", "Hesk", "Oruna",
        ];

        private static readonly string[] LegendaryEpithets =
        [
            "the Unbroken", "the Devourer", "the Hollow", "the Bloodsworn", "the Ever-Hungry", "the Pale", "of the Black Mire",
            "the Ruin-Born", "of a Thousand Scars", "the Ashen", "the Undying", "the Grave-Walker",
        ];

        /// <summary>The hand-made legendaries. Never remove one or change its base: saves hold them by id.</summary>
        public static readonly UniqueItem[] Uniques =
        [
            new("weapon_sword_longsword", "Grimmaw's Fang", 1.5f, Bleed, Leech, 20),
            new("weapon_axe_greatAxe", "The Ashen Maul", 1.55f, "flow_blast", Strength, 10),
            new("weapon_spear_wingSpear", "Rootbinder", 1.45f, JailerSpell, Dexterity, 10),
            new("weapon_dagger_ironDagger", "Whisper of the Black Mire", 1.5f, Poison, Crit, 10),
            new("weapon_bow_battleBow", "Stormcaller", 1.45f, "flow_thunderstorm", Mind, 10),
            new("weapon_sling_legendarySlingshot", "Skarn's Eye", 1.5f, "flow_ricochet", Crit, 10),
            new("armor_chest_lendha_heavy_name", "Hide of Ulvhild", 1.4f, Poison, Thorns, 40),
            new("item_armorring_1", "Ring of the Undying", 1.4f, "flow_natureTouch", Vitality, 25),
        ];

        /// <summary>The item sets. Never remove a piece or change its base: saves hold them by id.</summary>
        public static readonly ItemSet[] Sets =
        [
            new("Vorgath's Wrath",
                [("weapon_axe_warAxe", "Vorgath's Cleaver"), ("armor_chest_bandit_T4", "Vorgath's Hide"), ("item_trinket_boxer", "Vorgath's Knuckle")],
                [(Strength, 6), (Leech, 15)]),
            new("Nerith's Veil",
                [("weapon_dagger_silence", "Nerith's Whisper"), ("armor_chest_tunic_noble_name", "Nerith's Silks"),
                    ("item_trinket_doriansHeirloom", "Nerith's Locket")],
                [(Dexterity, 6), (Crit, 10)]),
            new("The Hollow Grove",
                [("weapon_bow_huntingBow", "Hollow Grove Bow"), ("armor_chest_leather_magielager_name", "Hollow Grove Mantle"),
                    ("item_trinket_pureDmg", "Hollow Grove Charm")],
                [(Mind, 6), (Thorns, 30)]),
        ];

        /// <summary>Readable id of an item's rarity variant.</summary>
        public static string VariantId(string baseId, int rarity, int variant)
        {
            return baseId + "_r" + rarity + "v" + variant;
        }

        /// <summary>Readable id of a unique item.</summary>
        public static string UniqueId(string baseId)
        {
            return baseId + "_unique";
        }

        /// <summary>Readable id of a set piece.</summary>
        public static string SetPieceId(string baseId)
        {
            return baseId + "_set";
        }

        /// <summary>The word in a set piece's description that the tooltip replaces by the number of pieces worn.</summary>
        public static string SetCountKey(int set)
        {
            return "SETCOUNT" + set;
        }

        /// <summary>The modifiers of a set piece. Its set bonuses come on top.</summary>
        public static ItemAffix SetPieceAffix()
        {
            return new ItemAffix(SetPieceRarity, SetPiecePower, "");
        }

        /// <summary>The bonuses a set gives with that many pieces worn.</summary>
        public static IEnumerable<(string Bonus, int Amount)> SetBonuses(ItemSet set, int worn)
        {
            return set.Bonuses.Take(Math.Max(0, worn - 1));
        }

        /// <summary>A set piece's description, with the count key the tooltip fills in.</summary>
        public static string SetText(int set)
        {
            ItemSet itemSet = Sets[set];
            return itemSet.Name + " (" + SetCountKey(set) + "/" + itemSet.Pieces.Length + " worn): "
                + string.Join(", ", itemSet.Pieces.Select(piece => piece.Name)) + ". "
                + string.Join(" ", itemSet.Bonuses.Select((bonus, i) => (i + 2) + " pieces: " + BonusText(bonus.Bonus, bonus.Amount)));
        }

        /// <summary>
        /// The set piece an epic or legendary kill drops: a missing one of the set the player has most of, or null once
        /// every piece is owned. So every piece comes within as many such kills as there are pieces.
        /// </summary>
        public static string? NextSetPiece(ICollection<string> owned, SeededRandom random)
        {
            List<ItemSet> open = Sets.Where(set => set.Pieces.Any(piece => !owned.Contains(SetPieceId(piece.Base)))).ToList();
            if (open.Count == 0)
            {
                return null;
            }

            int Owned(ItemSet set) => set.Pieces.Count(piece => owned.Contains(SetPieceId(piece.Base)));
            int most = open.Max(Owned);
            List<ItemSet> closest = open.Where(set => Owned(set) == most).ToList();
            List<string> missing = closest[random.Next(closest.Count)].Pieces.Select(piece => SetPieceId(piece.Base))
                .Where(id => !owned.Contains(id)).ToList();
            return missing[random.Next(missing.Count)];
        }

        /// <summary>
        /// <see cref="RollEnemy"/>, except that a common creature next to an epic or legendary one of its kind becomes its
        /// minion. <paramref name="neighbours"/> are its pack spawners, nearest first, or null.
        /// </summary>
        public static EnemyAffix RollWithPack(ulong seed, string guid, int frequency, IReadOnlyList<string>? neighbours)
        {
            EnemyAffix own = RollEnemy(seed, guid, frequency);
            if (own.Rarity > 0 || neighbours == null)
            {
                return own;
            }

            foreach (string neighbour in neighbours)
            {
                EnemyAffix leader = RollEnemy(seed, neighbour, frequency);
                if (leader.Rarity >= PackLeaderRarity)
                {
                    return Minion(leader, neighbour);
                }
            }
            return own;
        }

        /// <summary>An uncommon with half its leader's buffs and the same hit effect, but no special.</summary>
        public static EnemyAffix Minion(EnemyAffix leader, string leaderGuid)
        {
            return new EnemyAffix(1, 1f + (leader.Health - 1f) / 2f, 1f + (leader.Damage - 1f) / 2f, leader.Armour / 2,
                leader.MagicResistance / 2, leader.Speed, leader.Proc, leader.ProcChance, "", leaderGuid);
        }

        /// <summary>How much more experience an elite gives: 2, 3, 4 and 5 times.</summary>
        public static int ExperienceFactor(int rarity)
        {
            return 1 + rarity;
        }

        /// <summary>Rarity of a found item.</summary>
        public static int RollItemRarity(SeededRandom random)
        {
            return random.PickWeighted(ItemWeights);
        }

        /// <summary>
        /// An enemy's modifiers, the same for a seed and spawner guid on every load and machine. The rarity is also
        /// the number of modifiers, and every elite gets some health on top. <paramref name="frequency"/> is the
        /// elite frequency choice.
        /// </summary>
        public static EnemyAffix RollEnemy(ulong seed, string guid, int frequency)
        {
            SeededRandom random = new(seed ^ Generator.Fnv("elite" + guid));
            int rarity = random.PickWeighted(EnemyWeights[frequency]);
            EnemyAffix affix = new(rarity, 1f + 0.25f * rarity, 1f, 0, 0, 1f, "");
            foreach (int kind in random.Sample(Enumerable.Range(0, EnemyAffixKinds).ToList(), rarity))
            {
                affix = kind switch
                {
                    0 => affix with { Health = affix.Health + Scale(random, rarity, 0.3f, 0.6f) },
                    1 => affix with { Damage = 1f + Scale(random, rarity, 0.1f, 0.2f) },
                    2 => affix with { Armour = (int)MathF.Ceiling(Scale(random, rarity, 3f, 5f)) },
                    3 => affix with { MagicResistance = (int)MathF.Ceiling(Scale(random, rarity, 3f, 5f)) },
                    4 => affix with { Speed = 1f + Scale(random, rarity, 0.04f, 0.08f) },
                    _ => affix with { Proc = RollProc(random) },
                };
            }
            // Its own stream, so the other rolls of a seed stay as they are. Only poison may trigger on every hit.
            // A bleed or spell on every hit stacks up or floods the fight with summons.
            if (affix.Proc == Poison && new SeededRandom(seed ^ Generator.Fnv("always" + guid)).Next(AlwaysProcOneIn) == 0)
            {
                affix = affix with { ProcChance = 1f };
            }

            SeededRandom special = new(seed ^ Generator.Fnv("special" + guid));
            if (rarity > 0 && special.Next(100) < SpecialChance[rarity] * 100f)
            {
                affix = affix with { Special = Specials[special.Next(Specials.Length)] };
            }
            return affix;
        }

        /// <summary>The trait words of an elite's label, one per modifier it rolled.</summary>
        public static List<string> Traits(EnemyAffix affix)
        {
            List<string> traits = [];
            if (affix.Leader != "")
            {
                traits.Add("Minion");
            }
            else if (affix.Health > 1f + 0.25f * affix.Rarity + 0.01f)
            {
                traits.Add("Tough");
            }
            if (affix.Damage > 1f)
            {
                traits.Add("Brutal");
            }
            if (affix.Armour > 0)
            {
                traits.Add("Hardened");
            }
            if (affix.MagicResistance > 0)
            {
                traits.Add("Warded");
            }
            if (affix.Speed > 1f)
            {
                traits.Add("Swift");
            }
            if (affix.Proc != "")
            {
                traits.Add(affix.Proc switch
                {
                    Poison => affix.ProcChance >= 1f ? "Toxic" : "Venomous",
                    Bleed => "Savage",
                    _ => "Arcane",
                });
            }
            if (affix.Special != "")
            {
                traits.Add(affix.Special switch
                {
                    Vampiric => "Vampiric",
                    Thorns => "Thorned",
                    Explosive => "Volatile",
                    Jailer => "Jailer",
                    Summoner => "Summoner",
                    _ => "Regenerating",
                });
            }
            return traits;
        }

        /// <summary>A legendary elite's own name, the same for a seed and spawner guid every time.</summary>
        public static string LegendaryName(ulong seed, string guid)
        {
            SeededRandom random = new(seed ^ Generator.Fnv("name" + guid));
            return LegendaryNames[random.Next(LegendaryNames.Length)] + " " + LegendaryEpithets[random.Next(LegendaryEpithets.Length)];
        }

        /// <summary>The spell a summoner elite calls its helpers with, fixed by its spawner guid.</summary>
        public static string SummonSpell(string guid)
        {
            return SummonSpells[(int)(Generator.Fnv(guid) % (ulong)SummonSpells.Length)];
        }

        /// <summary>How often a summoner calls helpers in one fight: once up to rare, twice from epic. Each call brings a pack.</summary>
        public static int SummonCasts(int rarity)
        {
            return (rarity + 1) / 2;
        }

        /// <summary>A variant's modifiers, fixed by its id so they need no save state.</summary>
        public static ItemAffix RollItem(string variantId, int rarity, bool weapon)
        {
            SeededRandom random = new(Generator.Fnv(variantId));
            float power = 1f + MathF.Round(Scale(random, rarity, 0.06f, 0.12f), 2);
            bool proc = rarity == MaxRarity || random.Next(4) < rarity - 1;
            ItemAffix affix = new(rarity, power, proc ? RollProc(random) : "");

            // Its own stream, so the bonus roll leaves power and proc of a variant unchanged.
            SeededRandom bonusRandom = new(Generator.Fnv("bonus" + variantId));
            if (rarity >= 2 || bonusRandom.Next(2) == 0)
            {
                string[] bonuses = weapon ? WeaponBonuses : ArmourBonuses;
                string bonus = bonuses[bonusRandom.Next(bonuses.Length)];
                affix = affix with { Bonus = bonus, BonusAmount = BonusAmount(bonus, rarity) };
            }
            return affix;
        }

        /// <summary>The modifiers of a unique item.</summary>
        public static ItemAffix UniqueAffix(UniqueItem unique)
        {
            return new ItemAffix(MaxRarity, unique.Power, unique.Proc, unique.Bonus, unique.BonusAmount);
        }

        /// <summary>
        /// A bonus's worth at a rarity: percent of the damage for leech and thorns, percent points for crit, points otherwise.
        /// </summary>
        public static int BonusAmount(string bonus, int rarity)
        {
            return bonus switch
            {
                Leech => 4 * rarity,
                Thorns => 8 * rarity,
                Vitality => 5 * rarity,
                _ => 2 * rarity,
            };
        }

        /// <summary>A variant's name around its base item's name, "Venomous Sword of the Bear". Null keeps the base name.</summary>
        public static string? VariantName(ItemAffix affix, string baseName)
        {
            if (affix.Proc == "" && affix.Bonus == "")
            {
                return null;
            }

            string prefix = affix.Proc switch
            {
                "" => "",
                Poison => "Venomous ",
                Bleed => "Savage ",
                _ => "Arcane ",
            };
            string suffix = affix.Bonus switch
            {
                "" => "",
                Leech => " of Leeching",
                Thorns => " of Thorns",
                Strength => " of the Bear",
                Dexterity => " of the Fox",
                Mind => " of the Owl",
                Vitality => " of Vitality",
                _ => " of Precision",
            };
            return prefix + baseName + suffix;
        }

        /// <summary>The tooltip sentence of a bonus.</summary>
        public static string BonusText(string bonus, int amount)
        {
            return bonus switch
            {
                Leech => "Heals " + amount + "% of the damage dealt.",
                Thorns => "Reflects " + amount + "% of the damage taken.",
                Strength => "+" + amount + " Strength.",
                Dexterity => "+" + amount + " Dexterity.",
                Mind => "+" + amount + " Mind.",
                Vitality => "+" + amount + " maximum health.",
                _ => "+" + amount + "% critical hit chance.",
            };
        }

        /// <summary>The monster shuffle's health tier, 0 to 4, of an actor with this base health.</summary>
        public static int HealthTier(int health)
        {
            return HealthTierLimits.Count(limit => health > limit);
        }

        /// <summary>Gold a killed elite drops.</summary>
        public static int EliteGold(int rarity, int tier)
        {
            return GoldByRarity[rarity] * (1 + tier);
        }

        /// <summary>How many items a killed elite drops once it drops any: two or three for a legendary, one or two for an epic.</summary>
        public static int DropCount(int rarity, SeededRandom random)
        {
            return rarity switch
            {
                MaxRarity => 2 + random.Next(2),
                MaxRarity - 1 => 1 + random.Next(2),
                _ => 1,
            };
        }

        /// <summary>True when a legendary drop should be a unique item.</summary>
        public static bool RollUnique(SeededRandom random)
        {
            return random.Next(100) < UniqueChance * 100f;
        }

        /// <summary>Half of all procs cast a spell.</summary>
        private static string RollProc(SeededRandom random)
        {
            return random.Next(4) switch
            {
                0 => Poison,
                1 => Bleed,
                _ => Spells[random.Next(Spells.Length)],
            };
        }

        private static float Scale(SeededRandom random, int rarity, float low, float high)
        {
            return rarity * (low + (high - low) * random.Next(101) / 100f);
        }
    }
}
