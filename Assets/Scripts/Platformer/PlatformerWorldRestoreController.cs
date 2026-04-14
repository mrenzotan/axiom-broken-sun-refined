// Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs
using UnityEngine;
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
    /// Defeated enemies are destroyed unconditionally each Start() so returning from a
    /// Victory battle does not re-spawn the enemy inside the player's restored trigger
    /// zone, which would re-launch the same battle (Bug 2).
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

            DestroyDefeatedEnemies();

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
