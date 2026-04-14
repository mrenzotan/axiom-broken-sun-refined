using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class SavePointTrigger : MonoBehaviour
    {
        [SerializeField] private string _checkpointId = string.Empty;
        [SerializeField] private PlatformerFloatingNumberSpawner _floatingNumberSpawner;

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

            if (!GameManager.Instance.TryActivateCheckpointRegen(_checkpointId, out int healedHp, out int healedMp))
            {
                GameManager.Instance.PersistToDisk();
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
