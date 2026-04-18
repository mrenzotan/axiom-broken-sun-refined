namespace Axiom.Core
{
    /// <summary>
    /// Immutable snapshot of the player's XP progress within their current level.
    /// Produced by <see cref="ProgressionService.GetXpProgress"/> and consumed by
    /// the Victory screen so the UI avoids curve lookups and ratio math.
    /// </summary>
    public readonly struct XpProgress
    {
        /// <summary>XP accumulated toward the next level (i.e. <c>PlayerState.Xp</c>).</summary>
        public int CurrentXp { get; }

        /// <summary>XP required to cross into the next level, or 0 at the level cap.</summary>
        public int XpForNextLevel { get; }

        /// <summary>True when the player cannot level up further (<see cref="XpForNextLevel"/> is 0).</summary>
        public bool IsAtLevelCap { get; }

        /// <summary>
        /// Fractional progress into the current level, clamped to [0, 1].
        /// Always 0 when <see cref="IsAtLevelCap"/> is true.
        /// </summary>
        public float Progress01 { get; }

        public XpProgress(int currentXp, int xpForNextLevel, bool isAtLevelCap, float progress01)
        {
            CurrentXp      = currentXp;
            XpForNextLevel = xpForNextLevel;
            IsAtLevelCap   = isAtLevelCap;
            Progress01     = progress01;
        }
    }
}
