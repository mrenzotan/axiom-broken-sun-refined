using Axiom.Core;

namespace Axiom.Platformer
{
    /// <summary>
    /// Inspector-selectable identifier for "this trigger is a one-shot tutorial moment."
    /// Maps to a persisted bool flag on PlayerState via TutorialOneShotFlagResolver.
    /// None = the trigger replays every time the player enters its zone.
    /// </summary>
    public enum OneShotTutorialFlag
    {
        None,
        FirstBattle,
        SpellTutorialBattle,
        FirstSpikeHit,
        FirstDeath
    }

    /// <summary>
    /// Pure static helper — resolves a OneShotTutorialFlag against PlayerState.
    /// Extracted from TutorialPromptTrigger so it's unit-testable without spinning
    /// up a Unity scene (per CLAUDE.md: logic in plain C#).
    /// </summary>
    public static class TutorialOneShotFlagResolver
    {
        public static bool IsFlagSet(PlayerState ps, OneShotTutorialFlag flag)
        {
            if (ps == null) return false;
            return flag switch
            {
                OneShotTutorialFlag.FirstBattle          => ps.HasCompletedFirstBattleTutorial,
                OneShotTutorialFlag.SpellTutorialBattle  => ps.HasCompletedSpellTutorialBattle,
                OneShotTutorialFlag.FirstSpikeHit        => ps.HasSeenFirstSpikeHit,
                OneShotTutorialFlag.FirstDeath           => ps.HasSeenFirstDeath,
                _                                        => false,
            };
        }
    }
}
