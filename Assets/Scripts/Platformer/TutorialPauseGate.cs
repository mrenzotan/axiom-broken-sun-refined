namespace Axiom.Platformer
{
    /// <summary>
    /// Per-trigger pause-on-prompt gating. Decides whether entering a tutorial zone should
    /// pause the game right now, honoring the designer's "only once" vs "every entry" choice.
    /// One instance per TutorialPromptTrigger; state lives for that trigger's lifetime
    /// (resets on scene reload, which spawns a fresh trigger + gate).
    /// </summary>
    public class TutorialPauseGate
    {
        public bool HasPaused { get; private set; }

        /// <summary>
        /// Returns true if entering should pause now.
        /// <paramref name="pauseOnlyOnce"/>: when true, only the first entry pauses; later
        /// entries return false. When false, every entry returns true.
        /// </summary>
        public bool ShouldPause(bool pauseOnlyOnce)
        {
            if (pauseOnlyOnce && HasPaused) return false;
            HasPaused = true;
            return true;
        }
    }
}
