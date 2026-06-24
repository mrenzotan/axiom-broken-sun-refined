namespace Axiom.Platformer
{
    /// <summary>
    /// A spell-interactable environmental puzzle. The player's aura cue uses this to show the
    /// proximity cue only while the nearby puzzle can still be acted on (hides once solved).
    /// </summary>
    public interface ISpellPuzzle
    {
        /// <summary>True while the puzzle can still be progressed by a spell; false once solved/consumed.</summary>
        bool IsInteractable { get; }
    }
}
