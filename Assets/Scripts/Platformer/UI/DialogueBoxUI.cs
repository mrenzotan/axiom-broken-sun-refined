using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// MonoBehaviour that displays dialogue one line at a time in the platformer scene.
    /// Shows speaker name, portrait placeholder, and current dialogue line.
    /// Supports advancing via button press and skipping/fast-forwarding via held input.
    ///
    /// Lifecycle: wired by a CutsceneController or DialogueTriggerZone.
    /// Updates game state: not a state owner itself.
    /// </summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _speakerNameText;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _dialogueLineText;
        [SerializeField] private Button _advanceButton;

        private DialogueData _currentDialogue;
        private int _currentLineIndex;
        private bool _isDisplaying;

        public bool IsDisplaying => _isDisplaying;

        /// <summary>Fired when the player advances the dialogue by one line.</summary>
        public event System.Action OnLineAdvanced;

        /// <summary>Fired when all dialogue lines have been displayed and dismissed.</summary>
        public event System.Action OnDialogueDismissed;

        private void OnEnable()
        {
            if (_advanceButton != null)
                _advanceButton.onClick.AddListener(OnAdvanceButtonClicked);
        }

        private void OnDisable()
        {
            if (_advanceButton != null)
                _advanceButton.onClick.RemoveListener(OnAdvanceButtonClicked);
        }

        /// <summary>
        /// Displays a dialogue sequence one line at a time. Call this when a dialogue step is reached.
        /// </summary>
        public void ShowDialogue(DialogueData dialogueData)
        {
            Debug.Log("[DialogueBoxUI] ShowDialogue called");
            if (dialogueData == null) 
            {
                Debug.LogError("[DialogueBoxUI] DialogueData is null!");
                return;
            }

            _currentDialogue = dialogueData;
            _currentLineIndex = 0;
            _isDisplaying = true;

            Debug.Log($"[DialogueBoxUI] Panel: {_panel}, activating...");
            if (_panel != null) 
            {
                _panel.SetActive(true);
                Debug.Log("[DialogueBoxUI] Panel activated");
            }
            else
            {
                Debug.LogError("[DialogueBoxUI] Panel reference is null!");
            }

            DisplayCurrentLine();
        }

        /// <summary>
        /// Hides the dialogue box and clears state.
        /// </summary>
        public void Hide()
        {
            _isDisplaying = false;
            if (_panel != null) _panel.SetActive(false);
            _currentDialogue = null;
            _currentLineIndex = 0;
        }

        private void OnAdvanceButtonClicked()
        {
            if (!_isDisplaying) return;

            _currentLineIndex++;

            if (_currentLineIndex >= _currentDialogue.LineCount)
            {
                // All lines displayed — dismiss.
                Hide();
                OnDialogueDismissed?.Invoke();
            }
            else
            {
                // Display next line.
                DisplayCurrentLine();
                OnLineAdvanced?.Invoke();
            }
        }

        private void DisplayCurrentLine()
        {
            if (_currentDialogue == null || _currentLineIndex >= _currentDialogue.LineCount) return;

            if (_speakerNameText != null)
                _speakerNameText.text = _currentDialogue.speakerName;

            if (_portraitImage != null)
                _portraitImage.sprite = _currentDialogue.portraitSprite;

            if (_dialogueLineText != null)
                _dialogueLineText.text = _currentDialogue.dialogueLines[_currentLineIndex];
        }
    }
}
