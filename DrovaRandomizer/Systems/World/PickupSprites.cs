using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using DrovaRandomizer.Systems.World.Patches;
using Drova_Modding_API.Access;
using Il2CppDrova;
using Il2CppDrova.Items;
using Il2CppDrova.Saveables;
using MelonLoader;
using UnityEngine;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Shows the placed item on a loose pickup (<see cref="Saveable_PickUp_Once"/>): its inventory icon on the
    /// pickup's Visual renderer, set once when the pickup starts. The Shadow child is the vanilla item's
    /// silhouette, so it hides while the icon shows. Rewards that are no item and animated pickups (rune
    /// stones, whose Animator keys the sprite) keep their vanilla art.
    /// </summary>
    public sealed class PickupSprites : IRandoSystem
    {
        internal sealed record Pickup(SpriteRenderer Visual, Sprite Vanilla, Renderer? Shadow);

        /// <summary>Pickup guid -> its renderers, for the pickups started so far.</summary>
        internal static readonly Dictionary<string, Pickup> Pickups = new(StringComparer.Ordinal);

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            PickupSpritePatches.Apply(harmony);
        }

        void IRandoSystem.Configure(RandoSeed? seed)
        {
            foreach (string guid in Pickups.Keys.ToList())
            {
                Apply(guid);
            }
        }

        internal static void OnStart(Saveable_PickUp_Once pickup)
        {
            try
            {
                GameObject root = pickup._pickUpRoot;
                if (!root || root.GetComponent<Animator>())
                {
                    return;
                }
                GuidComponent owner = root.GetComponent<GuidComponent>();
                Transform visual = root.transform.Find("Visual");
                SpriteRenderer? renderer = visual ? visual.GetComponent<SpriteRenderer>() : null;
                if (!owner || renderer == null || !renderer)
                {
                    return;
                }
                Transform shadow = root.transform.Find("Shadow");
                Pickups[owner._guidString] = new Pickup(renderer, renderer.sprite, shadow ? shadow.GetComponent<Renderer>() : null);
                Apply(owner._guidString);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando pickup] Start postfix failed: " + e);
            }
        }

        private static void Apply(string guid)
        {
            Pickup pickup = Pickups[guid];
            if (!pickup.Visual)
            {
                Pickups.Remove(guid);
                return;
            }
            Sprite? icon = PlacedIcon(guid);
            pickup.Visual.sprite = icon ?? pickup.Vanilla;
            if (pickup.Shadow)
            {
                pickup.Shadow!.enabled = icon == null;
            }
        }

        /// <summary>The icon of the item placed at this pickup, or null for vanilla art.</summary>
        private static Sprite? PlacedIcon(string guid)
        {
            LocationDef? location = LocationTable.Get(guid);
            if (!Randomizer.IsLocation(location) || Randomizer.IsChecked(location))
            {
                return null;
            }
            ItemDef? placed = ItemTable.Get(Randomizer.Seed.Placement[location.Key]);
            if (placed == null || placed.Kind != GrantKind.Item)
            {
                return null;
            }
            Item? item = ProviderAccess.ItemDatabase?.GetItemByReadableId(placed.Id);
            return item ? item!.Icon : null;
        }
    }
}
