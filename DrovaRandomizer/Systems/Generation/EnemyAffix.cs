namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// The rolled modifiers of one enemy. Health, damage and speed are factors, armour and magic resistance
    /// are points. <c>Proc</c> is "", a constant of <see cref="Affixes"/> or a spell item id. <c>ProcChance</c> is the chance per hit, 1 for an elite whose proc
    /// always triggers. <c>Special</c> is "" or one of the special constants of <see cref="Affixes"/>. <c>Leader</c> is the
    /// spawner guid of the elite a minion belongs to, or "". Rarity 0 changes nothing.
    /// </summary>
    internal readonly record struct EnemyAffix(int Rarity, float Health, float Damage, int Armour, int MagicResistance, float Speed, string Proc,
        float ProcChance = Affixes.ProcChance, string Special = "", string Leader = "");
}
