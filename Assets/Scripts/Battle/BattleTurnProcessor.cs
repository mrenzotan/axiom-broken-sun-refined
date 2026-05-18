using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# service that processes condition ticks at the start of a character's turn
    /// and determines whether the turn should be skipped (e.g. Frozen).
    /// Extracted from BattleController so the MonoBehaviour remains a lifecycle/event
    /// adapter (DEV-74).
    /// </summary>
    public class BattleTurnProcessor
    {
        /// <summary>
        /// Processes condition ticks for the player at the start of their turn.
        /// Returns the turn result so the caller (BattleController) can fire events and
        /// start coroutines.
        /// </summary>
        public ConditionTurnResult ProcessPlayerTurnStart(CharacterStats playerStats)
        {
            return playerStats.ProcessConditionTurn();
        }

        /// <summary>
        /// Processes condition ticks for the enemy at the start of their turn.
        /// Returns the turn result so the caller (BattleController) can fire events and
        /// start coroutines.
        /// </summary>
        public ConditionTurnResult ProcessEnemyTurnStart(CharacterStats enemyStats)
        {
            return enemyStats.ProcessConditionTurn();
        }
    }
}
