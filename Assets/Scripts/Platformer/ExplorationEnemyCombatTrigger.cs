using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to an exploration enemy. Handles both combat engagement paths:
    ///
    ///   Surprised  — enemy body trigger overlaps the Player tag → enemy acts first.
    ///   Advantaged — PlayerExplorationAttack calls TriggerAdvantagedBattle() → player acts first.
    ///
    /// Sets GameManager.PendingBattle then loads the Battle scene.
    /// Requires a Collider2D on this GameObject with Is Trigger enabled for the Surprised path.
    /// Requires the player GameObject to have the "Player" tag.
    /// </summary>
    public class ExplorationEnemyCombatTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("EnemyData ScriptableObject for this enemy. Passed to BattleController at battle load.")]
        private EnemyData _enemyData;

        // Set once a battle path is committed — blocks any further trigger on this enemy.
        private bool _triggered;
        // Set by PlayerController.BeginAttack — blocks the Surprised path while the
        // attack animation plays so the enemy walking into the player can't override
        // the Advantaged state before OnAttackAnimationEnd fires.
        private bool _reservedForAdvantaged;

        /// <summary>
        /// Called by PlayerController.BeginAttack when the player commits to attacking
        /// this enemy. Blocks the Surprised path for the duration of the attack animation.
        /// </summary>
        public void ReserveForAdvantagedBattle()
        {
            _reservedForAdvantaged = true;
        }

        /// <summary>
        /// Called by PlayerController.OnAttackAnimationEnd after the attack clip finishes.
        /// Produces CombatStartState.Advantaged — player takes the first turn.
        /// No-op if a battle trigger is already in progress.
        /// </summary>
        public void TriggerAdvantagedBattle()
        {
            if (_triggered) return;
            TriggerBattle(CombatStartState.Advantaged);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered || _reservedForAdvantaged) return;
            if (!other.CompareTag("Player")) return;
            TriggerBattle(CombatStartState.Surprised);
        }

        private void TriggerBattle(CombatStartState startState)
        {
            _triggered = true;

            // No standalone fallback: without GameManager, PendingBattle cannot be set,
            // so BattleController would start with Inspector defaults and no correct enemy data.
            // Unlike BattleController.Fled (which can safely load Platformer without GameManager),
            // this path has no safe degraded mode.
            if (GameManager.Instance == null)
            {
                Debug.LogWarning(
                    "[ExplorationEnemyCombatTrigger] GameManager not found — battle cannot start and trigger is now consumed. " +
                    "Add the GameManager prefab to the Platformer scene.",
                    this);
                return;
            }

            if (GameManager.Instance.SceneTransition == null)
            {
                Debug.LogWarning(
                    "[ExplorationEnemyCombatTrigger] SceneTransitionController not found on GameManager " +
                    "— battle cannot start. Check the GameManager prefab has a SceneTransitionController child.",
                    this);
                return;
            }

            GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData));
            GameManager.Instance.SceneTransition.BeginTransition("Battle", TransitionStyle.WhiteFlash);
        }
    }
}
