using System.Globalization;
using System.Text;
using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Xunit;

namespace DrovaRandomizer.Tests
{
    public class AffixTests
    {
        private static readonly string[] Specials =
            [Affixes.Vampiric, Affixes.Thorns, Affixes.Explosive, Affixes.Jailer, Affixes.Summoner, Affixes.Regenerating];

        [Fact]
        public void EnemyRollsAreStableAndBounded()
        {
            int elites = 0;
            int always = 0;
            int specials = 0;
            for (int i = 0; i < 5000; i++)
            {
                string guid = "guid" + i;
                EnemyAffix affix = Affixes.RollEnemy(4242, guid, Affixes.NormalFrequency);
                Assert.Equal(affix, Affixes.RollEnemy(4242, guid, Affixes.NormalFrequency));
                Assert.InRange(affix.Rarity, 0, Affixes.MaxRarity);
                Assert.InRange(affix.Health, 1f, 5f);
                Assert.InRange(affix.Damage, 1f, 2f);
                Assert.InRange(affix.Speed, 1f, 1.4f);
                Assert.InRange(affix.Armour, 0, 20);
                Assert.InRange(affix.MagicResistance, 0, 20);
                Assert.True(affix.ProcChance == Affixes.ProcChance || (affix.ProcChance == 1f && affix.Proc == Affixes.Poison));
                Assert.True(affix.Special == "" || Specials.Contains(affix.Special));
                always += affix.ProcChance == 1f ? 1 : 0;
                specials += affix.Special != "" ? 1 : 0;
                if (affix.Rarity == 0)
                {
                    Assert.Equal(new EnemyAffix(0, 1f, 1f, 0, 0, 1f, ""), affix);
                }
                else
                {
                    elites++;
                    Assert.Equal(affix.Rarity + (affix.Special != "" ? 1 : 0), Affixes.Traits(affix).Count);
                    Assert.True(affix.Rarity < Affixes.MaxRarity || affix.Special != "");
                }
            }
            Assert.InRange(elites, 1000, 2000);
            Assert.InRange(always, 10, 200);
            Assert.InRange(specials, 200, 800);
            Assert.Equal(Affixes.LegendaryName(4242, "guid1"), Affixes.LegendaryName(4242, "guid1"));
            Assert.NotEqual(Affixes.LegendaryName(4242, "guid1"), Affixes.LegendaryName(4243, "guid1"));
        }

        /// <summary>Specials, bonuses and the frequency option leave the rolls of 0.3.0 runs and items as they were.</summary>
        [Fact]
        public void NormalRollsMatchEarlierReleases()
        {
            StringBuilder enemies = new();
            StringBuilder items = new();
            for (int i = 0; i < 3000; i++)
            {
                EnemyAffix a = Affixes.RollEnemy(4242, "guid" + i, Affixes.NormalFrequency);
                enemies.Append(string.Create(CultureInfo.InvariantCulture,
                    $"{a.Rarity}|{a.Health}|{a.Damage}|{a.Armour}|{a.MagicResistance}|{a.Speed}|{a.Proc}|{a.ProcChance};"));
                ItemAffix b = Affixes.RollItem(Affixes.VariantId("item" + i, 1 + i % 4, i % 2), 1 + i % 4, i % 3 == 0);
                items.Append(string.Create(CultureInfo.InvariantCulture, $"{b.Rarity}|{b.Power}|{b.Proc};"));
            }
            Assert.Equal(10555835744403441991UL, Generator.Fnv(enemies.ToString()));
            Assert.Equal(8494874282084282418UL, Generator.Fnv(items.ToString()));
        }

        [Fact]
        public void FrequencyRaisesTheShareOfElites()
        {
            int[] elites = new int[4];
            int[] legendaries = new int[4];
            for (int frequency = 0; frequency < 4; frequency++)
            {
                for (int i = 0; i < 4000; i++)
                {
                    int rarity = Affixes.RollEnemy(7, "guid" + i, frequency).Rarity;
                    elites[frequency] += rarity > 0 ? 1 : 0;
                    legendaries[frequency] += rarity == Affixes.MaxRarity ? 1 : 0;
                }
            }
            for (int frequency = 1; frequency < 4; frequency++)
            {
                Assert.True(elites[frequency] > elites[frequency - 1]);
                Assert.True(legendaries[frequency] > legendaries[frequency - 1]);
            }
            Assert.InRange(elites[3], 3000, 3400);
        }

        [Fact]
        public void ItemRollsGrowWithRarity()
        {
            int bonuses = 0;
            for (int rarity = 1; rarity <= Affixes.MaxRarity; rarity++)
            {
                for (int variant = 0; variant < Affixes.Variants; variant++)
                {
                    foreach (string baseId in new[] { "weapon_sword_moonSword", "armor_chest_heavy_atlanter", "item_armorring_0" })
                    {
                        bool weapon = baseId.StartsWith("weapon", StringComparison.Ordinal);
                        string id = Affixes.VariantId(baseId, rarity, variant);
                        ItemAffix affix = Affixes.RollItem(id, rarity, weapon);
                        Assert.Equal(affix, Affixes.RollItem(id, rarity, weapon));
                        Assert.InRange(affix.Power, 1f + 0.06f * rarity - 0.001f, 1f + 0.12f * rarity + 0.001f);
                        Assert.True(rarity > 1 || affix.Proc == "");
                        Assert.True(rarity < Affixes.MaxRarity || affix.Proc != "");
                        Assert.True(rarity < 2 || affix.Bonus != "");
                        Assert.NotEqual(weapon ? Affixes.Thorns : Affixes.Leech, affix.Bonus);
                        Assert.Equal(affix.Bonus == "" ? 0 : Affixes.BonusAmount(affix.Bonus, rarity), affix.BonusAmount);
                        bonuses += affix.Bonus != "" ? 1 : 0;
                    }
                }
            }
            Assert.InRange(bonuses, 18, 24);
            Assert.Equal(8, Affixes.BonusAmount(Affixes.Strength, 4));
            Assert.Equal(16, Affixes.BonusAmount(Affixes.Leech, 4));
            Assert.Equal(32, Affixes.BonusAmount(Affixes.Thorns, 4));
            Assert.Equal(20, Affixes.BonusAmount(Affixes.Vitality, 4));
        }

        [Fact]
        public void NamesShowTheModifiers()
        {
            Assert.Null(Affixes.VariantName(new ItemAffix(1, 1.1f, ""), "Sword"));
            Assert.Equal("Venomous Sword", Affixes.VariantName(new ItemAffix(2, 1.1f, Affixes.Poison), "Sword"));
            Assert.Equal("Savage Sword of Leeching", Affixes.VariantName(new ItemAffix(3, 1.1f, Affixes.Bleed, Affixes.Leech, 12), "Sword"));
            Assert.Equal("Arcane Ring of the Owl", Affixes.VariantName(new ItemAffix(4, 1.1f, "flow_blast", Affixes.Mind, 8), "Ring"));
            Assert.Equal("Sword of Precision", Affixes.VariantName(new ItemAffix(2, 1.1f, "", Affixes.Crit, 4), "Sword"));
            Assert.Equal("Reflects 16% of the damage taken.", Affixes.BonusText(Affixes.Thorns, 16));
            Assert.Equal("+4% critical hit chance.", Affixes.BonusText(Affixes.Crit, 4));
        }

        [Fact]
        public void UniquesAreDistinctLegendaries()
        {
            Assert.Equal(Affixes.Uniques.Length, Affixes.Uniques.Select(unique => unique.Base).Distinct().Count());
            Assert.Equal(Affixes.Uniques.Length, Affixes.Uniques.Select(unique => unique.Name).Distinct().Count());
            foreach (UniqueItem unique in Affixes.Uniques)
            {
                ItemAffix affix = Affixes.UniqueAffix(unique);
                Assert.Equal(Affixes.MaxRarity, affix.Rarity);
                Assert.NotEqual("", affix.Proc);
                Assert.True(affix.BonusAmount > Affixes.BonusAmount(affix.Bonus, Affixes.MaxRarity));
                Assert.NotEqual(unique.Base.StartsWith("weapon", StringComparison.Ordinal) ? Affixes.Thorns : Affixes.Leech, unique.Bonus);
                Assert.Contains(ItemTable.All, item => item.Id == unique.Base);
            }
        }

        [Fact]
        public void DropsGrowWithRarityAndTier()
        {
            Assert.Equal(0, Affixes.HealthTier(5));
            Assert.Equal(0, Affixes.HealthTier(100));
            Assert.Equal(1, Affixes.HealthTier(101));
            Assert.Equal(4, Affixes.HealthTier(2500));
            Assert.Equal(10, Affixes.EliteGold(1, 0));
            Assert.Equal(750, Affixes.EliteGold(4, 4));
            SeededRandom random = new(1);
            for (int i = 0; i < 200; i++)
            {
                Assert.Equal(1, Affixes.DropCount(2, random));
                Assert.InRange(Affixes.DropCount(3, random), 1, 2);
                Assert.InRange(Affixes.DropCount(4, random), 2, 3);
            }
            int uniques = Enumerable.Range(0, 1000).Count(_ => Affixes.RollUnique(random));
            Assert.InRange(uniques, 200, 300);
        }

        [Fact]
        public void SetsAreDistinctAndCompleteOnePieceAtATime()
        {
            List<string> bases = [.. Affixes.Sets.SelectMany(set => set.Pieces.Select(piece => piece.Base))];
            Assert.Equal(bases.Count, bases.Distinct().Count());
            Assert.DoesNotContain(bases, piece => Affixes.Uniques.Any(unique => unique.Base == piece));
            Assert.All(bases, piece => Assert.Contains(ItemTable.All, item => item.Id == piece));

            SeededRandom random = new(3);
            HashSet<string> owned = [];
            List<string> drops = [];
            for (string? next = Affixes.NextSetPiece(owned, random); next != null; next = Affixes.NextSetPiece(owned, random))
            {
                Assert.True(owned.Add(next));
                drops.Add(next);
            }
            Assert.Equal(bases.Count, drops.Count);
            // The set begun first is finished before the next one starts.
            Assert.Single(drops.Take(3).Select(id => Affixes.Sets.First(set => set.Pieces.Any(piece => Affixes.SetPieceId(piece.Base) == id))).Distinct());

            ItemSet wrath = Affixes.Sets[0];
            Assert.Empty(Affixes.SetBonuses(wrath, 1));
            Assert.Equal([(Affixes.Strength, 6)], Affixes.SetBonuses(wrath, 2));
            Assert.Equal(2, Affixes.SetBonuses(wrath, 3).Count());
            Assert.Equal("Vorgath's Wrath (SETCOUNT0/3 worn): Vorgath's Cleaver, Vorgath's Hide, Vorgath's Knuckle. "
                + "2 pieces: +6 Strength. 3 pieces: Heals 15% of the damage dealt.", Affixes.SetText(0));
        }

        /// <summary>Every set piece can be had: even at the lowest frequency the roaming monsters alone hold enough epic or better elites.</summary>
        [Fact]
        public void EverySeedHasEnoughEliteKillsForAllSetPieces()
        {
            int pieces = Affixes.Sets.Sum(set => set.Pieces.Length);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                for (int frequency = 0; frequency < 4; frequency++)
                {
                    int strong = MonsterTable.Spawners.Count(spawner => Affixes.RollEnemy(seed, spawner.Guid, frequency).Rarity >= Affixes.SetPieceRarity);
                    Assert.True(strong >= pieces, "seed " + seed + " frequency " + frequency + " has " + strong);
                }
            }
        }

        [Fact]
        public void CommonsNextToAStrongEliteAreItsMinions()
        {
            int minions = 0;
            int commons = 0;
            foreach ((string guid, string[] neighbours) in PackTable.Neighbours)
            {
                EnemyAffix own = Affixes.RollEnemy(4242, guid, Affixes.NormalFrequency);
                EnemyAffix packed = Affixes.RollWithPack(4242, guid, Affixes.NormalFrequency, neighbours);
                if (own.Rarity > 0)
                {
                    Assert.Equal(own, packed);
                    continue;
                }

                commons++;
                string? leader = neighbours.FirstOrDefault(n => Affixes.RollEnemy(4242, n, Affixes.NormalFrequency).Rarity >= 3);
                Assert.Equal(leader ?? "", packed.Leader);
                if (leader == null)
                {
                    Assert.Equal(own, packed);
                    continue;
                }

                minions++;
                EnemyAffix boss = Affixes.RollEnemy(4242, leader, Affixes.NormalFrequency);
                Assert.Equal(1, packed.Rarity);
                Assert.Equal(boss.Proc, packed.Proc);
                Assert.Equal("", packed.Special);
                Assert.InRange(packed.Health, 1f, boss.Health);
                Assert.Equal("Minion", Affixes.Traits(packed)[0]);
            }
            Assert.InRange(minions, commons / 25, commons / 6);
            Assert.Equal(Affixes.RollEnemy(4242, "guid1", 1), Affixes.RollWithPack(4242, "guid1", 1, null));
        }

        [Fact]
        public void SummonersKeepTheirSpell()
        {
            Assert.Equal(Affixes.SummonSpell("guid1"), Affixes.SummonSpell("guid1"));
            Assert.StartsWith("flow_conjure_", Affixes.SummonSpell("guid2"));
            Assert.Equal([1, 1, 2, 2], Enumerable.Range(1, 4).Select(Affixes.SummonCasts));
        }
    }
}
