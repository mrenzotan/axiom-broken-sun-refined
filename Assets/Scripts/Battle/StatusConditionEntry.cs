using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Tracks one active status condition on a character during combat.
    /// TickCount records how many DoT ticks have already fired — used by
    /// escalating conditions like Corroded to compute the current damage multiplier.
    /// BaseDamage is set by SpellEffectResolver when the condition is applied and
    /// used by ProcessConditionTurn for DoT calculations.
    /// </summary>
    public struct StatusConditionEntry
    {
        public ChemicalCondition Condition;
        public int TurnsRemaining;
        public int TickCount;
        public int BaseDamage;
    }
}
