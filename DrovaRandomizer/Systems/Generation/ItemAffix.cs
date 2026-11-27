namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// The rolled modifiers of one item variant. A weapon multiplies the damage it deals by
    /// <see cref="Power"/>, an armour piece its own stat bonuses. <c>Proc</c> is as in <see cref="EnemyAffix"/>.
    /// <c>Bonus</c> is "" or one of the bonus constants of <see cref="Affixes"/>, worth <c>BonusAmount</c>.
    /// </summary>
    internal readonly record struct ItemAffix(int Rarity, float Power, string Proc, string Bonus = "", int BonusAmount = 0);
}
