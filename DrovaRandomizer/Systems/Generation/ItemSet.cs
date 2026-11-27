namespace DrovaRandomizer.Systems.Generation
{
    /// <summary>
    /// A set of named pieces, each a vanilla item copy. <c>Bonuses[i]</c> applies once <c>i + 2</c> pieces are worn, a
    /// bonus as in <see cref="ItemAffix"/>.
    /// </summary>
    internal sealed record ItemSet(string Name, (string Base, string Name)[] Pieces, (string Bonus, int Amount)[] Bonuses);
}
