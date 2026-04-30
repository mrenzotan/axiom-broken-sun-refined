using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Instantiates the battle visual prefab from an EnemyData under a spawn anchor and
    /// returns its EnemyBattleAnimator. Returns the supplied fallback animator unchanged
    /// when EnemyData, battleVisualPrefab, or anchor is null, or when the spawned prefab
    /// has no EnemyBattleAnimator — preserving standalone Battle scene play-from-scene.
    /// Pure C# — zero Unity lifecycle. Call from BattleController.Start() before Initialize.
    /// </summary>
    public sealed class EnemyVisualSpawner
    {
        public EnemyBattleAnimator Spawn(
            EnemyData data,
            Transform anchor,
            EnemyBattleAnimator fallback)
        {
            if (data == null) return fallback;

            if (data.battleVisualPrefab == null)
            {
                Debug.LogWarning(
                    $"[Battle] EnemyData '{data.enemyName}' has no battleVisualPrefab assigned — " +
                    "using Inspector-assigned _enemyAnimator. Assign a battle prefab on the " +
                    "EnemyData asset to swap the enemy GameObject per battle.");
                return fallback;
            }

            if (anchor == null)
            {
                Debug.LogWarning(
                    $"[Battle] EnemyVisualSpawner.Spawn called with null anchor for " +
                    $"'{data.enemyName}' — using Inspector-assigned _enemyAnimator. " +
                    "Assign _enemySpawnAnchor on BattleController in the Battle scene.");
                return fallback;
            }

            GameObject instance = Object.Instantiate(data.battleVisualPrefab, anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.SetActive(true);

            EnemyBattleAnimator spawned = instance.GetComponentInChildren<EnemyBattleAnimator>(
                includeInactive: true);
            if (spawned == null)
            {
                Debug.LogWarning(
                    $"[Battle] Spawned battleVisualPrefab for '{data.enemyName}' has no " +
                    "EnemyBattleAnimator component on the root or any child — using fallback. " +
                    "Add an EnemyBattleAnimator to the prefab.");
                DestroySafely(instance);
                return fallback;
            }

            return spawned;
        }

        // Object.Destroy is illegal outside Play Mode and triggers a
        // "Destroy may not be called from edit mode" error in EditMode tests,
        // which Unity Test Framework treats as a failure. Fall back to
        // DestroyImmediate when the application is not playing.
        private static void DestroySafely(Object obj)
        {
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
