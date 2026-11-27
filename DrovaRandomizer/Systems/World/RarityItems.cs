using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Items;
using Il2Cpp;
using Il2CppCustomFramework.Localization;
using Il2CppDrova;
using Il2CppDrova.Cognition;
using Il2CppDrova.Items;
using Il2CppDrova.Items.Stats;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using static Il2CppCustomFramework.Localization.LocalizationDB;
using Il2CppCollections = Il2CppSystem.Collections.Generic;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// The rarity variants of weapons and armour, the uniques and the set pieces, custom items the Modding API registers
    /// from a file this writes. An armour variant carries its power in its own stat modifiers, a weapon variant in the damage funnel.
    /// </summary>
    internal static class RarityItems
    {
        /// <summary>The loca path of the variants' texts and the options' labels.</summary>
        internal const string LocaPath = "DrovaRandomizerRarities";
        private const string FileName = "DrovaRandomizer_rarities.json";

        /// <summary>The name of the behaviour a variant's attribute, crit or health bonus lives in.</summary>
        private const string BonusBehaviourName = "DrovaRandomizerBonus";

        private static readonly Dictionary<string, ItemAffix> AffixById = new(StringComparer.Ordinal);

        /// <summary>Each variant's value tier, 0 to 4 like <see cref="Affixes.HealthTier"/>, from its base item's price.</summary>
        private static readonly Dictionary<string, int> TierById = new(StringComparer.Ordinal);

        private static readonly HashSet<string> UniqueIds = new(StringComparer.Ordinal);

        /// <summary>Each set piece id's index in <see cref="Affixes.Sets"/>.</summary>
        private static readonly Dictionary<string, int> SetOfPiece = new(StringComparer.Ordinal);

        /// <summary>Per set, the number its pieces' tooltips show: how many of them the player wears.</summary>
        private static readonly FloatValueContainer?[] SetCounters = new FloatValueContainer?[Affixes.Sets.Length];

        /// <summary>
        /// Variants at or below their base item's rarity look exactly like it. They stay registered for saves that
        /// hold one, but are never handed out.
        /// </summary>
        private static readonly HashSet<string> SaveOnlyIds = new(StringComparer.Ordinal);

        /// <summary>What each body's gear does in a fight, by instance id. An equip change drops the entry.</summary>
        private static readonly Dictionary<int, GearEffects> GearByBody = [];

        /// <summary>Bodies that carry a set bonus. The others skip the stat work on every equip, which NPCs do when they spawn.</summary>
        private static readonly HashSet<int> SetBonusBodies = [];

        private static bool _locaWritten;

        /// <summary>How many item variants are registered.</summary>
        internal static int VariantCount => AffixById.Count;

#if DEBUG
        /// <summary>The modifiers of a variant or unique id, or null for any other id. For the test commands.</summary>
        internal static ItemAffix? AffixOf(string id)
        {
            return AffixById.TryGetValue(id, out ItemAffix affix) ? affix : null;
        }

        /// <summary>The set pieces the player wears and the counts the tooltips show, as "worn=a,b,c shown=a,b,c". For the test commands.</summary>
        internal static string SetState()
        {
            Actor? player = Core.Player;
            return "worn=" + (player != null ? string.Join(",", WornSetPieces(player)) : "-")
                + " shown=" + string.Join(",", SetCounters.Select(counter => counter != null ? counter._value.ToString(CultureInfo.InvariantCulture) : "-"));
        }

        /// <summary>The value tier of a variant or unique id, or -1. For the test commands.</summary>
        internal static int TierOf(string id)
        {
            return TierById.TryGetValue(id, out int tier) ? tier : -1;
        }
#endif

        internal static bool TryGetVariant(string id, [NotNullWhen(true)] out Item? variant)
        {
            variant = null;
            return !SaveOnlyIds.Contains(id) && CustomItemSystem.Items.TryGetValue(id, out variant);
        }

        internal static bool TryGetAffix(string id, out ItemAffix affix)
        {
            return AffixById.TryGetValue(id, out affix);
        }

        /// <summary>
        /// After a slot's item effects come or go: the actor's set attribute and crit bonuses follow the pieces it wears,
        /// and for the player the set tooltips' count does too.
        /// </summary>
        internal static void OnEquipEffectsChanged(Actor actor)
        {
            try
            {
                if (actor == null)
                {
                    return;
                }

                int body = actor.GetInstanceID();
                GearByBody.Remove(body);
                if (SetOfPiece.Count == 0 || actor.GetStats() == null)
                {
                    return;
                }

                int[] worn = WornSetPieces(actor);
                bool player = actor.IsPlayer && !CoopAccess.IsRemotePlayer(actor);
                bool wearsSet = worn.Any(count => count > 0);
                if (!player && !wearsSet && !SetBonusBodies.Contains(body))
                {
                    return;
                }

                if (wearsSet)
                {
                    SetBonusBodies.Add(body);
                }
                else
                {
                    SetBonusBodies.Remove(body);
                }
                StatContainer stats = GlobalStatsSettings.GetStatContainer();
                for (int set = 0; set < Affixes.Sets.Length; set++)
                {
                    FloatValueContainer? counter = SetCounters[set];
                    if (counter == null)
                    {
                        continue;
                    }

                    // The counter doubles as the source the bonus is filed under, so it comes off as a whole.
                    actor.GetStats().RemoveDelegated(counter);
                    GenericStatModificationSet bonuses = new();
                    foreach ((string bonus, int amount) in Affixes.SetBonuses(Affixes.Sets[set], worn[set]))
                    {
                        GenericStatModification? modification = StatModification(stats, bonus, amount);
                        if (modification != null)
                        {
                            bonuses._modifications.Add(modification);
                        }
                    }
                    if (bonuses._modifications.Count > 0)
                    {
                        actor.GetStats().ModifyDelegated(counter, bonuses);
                    }
                    if (player)
                    {
                        counter._value = worn[set];
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[Rando rarities] set bonus failed: " + e);
            }
        }

        /// <summary>Per set, how many of its different pieces the actor wears with their effects on.</summary>
        private static int[] WornSetPieces(Actor actor)
        {
            int[] worn = new int[Affixes.Sets.Length];
            HashSet<string> counted = new(StringComparer.Ordinal);
            Actor.EquipmentModule? equipment = actor.GetEquipmentModule();
            Il2CppCollections.List<ActorEquipSlot>? slots = equipment != null ? equipment.AllItemSlotsRef : null;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                Item? item = slots[i]._hasActiveEffectsApplied ? slots[i].GetItemOnSlot() : null;
                if (item != null && SetOfPiece.TryGetValue(item.ReadableId, out int set) && counted.Add(item.ReadableId))
                {
                    worn[set]++;
                }
            }
            return worn;
        }

        /// <summary>What the actor's gear does when it hits or is hit, read once per equip change.</summary>
        internal static GearEffects GearOf(Actor actor)
        {
            int body = actor.GetInstanceID();
            if (GearByBody.TryGetValue(body, out GearEffects gear))
            {
                return gear;
            }

            List<ItemAffix> armour = [];
            Actor.EquipmentModule? equipment = actor.GetEquipmentModule();
            Il2CppCollections.List<ActorEquipSlot>? slots = equipment != null ? equipment.AllItemSlotsRef : null;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                Item? worn = slots[i].IsInActiveSet() ? slots[i].GetItemOnSlot() : null;
                if (worn != null && worn.Category.MainCategory == ItemMainCategory.Armor
                    && AffixById.TryGetValue(worn.ReadableId, out ItemAffix affix) && (affix.Proc != "" || affix.Bonus == Affixes.Thorns))
                {
                    armour.Add(affix);
                }
            }

            int[] pieces = SetOfPiece.Count > 0 ? WornSetPieces(actor) : new int[Affixes.Sets.Length];
            gear = new GearEffects(SetBonus(pieces, Affixes.Leech), SetBonus(pieces, Affixes.Thorns), [.. armour]);
            GearByBody[body] = gear;
            return gear;
        }

        /// <summary>The set bonus of one kind for these worn piece counts, summed over the sets.</summary>
        private static int SetBonus(int[] worn, string bonus)
        {
            return Enumerable.Range(0, Affixes.Sets.Length)
                .Sum(set => Affixes.SetBonuses(Affixes.Sets[set], worn[set]).Where(active => active.Bonus == bonus).Sum(active => active.Amount));
        }

        /// <summary>A stat modification for an attribute or crit bonus, or null for any other bonus.</summary>
        private static GenericStatModification? StatModification(StatContainer stats, string bonus, int amount)
        {
            GenericStatDesc? stat = bonus switch
            {
                Affixes.Strength => stats.StrengthStat,
                Affixes.Dexterity => stats.DexStat,
                Affixes.Mind => stats.MindStat,
                Affixes.Crit => stats.CritChanceStat,
                _ => null,
            };
            if (stat == null)
            {
                return null;
            }

            GenericStatModification modification = new() { StatDesc = stat, Mode = ModifiableFloat.Mode.Add };
            // Crit chance is a fraction, the bonus amount percent points.
            modification._valueProp = bonus == Affixes.Crit ? amount / 100f : amount;
            return modification;
        }

        /// <summary>The tooltip's and the weapon-scaled spells' base damage of a weapon variant.</summary>
        internal static void OnBaseDamage(ActionStats stats, ref float result)
        {
            Item? item = stats._item;
            if (item != null && item.Category.MainCategory == ItemMainCategory.Weapon
                && AffixById.TryGetValue(item.ReadableId, out ItemAffix affix))
            {
                result *= affix.Power;
            }
        }

        /// <summary>
        /// Sets a variant's own stat modifiers and health bonus from its base item's, so reapplying never compounds.
        /// </summary>
        private static void ScaleArmourStats(Item item, Item baseItem, float power)
        {
            for (int i = 0; i < item._itemBehaviours.Count && i < baseItem._itemBehaviours.Count; i++)
            {
                ItemBhvr_EquipEffect_StatModification? stats = item._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_StatModification>();
                ItemBhvr_EquipEffect_StatModification? baseStats = baseItem._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_StatModification>();
                if (stats != null && baseStats != null)
                {
                    ScaleModifications(stats._equipModifications, baseStats._equipModifications, power);
                    ScaleModifications(stats._equipNPCModifications, baseStats._equipNPCModifications, power);
                }

                ItemBhvr_EquipEffect_MaxHealth? health = item._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_MaxHealth>();
                ItemBhvr_EquipEffect_MaxHealth? baseHealth = baseItem._itemBehaviours[i].TryCast<ItemBhvr_EquipEffect_MaxHealth>();
                if (health != null && baseHealth != null)
                {
                    health._maxHealth = (int)MathF.Ceiling(baseHealth._maxHealth * power);
                }
            }
        }

        private static void ScaleModifications(GenericStatModificationSet? set, GenericStatModificationSet? baseSet, float power)
        {
            Il2CppCollections.List<GenericStatModification>? values = set != null ? set._modifications : null;
            Il2CppCollections.List<GenericStatModification>? baseValues = baseSet != null ? baseSet._modifications : null;
            for (int i = 0; values != null && baseValues != null && i < values.Count && i < baseValues.Count; i++)
            {
                float value = baseValues[i].Value;
                if (baseValues[i].Mode != ModifiableFloat.Mode.Add || value <= 0f)
                {
                    continue;
                }

                // Whole points round up, so every rarity shows. Fractions are chances and stay fractions.
                values[i]._useContainer = false;
                values[i]._valueProp = value == MathF.Round(value) ? MathF.Ceiling(value * power) : value * power;
            }
        }

        private static string SpellName(string spellId)
        {
            SubDatabase_Item? database = ProviderAccess.ItemDatabase;
            Item? spell = database != null ? database.GetItemByReadableId(spellId) : null;
            return spell != null ? spell.GetLocalizedItemName() : spellId;
        }

        /// <summary>
        /// A unique for some legendary drops, else a variant of the rarity. Either has a value tier within one of the elite's
        /// when there is one, so weak legendaries never drop the uniques of strong items.
        /// </summary>
        internal static Item? PickDrop(int rarity, int tier)
        {
            List<string> pool = rarity == Affixes.MaxRarity && Affixes.RollUnique(Rarities.LootRandom)
                ? UniqueIds.Where(id => Math.Abs(TierById[id] - tier) <= 1).ToList()
                : [];
            if (pool.Count == 0)
            {
                List<string> ofRarity = AffixById.Where(variant => variant.Value.Rarity == rarity && !SaveOnlyIds.Contains(variant.Key)
                    && !UniqueIds.Contains(variant.Key) && !SetOfPiece.ContainsKey(variant.Key)).Select(variant => variant.Key).ToList();
                pool = ofRarity.Where(id => Math.Abs(TierById[id] - tier) <= 1).ToList();
                pool = pool.Count > 0 ? pool : ofRarity;
            }
            return pool.Count > 0 && CustomItemSystem.Items.TryGetValue(pool[Rarities.LootRandom.Next(pool.Count)], out Item? drop) ? drop : null;
        }

        /// <summary>
        /// Writes the variants as a custom item file. The API then registers them at every main menu before a
        /// save loads and updates them in place, which registering from code each time would not.
        /// </summary>
        internal static void RegisterVariants()
        {
            SubDatabase_Item? database = ProviderAccess.ItemDatabase;
            if (database == null)
            {
                return;
            }

            AffixById.Clear();
            GearByBody.Clear();
            SetBonusBodies.Clear();
            SaveOnlyIds.Clear();
            TierById.Clear();
            UniqueIds.Clear();
            List<CustomItemDefinition> definitions = [];
            List<LocalizationAccess.LocalizationEntry> loca = [];
            HashSet<string> named = new(StringComparer.Ordinal);
            HashSet<string> described = new(StringComparer.Ordinal);
            // The game ships a folder only for the languages it has.
            ELanguage[] languages = Enum.GetValues<ELanguage>()
                .Where(language => Directory.Exists(LocalizationDB.Instance.GetLanguageFolderPath(language))).ToArray();

            List<Item> bases = [];
            for (int i = 0; i < database._entries.Count; i++)
            {
                Item? item = database._entries[i].Item;
                if (item != null && item.ReadableId == database._entries[i].ReadableId && CanHaveRarity(item))
                {
                    bases.Add(item);
                }
            }
            // Fifths of the price range, so a drop can match the health tier of the elite that dropped it.
            List<Item> byValue = [.. bases.OrderBy(item => item.BuyValue)];
            Dictionary<string, int> baseTier = new(StringComparer.Ordinal);
            for (int i = 0; i < byValue.Count; i++)
            {
                baseTier[byValue[i].ReadableId] = i * (Affixes.MaxRarity + 1) / byValue.Count;
            }

            void Add(Item item, string id, ItemAffix affix, int priceFactor, string? name, string? description = null)
            {
                bool weapon = item.Category.MainCategory == ItemMainCategory.Weapon;
                AffixById[id] = affix;
                TierById[id] = baseTier[item.ReadableId];
                definitions.Add(new CustomItemDefinition
                {
                    ReadableId = id,
                    Base = item.ReadableId,
                    LocaPath = LocaPath,
                    BuyValue = item.BuyValue * priceFactor,
                    SellValue = item.SellValue * priceFactor,
                });
                description ??= affix.Proc != "" || affix.Bonus != "" ? Describe(affix, weapon) : null;
                if (name != null)
                {
                    named.Add(id);
                }
                if (description != null)
                {
                    described.Add(id);
                }
                foreach (ELanguage language in languages)
                {
                    if (name != null)
                    {
                        loca.Add(new LocalizationAccess.LocalizationEntry(id + "_name", name, language));
                    }
                    if (description != null)
                    {
                        loca.Add(new LocalizationAccess.LocalizationEntry(id + "_description", description, language));
                    }
                }
            }

            foreach (Item item in bases)
            {
                string? baseName = NameTag(item);
                for (int rarity = 1; rarity <= Affixes.MaxRarity; rarity++)
                {
                    for (int variant = 0; variant < Affixes.Variants; variant++)
                    {
                        string id = Affixes.VariantId(item.ReadableId, rarity, variant);
                        ItemAffix affix = Affixes.RollItem(id, rarity, item.Category.MainCategory == ItemMainCategory.Weapon);
                        if (rarity <= (int)item.Rarity)
                        {
                            SaveOnlyIds.Add(id);
                        }
                        Add(item, id, affix, 1 + rarity, baseName != null ? Affixes.VariantName(affix, baseName) : null);
                    }
                }
            }

            foreach (UniqueItem unique in Affixes.Uniques)
            {
                Item? item = bases.FirstOrDefault(candidate => candidate.ReadableId == unique.Base);
                if (item == null)
                {
                    MelonLogger.Warning("[Rando rarities] the base of the unique " + unique.Name + " cannot have a rarity: " + unique.Base);
                    continue;
                }
                string id = Affixes.UniqueId(unique.Base);
                UniqueIds.Add(id);
                Add(item, id, Affixes.UniqueAffix(unique), 2 + Affixes.MaxRarity, unique.Name);
            }

            SetOfPiece.Clear();
            for (int set = 0; set < Affixes.Sets.Length; set++)
            {
                if (SetCounters[set] == null)
                {
                    FloatValueContainer counter = ScriptableObject.CreateInstance(Il2CppType.Of<FloatValueContainer>()).Cast<FloatValueContainer>();
                    counter.name = "DrovaRandomizerSet" + set;
                    counter.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    SetCounters[set] = counter;
                }
                SetCounters[set]!._value = 0f;
                foreach ((string baseId, string name) in Affixes.Sets[set].Pieces)
                {
                    Item? item = bases.FirstOrDefault(candidate => candidate.ReadableId == baseId);
                    if (item == null)
                    {
                        MelonLogger.Warning("[Rando rarities] the base of the set piece " + name + " cannot have a rarity: " + baseId);
                        continue;
                    }
                    string id = Affixes.SetPieceId(baseId);
                    SetOfPiece[id] = set;
                    Add(item, id, Affixes.SetPieceAffix(), 2 + Affixes.SetPieceRarity, name, Affixes.SetText(set));
                }
            }

            string path = Path.Combine(CustomItemSystem.GetFolderPath(), FileName);
            string json = JsonSerializer.Serialize(new CustomItemConfigFile { Items = definitions });
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            {
                File.WriteAllText(path, json);
                CustomItemSystem.Reload();
            }

            foreach (KeyValuePair<string, ItemAffix> variant in AffixById)
            {
                if (CustomItemSystem.Items.TryGetValue(variant.Key, out Item? item))
                {
                    // A variant with a proc or bonus is named around the base item's name, the others keep it. The rarity
                    // colours it and the inventory border. The description is the modifiers', or the base item's without any.
                    item._copyName = !named.Contains(variant.Key);
                    item._copyDescription = !described.Contains(variant.Key);
                    if (described.Contains(variant.Key))
                    {
                        // Some base items carry a description of their own that would win over the modifiers'.
                        item._customLocaDesc = null;
                    }
                    item._rarity = (Item.ItemRarity)Math.Max((int)item._rarity, variant.Value.Rarity);
                    if (item._copyLocaItem != null && item.Category.MainCategory == ItemMainCategory.Armor)
                    {
                        ScaleArmourStats(item, item._copyLocaItem, variant.Value.Power);
                    }
                    ApplyBonus(item, variant.Value);
                    if (SetOfPiece.TryGetValue(variant.Key, out int pieceSet))
                    {
                        ShowSetCount(item, pieceSet);
                    }
                }
            }

            // The game caches an item's tooltip stats by guid.
            ItemStatsFactory.ClearCache();
            foreach (ELanguage language in languages)
            {
                loca.Add(new LocalizationAccess.LocalizationEntry("OptionsTitle", "Drova Randomizer", language));
                loca.Add(new LocalizationAccess.LocalizationEntry("OptionLabels", "Elite names", language));
                loca.Add(new LocalizationAccess.LocalizationEntry("OptionLabelsOn", "Shown", language));
                loca.Add(new LocalizationAccess.LocalizationEntry("OptionLabelsOff", "Hidden", language));
                loca.Add(new LocalizationAccess.LocalizationEntry("OptionLabelSize", "Elite name size", language));
            }
            if (!_locaWritten)
            {
                LocalizationAccess.CreateLocalizationEntries(loca, LocaPath);
                _locaWritten = true;
            }
            MelonLogger.Msg("[Rando rarities] " + AffixById.Count + " item variants.");
        }

        private static bool CanHaveRarity(Item item)
        {
            return item.Category.MainCategory is ItemMainCategory.Weapon or ItemMainCategory.Armor
                && item.Category.SubCategory is not (ItemSubCategory.Weapon_Bow_Ammo or ItemSubCategory.Weapon_Slingshot_Ammo)
                && item.BuyValue > 0 && item.SellValue > 0 && !item.IsInQuestCategory
                && !CustomItemSystem.Items.ContainsKey(item.ReadableId);
        }

        private static string Describe(ItemAffix affix, bool weapon)
        {
            string proc = affix.Proc == "" ? ""
                : MathF.Round(Affixes.ProcChance * 100f) + "% chance " + (weapon ? "on hit" : "when hit") + ": " + ProcEffect(affix.Proc) + ".";
            string bonus = affix.Bonus == "" ? "" : Affixes.BonusText(affix.Bonus, affix.BonusAmount);
            return (proc + " " + bonus).Trim();
        }

        /// <summary>
        /// The game's <c>$LOCA:path.key$</c> tag of an item's name, so a variant name reads it in every language.
        /// Null when the path or key would break the tag.
        /// </summary>
        private static string? NameTag(Item item)
        {
            Item source = item;
            while (source._copyLocaFromItem && source._copyName && source._copyLocaItem != null && source._copyLocaItem != source)
            {
                source = source._copyLocaItem;
            }
            string path = source.LocaPath + "." + source.GetNameLocaKey();
            return path.Count(c => c == '.') == 1 && path.IndexOfAny([':', '$']) < 0 ? "$LOCA:" + path + "$" : null;
        }

        /// <summary>
        /// Puts an attribute, crit or health bonus into a behaviour of the variant's own, which equipping applies like the
        /// item's other effects. The behaviour is found again on later main menus, so the bonus never stacks.
        /// </summary>
        private static void ApplyBonus(Item item, ItemAffix affix)
        {
            if (affix.Bonus == Affixes.Vitality)
            {
                OwnBonus<ItemBhvr_EquipEffect_MaxHealth>(item)._maxHealth = affix.BonusAmount;
                return;
            }

            GenericStatModification? modification = StatModification(GlobalStatsSettings.GetStatContainer(), affix.Bonus, affix.BonusAmount);
            if (modification == null)
            {
                return;
            }

            GenericStatModificationSet set = new();
            set._modifications.Add(modification);
            ItemBhvr_EquipEffect_StatModification behaviour = OwnBonus<ItemBhvr_EquipEffect_StatModification>(item);
            behaviour._equipModifications = set;
            behaviour._equipNPCModifications = new GenericStatModificationSet();
        }

        /// <summary>
        /// The game swaps a tooltip value's key in an item's description for its container's value on every tooltip, so
        /// the count of worn pieces stays current without touching the text.
        /// </summary>
        private static void ShowSetCount(Item item, int set)
        {
            string key = Affixes.SetCountKey(set);
            if (item._tooltipValues == null)
            {
                item._tooltipValues = new Il2CppCollections.List<Item.CustomValueTooltip>();
            }
            for (int i = 0; i < item._tooltipValues.Count; i++)
            {
                if (item._tooltipValues[i].Key == key)
                {
                    item._tooltipValues[i].Container = SetCounters[set];
                    return;
                }
            }
            item._tooltipValues.Add(new Item.CustomValueTooltip { Key = key, Container = SetCounters[set], IsPercent = false });
        }

        private static T OwnBonus<T>(Item item) where T : ItemBhvr_ABase
        {
            for (int i = 0; i < item._itemBehaviours.Count; i++)
            {
                T? own = item._itemBehaviours[i].name == BonusBehaviourName ? item._itemBehaviours[i].TryCast<T>() : null;
                if (own != null)
                {
                    return own;
                }
            }

            T behaviour = ScriptableObject.CreateInstance(Il2CppType.Of<T>()).Cast<T>();
            behaviour.name = BonusBehaviourName;
            behaviour.hideFlags = HideFlags.DontUnloadUnusedAsset;
            behaviour.AfterInstantiate(item);
            item._itemBehaviours.Add(behaviour);
            return behaviour;
        }

        private static string ProcEffect(string proc)
        {
            return proc switch
            {
                Affixes.Poison => "poisons",
                Affixes.Bleed => "causes bleeding",
                _ => "casts " + SpellName(proc),
            };
        }

        /// <summary>A body's set leech and thorns in percent, and the modifiers of its rarity armour with a hit effect or thorns.</summary>
        internal readonly record struct GearEffects(int SetLeech, int SetThorns, ItemAffix[] Armour);
    }
}
