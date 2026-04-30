using System.Collections;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer.UI;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour that orchestrates cutscene playback. Owns a CutsceneRunner,
    /// wires it to DialogueBoxUI and scene systems, blocks player input during playback,
    /// and handles step timing (waits, camera lerps, etc.).
    ///
    /// Attach to any GameObject in the Platformer scene (e.g., the Canvas or a dedicated
    /// cutscene manager). Finds DialogueBoxUI in the scene at runtime.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        private CutsceneRunner _runner = new CutsceneRunner();
        private DialogueBoxUI _dialogueBoxUI;
        private PlayerController _playerController;
        private Animator _playerAnimator;
        private Coroutine _cutsceneCoroutine;
        private bool _inCutscene;

        public bool IsInCutscene => _inCutscene;

        private void OnEnable()
        {
            _playerController = FindAnyObjectByType<PlayerController>();

            _runner.OnDialogueStep += HandleDialogueStep;
            _runner.OnSpellUnlockStep += HandleSpellUnlockStep;
            _runner.OnSequenceEnd += HandleSequenceEnd;
        }

        private void OnDisable()
        {
            _runner.OnDialogueStep -= HandleDialogueStep;
            _runner.OnSpellUnlockStep -= HandleSpellUnlockStep;
            _runner.OnSequenceEnd -= HandleSequenceEnd;
        }

        /// <summary>
        /// Starts a cutscene sequence. No-op if a cutscene is already playing.
        /// </summary>
        public void StartCutscene(CutsceneSequence sequence)
        {
            Debug.Log("[CutsceneController] StartCutscene called");
            if (sequence == null) 
            {
                Debug.LogError("[CutsceneController] Sequence is null!");
                return;
            }
            if (_inCutscene) 
            {
                Debug.LogWarning("[CutsceneController] Already in cutscene, ignoring.");
                return;
            }

            // Find DialogueBoxUI on-demand (not in OnEnable).
            // Search inactive objects too, since DialogueBox may be disabled at start.
            _dialogueBoxUI = FindAnyObjectByType<DialogueBoxUI>(FindObjectsInactive.Include);
            if (_dialogueBoxUI == null)
            {
                Debug.LogError("[CutsceneController] DialogueBoxUI not found in scene!");
                return;
            }

            Debug.Log("[CutsceneController] Starting runner...");
            if (!_runner.Start(sequence))
            {
                Debug.LogError("[CutsceneController] Failed to start cutscene.");
                return;
            }

            Debug.Log("[CutsceneController] Runner started, blocking player control...");
            _inCutscene = true;
            BlockPlayerControl();

            // Start the main cutscene loop.
            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(PlayCutsceneSequence());
            Debug.Log("[CutsceneController] Cutscene coroutine started");
        }

        /// <summary>
        /// Immediately skips the current cutscene.
        /// </summary>
        public void SkipCutscene()
        {
            if (!_inCutscene) return;
            _runner.RequestSkip();
        }

        private IEnumerator PlayCutsceneSequence()
        {
            // Advance to the first step.
            if (!_runner.AdvanceStep())
            {
                // Sequence was empty.
                yield break;
            }

            // Wait for the sequence to complete. Event handlers (dialogue, waits, etc.)
            // will call AdvanceStep() when their step is done. Do NOT advance here.
            while (_runner.IsRunning && !_runner.IsSkipRequested)
            {
                yield return null;
            }

            // If skip was requested, abort the sequence.
            if (_runner.IsSkipRequested)
            {
                _runner.Abort();
            }
        }

        private void HandleDialogueStep(DialogueData dialogueData)
        {
            Debug.Log("[CutsceneController] HandleDialogueStep called");
            if (_dialogueBoxUI != null)
            {
                Debug.Log("[CutsceneController] DialogueBoxUI found, showing dialogue");
                _dialogueBoxUI.ShowDialogue(dialogueData);
                // OnDialogueDismissed will trigger the next AdvanceStep call.
                _dialogueBoxUI.OnDialogueDismissed += AdvanceOnDialogueDismissed;
            }
            else
            {
                Debug.LogError("[CutsceneController] DialogueBoxUI not found!");
            }
        }

        private void AdvanceOnDialogueDismissed()
        {
            if (_dialogueBoxUI != null)
                _dialogueBoxUI.OnDialogueDismissed -= AdvanceOnDialogueDismissed;

            if (_runner.IsRunning)
                _runner.AdvanceStep();
        }

        private void HandleSpellUnlockStep(SpellData spellData)
        {
            if (spellData == null) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpellUnlockService.Unlock(spellData);
                Debug.Log($"[CutsceneController] Spell unlocked: {spellData.spellName}");
            }

            // Spell unlock is instant, so advance to next step immediately.
            if (_runner.IsRunning)
                _runner.AdvanceStep();
        }

        private void HandleSequenceEnd()
        {
            _inCutscene = false;
            RestorePlayerControl();

            if (_dialogueBoxUI != null)
                _dialogueBoxUI.Hide();

            if (_cutsceneCoroutine != null)
            {
                StopCoroutine(_cutsceneCoroutine);
                _cutsceneCoroutine = null;
            }
        }

        private void BlockPlayerControl()
        {
            if (_playerController != null)
            {
                _playerController.enabled = false;
                Debug.Log("[CutsceneController] Player controller disabled");
                
                // Zero out velocity so the player stops immediately.
                Rigidbody2D playerRb = _playerController.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero;
                    Debug.Log("[CutsceneController] Player velocity zeroed");
                }

                // Disable animator completely to freeze animation.
                _playerAnimator = _playerController.GetComponent<Animator>();
                if (_playerAnimator != null)
                {
                    _playerAnimator.enabled = false;
                    Debug.Log("[CutsceneController] Player animator disabled (animation frozen)");
                }
            }
        }

        private void RestorePlayerControl()
        {
            if (_playerController != null)
            {
                _playerController.enabled = true;
                Debug.Log("[CutsceneController] Player controller re-enabled");
            }

            if (_playerAnimator != null)
            {
                _playerAnimator.enabled = true;
                Debug.Log("[CutsceneController] Player animator re-enabled");
            }
        }
    }
}
