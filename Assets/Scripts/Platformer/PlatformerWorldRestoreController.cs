// Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Core;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour — restores Platformer world state after returning from a battle.
    ///
    /// Add one instance of this to the Platformer scene root.
    ///
    /// Restoration runs in Start() — NOT on OnSceneReady. The SceneTransitionController
    /// activates the scene under an opaque fade overlay, then fades in; teleporting in
    /// Start places the player at the correct position before the first rendered frame
    /// the user sees, so they never glimpse the initial spawn point (Bug 1).
    ///
    /// Defeated enemies and collected pickups are destroyed unconditionally each Start()
    /// so returning from a Victory battle does not re-spawn them inside the player's
    /// restored trigger zone, which would re-launch the same battle (Bug 2) or re-grant
    /// the same item.
    ///
    /// Script Execution Order: set to -10 (Edit → Project Settings → Script Execution Order)
    /// so this runs before PlayerController's Start, ensuring position is teleported
    /// before input is enabled.
    /// </summary>
    public class PlatformerWorldRestoreController : MonoBehaviour
    {
        private void Start()
        {
            if (GameManager.Instance == null) return;

            // Per-scene enemy state (DEV-XX) keys off PlayerState.ActiveSceneName. After a
            // LevelExitTrigger transition, that field still names the previous scene until
            // the player touches a save point — so we resync here on every level-scene load.
            GameManager.Instance.PlayerState.SetActiveScene(SceneManager.GetActiveScene().name);

            DestroyDefeatedEnemies();
            DestroyCollectedPickups();

            if (GameManager.Instance.CurrentWorldSnapshot != null)
                RestoreWorldState();
        }

        private void DestroyDefeatedEnemies()
        {
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude);
            foreach (EnemyController enemy in enemies)
            {
                if (GameManager.Instance.IsEnemyDefeated(enemy.EnemyId))
                    Destroy(enemy.gameObject);
            }
        }

        private void DestroyCollectedPickups()
        {
            ItemPickup[] pickups = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude);
            foreach (ItemPickup pickup in pickups)
            {
                if (GameManager.Instance.IsPickupCollected(pickup.PickupId))
                    Destroy(pickup.gameObject);
            }
        }

        private void RestoreWorldState()
        {
            WorldSnapshot snapshot  = GameManager.Instance.CurrentWorldSnapshot;
            PlayerState playerState = GameManager.Instance.PlayerState;

            // ── 1. Restore player position ──────────────────────────────────
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = new Vector3(
                    playerState.WorldPositionX,
                    playerState.WorldPositionY,
                    player.transform.position.z);

                // Zero any residual velocity so the player does not carry
                // pre-battle motion into the restored position.
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                    body.linearVelocity = Vector2.zero;
            }
            else
            {
                Debug.LogWarning(
                    "[PlatformerWorldRestoreController] PlayerController not found in scene — " +
                    "player position will not be restored.",
                    this);
            }

            // ── 2. Restore enemy positions (skip ones already destroyed) ────
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude);
            foreach (EnemyController enemy in enemies)
            {
                if (snapshot.TryGetEnemy(enemy.EnemyId, out EnemyWorldState state))
                    enemy.RestoreWorldPosition(state.PositionX, state.PositionY);
            }

            // ── 3. Clear snapshot — restoration complete ────────────────────
            GameManager.Instance.ClearWorldSnapshot();
        }
    }
}
