using System;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Plain C# sequencer that executes a CutsceneSequence step-by-step.
    /// Owns state (current step index, skip/fast-forward flags, cleanup).
    /// Emits events so MonoBehaviours (DialogueBoxUI, camera, animations) can react.
    ///
    /// No MonoBehaviour. No Unity lifecycle. All methods are reentrant.
    /// </summary>
    public class CutsceneRunner
    {
        private CutsceneSequence _sequence;
        private int _currentStepIndex = -1;
        private bool _isRunning;
        private bool _skipRequested;
        private bool _fastForwardActive;

        /// <summary>Fired when a dialogue step is reached. Passes the DialogueData to display.</summary>
        public event Action<DialogueData> OnDialogueStep;

        /// <summary>Fired when a camera move step is reached. Passes target position and rotation.</summary>
        public event Action<Vector3, Vector3, float> OnCameraMoveStep;

        /// <summary>Fired when an animation step is reached. Passes animator parameter name.</summary>
        public event Action<string> OnAnimationStep;

        /// <summary>Fired when a spell unlock step is reached. Passes the SpellData to unlock.</summary>
        public event Action<SpellData> OnSpellUnlockStep;

        /// <summary>Fired when a custom scene event step is reached. Passes event name.</summary>
        public event Action<string> OnSceneEventStep;

        /// <summary>Fired when the sequence completes or is skipped.</summary>
        public event Action OnSequenceEnd;

        /// <summary>Returns true if a sequence is currently running.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Starts a new cutscene sequence. Returns false if a sequence is already running.
        /// </summary>
        public bool Start(CutsceneSequence sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (_isRunning) return false;

            _sequence = sequence;
            _currentStepIndex = -1;
            _skipRequested = false;
            _fastForwardActive = false;
            _isRunning = true;

            return true;
        }

        /// <summary>
        /// Advances to the next step. Returns false if the sequence is complete.
        /// Emits the appropriate event for the step type (dialogue, camera, animation, etc.).
        /// </summary>
        public bool AdvanceStep()
        {
            if (!_isRunning) return false;
            if (_sequence == null) return false;

            _currentStepIndex++;

            // Check if we've reached the end.
            if (_currentStepIndex >= _sequence.StepCount)
            {
                End();
                return false;
            }

            CutsceneStep step = _sequence.steps[_currentStepIndex];
            ExecuteStep(step);
            return true;
        }

        /// <summary>
        /// Requests immediate skip. The current step will complete, but no further
        /// steps will execute — the sequence will end after the next AdvanceStep call.
        /// </summary>
        public void RequestSkip()
        {
            _skipRequested = true;
        }

        /// <summary>
        /// Sets fast-forward mode, which may affect step timing (e.g., dialogue lines
        /// advance faster or camera lerps complete instantly).
        /// </summary>
        public void SetFastForward(bool active)
        {
            _fastForwardActive = active;
        }

        /// <summary>
        /// Returns true if fast-forward mode is active.
        /// </summary>
        public bool IsFastForwarding => _fastForwardActive;

        /// <summary>
        /// Returns true if skip was requested.
        /// </summary>
        public bool IsSkipRequested => _skipRequested;

        private void ExecuteStep(CutsceneStep step)
        {
            if (step == null) return;

            switch (step.stepType)
            {
                case CutsceneStepType.Dialogue:
                    if (step.dialogueData != null)
                        OnDialogueStep?.Invoke(step.dialogueData);
                    break;

                case CutsceneStepType.CameraMove:
                    OnCameraMoveStep?.Invoke(step.cameraTargetPosition, step.cameraTargetRotation, step.cameraDuration);
                    break;

                case CutsceneStepType.PlayAnimation:
                    OnAnimationStep?.Invoke(step.animationParameter);
                    break;

                case CutsceneStepType.WaitSeconds:
                    // Wait steps are handled by the caller (MonoBehaviour) via timing.
                    break;

                case CutsceneStepType.UnlockSpell:
                    if (step.spellToUnlock != null)
                        OnSpellUnlockStep?.Invoke(step.spellToUnlock);
                    break;

                case CutsceneStepType.SceneEvent:
                    OnSceneEventStep?.Invoke(step.sceneEventName);
                    break;
            }
        }

        /// <summary>
        /// Ends the sequence and fires OnSequenceEnd.
        /// </summary>
        private void End()
        {
            _isRunning = false;
            OnSequenceEnd?.Invoke();
        }

        /// <summary>
        /// Immediately terminates the sequence (e.g., on skip). Fires OnSequenceEnd.
        /// </summary>
        public void Abort()
        {
            if (!_isRunning) return;
            End();
        }
    }
}
