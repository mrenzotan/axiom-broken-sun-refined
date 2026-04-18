using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Battle.UI;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Orchestrates the post-battle UI sequence. Sits on the BattleController
    /// GameObject (or a sibling) in the Battle scene.
    ///
    /// Victory flow:
    ///   1. Compute <see cref="PostBattleResult"/>.
    ///   2. Award XP — level-up events fire synchronously and queue in <see cref="LevelUpPromptUI"/>.
    ///   3. Grant loot items to <see cref="PlayerState.Inventory"/>.
    ///   4. Show <see cref="VictoryScreenUI"/>.
    ///   5. On dismissal, show <see cref="LevelUpPromptUI.ShowIfPending"/>.
    ///   6. On dismissal, mark enemy defeated, clear damaged-HP override, persist, transition to Platformer.
    ///
    /// Defeat flow:
    ///   1. Show <see cref="DefeatScreenUI"/>.
    ///   2. On continue, call <see cref="GameManager.TryContinueGame"/>.
    ///   3. If no save exists, transition to MainMenu.
    /// </summary>
    public class PostBattleFlowController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign the VictoryScreenUI from the Battle Canvas.")]
        private VictoryScreenUI _victoryScreenUI;

        [SerializeField]
        [Tooltip("Assign the DefeatScreenUI from the Battle Canvas.")]
        private DefeatScreenUI _defeatScreenUI;

        [SerializeField]
        [Tooltip("Assign the LevelUpPromptUI from the Battle Canvas (already present — DEV-40).")]
        private LevelUpPromptUI _levelUpPromptUI;

        [SerializeField]
        [Tooltip("Scene to load after Victory and after Continue. Usually Platformer.")]
        private string _returnScene = "Platformer";

        [SerializeField]
        [Tooltip("Scene to fall back to on Defeat if no save file exists. Usually MainMenu.")]
        private string _noSaveFallbackScene = "MainMenu";

        private readonly PostBattleOutcomeService _service = new PostBattleOutcomeService();
        private readonly System.Random _random = new System.Random();

        // Context snapshotted when the flow begins so the Victory branch can mark
        // the exact enemy defeated after the UI resolves.
        private EnemyData _pendingEnemy;
        private string _pendingEnemyId;

        /// <summary>
        /// Called by <see cref="BattleController"/> when <see cref="BattleState.Victory"/> is entered.
        /// </summary>
        public void BeginVictoryFlow(EnemyData enemy, string battleEnemyId)
        {
            _pendingEnemy   = enemy;
            _pendingEnemyId = battleEnemyId;

            PostBattleResult result = enemy != null
                ? _service.ResolveVictory(enemy, _random)
                : new PostBattleResult(0, Array.Empty<ItemGrant>());

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (result.Xp > 0)
                    gm.AwardXp(result.Xp);

                for (int i = 0; i < result.Items.Count; i++)
                    gm.PlayerState.Inventory.Add(result.Items[i].ItemId, result.Items[i].Quantity);
            }

            if (_victoryScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _victoryScreenUI is not assigned — skipping Victory UI.", this);
                HandleVictoryScreenDismissed();
                return;
            }

            XpProgress xpProgress = gm?.ProgressionService != null
                ? gm.ProgressionService.GetXpProgress()
                : new XpProgress(currentXp: 0, xpForNextLevel: 0, isAtLevelCap: true, progress01: 0f);

            _victoryScreenUI.OnDismissed += HandleVictoryScreenDismissed;
            _victoryScreenUI.Show(result, xpProgress);
        }

        private void HandleVictoryScreenDismissed()
        {
            if (_victoryScreenUI != null)
                _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;

            if (_levelUpPromptUI == null)
            {
                HandleLevelUpPromptDismissed();
                return;
            }

            _levelUpPromptUI.OnDismissed += HandleLevelUpPromptDismissed;
            _levelUpPromptUI.ShowIfPending();
        }

        private void HandleLevelUpPromptDismissed()
        {
            if (_levelUpPromptUI != null)
                _levelUpPromptUI.OnDismissed -= HandleLevelUpPromptDismissed;

            GameManager gm = GameManager.Instance;
            if (gm != null && !string.IsNullOrEmpty(_pendingEnemyId))
            {
                gm.MarkEnemyDefeated(_pendingEnemyId);
                gm.ClearDamagedEnemyHp(_pendingEnemyId);
            }
            gm?.PersistToDisk();

            _pendingEnemy   = null;
            _pendingEnemyId = null;

            if (gm?.SceneTransition != null)
                gm.SceneTransition.BeginTransition(_returnScene, TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene(_returnScene);
        }

        /// <summary>
        /// Called by <see cref="BattleController"/> when <see cref="BattleState.Defeat"/> is entered.
        /// </summary>
        public void BeginDefeatFlow()
        {
            if (_defeatScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _defeatScreenUI is not assigned — continuing immediately.", this);
                HandleDefeatContinue();
                return;
            }

            _defeatScreenUI.OnContinueClicked += HandleDefeatContinue;
            _defeatScreenUI.Show();
        }

        private void HandleDefeatContinue()
        {
            if (_defeatScreenUI != null)
                _defeatScreenUI.OnContinueClicked -= HandleDefeatContinue;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.HasSaveFile())
            {
                gm.TryContinueGame();
                return;
            }

            // No save — fall back to MainMenu.
            if (gm?.SceneTransition != null)
                gm.SceneTransition.BeginTransition(_noSaveFallbackScene, TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene(_noSaveFallbackScene);
        }

        private void OnDestroy()
        {
            if (_victoryScreenUI != null)
                _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;
            if (_levelUpPromptUI != null)
                _levelUpPromptUI.OnDismissed -= HandleLevelUpPromptDismissed;
            if (_defeatScreenUI != null)
                _defeatScreenUI.OnContinueClicked -= HandleDefeatContinue;
        }
    }
}
