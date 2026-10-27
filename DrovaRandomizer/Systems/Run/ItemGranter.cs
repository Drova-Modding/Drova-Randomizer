using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Talents;
using Il2CppDrova;
using Il2CppDrova.InventorySystem;
using Il2CppDrova.Items;
using Il2CppDrova.Items.Stats;
using Il2CppDrova.Talent;
using MelonLoader;

namespace DrovaRandomizer.Systems.Run
{
    /// <summary>Hands one reward to the player. Safe to call every frame.</summary>
    public class ItemGranter
    {
        private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

        private readonly Random _amountRoll = new();

        /// <summary>Set from the run's options. Full matches the generated table amounts.</summary>
        public ConsumableStackSize StackSize { get; set; } = ConsumableStackSize.Full;

        private int RollAmount(int tableAmount)
        {
            (int low, int high) = Generator.GrantRange(tableAmount, StackSize);
            return _amountRoll.Next(low, high + 1);
        }

        /// <summary>
        /// Applies one reward by id. Main thread only. False when the game is not ready yet, so the
        /// caller keeps it and retries. An unknown id is logged once and consumed.
        /// </summary>
        public bool TryGrant(string id)
        {
            Actor? player = Core.Player;
            if (!Core.InGameplayScene || player == null || !player)
            {
                return false;
            }

            ItemDef? grant = ItemTable.Get(id);
            if (grant == null)
            {
                if (_warned.Add(id))
                {
                    MelonLogger.Warning("No reward named '" + id + "'; skipping it.");
                }
                return true;
            }

            switch (grant.Kind)
            {
                case GrantKind.Item:
                    return GrantItem(player, grant);
                case GrantKind.Xp:
                    return WithStats(stats => stats.AddExperiencePoints(grant.Amount), grant);
                case GrantKind.LearningPoint:
                    return WithStats(stats => stats.GiveLearningPoint(grant.Amount), grant);
                case GrantKind.Attribute:
                    return GrantAttribute(grant);
                case GrantKind.MaxHealth:
                    return GrantMaxHealth(player, grant);
                case GrantKind.Talent:
                    return GrantTalent(player, grant);
                default:
                    MelonLogger.Error("Unhandled grant kind " + grant.Kind + " for '" + id + "'.");
                    return true;
            }
        }

        private bool GrantItem(Actor player, ItemDef grant)
        {
            try
            {
                SubDatabase_Item? database = ProviderAccess.ItemDatabase;
                if (database == null)
                {
                    return false;
                }

                Item item = database.GetItemByReadableId(grant.Id);
                if (!item)
                {
                    MelonLogger.Error("Reward '" + grant.Name + "' maps to unknown readable id '" + grant.Id + "'.");
                    return true;
                }

                Inventory inventory = player.GetInventory();
                if (!inventory)
                {
                    return false;
                }

                int amount = RollAmount(grant.Amount);
                inventory.AddItem(new ItemStack(item, amount), true);
                MelonLogger.Msg("Granted " + amount + "x " + grant.Id + ".");
                return true;
            }
            catch (Exception e)
            {
                if (_warned.Add("grantfail:" + grant.Id))
                {
                    MelonLogger.Error("Granting '" + grant.Id + "' failed: " + e);
                }
                return false;
            }
        }

        private static bool WithStats(Action<PlayerAttributeStats> apply, ItemDef grant)
        {
            PlayerAttributeStats? stats;
            try
            {
                stats = PlayerAccess.GetPlayerAttributeStats();
            }
            catch
            {
                return false;
            }
            if (!stats)
            {
                return false;
            }

            apply(stats);
            MelonLogger.Msg("Granted " + grant.Name + ".");
            return true;
        }

        private static bool GrantAttribute(ItemDef grant)
        {
            GenericStatDesc desc;
            try
            {
                StatContainer? container = GlobalStatsSettings.GetStatContainer();
                if (container == null)
                {
                    return false;
                }
                desc = grant.Id switch
                {
                    "strength" => container.StrengthStat,
                    "dexterity" => container.DexStat,
                    _ => container.MindStat,
                };
            }
            catch
            {
                return false;
            }

            return desc != null && WithStats(stats => stats.ImproveAttribute(desc, grant.Amount), grant);
        }

        private static bool GrantTalent(Actor player, ItemDef grant)
        {
            if (!TalentContainerDatabase.TryGetTalent(grant.Id, out TalentContainer? talent))
            {
                MelonLogger.Warning("Unknown talent '" + grant.Id + "'; skipping it.");
                return true;
            }
            if (player.TalentActorModule.HasTalentLearned(talent.GUID))
            {
                int points = Math.Max(1, talent.TalentCostLearnPoints);
                return WithStats(stats => stats.GiveLearningPoint(points), grant with { Name = grant.Name + " (known, " + points + " LP)" });
            }

            player.TalentActorModule.ForceLearnTalent(talent);
            MelonLogger.Msg("Granted " + grant.Name + ".");
            return true;
        }

        private static bool GrantMaxHealth(Actor player, ItemDef grant)
        {
            Health? health = player.GetHealth();
            if (health == null)
            {
                return false;
            }

            health.ChangeMaxHealth(grant.Amount, true);
            MelonLogger.Msg("Granted " + grant.Name + ".");
            return true;
        }
    }
}
