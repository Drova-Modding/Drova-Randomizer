namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// A hand-made legendary: a vanilla item with its own name and fixed modifiers. <c>Proc</c> and <c>Bonus</c> are as in
    /// <see cref="ItemAffix"/>.
    /// </summary>
    internal sealed record UniqueItem(string Base, string Name, float Power, string Proc, string Bonus, int BonusAmount);
}
