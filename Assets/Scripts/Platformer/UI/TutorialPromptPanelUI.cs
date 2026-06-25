using System;
using Axiom.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Simple prompt panel anchored to the platformer HUD. Shown when the player enters a
    /// TutorialPromptTrigger zone; hidden when they leave it.
    ///
    /// Pause-on-prompt (DEV-133): ShowAndPause freezes the game (Time.timeScale = 0), shows a
    /// bottom-center Continue button, and resumes only when the player clicks Continue or
    /// presses Enter (Enter resolves to the focused Continue button via the UI/Submit action).
    /// </summary>
    public class TutorialPromptPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField]
        [Tooltip("Bottom-center button shown only in pause-on-prompt mode. Required for ShowAndPause.")]
        private Button _continueButton;

        private Action _onContinue;
        private bool _pausedByThisPanel;

        private string _pendingBody;
        private Action _pendingOnContinue;
        private bool _waitingForSceneReady;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(true);
        }

        /// <summary>
        /// Shows the prompt, freezes the game, and shows the Continue button. The game stays
        /// paused until the player presses Continue, at which point timeScale is restored,
        /// the panel hides, and <paramref name="onContinue"/> is invoked.
        /// </summary>
        public void ShowAndPause(string body, Action onContinue)
        {
            if (_continueButton == null)
            {
                Debug.LogError($"{name}: ShowAndPause requested but no Continue button is wired. " +
                               "Falling back to non-paused Show.", this);
                Show(body);
                onContinue?.Invoke();
                return;
            }

            // If a scene transition is still running (e.g. the white-flash fade-in after a load),
            // freezing now would set Time.timeScale = 0, which stalls the fade-in coroutine (it
            // advances on Time.deltaTime) — leaving the overlay opaque forever. Defer the show +
            // freeze until the scene is fully presented (OnSceneReady fires after fade-in).
            if (!_waitingForSceneReady && GameManager.Instance != null &&
                GameManager.Instance.SceneTransition != null &&
                GameManager.Instance.SceneTransition.IsTransitioning)
            {
                _pendingBody = body;
                _pendingOnContinue = onContinue;
                _waitingForSceneReady = true;
                GameManager.Instance.OnSceneReady += HandleSceneReadyThenPause;
                return;
            }

            DoShowAndPause(body, onContinue);
        }

        private void HandleSceneReadyThenPause()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnSceneReady -= HandleSceneReadyThenPause;
            _waitingForSceneReady = false;
            string body = _pendingBody;
            Action onContinue = _pendingOnContinue;
            _pendingBody = null;
            _pendingOnContinue = null;
            DoShowAndPause(body, onContinue);
        }

        private void DoShowAndPause(string body, Action onContinue)
        {
            _onContinue = onContinue;
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);

            _continueButton.gameObject.SetActive(true);
            _continueButton.onClick.RemoveListener(HandleContinue);
            _continueButton.onClick.AddListener(HandleContinue);

            Time.timeScale = 0f;
            _pausedByThisPanel = true;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }

        public void Hide()
        {
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_root != null) _root.SetActive(false);
        }

        private void HandleContinue()
        {
            _continueButton.onClick.RemoveListener(HandleContinue);
            Action callback = _onContinue;
            _onContinue = null;
            ResumeFromPause();
            Hide();
            callback?.Invoke();
        }

        private void ResumeFromPause()
        {
            if (!_pausedByThisPanel) return;
            _pausedByThisPanel = false;
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.SuppressPauseToggle = false;
        }

        private void OnDisable()
        {
            // Safety: never leave the game frozen if this panel is torn down mid-pause
            // (e.g. a scene transition while a prompt is up).
            if (_pausedByThisPanel) ResumeFromPause();

            // Drop any pending scene-ready subscription so we don't fire into a disabled panel.
            if (_waitingForSceneReady)
            {
                if (GameManager.Instance != null) GameManager.Instance.OnSceneReady -= HandleSceneReadyThenPause;
                _waitingForSceneReady = false;
                _pendingBody = null;
                _pendingOnContinue = null;
            }
        }
    }
}
