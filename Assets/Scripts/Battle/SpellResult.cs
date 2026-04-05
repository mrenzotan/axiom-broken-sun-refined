using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Return value of SpellEffectResolver.Resolve().
    /// Carries all spell outcome data so BattleController can fire the correct UI events.
    /// </summary>
    public struct SpellResult
    {
        /// <summary>The category of effect that was applied.</summary>
        public SpellEffectType EffectType;

        /// <summary>
        /// Total effect magnitude after reaction bonus is applied.
        /// Damage dealt, HP healed, or shield HP added — depending on EffectType.
        /// </summary>
        public int Amount;

        /// <summary>True if the spell's primary target (enemy for Damage) was defeated.</summary>
        public bool TargetDefeated;

        /// <summary>True if a chemical reaction triggered during resolution.</summary>
        public bool ReactionTriggered;

        /// <summary>True if a material condition transformation was applied to the target.</summary>
        public bool MaterialTransformed;

        /// <summary>The status condition inflicted during this spell, or None if none was applied.</summary>
        public ChemicalCondition ConditionApplied;
    }
}
