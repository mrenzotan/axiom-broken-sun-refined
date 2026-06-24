using System.Collections.Generic;
using Axiom.Platformer;

/// <summary>
/// Plain C# — computes whether the player's proximity aura should be visible: visible when the
/// player is inside the proximity zone of at least one still-interactable puzzle AND the cue is
/// not suppressed (e.g. mid-cast). The MonoBehaviour wrapper (PlayerAuraCue) owns the visuals.
/// </summary>
public class AuraVisibilityState
{
    private readonly HashSet<ISpellPuzzle> _inRange = new();
    private bool _suppressed;

    public bool IsVisible => !_suppressed && AnyInteractableInRange();

    public void Enter(ISpellPuzzle puzzle)
    {
        if (puzzle != null) _inRange.Add(puzzle);
    }

    public void Exit(ISpellPuzzle puzzle)
    {
        if (puzzle != null) _inRange.Remove(puzzle);
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }

    private bool AnyInteractableInRange()
    {
        foreach (ISpellPuzzle puzzle in _inRange)
        {
            if (puzzle != null && puzzle.IsInteractable) return true;
        }
        return false;
    }
}
