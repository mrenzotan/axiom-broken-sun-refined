using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class SavePointTrigger : MonoBehaviour
    {
        [SerializeField] private string _checkpointId = string.Empty;
        [SerializeField] private PlatformerFloatingNumberSpawner _floatingNumberSpawner;

        [SerializeField]
        [Tooltip(
            "When true, touching this save point still activates the checkpoint (so it can " +
            "act as a cross-level safety net for the next scene's missed cp_start), but does " +
            "NOT update the player's respawn point. Use this on level-end checkpoints so " +
            "backtracking after touching them still respawns the player at the earlier in-level " +
            "checkpoint instead of teleporting them to the level exit on death.")]
        private bool _isFallbackOnly;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[SavePointTrigger] GameManager not found. Save skipped.", this);
                return;
            }

            GameManager.Instance.CaptureWorldSnapshot(other.transform.position);

            if (string.IsNullOrWhiteSpace(_checkpointId))
            {
                Debug.LogWarning("[SavePointTrigger] checkpointId is empty. Regen skipped.", this);
                GameManager.Instance.PersistToDisk();
                return;
            }

            // Update the respawn point on every touch (re-touch updates too) so the player
            // respawns at the most recently visited save point — unless this trigger is
            // marked fallback-only, in which case the activation still counts but the
            // respawn point stays at the earlier in-level checkpoint.
            if (!_isFallbackOnly)
            {
                GameManager.Instance.SetLastCheckpoint(
                    other.transform.position.x,
                    other.transform.position.y);
            }

            if (!GameManager.Instance.TryActivateCheckpointRegen(_checkpointId, out int healedHp, out int healedMp))
            {
                return;
            }

            if (_floatingNumberSpawner == null)
            {
                Debug.LogWarning("[SavePointTrigger] floatingNumberSpawner not assigned.", this);
                return;
            }

            _floatingNumberSpawner.SpawnHealNumbers(other.transform.position, healedHp, healedMp);
        }
    }
}
