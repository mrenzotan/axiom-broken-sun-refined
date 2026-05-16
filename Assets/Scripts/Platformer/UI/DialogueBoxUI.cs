using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Axiom.Data;
using Axiom.Core;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// MonoBehaviour that displays dialogue one line at a time in turn-based conversation style.
    /// The protagonist and NPC take turns speaking — each line is shown individually with typewriter effect.
    /// Player advances via spacebar, enter key, or mouse click (skips typewriter if still animating).
    ///
    /// Portraits change based on the current speaker's name.
    /// Wired by CutsceneController or DialogueTriggerZone.
    /// </summary>
    public class DialogueBoxUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _speakerNameText;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _dialogueLineText;
        [SerializeField] private TextMeshProUGUI _continuePromptText;

        [SerializeField]
        [Tooltip("Characters revealed per second in typewriter effect. Default: 40.")]
        [Min(1f)]
        private float _charsPerSecond = 40f;

        private DialogueData _currentDialogue;
        private System.Collections.Generic.List<DialogueData.ParsedLine> _activeLines;
        private int _currentLineIndex;
        private bool _isDisplaying;
        private TypewriterEffect _typewriter;
        private bool _typewriterStarted;

        public bool IsDisplaying => _isDisplaying;

        /// <summary>Fired when the player advances the dialogue by one line.</summary>
        public event System.Action OnLineAdvanced;

        /// <summary>Fired when all dialogue lines have been displayed and dismissed.</summary>
        public event System.Action OnDialogueDismissed;

        private void Update()
        {
            if (!_isDisplaying) return;

            // Update typewriter effect if running
            if (_typewriter != null && _typewriterStarted && !_typewriter.IsComplete)
            {
                _typewriter.Update(Time.deltaTime);
                if (_dialogueLineText != null)
                    _dialogueLineText.text = _typewriter.VisibleText;
            }

            // Check for advance/skip input: spacebar, enter, or mouse click
            bool inputPressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    inputPressed = true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                inputPressed = true;
            }

            if (inputPressed)
            {
                // If typewriter is still running, skip it to the end
                if (_typewriter != null && !_typewriter.IsComplete)
                {
                    _typewriter.SkipToEnd();
                    if (_dialogueLineText != null)
                        _dialogueLineText.text = _typewriter.VisibleText;
                }
                else if (_typewriter != null && _typewriter.IsComplete)
                {
                    // Typewriter is done, advance to next line
                    AdvanceLine();
                }
            }
        }

        /// <summary>
        /// Displays a dialogue sequence one line at a time in turn-based conversation format.
        /// </summary>
        public void ShowDialogue(DialogueData dialogueData)
        {
            if (dialogueData == null)
            {
                Debug.LogError("[DialogueBoxUI] DialogueData is null!");
                return;
            }

            _currentDialogue = dialogueData;
            _activeLines = _currentDialogue.GetParsedLines();
            _currentLineIndex = 0;
            _isDisplaying = true;
            _typewriter = new TypewriterEffect();
            _typewriterStarted = false;

            if (_panel != null)
                _panel.SetActive(true);

            DisplayCurrentLine();
        }

        /// <summary>
        /// Hides the dialogue box and clears state.
        /// </summary>
        public void Hide()
        {
            _isDisplaying = false;
            _typewriterStarted = false;
            if (_panel != null) _panel.SetActive(false);
            _currentDialogue = null;
            _activeLines = null;
            _currentLineIndex = 0;
            _typewriter = null;
        }

        private void AdvanceLine()
        {
            _currentLineIndex++;

            if (_activeLines == null || _currentLineIndex >= _activeLines.Count)
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
            if (_currentDialogue == null || _activeLines == null || _currentLineIndex >= _activeLines.Count)
                return;

            DialogueData.ParsedLine line = _activeLines[_currentLineIndex];
            string speakerName = line.speakerName;
            string lineText = line.lineText;

            // Update speaker name
            if (_speakerNameText != null)
                _speakerNameText.text = speakerName;

            // Update portrait based on speaker name
            if (_portraitImage != null)
            {
                Sprite portraitSprite = _currentDialogue.GetPortraitForSpeaker(speakerName);
                if (portraitSprite != null)
                {
                    _portraitImage.sprite = portraitSprite;
                    _portraitImage.gameObject.SetActive(true);
                }
                else
                {
                    _portraitImage.gameObject.SetActive(false);
                }
            }

            // Start typewriter effect
            if (_typewriter != null)
            {
                _typewriter.Start(lineText, _charsPerSecond);
                _typewriterStarted = true;
            }
            else
            {
                // Fallback if typewriter not initialized
                if (_dialogueLineText != null)
                    _dialogueLineText.text = lineText;
            }

            // Show continue prompt
            if (_continuePromptText != null)
                _continuePromptText.text = "[SPACE/CLICK to continue]";
        }
    }
}
