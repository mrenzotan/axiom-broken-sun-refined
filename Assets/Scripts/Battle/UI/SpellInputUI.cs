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
    /// Voice spell text is shown through the shared battle message box so the player has one
    /// narration surface. Continue remains owned by queued battle messages only.
    ///
    /// The PTT InputAction is read independently for visual-only purposes.
    /// Call <see cref="Setup"/> from BattleController.Initialize() before any events fire.
    /// </summary>
    public class SpellInputUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The same PTT InputAction used by MicrophoneInputHandler — read here for visual feedback only.")]
        private InputActionReference _pushToTalkAction;

        [Header("Root — the SpellInputPanel itself (self-reference)")]
        [SerializeField] private GameObject _panel;

        [Header("Shared battle message box")]
        [SerializeField] private StatusMessageUI _statusMessageUI;

        [Header("Panels — assign child GameObjects from the Battle Canvas")]
        [SerializeField] private GameObject _promptPanel;
        [SerializeField] private GameObject _listeningPanel;
        [SerializeField] private GameObject _feedbackPanel;

        [Header("Feedback text — TMP component inside FeedbackPanel")]
        [SerializeField] private TMP_Text _feedbackText;

        [SerializeField]
        [Tooltip("Seconds before the feedback panel auto-hides after a recognition result.")]
        private float _feedbackAutoHideDelay = 2f;

        [SerializeField] private string _promptMessage = "Hold [Left Shift] to speak a spell\n\n[Esc] to cancel";
        [SerializeField] private string _listeningMessage = "\"Listening...\"";
        [SerializeField] private string _notRecognizedMessage = "Not recognized. Try again.";

        [Header("Cancel input — DEV-91")]

        [SerializeField]
        [Tooltip("Cancel InputAction (Esc on keyboard, B on gamepad). Wired to BattleController.CancelSpellPhase. Required for keyboard/gamepad cancel — leave unassigned only if the Cancel button is the sole cancel route.")]
        private InputActionReference _cancelSpellAction;

        [SerializeField]
        [Tooltip("Optional. Visible Cancel button child of the SpellInputPanel. Provides discoverable cancel for mouse/touch users; clicks call the same path as the Cancel input action.")]
        private UnityEngine.UI.Button _cancelButton;

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
            _battleController.OnSpellPhaseStarted   += HandleSpellPhaseStarted;
            _battleController.OnSpellRecognized     += HandleSpellRecognized;
            _battleController.OnSpellNotRecognized  += HandleSpellNotRecognized;
            _battleController.OnSpellCastRejected   += HandleSpellCastRejected;
            _battleController.OnBattleStateChanged  += HandleBattleStateChanged;
            _battleController.OnSpellPhaseCancelled += HandleSpellPhaseCancelled;

            _logic.Hide();
            Refresh();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_pushToTalkAction != null)
            {
                _pushToTalkAction.action.started  += OnPTTStarted;
                _pushToTalkAction.action.canceled += OnPTTCanceled;
            }

            if (_cancelSpellAction != null && _cancelSpellAction.action != null)
            {
                _cancelSpellAction.action.performed += OnCancelSpellPerformed;
                _cancelSpellAction.action.Enable();
            }

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        private void OnDisable()
        {
            if (_pushToTalkAction != null)
            {
                _pushToTalkAction.action.started  -= OnPTTStarted;
                _pushToTalkAction.action.canceled -= OnPTTCanceled;
            }

            if (_cancelSpellAction != null && _cancelSpellAction.action != null)
            {
                _cancelSpellAction.action.performed -= OnCancelSpellPerformed;
                _cancelSpellAction.action.Disable();
            }

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
        }

        private void OnDestroy() => Unsubscribe();

        // ── BattleController event handlers ───────────────────────────────────────

        private void HandleSpellPhaseStarted()
        {
            CancelAutoHide();
            if (_panel != null) _panel.SetActive(true);
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

        private void HandleSpellCastRejected(string reason)
        {
            CancelAutoHide();
            _logic.ShowRejection(reason);
            Refresh();
            // Return to action menu automatically — player must re-select Spell or another action.
            _autoHide = StartCoroutine(AutoHideAfterDelay(returnToPrompt: false));
        }

        private void HandleBattleStateChanged(BattleState state)
        {
            // When the turn advances (EnemyTurn, Victory, Defeat, Fled), hide everything.
            if (state == BattleState.PlayerTurn) return;
            CancelAutoHide();
            _logic.Hide();
            Refresh();
            if (_panel != null) _panel.SetActive(false);
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
            bool useSharedMessageBox = _statusMessageUI != null;

            SetActive(_promptPanel,    !useSharedMessageBox && state == SpellInputUILogic.State.PromptVisible);
            SetActive(_listeningPanel, !useSharedMessageBox && state == SpellInputUILogic.State.Listening);
            SetActive(_feedbackPanel,  !useSharedMessageBox
                                    && (state == SpellInputUILogic.State.SpellRecognized
                                     || state == SpellInputUILogic.State.NotRecognized
                                     || state == SpellInputUILogic.State.Rejected));

            string message = GetSharedMessage(state);
            if (useSharedMessageBox && !string.IsNullOrWhiteSpace(message))
                _statusMessageUI.ShowSpellPrompt(message);
            else if (useSharedMessageBox)
                _statusMessageUI.ClearSpellPrompt();

            if (_feedbackText != null)
            {
                _feedbackText.text = state switch
                {
                    SpellInputUILogic.State.SpellRecognized => char.ToUpper(_logic.RecognizedSpellName[0]) + _logic.RecognizedSpellName[1..],
                    SpellInputUILogic.State.NotRecognized   => _notRecognizedMessage,
                    SpellInputUILogic.State.Rejected        => _logic.RejectionMessage,
                    _                                       => string.Empty
                };
            }
        }

        private string GetSharedMessage(SpellInputUILogic.State state)
        {
            return state switch
            {
                SpellInputUILogic.State.PromptVisible   => _promptMessage,
                SpellInputUILogic.State.Listening       => _listeningMessage,
                SpellInputUILogic.State.SpellRecognized => char.ToUpper(_logic.RecognizedSpellName[0]) + _logic.RecognizedSpellName[1..],
                SpellInputUILogic.State.NotRecognized   => _notRecognizedMessage,
                SpellInputUILogic.State.Rejected        => _logic.RejectionMessage,
                _                                       => string.Empty
            };
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
            if (!returnToPrompt && _panel != null) _panel.SetActive(false);
            _autoHide = null;
        }

        // ── Cancel handlers (DEV-91) ──────────────────────────────────────────────

        private void OnCancelSpellPerformed(InputAction.CallbackContext _) => RequestCancel();

        private void OnCancelButtonClicked() => RequestCancel();

        private void RequestCancel()
        {
            if (_battleController == null) return;
            _battleController.CancelSpellPhase();
        }

        private void HandleSpellPhaseCancelled()
        {
            CancelAutoHide();
            _logic.Hide();
            Refresh();
            if (_panel != null) _panel.SetActive(false);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        private void Unsubscribe()
        {
            if (_battleController == null) return;
            _battleController.OnSpellPhaseStarted   -= HandleSpellPhaseStarted;
            _battleController.OnSpellRecognized     -= HandleSpellRecognized;
            _battleController.OnSpellNotRecognized  -= HandleSpellNotRecognized;
            _battleController.OnSpellCastRejected   -= HandleSpellCastRejected;
            _battleController.OnBattleStateChanged  -= HandleBattleStateChanged;
            _battleController.OnSpellPhaseCancelled -= HandleSpellPhaseCancelled;
        }
    }
}
