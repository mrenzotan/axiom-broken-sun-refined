using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to an overworld enemy. Handles both combat engagement paths:
    ///
    ///   Surprised  — enemy body trigger overlaps the Player tag → enemy acts first.
    ///   Advantaged — PlayerOverworldAttack calls TriggerAdvantagedBattle() → player acts first.
    ///
    /// Sets GameManager.PendingBattle then loads the Battle scene.
    /// Requires a Collider2D on this GameObject with Is Trigger enabled for the Surprised path.
    /// Requires the player GameObject to have the "Player" tag.
    /// </summary>
    public class OverworldEnemyCombatTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("EnemyData ScriptableObject for this enemy. Passed to BattleController at battle load.")]
        private EnemyData _enemyData;

        // Prevents double-trigger if Advantaged and Surprised fire in the same frame.
        private bool _triggered;

        /// <summary>
        /// Called by PlayerOverworldAttack when the player attacks this enemy first.
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
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;
            TriggerBattle(CombatStartState.Surprised);
        }

        private void TriggerBattle(CombatStartState startState)
        {
            _triggered = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData));
            }
            else
            {
                Debug.LogWarning(
                    "[OverworldEnemyCombatTrigger] GameManager not found — Battle scene will " +
                    "use BattleController Inspector fallback values.",
                    this);
            }

            SceneManager.LoadScene("Battle");
        }
    }
}
