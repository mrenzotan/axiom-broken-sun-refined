using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// Modal confirmation dialog shown before <see cref="GameManager.StartNewGame"/> runs while
    /// a save file exists. MonoBehaviour handles lifecycle only: activates/deactivates the dialog
    /// root, wires Yes/No buttons through <see cref="ConfirmDialogController"/>, and restores
    /// EventSystem focus to the caller-supplied focus target on dismiss.
    /// </summary>
    public class ConfirmNewGameDialogUI : MonoBehaviour, ICancelHandler
    {
        [SerializeField]
        [Tooltip("Yes, start new game — confirms destruction of existing save.")]
        private Button _yesButton;

        [SerializeField]
        [Tooltip("No, go back — dismisses the dialog without touching state.")]
        private Button _noButton;

        [SerializeField]
        [Tooltip("Optional — GameObject to receive EventSystem focus when the dialog hides. " +
                 "Typically the New Game button in the main menu.")]
        private GameObject _focusOnHide;

        private ConfirmDialogController _controller;
        private Action _pendingConfirm;

        /// <summary>True while the dialog is active on screen.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// Opens the dialog. <paramref name="onConfirm"/> fires when the player clicks Yes;
        /// No / Esc / B-button dismiss silently. The dialog hides itself after either path.
        /// </summary>
        public void Show(Action onConfirm)
        {
            if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));

            _pendingConfirm = onConfirm;
            _controller = new ConfirmDialogController(
                onConfirm: HandleConfirm,
                onCancel:  HandleCancel);

            gameObject.SetActive(true);

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && _noButton != null)
                eventSystem.SetSelectedGameObject(_noButton.gameObject);
        }

        /// <summary>Explicit hide used by integration wiring. Does not invoke either callback.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && _focusOnHide != null)
                eventSystem.SetSelectedGameObject(_focusOnHide);
        }

        private void Awake()
        {
            gameObject.SetActive(false);

            if (_yesButton != null) _yesButton.onClick.AddListener(OnYesButton);
            if (_noButton  != null) _noButton .onClick.AddListener(OnNoButton);
        }

        private void OnDestroy()
        {
            _yesButton?.onClick.RemoveAllListeners();
            _noButton ?.onClick.RemoveAllListeners();
        }

        private void OnYesButton() => _controller?.OnYesClicked();
        private void OnNoButton()  => _controller?.OnNoClicked();

        /// <summary>
        /// Invoked by the UI Input Module when the player presses Cancel (Esc / gamepad B) while a
        /// descendant of this GameObject is focused. ExecuteEvents.ExecuteHierarchy bubbles the event
        /// up from the currently-selected button, so this fires whether Yes or No has focus.
        /// </summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (_controller == null) return;
            _controller.OnNoClicked();
        }

        /// <summary>
        /// Direct New Input System polling for Esc / gamepad B. Serves as a reliable
        /// cancel path independent of EventSystem selection state and UI action-map
        /// enable timing — ICancelHandler silently no-ops when nothing is selected
        /// or when the UI action map isn't active at dialog-open time.
        /// </summary>
        private void Update()
        {
            if (_controller == null) return;

            bool cancelPressed =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current  != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (cancelPressed)
                _controller.OnNoClicked();
        }

        private void HandleConfirm()
        {
            Action confirmed = _pendingConfirm;
            _pendingConfirm = null;
            _controller = null;

            Hide();
            confirmed?.Invoke();
        }

        private void HandleCancel()
        {
            _pendingConfirm = null;
            _controller = null;
            Hide();
        }
    }
}
