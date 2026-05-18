using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Plain C# service that encapsulates the business logic of initiating a battle
    /// transition from the platformer scene. Extracted from ExplorationEnemyCombatTrigger
    /// so the MonoBehaviour remains a lifecycle/event adapter (DEV-74).
    /// </summary>
    public class BattleTriggerService
    {
        private readonly GameManager _gameManager;

        public BattleTriggerService(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        /// <summary>
        /// Returns true if the battle can be started (GameManager and SceneTransition are present).
        /// </summary>
        public bool CanStartBattle()
        {
            return _gameManager != null && _gameManager.SceneTransition != null;
        }

        /// <summary>
        /// Builds a world snapshot, sets the pending battle entry, persists state, and
        /// begins the scene transition to the Battle scene.
        /// </summary>
        public void TriggerBattle(
            CombatStartState startState,
            EnemyData enemyData,
            string enemyId,
            BattleEnvironmentData environment,
            BattleTutorialMode tutorialMode,
            Vector2 playerWorldPosition)
        {
            // Build world snapshot: capture every enemy's current position by stable ID.
            var snapshot = new WorldSnapshot();
            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (EnemyController enemy in enemies)
                snapshot.CaptureEnemy(enemy.EnemyId, enemy.transform.position.x, enemy.transform.position.y);

            _gameManager.SetWorldSnapshot(snapshot);

            int enemyCurrentHp = _gameManager.GetDamagedEnemyHp(enemyId);
            _gameManager.SetPendingBattle(
                new BattleEntry(startState, enemyData, enemyId, enemyCurrentHp,
                    environment, tutorialMode));

            _gameManager.CaptureWorldSnapshot(playerWorldPosition);
            _gameManager.PersistToDisk();
            _gameManager.SceneTransition.BeginTransition("Battle", TransitionStyle.WhiteFlash);
        }
    }
}
