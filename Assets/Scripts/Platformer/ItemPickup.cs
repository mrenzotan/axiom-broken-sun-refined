using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to a world pickup GameObject with a Trigger Collider2D.
    /// On player contact, grants the configured item to inventory and despawns.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Item data asset to grant on collect.")]
        private ItemData _itemData;

        [SerializeField]
        [Tooltip("Quantity to grant.")]
        private int _quantity = 1;

        [SerializeField]
        [Tooltip("Unique pickup ID for session persistence. Must be unique per pickup instance in the scene.")]
        private string _pickupId;

        [SerializeField]
        [Tooltip("Optional Animator to play a collect animation before despawning.")]
        private Animator _animator;

        public string PickupId => _pickupId;

        private ItemPickupController _controller;

        private void Awake()
        {
            if (_itemData == null)
            {
                Debug.LogWarning($"[ItemPickup] itemData is not assigned on '{gameObject.name}'.", this);
                return;
            }

            _controller = new ItemPickupController(_itemData, _quantity);
        }

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_controller == null) return;
            if (string.IsNullOrWhiteSpace(_pickupId)) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.IsPickupCollected(_pickupId)) return;

            _controller.GrantTo(GameManager.Instance.PlayerState.Inventory);
            GameManager.Instance.MarkPickupCollected(_pickupId);

            if (_animator != null)
                _animator.SetTrigger("Collect");

            Destroy(gameObject);
        }
    }
}
