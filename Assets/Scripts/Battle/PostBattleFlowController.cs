using System;
using System.Collections;
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
    ///   2. On continue, call <see cref="GameManager.RespawnAtLastCheckpoint"/> — heals the
    ///      player and loads the level scene of the most recently touched save point.
    ///   3. If no checkpoint has been activated, transition to MainMenu.
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

        [Header("Fade (DEV-77)")]
        [SerializeField]
        [Tooltip("CanvasGroup on the VictoryScreenPanel root. Drives the crossfade to Level-Up.")]
        private CanvasGroup _victoryCanvasGroup;

        [SerializeField]
        [Tooltip("CanvasGroup on the LevelUpPromptPanel root. Drives the crossfade from Victory.")]
        private CanvasGroup _levelUpCanvasGroup;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Fade duration for each leg of the crossfade, seconds. DEV-77 spec: ≤0.2s.")]
        private float _fadeDuration = 0.2f;

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

            XpProgress xpBefore = gm?.ProgressionService != null
                ? gm.ProgressionService.GetXpProgress()
                : new XpProgress(currentXp: 0, xpForNextLevel: 0, isAtLevelCap: true, progress01: 0f);

            int levelsGained = 0;

            if (gm != null)
            {
                ProgressionService progression = gm.ProgressionService;
                if (progression != null)
                    progression.OnLevelUp += OnLevelUpDuringAward;

                if (result.Xp > 0)
                    gm.AwardXp(result.Xp);

                if (progression != null)
                    progression.OnLevelUp -= OnLevelUpDuringAward;

                for (int i = 0; i < result.Items.Count; i++)
                    gm.PlayerState.Inventory.Add(result.Items[i].ItemId, result.Items[i].Quantity);
            }

            void OnLevelUpDuringAward(LevelUpResult _) => levelsGained++;

            if (_victoryScreenUI == null)
            {
                Debug.LogWarning(
                    "[PostBattleFlow] _victoryScreenUI is not assigned — skipping Victory UI.", this);
                HandleVictoryScreenDismissed();
                return;
            }

            XpProgress xpAfter = gm?.ProgressionService != null
                ? gm.ProgressionService.GetXpProgress()
                : new XpProgress(currentXp: 0, xpForNextLevel: 0, isAtLevelCap: true, progress01: 0f);

            _victoryScreenUI.OnDismissed += HandleVictoryScreenDismissed;

            if (_victoryCanvasGroup != null)
                SetCanvasGroupAlpha(_victoryCanvasGroup, 1f, interactable: true);

            _victoryScreenUI.Show(result, xpBefore, xpAfter, levelsGained);
        }

        private void HandleVictoryScreenDismissed()
        {
            if (_victoryScreenUI != null)
                _victoryScreenUI.OnDismissed -= HandleVictoryScreenDismissed;

            StartCoroutine(CrossfadeVictoryToLevelUp());
        }

        private IEnumerator CrossfadeVictoryToLevelUp()
        {
            // Leg 1: fade Victory out (if the panel is actually showing and we have a CanvasGroup).
            if (_victoryScreenUI != null && _victoryScreenUI.IsShowing && _victoryCanvasGroup != null)
                yield return FadeCanvasGroup(_victoryCanvasGroup, 1f, 0f, _fadeDuration);

            if (_victoryScreenUI != null)
                _victoryScreenUI.Hide();

            // No level-up UI wired → go straight to transition.
            if (_levelUpPromptUI == null)
            {
                HandleLevelUpPromptDismissed();
                yield break;
            }

            // Pre-set Level-Up alpha to 0 BEFORE ShowIfPending so it doesn't flash at
            // full alpha for one frame if the panel is about to activate.
            if (_levelUpCanvasGroup != null)
                SetCanvasGroupAlpha(_levelUpCanvasGroup, 0f, interactable: false);

            _levelUpPromptUI.OnDismissed += HandleLevelUpPromptDismissed;
            _levelUpPromptUI.ShowIfPending();

            // Empty-queue path: ShowIfPending fired OnDismissed synchronously without
            // activating the panel. HandleLevelUpPromptDismissed has already run. Skip
            // the fade-in on an invisible panel — scene transition is already queued.
            if (!_levelUpPromptUI.IsShowing)
                yield break;

            // Leg 2: fade Level-Up in.
            if (_levelUpCanvasGroup != null)
                yield return FadeCanvasGroup(_levelUpCanvasGroup, 0f, 1f, _fadeDuration);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            // Disable input for the duration of the fade so mid-fade clicks can't
            // double-dismiss the panel.
            bool targetInteractable = to >= 1f;
            group.interactable = false;
            group.blocksRaycasts = targetInteractable; // keep raycasts blocked while fading in; release while fading out
            group.alpha = from;

            if (duration <= 0f)
            {
                SetCanvasGroupAlpha(group, to, interactable: targetInteractable);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            SetCanvasGroupAlpha(group, to, interactable: targetInteractable);
        }

        private static void SetCanvasGroupAlpha(CanvasGroup group, float alpha, bool interactable)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
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

            if (gm != null)
                gm.ReturnToWorldScene();
            else
                SceneManager.LoadScene("Platformer");
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
            if (gm != null && gm.RespawnAtLastCheckpoint(TransitionStyle.BlackFade))
                return;

            // No checkpoint activated (or no GameManager) — fall back to MainMenu.
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
