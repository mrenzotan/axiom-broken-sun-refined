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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            if (_menuController == null)
            {
                Debug.LogWarning("[ExplorationButtonGateTrigger] _menuController is not assigned.", this);
                return;
            }

            _triggered = true;
            _menuController.EnableButtons();
            Debug.Log("[ExplorationButtonGateTrigger] Spellbook and Items buttons enabled.", this);
        }
    }
}
