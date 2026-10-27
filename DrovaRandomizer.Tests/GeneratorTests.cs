using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Xunit;

namespace DrovaRandomizer.Tests
{
    public class GeneratorTests
    {
        private static ItemDef Item(string id) => ItemTable.Get(id)!;

        private static LocationDef Location(string key) => LocationTable.Get(key)!;

        public static IEnumerable<object[]> Variants()
        {
            foreach (string preset in RandoOptions.Presets.Keys)
            {
                for (int faction = 0; faction < 2; faction++)
                {
                    RandoOptions options = RandoOptions.FromPreset(preset)!;
                    options[RandoOptions.Faction] = faction;
                    yield return [preset + "/faction" + faction, options.ToString(), 25];
                }
            }

            RandoOptions everything = RandoOptions.FromPreset("completionist")!;
            foreach (string key in new[] { RandoOptions.Traders, RandoOptions.Muggings, RandoOptions.AllItems, RandoOptions.Teleporters, RandoOptions.Runes })
            {
                everything[key] = 1;
            }
            everything[RandoOptions.KillChecks] = 50;
            everything[RandoOptions.AttributeChecks] = 80;
            everything[RandoOptions.TalentChecks] = 10;
            everything[RandoOptions.Faction] = 1;
            yield return ["everything", everything.ToString(), 5];

            RandoOptions carnage = RandoOptions.FromPreset("minimal")!;
            carnage[RandoOptions.Carnage] = 1;
            carnage[RandoOptions.Teleporters] = 1;
            carnage[RandoOptions.Muggings] = 1;
            carnage[RandoOptions.Traders] = 1;
            yield return ["carnage", carnage.ToString(), 10];
        }

        [Theory]
        [MemberData(nameof(Variants))]
        public void EverySeedIsValidAndBeatable(string name, string optionText, int seeds)
        {
            RandoOptions options = RandoOptions.Parse(optionText);
            List<LocationDef> enabled = Generator.EnabledLocations(options);
            Dictionary<string, string> rules = Generator.Rules(enabled);
            Faction faction = options[RandoOptions.Faction] == 0 ? Faction.Nemeton : Faction.Remnants;

            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                RandoSeed run = Generator.Generate(seed * 7919, options);

                Assert.Equal(enabled.Count, run.Placement.Count);
                Assert.All(enabled, location => Assert.True(run.Placement.ContainsKey(location.Key), location.Name));
                Assert.All(run.Placement.Values, id => Assert.NotNull(Item(id)));

                foreach (LocationDef location in enabled)
                {
                    ItemDef item = Item(run.Placement[location.Key]);
                    bool otherFaction = location.Faction != Faction.Neutral && location.Faction != faction;
                    Assert.False(otherFaction && location.Category != LocationCategory.Mugging, name + ": " + location.Name);
                    if (Generator.IsExcluded(location, options))
                    {
                        Assert.NotEqual(Tier.Progression, item.Tier);
                    }
                    if (location.Category == LocationCategory.Trader)
                    {
                        Assert.True(item.Sellable, name + ": " + location.Name + " holds " + item.Name);
                    }
                }

                List<(LocationDef, ItemDef)> placed = [.. run.Placement.Select(p => (Location(p.Key), Item(p.Value)))];
                HashSet<string> have = Generator.Sweep([], placed, rules);
                Assert.All(Generator.ProgressionItems(options), item => Assert.Contains(item.Id, have));
                Assert.All(enabled, location => Assert.True(Generator.CanReach(location, have, rules), location.Name));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void MaxOptionsKeepProgressionWithinMilestoneBounds(int faction)
        {
            RandoOptions options = new();
            foreach (OptionDef def in RandoOptions.Defs)
            {
                options[def.Key] = def.Max;
            }
            options[RandoOptions.Faction] = faction;
            Assert.Contains(Generator.EnabledLocations(options), location => Generator.IsExcluded(location, options)
                && location.Category is LocationCategory.Kills or LocationCategory.Attributes);

            for (ulong seed = 1; seed <= 5; seed++)
            {
                RandoSeed run = Generator.Generate(seed, options);
                foreach (KeyValuePair<string, string> placed in run.Placement.Where(p => Item(p.Value).Tier == Tier.Progression))
                {
                    Assert.False(Generator.IsExcluded(Location(placed.Key), options), Location(placed.Key).Name);
                }
            }
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(0, true)]
        [InlineData(1, true)]
        public void MuggingHeavyRunsGenerate(int faction, bool quests)
        {
            RandoOptions options = RandoOptions.FromPreset("minimal")!;
            options[RandoOptions.Chests] = 0;
            options[RandoOptions.Quests] = quests ? 1 : 0;
            options[RandoOptions.Muggings] = 1;
            options[RandoOptions.Faction] = faction;

            for (ulong seed = 1; seed <= 10; seed++)
            {
                RandoSeed run = Generator.Generate(seed, options);
                Assert.Equal(Generator.EnabledLocations(options).Count, run.Placement.Count);
                Assert.All(run.Placement.Where(p => Generator.IsExcluded(Location(p.Key), options)),
                    p => Assert.NotEqual(Tier.Progression, Item(p.Value).Tier));
            }
        }

        [Theory]
        [InlineData(ConsumableStackSize.Full)]
        [InlineData(ConsumableStackSize.Small)]
        [InlineData(ConsumableStackSize.Single)]
        public void SupplyFloorHoldsWhateverTheAmountRoll(ConsumableStackSize stackSize)
        {
            RandoOptions options = RandoOptions.FromPreset("completionist")!;
            options[RandoOptions.StackSize] = (int)stackSize;
            RandoSeed run = Generator.Generate(4242, options);

            foreach ((ItemDef item, int _) in Generator.SupplyCopies(options))
            {
                int units = item.SupplyGroup switch
                {
                    "permanent_herb" => options[RandoOptions.HerbSupply],
                    "mandragora" => options[RandoOptions.MandragoraSupply],
                    _ => options[RandoOptions.QuestItemSupply],
                };
                int copies = run.Placement.Values.Count(id => id == item.Id);
                Assert.True(copies * Generator.GrantRange(item.Amount, stackSize).Low >= units, item.Id + ": " + copies + " copies");
            }
        }

        [Fact]
        public void SameSeedSameRun()
        {
            RandoOptions options = RandoOptions.FromPreset("completionist")!;
            options[RandoOptions.Teleporters] = 1;
            options[RandoOptions.Runes] = 1;

            string first = Generator.Generate(12345, options).Spoiler();
            Assert.Equal(first, Generator.Generate(12345, RandoOptions.Parse(options.ToString())).Spoiler());
            Assert.NotEqual(first, Generator.Generate(12346, options).Spoiler());
        }

        [Fact]
        public void FeaturesDoNotDisturbEachOther()
        {
            string[] features = [RandoOptions.Teleporters, RandoOptions.Runes, RandoOptions.MandragoraPlacement,
                RandoOptions.MonsterShuffle, RandoOptions.TeacherShuffle, RandoOptions.RandomLearning];
            RandoOptions everything = new();
            foreach (string feature in features)
            {
                everything[feature] = 1;
            }
            RandoSeed all = Generator.Generate(777, everything);

            Assert.Equal(Generator.Generate(777, new RandoOptions()).Placement, all.Placement);
            foreach (string feature in features)
            {
                RandoOptions alone = new() { [feature] = 1 };
                RandoSeed run = Generator.Generate(777, alone);
                Assert.Equal(all.Placement, run.Placement);
                Assert.True(run.Teleporters.Count == 0 || run.Teleporters.SequenceEqual(all.Teleporters));
                Assert.True(run.Runes.Count == 0 || run.Runes.SequenceEqual(all.Runes) && run.FloorRunes.SequenceEqual(all.FloorRunes));
                Assert.True(run.Alraunes.Count == 0 || run.Alraunes.SequenceEqual(all.Alraunes));
                Assert.True(run.Monsters.Count == 0 || run.Monsters.SequenceEqual(all.Monsters));
                Assert.True(run.Teachers.Count == 0 || run.Teachers.SequenceEqual(all.Teachers));
            }
        }

        [Fact]
        public void ShufflesFollowTheirRules()
        {
            RandoOptions options = new()
            {
                [RandoOptions.Teleporters] = 1,
                [RandoOptions.Runes] = 1,
                [RandoOptions.MandragoraPlacement] = 1,
            };
            for (ulong seed = 1; seed <= 50; seed++)
            {
                RandoSeed run = Generator.Generate(seed, options);
                Assert.Equal(AlrauneSpots.Plants.OrderBy(x => x), run.Alraunes.Keys.OrderBy(x => x));
                Assert.Equal(AlrauneSpots.Plants.Length, run.Alraunes.Values.Distinct().Count());
                Assert.All(run.Alraunes.Values, spot => Assert.Contains(AlrauneSpots.Spots, s => s.X == spot.X && s.Y == spot.Y && s.Area != "Riverbed"));
                Assert.Equal(TeleporterTable.SafePairs.Select(p => p[1]).OrderBy(x => x), run.Teleporters.Values.OrderBy(x => x));
                Assert.Equal(RuneTable.Pool.OrderBy(x => x), run.Runes.Values.OrderBy(x => x));
                Assert.Equal(3, run.FloorRunes.Distinct().Count());
                Assert.NotEqual(new[] { "Fire", "Sound", "Summon" }, run.FloorRunes);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void MonsterShuffleKeepsEveryCreatureAndTier(int mode)
        {
            RandoOptions options = new() { [RandoOptions.MonsterShuffle] = mode };
            Dictionary<string, int> tierOf = MonsterTable.Spawners.GroupBy(s => s.Prefab).ToDictionary(g => g.Key, g => g.First().Tier);
            RandoSeed run = Generator.Generate(99, options);

            Assert.True(run.Monsters.Count > MonsterTable.Spawners.Length / 2);
            List<string> after = [.. MonsterTable.Spawners.Select(s => run.Monsters.GetValueOrDefault(s.Guid, s.Prefab))];
            Assert.Equal(MonsterTable.Spawners.Select(s => s.Prefab).OrderBy(p => p), after.OrderBy(p => p));
            if (mode == 1)
            {
                Assert.All(MonsterTable.Spawners, s => Assert.Equal(s.Tier, tierOf[run.Monsters.GetValueOrDefault(s.Guid, s.Prefab)]));
            }
        }

        [Fact]
        public void TalentsArePlacedShuffledOrGivenOnce()
        {
            RandoOptions options = new()
            {
                [RandoOptions.TalentItems] = 1,
                [RandoOptions.TeacherShuffle] = 1,
                [RandoOptions.StartingTalents] = 3,
            };
            for (ulong seed = 1; seed <= 20; seed++)
            {
                RandoSeed run = Generator.Generate(seed, options);
                List<string> start = [.. run.StartItems.Where(id => Item(id).Kind == GrantKind.Talent)];
                List<string> placed = [.. run.Placement.Values.Where(id => Item(id).Kind == GrantKind.Talent)];

                Assert.Equal(3, start.Distinct().Count());
                Assert.Equal(TalentTable.Teachable.OrderBy(t => t), start.Concat(placed).OrderBy(t => t));
                Assert.Equal(placed.OrderBy(t => t), run.TalentItems.OrderBy(t => t));
                Assert.Equal(TalentTable.Teachable.OrderBy(t => t), run.Teachers.Values.OrderBy(t => t));
            }
        }

        [Fact]
        public void KeysNeverLockThemselvesAway()
        {
            RandoOptions options = RandoOptions.FromPreset("completionist")!;
            List<LocationDef> enabled = Generator.EnabledLocations(options);
            Dictionary<string, string> rules = Generator.Rules(enabled);
            Assert.Contains("Wilds 18_29 - Chest 1", enabled.Where(l => rules.ContainsKey(l.Key)).Select(l => l.Name));
            Assert.True(rules.Count > 90);

            for (ulong seed = 1; seed <= 30; seed++)
            {
                RandoSeed run = Generator.Generate(seed, options);
                foreach (KeyValuePair<string, string> rule in rules)
                {
                    Assert.NotEqual(rule.Value, run.Placement[rule.Key]);
                }
            }
        }

        [Fact]
        public void OptionsRoundTripAndClamp()
        {
            RandoOptions options = new() { [RandoOptions.KillChecks] = 999 };
            Assert.Equal(50, options[RandoOptions.KillChecks]);
            Assert.Equal(options.ToString(), RandoOptions.Parse(options.ToString()).ToString());
            Assert.Equal(new RandoOptions().ToString(), RandoOptions.Parse("x.y").ToString());
            Assert.True(options.ToString().Length < 100);
        }

        [Fact]
        public void TooFewLocationsIsAnError()
        {
            RandoOptions options = new()
            {
                [RandoOptions.Chests] = 0,
                [RandoOptions.Containers] = 0,
                [RandoOptions.Quests] = 0,
            };
            Assert.Throws<InvalidOperationException>(() => Generator.Generate(1, options));
        }
    }
}
