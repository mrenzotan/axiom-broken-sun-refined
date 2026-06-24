using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Returned by PlayerActionHandler.ExecuteAttack() and EnemyActionHandler.ExecuteAttack().
    ///
    /// IsCrit and IsImmune are player-attack-only fields — enemy attacks always deal
    /// damage and never crit. They default to false on enemy results.
    ///
    /// ConditionApplied is enemy-attack-only — set when an on-hit condition proc fires.
    /// Defaults to None on player attack results.
    /// </summary>
    public struct AttackResult
    {
        public int  Damage;
        public bool IsCrit;
        public bool TargetDefeated;
        public bool IsImmune;

        /// <summary>
        /// The condition applied to the player by an enemy on-hit proc, or None if no proc fired.
        /// Always None for player attack results.
        /// </summary>
        public ChemicalCondition ConditionApplied;
    }
}