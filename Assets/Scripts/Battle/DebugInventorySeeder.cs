using UnityEngine;
using Axiom.Core;

namespace Axiom.Battle
{
    /// <summary>
    /// TEMPORARY test-only seeder for DEV-39. Seeds the player inventory on scene load
    /// so the Item action has something to show. Delete this script and its scene
    /// GameObject once a real item-grant system (loot / pickups / shop) exists.
    /// </summary>
    public class DebugInventorySeeder : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        private void Start()
        {
            if (!_enabled) return;

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[DebugInventorySeeder] No GameManager.Instance — skipping seed.");
                return;
            }

            gm.PlayerState.Inventory.Add("potion", 3);
            gm.PlayerState.Inventory.Add("ether", 2);
            Debug.Log("[DebugInventorySeeder] Seeded potion x3, ether x2.");
        }
    }
}
