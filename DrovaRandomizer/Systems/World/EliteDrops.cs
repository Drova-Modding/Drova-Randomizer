using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Items;
using Il2CppDrova;
using Il2CppDrova.Items;
using MelonLoader;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Random = UnityEngine.Random;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>The gold, items and set pieces a killed elite drops.</summary>
    internal static class EliteDrops
    {
        /// <summary>Chance by rarity that a killed elite drops a variant of its rarity.</summary>
        private static readonly float[] DropChance = [0f, 0.15f, 0.35f, 0.6f, 1f];

#if DEBUG
        /// <summary>The last elite drop as "gold=N tier=N items=id+id". For the test commands.</summary>
        internal static string LastDrop { get; set; } = "none";
#endif

        /// <summary>
        /// An elite the player fought died: it bursts if volatile and drops gold and, by chance, items of its rarity that
        /// suit its strength. Whoever dealt the last blow, so in co-op every player who fought it gets their own drop. A
        /// teammate's kill reaches this machine as a health write without a killer, which <c>OnPlayerKilledActor</c> misses.
        /// </summary>
        internal static void OnEntityDied(Entity entity)
        {
            try
            {
                Actor? victim = entity != null ? entity.TryCast<Actor>() : null;
                Actor? player = Core.Player;
                if (victim == null || player == null || !EliteLabels.WasFought(victim) || !Rarities.TryGetElite(victim, out EnemyAffix affix))
                {
                    return;
                }

                if (affix.Special == Affixes.Explosive)
                {
                    EliteCombat.Later(() =>
                    {
                        if (victim && player)
                        {
                            EliteCombat.RunProc(victim, player, Affixes.ExplosionSpell);
                        }
                    });
                }

                int tier = Affixes.HealthTier(victim.GetHealth().BaseMaxHealth);
                int gold = Affixes.EliteGold(affix.Rarity, tier);
                player.GetInventory().AddItem(new ItemStack(ItemUtil.GetMoneyItem(), gold), true);
                List<Item> drops = [];
                for (int i = Random.value < DropChance[affix.Rarity] ? Affixes.DropCount(affix.Rarity, Rarities.LootRandom) : 0; i > 0; i--)
                {
                    Item? drop = RarityItems.PickDrop(affix.Rarity, tier);
                    if (drop != null)
                    {
                        player.GetInventory().AddItem(new ItemStack(drop, 1), true);
                        drops.Add(drop);
                    }
                }

                if (affix.Rarity >= Affixes.SetPieceRarity && Affixes.NextSetPiece(OwnedIds(player), Rarities.LootRandom) is { } pieceId
                    && CustomItemSystem.Items.TryGetValue(pieceId, out Item? piece))
                {
                    player.GetInventory().AddItem(new ItemStack(piece, 1), true);
                    drops.Add(piece);
                }

                ToastAccess.Show(string.Join(", ", drops.Select(drop => drop.GetLocalizedItemName()).Append(gold + " gold")) + "  (elite drop)");
#if DEBUG
                LastDrop = "gold=" + gold + " tier=" + tier + " items=" + string.Join("+", drops.Select(drop => drop.ReadableId));
#endif
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] elite drop failed: " + e);
            }
        }

        /// <summary>The readable ids the player carries or wears.</summary>
        private static HashSet<string> OwnedIds(Actor player)
        {
            HashSet<string> owned = new(StringComparer.Ordinal);
            foreach (ItemStack stack in player.GetInventory().InventoryItems)
            {
                owned.Add(stack.Item.ReadableId);
            }
            Il2CppCollections.List<ActorEquipSlot> slots = player.GetEquipmentModule().AllItemSlotsRef;
            for (int i = 0; i < slots.Count; i++)
            {
                Item? item = slots[i].GetItemOnSlot();
                if (item != null)
                {
                    owned.Add(item.ReadableId);
                }
            }
            return owned;
        }
    }
}
