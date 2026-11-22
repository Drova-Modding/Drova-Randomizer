using DrovaRandomizer.Systems.Generation;
using Xunit;

namespace DrovaRandomizer.Tests
{
    public class AffixTests
    {
        [Fact]
        public void EnemyRollsAreStableAndBounded()
        {
            int elites = 0;
            int always = 0;
            for (int i = 0; i < 5000; i++)
            {
                string guid = "guid" + i;
                EnemyAffix affix = Affixes.RollEnemy(4242, guid);
                Assert.Equal(affix, Affixes.RollEnemy(4242, guid));
                Assert.InRange(affix.Rarity, 0, Affixes.MaxRarity);
                Assert.InRange(affix.Health, 1f, 5f);
                Assert.InRange(affix.Damage, 1f, 2f);
                Assert.InRange(affix.Speed, 1f, 1.4f);
                Assert.InRange(affix.Armour, 0, 20);
                Assert.InRange(affix.MagicResistance, 0, 20);
                Assert.True(affix.ProcChance == Affixes.ProcChance || (affix.ProcChance == 1f && affix.Proc != ""));
                always += affix.ProcChance == 1f ? 1 : 0;
                if (affix.Rarity == 0)
                {
                    Assert.Equal(new EnemyAffix(0, 1f, 1f, 0, 0, 1f, ""), affix);
                }
                else
                {
                    elites++;
                }
            }
            Assert.InRange(elites, 1000, 2000);
            Assert.InRange(always, 10, 200);
        }

        [Fact]
        public void ItemRollsGrowWithRarity()
        {
            for (int rarity = 1; rarity <= Affixes.MaxRarity; rarity++)
            {
                for (int variant = 0; variant < Affixes.Variants; variant++)
                {
                    string id = Affixes.VariantId("weapon_sword_moonSword", rarity, variant);
                    ItemAffix affix = Affixes.RollItem(id, rarity);
                    Assert.Equal(affix, Affixes.RollItem(id, rarity));
                    Assert.InRange(affix.Power, 1f + 0.06f * rarity - 0.001f, 1f + 0.12f * rarity + 0.001f);
                    Assert.True(rarity > 1 || affix.Proc == "");
                    Assert.True(rarity < Affixes.MaxRarity || affix.Proc != "");
                }
            }
        }
    }
}
