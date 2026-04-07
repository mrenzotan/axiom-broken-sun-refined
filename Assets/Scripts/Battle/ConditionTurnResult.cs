namespace Axiom.Battle
{
    /// <summary>
    /// Return value of CharacterStats.ProcessConditionTurn().
    /// Tells BattleController what happened during the condition tick for this character's turn.
    /// </summary>
    public struct ConditionTurnResult
    {
        /// <summary>Sum of all DoT damage dealt to this character this tick.</summary>
        public int TotalDamageDealt;

        /// <summary>True if the character has the Frozen status — their action should be skipped.</summary>
        public bool ActionSkipped;
    }
}
