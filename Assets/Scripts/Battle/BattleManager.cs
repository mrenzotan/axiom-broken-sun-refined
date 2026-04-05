using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# state machine for turn-based combat.
    /// Contains zero UnityEngine calls — all Unity lifecycle is handled by BattleController.
    /// </summary>
    public class BattleManager
    {
        public BattleState CurrentState { get; private set; }

        /// <summary>Fires every time the state changes, passing the new state.</summary>
        public event Action<BattleState> OnStateChanged;

        /// <summary>
        /// Starts the battle. Advantaged gives the player the first turn;
        /// Surprised gives the enemy the first turn.
        /// </summary>
        public void StartBattle(CombatStartState startState)
        {
            var firstState = startState == CombatStartState.Advantaged
                ? BattleState.PlayerTurn
                : BattleState.EnemyTurn;
            TransitionTo(firstState);
        }

        /// <summary>
        /// Call when the player finishes their action.
        /// Pass enemyDefeated=true to end in Victory; false to continue to EnemyTurn.
        /// No-op if called outside PlayerTurn.
        /// </summary>
        public void OnPlayerActionComplete(bool enemyDefeated)
        {
            if (CurrentState != BattleState.PlayerTurn) return;
            TransitionTo(enemyDefeated ? BattleState.Victory : BattleState.EnemyTurn);
        }

        /// <summary>
        /// Call when the player chooses to flee.
        /// Transitions to Fled. No-op if called outside PlayerTurn.
        /// </summary>
        public void OnPlayerFled()
        {
            if (CurrentState != BattleState.PlayerTurn) return;
            TransitionTo(BattleState.Fled);
        }

        /// <summary>
        /// Call when the enemy finishes their action.
        /// Pass playerDefeated=true to end in Defeat; false to continue to PlayerTurn.
        /// No-op if called outside EnemyTurn.
        /// </summary>
        public void OnEnemyActionComplete(bool playerDefeated)
        {
            if (CurrentState != BattleState.EnemyTurn) return;
            TransitionTo(playerDefeated ? BattleState.Defeat : BattleState.PlayerTurn);
        }

        private void TransitionTo(BattleState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
