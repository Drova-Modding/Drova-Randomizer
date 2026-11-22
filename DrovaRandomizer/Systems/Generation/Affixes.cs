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

        public const string Poison = "poison";
        public const string Bleed = "bleed";

        private const int EnemyAffixKinds = 6;

        /// <summary>One in this many elites with a proc triggers it on every hit.</summary>
        private const int AlwaysProcOneIn = 4;

        private static readonly float[] EnemyWeights = [70f, 18f, 8f, 3f, 1f];
        private static readonly float[] ItemWeights = [55f, 25f, 12f, 6f, 2f];

        /// <summary>The spells a proc can cast, by item id.</summary>
        private static readonly string[] Spells =
        [
            "flow_aggressiveCry", "flow_ballista", "flow_battleCry", "flow_blast", "flow_blink", "flow_bloodRage",
            "flow_bruntShot", "flow_bullRush", "flow_conflagration", "flow_conjure_insects", "flow_conjure_pack",
            "flow_conjure_rats", "flow_conjure_spiritCall", "flow_cyclone", "flow_dash", "flow_dashStab",
            "flow_energyProjectile", "flow_execute", "flow_ferventCry", "flow_fireCry", "flow_flowForm", "flow_fogForm",
            "flow_graspingRoots", "flow_heavyBlow", "flow_infection", "flow_magicNuke", "flow_magicVision",
            "flow_natureTouch", "flow_obsidianSpear", "flow_pierce", "flow_poisonArrow", "flow_ricochet", "flow_root_aoe",
            "flow_root_cone", "flow_shockingBlow", "flow_spartanKick", "flow_spiritShot", "flow_sunder",
            "flow_thunderstorm", "flow_travelForm", "flow_venomCry", "flow_whirlingEnergy", "flow_wound",
        ];

        /// <summary>Readable id of an item's rarity variant.</summary>
        public static string VariantId(string baseId, int rarity, int variant)
        {
            return baseId + "_r" + rarity + "v" + variant;
        }

        /// <summary>How much more experience an elite gives: 2, 5, 10 and 17 times.</summary>
        public static int ExperienceFactor(int rarity)
        {
            return 1 + rarity * rarity;
        }

        /// <summary>Rarity of a found item.</summary>
        public static int RollItemRarity(SeededRandom random)
        {
            return random.PickWeighted(ItemWeights);
        }

        /// <summary>
        /// An enemy's modifiers, the same for a seed and spawner guid on every load and machine. The rarity is also
        /// the number of affixes, and every elite gets some health on top.
        /// </summary>
        public static EnemyAffix RollEnemy(ulong seed, string guid)
        {
            SeededRandom random = new(seed ^ Generator.Fnv("elite" + guid));
            int rarity = random.PickWeighted(EnemyWeights);
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
                    // Its own stream, so the other rolls of a seed stay as they are.
                    _ => affix with
                    {
                        Proc = RollProc(random),
                        ProcChance = new SeededRandom(seed ^ Generator.Fnv("always" + guid)).Next(AlwaysProcOneIn) == 0 ? 1f : ProcChance,
                    },
                };
            }
            return affix;
        }

        /// <summary>A variant's modifiers, fixed by its id so they need no save state.</summary>
        public static ItemAffix RollItem(string variantId, int rarity)
        {
            SeededRandom random = new(Generator.Fnv(variantId));
            float power = 1f + MathF.Round(Scale(random, rarity, 0.06f, 0.12f), 2);
            bool proc = rarity == MaxRarity || random.Next(4) < rarity - 1;
            return new ItemAffix(rarity, power, proc ? RollProc(random) : "");
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
