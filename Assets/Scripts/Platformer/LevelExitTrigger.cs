using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Platformer
{
    /// <summary>
    /// Trigger collider placed at a level's exit. On player contact, transitions to
    /// the configured scene using the shared SceneTransitionController on GameManager.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExitTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Exact scene name to load. Must be added to Build Settings.")]
        private string _targetSceneName = string.Empty;

        [SerializeField]
        [Tooltip("Visual style of the scene transition. Defaults to WhiteFlash to match battle transitions.")]
        private TransitionStyle _transitionStyle = TransitionStyle.WhiteFlash;

        [Header("Post-Boss Cutscene")]
        [SerializeField]
        [Tooltip("If set, plays a cutscene before exiting when the boss is defeated.")]
        private CutsceneData _postBossCutscene;

        [SerializeField]
        [Tooltip("Boss enemy id required to trigger the post-boss cutscene.")]
        private string _bossEnemyId = string.Empty;

        private bool _triggered;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            if (string.IsNullOrWhiteSpace(_targetSceneName))
            {
                Debug.LogWarning("[LevelExitTrigger] _targetSceneName is empty — exit ignored.", this);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.SceneTransition == null)
            {
                Debug.LogWarning("[LevelExitTrigger] GameManager or SceneTransition missing — exit ignored.", this);
                return;
            }

            _triggered = true;

            GameManager.Instance.CaptureWorldSnapshot(other.transform.position);
            GameManager.Instance.PersistToDisk();

            if (_postBossCutscene != null &&
                BossVictoryChecker.IsVictorious(
                    defeatedEnemyIds: GameManager.Instance.DefeatedEnemyIdsInScene(SceneManager.GetActiveScene().name),
                    bossEnemyId: _bossEnemyId))
            {
                GameManager.Instance.BeginCutscene(_postBossCutscene, _transitionStyle);
                return;
            }

            GameManager.Instance.SceneTransition.BeginTransition(_targetSceneName, _transitionStyle);
        }
    }
}
