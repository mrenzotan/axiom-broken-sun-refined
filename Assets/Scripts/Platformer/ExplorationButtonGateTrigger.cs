using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class ExplorationButtonGateTrigger : MonoBehaviour
    {
        [SerializeField] private ExplorationMenuController _menuController;

        private bool _triggered;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Start()
        {
            if (_menuController == null)
                _menuController = FindObjectOfType<ExplorationMenuController>();

            if (_menuController == null)
            {
                Debug.LogError("[ExplorationButtonGateTrigger] No ExplorationMenuController found in scene. " +
                               "Create one or assign it manually in the Inspector.", this);
                return;
            }

            Collider2D col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogError("[ExplorationButtonGateTrigger] No Collider2D on this GameObject. " +
                               "Attach one and enable IsTrigger.", this);
            }
            else if (!col.isTrigger)
            {
                Debug.LogWarning("[ExplorationButtonGateTrigger] Collider2D.IsTrigger is false. " +
                                 "OnTriggerEnter2D will not fire.", this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (_menuController == null) return;
            if (!other.CompareTag("Player")) return;

            _triggered = true;
            _menuController.EnableButtons();
        }
    }
}
