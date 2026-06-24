using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Axiom.Battle
{
    /// <summary>
    /// Unity lifecycle and input wrapper for acknowledgment-gated battle messages.
    /// </summary>
    public class StatusMessageUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TMP text component that displays battle narration.")]
        private TMP_Text _text;

        [SerializeField] private GameObject _messageRoot;
        [SerializeField] private GameObject _backgroundRoot;
        [SerializeField] private Button _continueButton;
        [SerializeField] private float _charactersPerSecond = 30f;

        private StatusMessageQueue _queue;
        private bool _isShowingSpellPrompt;
        private string _spellPromptText = string.Empty;

        public event Action<bool> BusyStateChanged;
        public bool IsBusy => _queue != null && _queue.IsBusy;

        private void Awake()
        {
            if (_messageRoot == null)
                _messageRoot = gameObject;

            _queue = new StatusMessageQueue(_charactersPerSecond);
            _queue.BusyStateChanged += HandleBusyStateChanged;
            RefreshDisplay();
        }

        private void OnEnable() => _continueButton.onClick.AddListener(Continue);

        private void OnDisable() => _continueButton.onClick.RemoveListener(Continue);

        private void Update()
        {
            _queue.Update(Time.deltaTime);
            _text.text = _isShowingSpellPrompt ? _spellPromptText : _queue.VisibleText;
        }

        private void OnDestroy() => _queue.BusyStateChanged -= HandleBusyStateChanged;

        public void Post(string message) => _queue.Post(message);

        public void Continue()
        {
            if (_isShowingSpellPrompt)
                return;

            _queue.Continue();
        }

        public void ShowSpellPrompt(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Spell prompt text cannot be empty.", nameof(message));

            _spellPromptText = message;
            _isShowingSpellPrompt = true;
            _text.text = _spellPromptText;
            RefreshDisplay();
        }

        public void ClearSpellPrompt()
        {
            _spellPromptText = string.Empty;
            _isShowingSpellPrompt = false;
            _text.text = _queue.VisibleText;
            RefreshDisplay();
        }

        private void HandleBusyStateChanged(bool isBusy)
        {
            RefreshDisplay();

            BusyStateChanged?.Invoke(isBusy);
        }

        private void RefreshDisplay()
        {
            bool showMessageBox = IsBusy || _isShowingSpellPrompt;

            if (_backgroundRoot != null)
                _backgroundRoot.SetActive(showMessageBox);

            if (_messageRoot != null && _messageRoot.activeSelf != showMessageBox)
                _messageRoot.SetActive(showMessageBox);

            RefreshContinueButton();
            SelectContinueButtonIfVisible();
        }

        private void RefreshContinueButton()
        {
            bool showContinue = IsBusy && !_isShowingSpellPrompt;
            _continueButton.gameObject.SetActive(showContinue);
            _continueButton.interactable = showContinue;
        }

        private void SelectContinueButtonIfVisible()
        {
            if (!IsBusy || _isShowingSpellPrompt || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
        }
    }
}
