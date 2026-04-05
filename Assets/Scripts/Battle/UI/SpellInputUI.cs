using System.Collections;
using Axiom.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour that drives the spell input UI panels during the voice spell phase.
    ///
    /// Panel visibility is controlled by three child GameObjects assigned in the Inspector:
    ///   - PromptPanel    — visible in PromptVisible state ("Hold [Space] and speak a spell name")
    ///   - ListeningPanel — visible in Listening state ("Listening...")
    ///   - FeedbackPanel  — visible in SpellRecognized / NotRecognized states (dynamic TMP text)
    ///
    /// The PTT InputAction is read independently for visual-only purposes.
    /// Call <see cref="Setup"/> from BattleController.Initialize() before any events fire.
    /// </summary>
    public class SpellInputUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The same PTT InputAction used by MicrophoneInputHandler — read here for visual feedback only.")]
        private InputActionReference _pushToTalkAction;

        [Header("Panels — assign child GameObjects from the Battle Canvas")]
        [SerializeField] private GameObject _promptPanel;
        [SerializeField] private GameObject _listeningPanel;
        [SerializeField] private GameObject _feedbackPanel;

        [Header("Feedback text — TMP component inside FeedbackPanel")]
        [SerializeField] private TMP_Text _feedbackText;

        [SerializeField]
        [Tooltip("Seconds before the feedback panel auto-hides after a recognition result.")]
        private float _feedbackAutoHideDelay = 2f;

        private readonly SpellInputUILogic _logic = new SpellInputUILogic();
        private BattleController           _battleController;
        private Coroutine                  _autoHide;

        // ── Setup ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="BattleController.Initialize"/> to wire up battle events.
        /// Safe to call more than once; unsubscribes from any previous controller first.
        /// </summary>
        public void Setup(BattleController battleController)
        {
            if (_battleController != null) Unsubscribe();

            _battleController = battleController;
            _battleController.OnSpellPhaseStarted  += HandleSpellPhaseStarted;
            _battleController.OnSpellRecognized    += HandleSpellRecognized;
            _battleController.OnSpellNotRecognized += HandleSpellNotRecognized;
            _battleController.OnBattleStateChanged += HandleBattleStateChanged;

            _logic.Hide();
            Refresh();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_pushToTalkAction == null) return;
            _pushToTalkAction.action.started  += OnPTTStarted;
            _pushToTalkAction.action.canceled += OnPTTCanceled;
        }

        private void OnDisable()
        {
            if (_pushToTalkAction == null) return;
            _pushToTalkAction.action.started  -= OnPTTStarted;
            _pushToTalkAction.action.canceled -= OnPTTCanceled;
        }

        private void OnDestroy() => Unsubscribe();

        // ── BattleController event handlers ───────────────────────────────────────

        private void HandleSpellPhaseStarted()
        {
            CancelAutoHide();
            _logic.ShowPrompt();
            Refresh();
        }

        private void HandleSpellRecognized(SpellData spell)
        {
            CancelAutoHide();
            _logic.ShowResult(spell.spellName);
            Refresh();
            // After spell resolves the turn advances, so return to Idle (not prompt).
            _autoHide = StartCoroutine(AutoHideAfterDelay(returnToPrompt: false));
        }

        private void HandleSpellNotRecognized()
        {
            CancelAutoHide();
            _logic.ShowError();
            Refresh();
            // Player can try again — return to prompt so they see the PTT cue.
            _autoHide = StartCoroutine(AutoHideAfterDelay(returnToPrompt: true));
        }

        private void HandleBattleStateChanged(BattleState state)
        {
            // When the turn advances (EnemyTurn, Victory, Defeat, Fled), hide everything.
            if (state == BattleState.PlayerTurn) return;
            CancelAutoHide();
            _logic.Hide();
            Refresh();
        }

        // ── PTT input handlers (visual only) ──────────────────────────────────────

        private void OnPTTStarted(InputAction.CallbackContext _)
        {
            if (_logic.CurrentState != SpellInputUILogic.State.PromptVisible) return;
            _logic.StartListening();
            Refresh();
        }

        private void OnPTTCanceled(InputAction.CallbackContext _)
        {
            if (_logic.CurrentState != SpellInputUILogic.State.Listening) return;
            // Return to prompt while recognition processes on the background thread.
            _logic.ShowPrompt();
            Refresh();
        }

        // ── Display ───────────────────────────────────────────────────────────────

        private void Refresh()
        {
            SpellInputUILogic.State state = _logic.CurrentState;

            SetActive(_promptPanel,    state == SpellInputUILogic.State.PromptVisible);
            SetActive(_listeningPanel, state == SpellInputUILogic.State.Listening);
            SetActive(_feedbackPanel,  state == SpellInputUILogic.State.SpellRecognized
                                    || state == SpellInputUILogic.State.NotRecognized);

            if (_feedbackText != null)
            {
                _feedbackText.text = state == SpellInputUILogic.State.SpellRecognized
                    ? _logic.RecognizedSpellName
                    : state == SpellInputUILogic.State.NotRecognized
                        ? "Not recognized. Try again."
                        : string.Empty;
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        // ── Auto-hide coroutine ───────────────────────────────────────────────────

        private void CancelAutoHide()
        {
            if (_autoHide == null) return;
            StopCoroutine(_autoHide);
            _autoHide = null;
        }

        private IEnumerator AutoHideAfterDelay(bool returnToPrompt)
        {
            yield return new WaitForSeconds(_feedbackAutoHideDelay);
            if (returnToPrompt)
                _logic.ShowPrompt();
            else
                _logic.Hide();
            Refresh();
            _autoHide = null;
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        private void Unsubscribe()
        {
            if (_battleController == null) return;
            _battleController.OnSpellPhaseStarted  -= HandleSpellPhaseStarted;
            _battleController.OnSpellRecognized    -= HandleSpellRecognized;
            _battleController.OnSpellNotRecognized -= HandleSpellNotRecognized;
            _battleController.OnBattleStateChanged -= HandleBattleStateChanged;
        }
    }
}
