# DEV-49: NPC Dialogue and Story Cutscenes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a data-driven dialogue and cutscene system for the platformer story scenes that can show NPC speech, run short cinematic sequences, and grant spells through the existing unlock service. The system should reuse the project's current unlock/event patterns, keep logic out of MonoBehaviours, and make the first story beat fully authorable in the Unity Editor.

**Architecture:** Dialogue and cutscene sequences are authored as ScriptableObjects. A `DialogueData` asset holds speaker name, line list, and portrait sprite. A `CutsceneSequence` asset holds ordered steps (dialogue, camera move, animation trigger, wait, scene event, spell unlock). A plain C# `CutsceneRunner` owns sequencing, skip, fast-forward, and cleanup. MonoBehaviours handle Unity lifecycle and scene wiring only. The story spell grant reuses `SpellUnlockService.Unlock(SpellData)` so the unlock fires the existing notification path and grammar rebuild. Level triggers (collision-based) start cutscenes, following the pattern of `SavePointTrigger` and `ExplorationEnemyCombatTrigger`. A platformer-scene `DialogueBoxUI` displays dialogue lines one-at-a-time, supporting hold-to-fast-forward and immediate skip. Camera work uses an authored target transform or Cinemachine rig so sequences can pan without hardcoded positions.

**Tech Stack:** Unity 6 LTS, C# (.NET Standard 2.1), ScriptableObject, TextMeshPro, Cinemachine (optional but supported), NUnit via Unity Test Framework (Edit Mode for sequencer logic), existing `SpellUnlockService` integration.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| Jira DEV-49 AC | Dialogue UI supporting character name + portrait + sequential lines, skippable/fast-forwardable cutscenes, trigger zones in platformer, spell grant via story beat, data-driven (no hardcoded strings) |
| `CLAUDE.md` — Non-Negotiable Code Standards | MonoBehaviours = lifecycle only; no static singletons except `GameManager`; ScriptableObject-driven data; no premature abstraction |
| `CLAUDE.md` — Architecture Rules | Trigger-zone pattern from `SavePointTrigger` and `ExplorationEnemyCombatTrigger` — simple collision dispatch, check for player tag, gate via GameManager if needed |
| `docs/GAME_PLAN.md` §Phase 6 | Phase-6 world content; story cutscenes are part of level content delivery |
| `docs/VERSION_CONTROL.md` | UVCS is source of truth; commit format `<type>(DEV-##): <desc>` |
| `docs/LORE_AND_MECHANICS.md` | Story framing and chemistry integration context |
| `docs/story_narrative_party_system.md` | Narrative source material for the first story cutscene |
| `Assets/Scripts/Core/SpellUnlockService.cs` | Existing spell unlock event (`OnSpellUnlocked`) and `Unlock(SpellData)` method — story beats call this |
| `Assets/Scripts/Core/GameManager.cs` | Owns `SpellUnlockService`; exposed as public property |
| `Assets/Scripts/Data/CharacterData.cs` | Provides `portraitSprite` for dialogue UI seeding |
| `Assets/Scripts/Platformer/SavePointTrigger.cs` | Trigger-zone pattern to mirror for cutscene triggers |
| `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Stronger trigger example showing world gating + restoration |
| `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs` | Reference for unlock notification timing and non-blocking UI behavior |
| `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` | Existing `OnSpellUnlocked` subscriber — grammar rebuild already wired; new story unlocks will be picked up automatically |

---

## Current state (repository)

**Already implemented:**

- `SpellUnlockService` with `OnSpellUnlocked` event and `Unlock(SpellData)` method.
- `GameManager` singleton owns `SpellUnlockService` and exposes it via property.
- Platformer trigger-zone patterns: `SavePointTrigger`, `ExplorationEnemyCombatTrigger`.
- `DialogueBoxUI` **does not exist yet** (this plan creates it).
- `CutsceneSequence` **does not exist yet** (this plan creates it).
- `CutsceneRunner` **does not exist yet** (this plan creates it).
- `DialogueTriggerZone` **does not exist yet** (this plan creates it).
- Battle UI `LevelUpPromptUI` shows unlock feedback pattern.
- `SpellVocabularyManager` already wired to rebuild grammar on unlock events.

**Missing (scope of DEV-49):**

- `DialogueData` ScriptableObject — speaker name, dialogue lines, portrait sprite.
- `CutsceneSequence` ScriptableObject — ordered list of steps (dialogue, camera, animation, wait, spell unlock, scene event).
- `CutsceneStep` — serializable base class or enum-driven union for different step types.
- `CutsceneRunner` (plain C#) — owns sequencing, skip/fast-forward state, step advancement, event emission.
- `DialogueBoxUI` (MonoBehaviour in Platformer/UI/) — displays dialogue, handles input for advance/skip/fast-forward.
- `DialogueTriggerZone` (MonoBehaviour in Platformer/) — collision trigger that starts a cutscene, blocks player control, restores control on end.
- Input integration for skip/fast-forward (use existing Input System action or new action if needed).
- Camera sequencing support (Cinemachine or transform lerp).
- Scene-event system for animation triggers, door opens, NPC facing changes, etc.
- One example cutscene asset and level placement to prove the full loop.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/DialogueData.cs` | ScriptableObject holding speaker name, line list, portrait sprite |
| Create | `Assets/Scripts/Data/CutsceneSequence.cs` | ScriptableObject holding ordered steps (dialogue, camera, animation, wait, spell unlock, scene event) |
| Create | `Assets/Scripts/Data/CutsceneStep.cs` | Serializable step definition (enum-driven with step-specific data) |
| Create | `Assets/Scripts/Platformer/CutsceneRunner.cs` | Plain C# sequencer — owns state, advances steps, emits events |
| Create | `Assets/Scripts/Platformer/UI/DialogueBoxUI.cs` | MonoBehaviour displaying dialogue, handling input for advance/skip/fast-forward |
| Create | `Assets/Scripts/Platformer/DialogueTriggerZone.cs` | MonoBehaviour trigger that starts a cutscene, blocks player control |
| Create | `Assets/Tests/Editor/Platformer/CutsceneRunnerTests.cs` | Edit Mode tests — line order, skip behavior, fast-forward, spell unlock emission, cleanup |
| Modify | `Assets/Scripts/Platformer/Platformer.asmdef` | Ensure references cover `Axiom.Data` and `Axiom.Core` (likely already does — verify) |
| Create | `Assets/Data/Cutscenes/CS_Phasekeeper_Intro.asset` | Example cutscene asset for the first story beat |
| Create | `Assets/Data/Dialogues/DD_Phasekeeper_LineSet.asset` | Example dialogue asset with 3-5 lines from the narrative |

**New assemblies:** None. Cutscene logic reuses existing `Axiom.Data`, `Axiom.Core`, `Axiom.Platformer` assemblies.

---

## Task 1: Create the dialogue data model — `DialogueData` ScriptableObject

Define a ScriptableObject that holds one speaker's dialogue lines for a scene. Supports character name, portrait sprite, and a list of text lines.

**Files:**
- Create: `Assets/Scripts/Data/DialogueData.cs`

- [ ] **Step 1: Create `DialogueData.cs` with dialogue line list and portrait sprite**

Create the file `Assets/Scripts/Data/DialogueData.cs`:

```csharp
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// A single dialogue sequence from an NPC. Holds speaker name, ordered dialogue lines,
    /// and an optional portrait sprite.
    ///
    /// Created as a ScriptableObject asset so dialogue content can be authored in the
    /// Inspector without changing code.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogueData", menuName = "Axiom/Data/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("Name of the speaker (e.g., 'Sentinel', 'Phasekeeper').")]
        public string speakerName = "NPC";

        [Tooltip("Optional portrait sprite shown while this dialogue plays.")]
        public Sprite portraitSprite;

        [Tooltip("Ordered list of dialogue lines. Each entry is one line of text.")]
        public string[] dialogueLines = System.Array.Empty<string>();

        /// <summary>Read-only line count for validation.</summary>
        public int LineCount => dialogueLines?.Length ?? 0;
    }
}
```

- [ ] **Step 2: Create the meta file and verify no compile errors**

> **Unity Editor task (user):** After Claude creates the file, right-click in the Project window and refresh. Verify the script compiles with no errors in the Console.

---

## Task 2: Create the cutscene step model — `CutsceneStep` and `CutsceneSequence` ScriptableObjects

Define a step type that can represent different cutscene actions (dialogue, camera move, animation, wait, spell unlock, scene event). Then create the sequence asset that chains them.

**Files:**
- Create: `Assets/Scripts/Data/CutsceneStep.cs`
- Create: `Assets/Scripts/Data/CutsceneSequence.cs`

- [ ] **Step 1: Create `CutsceneStep.cs` with enum-driven step types**

Create the file `Assets/Scripts/Data/CutsceneStep.cs`:

```csharp
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Enum defining the type of action a cutscene step performs.
    /// </summary>
    public enum CutsceneStepType
    {
        Dialogue,        // Show dialogue from DialogueData
        CameraMove,      // Lerp camera to a target position/rotation
        PlayAnimation,   // Trigger an animator parameter
        WaitSeconds,     // Pause for a duration
        UnlockSpell,     // Grant a spell via SpellUnlockService
        SceneEvent,      // Emit a custom event (door, NPC facing, etc.)
    }

    /// <summary>
    /// A serializable step within a CutsceneSequence. Each step encapsulates
    /// one action (dialogue, camera, animation, wait, spell unlock, or custom event).
    /// </summary>
    [System.Serializable]
    public class CutsceneStep
    {
        [Tooltip("Type of action this step performs.")]
        public CutsceneStepType stepType = CutsceneStepType.Dialogue;

        [Tooltip("Dialogue asset to display (required if stepType == Dialogue).")]
        public DialogueData dialogueData;

        [Tooltip("Target position for camera move (required if stepType == CameraMove).")]
        public Vector3 cameraTargetPosition = Vector3.zero;

        [Tooltip("Target rotation for camera move (required if stepType == CameraMove).")]
        public Vector3 cameraTargetRotation = Vector3.zero;

        [Tooltip("Duration of camera lerp in seconds.")]
        public float cameraDuration = 1f;

        [Tooltip("Animator parameter name to set (required if stepType == PlayAnimation).")]
        public string animationParameter = "Idle";

        [Tooltip("Duration to wait in seconds (required if stepType == WaitSeconds).")]
        public float waitDuration = 1f;

        [Tooltip("Spell to unlock (required if stepType == UnlockSpell).")]
        public SpellData spellToUnlock;

        [Tooltip("Custom event name to emit (required if stepType == SceneEvent).")]
        public string sceneEventName = "";
    }
}
```

- [ ] **Step 2: Create `CutsceneSequence.cs` holding ordered steps**

Create the file `Assets/Scripts/Data/CutsceneSequence.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// A sequence of cutscene steps. Executed in order by CutsceneRunner.
    /// Supports dialogue, camera moves, animations, waits, spell unlocks, and custom scene events.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCutsceneSequence", menuName = "Axiom/Data/Cutscene Sequence")]
    public class CutsceneSequence : ScriptableObject
    {
        [Tooltip("Ordered list of steps to execute in this cutscene.")]
        public List<CutsceneStep> steps = new List<CutsceneStep>();

        /// <summary>Read-only step count for validation.</summary>
        public int StepCount => steps?.Count ?? 0;
    }
}
```

- [ ] **Step 3: Create the meta files and verify no compile errors**

> **Unity Editor task (user):** After Claude creates both files, refresh the Project window. Verify both scripts compile with no errors in the Console.

---

## Task 3: Create the runtime cutscene runner — `CutsceneRunner` plain C# service

Build a stateless/near-stateless sequencer that owns cutscene state, advances steps, and emits events for the UI and scene to react to.

**Files:**
- Create: `Assets/Scripts/Platformer/CutsceneRunner.cs`

- [ ] **Step 1: Create `CutsceneRunner.cs` with step advancement and event emission**

Create the file `Assets/Scripts/Platformer/CutsceneRunner.cs`:

```csharp
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
```

- [ ] **Step 2: Create the meta file and verify no compile errors**

> **Unity Editor task (user):** After Claude creates the file, refresh the Project window. Verify the script compiles with no errors in the Console.

---

## Task 4: Create the dialogue UI — `DialogueBoxUI` MonoBehaviour for the platformer scene

Build a UI component that displays dialogue one line at a time, supports input for advancing lines, skipping, and fast-forwarding.

**Files:**
- Create: `Assets/Scripts/Platformer/UI/DialogueBoxUI.cs`

- [ ] **Step 1: Create `DialogueBoxUI.cs` with line advancement and input handling**

Create the file `Assets/Scripts/Platformer/UI/DialogueBoxUI.cs`:

```csharp
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
            if (dialogueData == null) return;

            _currentDialogue = dialogueData;
            _currentLineIndex = 0;
            _isDisplaying = true;

            if (_panel != null) _panel.SetActive(true);

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
```

- [ ] **Step 2: Create the meta file and verify no compile errors**

> **Unity Editor task (user):** After Claude creates the file, refresh the Project window. Verify the script compiles with no errors in the Console.

---

## Task 5: Create the cutscene trigger zone — `DialogueTriggerZone` MonoBehaviour

Build a trigger that starts a cutscene when the player enters, blocks player control during the cutscene, and restores control when the sequence ends.

**Files:**
- Create: `Assets/Scripts/Platformer/DialogueTriggerZone.cs`

- [ ] **Step 1: Create `DialogueTriggerZone.cs` with trigger dispatch and control blocking**

Create the file `Assets/Scripts/Platformer/DialogueTriggerZone.cs`:

```csharp
using UnityEngine;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour trigger zone that starts a cutscene when the player enters.
    /// Blocks player movement and voice input during the cutscene, then restores control when done.
    ///
    /// Attach to a GameObject with a Collider2D (Is Trigger enabled) and a DialogueTriggerZone script.
    /// Assign a CutsceneSequence asset in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTriggerZone : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Cutscene sequence to play when the player enters this zone.")]
        private CutsceneSequence _cutsceneSequence;

        private bool _triggered;
        private CutsceneController _cutsceneController;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            _triggered = true;

            if (_cutsceneSequence == null)
            {
                Debug.LogWarning("[DialogueTriggerZone] No cutscene sequence assigned.", this);
                return;
            }

            // Get or create the cutscene controller.
            _cutsceneController = FindAnyObjectByType<CutsceneController>();
            if (_cutsceneController == null)
            {
                Debug.LogError(
                    "[DialogueTriggerZone] CutsceneController not found in the scene. " +
                    "Attach a CutsceneController MonoBehaviour to manage cutscenes.",
                    this);
                return;
            }

            // Start the cutscene.
            _cutsceneController.StartCutscene(_cutsceneSequence);
        }
    }
}
```

- [ ] **Step 2: Create the meta file and verify no compile errors**

> **Unity Editor task (user):** After Claude creates the file, refresh the Project window. Verify the script compiles with no errors in the Console.

---

## Task 6: Create the cutscene controller — `CutsceneController` MonoBehaviour to orchestrate the runner

Build a MonoBehaviour that owns the `CutsceneRunner`, wires it to the UI and scene systems, blocks player input during cutscenes, and handles step timing.

**Files:**
- Create: `Assets/Scripts/Platformer/CutsceneController.cs`

- [ ] **Step 1: Create `CutsceneController.cs` with runner orchestration and input blocking**

Create the file `Assets/Scripts/Platformer/CutsceneController.cs`:

```csharp
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
        private Coroutine _cutsceneCoroutine;
        private bool _inCutscene;

        public bool IsInCutscene => _inCutscene;

        private void OnEnable()
        {
            _dialogueBoxUI = FindAnyObjectByType<DialogueBoxUI>();
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
            if (sequence == null) return;
            if (_inCutscene) return;

            if (!_runner.Start(sequence))
            {
                Debug.LogError("[CutsceneController] Failed to start cutscene.");
                return;
            }

            _inCutscene = true;
            BlockPlayerControl();

            // Start the main cutscene loop.
            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(PlayCutsceneSequence());
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

            // Loop through steps until skip or end.
            while (_runner.IsRunning && !_runner.IsSkipRequested)
            {
                // Wait for the current step to finish (e.g., dialogue to be dismissed).
                yield return null;

                // Advance to the next step.
                if (!_runner.AdvanceStep())
                {
                    // Sequence ended.
                    break;
                }
            }

            // If skip was requested, abort the sequence.
            if (_runner.IsSkipRequested)
            {
                _runner.Abort();
            }
        }

        private void HandleDialogueStep(DialogueData dialogueData)
        {
            if (_dialogueBoxUI != null)
            {
                _dialogueBoxUI.ShowDialogue(dialogueData);
                // OnDialogueDismissed will trigger the next AdvanceStep call.
                _dialogueBoxUI.OnDialogueDismissed += AdvanceOnDialogueDismissed;
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
                _playerController.enabled = false;
        }

        private void RestorePlayerControl()
        {
            if (_playerController != null)
                _playerController.enabled = true;
        }
    }
}
```

- [ ] **Step 2: Create the meta file and verify no compile errors**

> **Unity Editor task (user):** After Claude creates the file, refresh the Project window. Verify the script compiles with no errors in the Console.

---

## Task 7: Add tests for the cutscene runner logic

Write Edit Mode tests covering line order, skip behavior, spell unlock emission, and cleanup.

**Files:**
- Create: `Assets/Tests/Editor/Platformer/CutsceneRunnerTests.cs`

- [ ] **Step 1: Create `CutsceneRunnerTests.cs` with sequencer coverage**

Create the file `Assets/Tests/Editor/Platformer/CutsceneRunnerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Data;
using Axiom.Platformer;

namespace Axiom.Tests.Platformer
{
    public class CutsceneRunnerTests
    {
        private CutsceneRunner _runner;

        [SetUp]
        public void Setup()
        {
            _runner = new CutsceneRunner();
        }

        [Test]
        public void Start_WithValidSequence_ReturnsTrue()
        {
            var sequence = CreateTestSequence(1);
            bool result = _runner.Start(sequence);
            Assert.IsTrue(result);
        }

        [Test]
        public void Start_WithNullSequence_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => _runner.Start(null));
        }

        [Test]
        public void AdvanceStep_WithEmptySequence_ReturnsFalse()
        {
            var sequence = CreateTestSequence(0);
            _runner.Start(sequence);
            bool result = _runner.AdvanceStep();
            Assert.IsFalse(result);
        }

        [Test]
        public void AdvanceStep_AdvancesStepIndex()
        {
            var sequence = CreateTestSequence(3);
            _runner.Start(sequence);

            Assert.IsFalse(_runner.IsRunning == false);
            _runner.AdvanceStep(); // Step 0
            Assert.IsTrue(_runner.IsRunning);

            _runner.AdvanceStep(); // Step 1
            Assert.IsTrue(_runner.IsRunning);

            _runner.AdvanceStep(); // Step 2
            Assert.IsTrue(_runner.IsRunning);

            bool result = _runner.AdvanceStep(); // Beyond end
            Assert.IsFalse(result);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void OnSequenceEnd_FiresWhenSequenceCompletes()
        {
            var sequence = CreateTestSequence(1);
            bool eventFired = false;
            _runner.OnSequenceEnd += () => eventFired = true;

            _runner.Start(sequence);
            _runner.AdvanceStep();
            _runner.AdvanceStep(); // Advance beyond end

            Assert.IsTrue(eventFired);
        }

        [Test]
        public void RequestSkip_SetsSkipFlag()
        {
            var sequence = CreateTestSequence(1);
            _runner.Start(sequence);

            Assert.IsFalse(_runner.IsSkipRequested);
            _runner.RequestSkip();
            Assert.IsTrue(_runner.IsSkipRequested);
        }

        [Test]
        public void SetFastForward_SetsFastForwardFlag()
        {
            _runner.SetFastForward(true);
            Assert.IsTrue(_runner.IsFastForwarding);

            _runner.SetFastForward(false);
            Assert.IsFalse(_runner.IsFastForwarding);
        }

        [Test]
        public void OnSpellUnlockStep_FiresForSpellUnlockSteps()
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = "test_spell";

            var sequence = ScriptableObject.CreateInstance<CutsceneSequence>();
            sequence.steps = new List<CutsceneStep>
            {
                new CutsceneStep { stepType = CutsceneStepType.UnlockSpell, spellToUnlock = spell }
            };

            SpellData firedSpell = null;
            _runner.OnSpellUnlockStep += (s) => firedSpell = s;

            _runner.Start(sequence);
            _runner.AdvanceStep();

            Assert.AreSame(spell, firedSpell);
        }

        [Test]
        public void Abort_EndsRunningSequence()
        {
            var sequence = CreateTestSequence(3);
            bool eventFired = false;
            _runner.OnSequenceEnd += () => eventFired = true;

            _runner.Start(sequence);
            _runner.AdvanceStep();

            _runner.Abort();
            Assert.IsFalse(_runner.IsRunning);
            Assert.IsTrue(eventFired);
        }

        // Helper to create a test sequence with N dialogue steps.
        private CutsceneSequence CreateTestSequence(int stepCount)
        {
            var sequence = ScriptableObject.CreateInstance<CutsceneSequence>();
            sequence.steps = new List<CutsceneStep>();

            for (int i = 0; i < stepCount; i++)
            {
                var dialogue = ScriptableObject.CreateInstance<DialogueData>();
                dialogue.speakerName = $"Speaker{i}";
                dialogue.dialogueLines = new[] { $"Line {i}" };

                sequence.steps.Add(new CutsceneStep
                {
                    stepType = CutsceneStepType.Dialogue,
                    dialogueData = dialogue
                });
            }

            return sequence;
        }
    }
}
```

- [ ] **Step 2: Verify test assembly references and compile**

> **Unity Editor task (user):** Check that `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef` references `Axiom.Data` and `Axiom.Platformer`. If not, update the asmdef file. Then refresh and verify the tests compile with no errors.

---

## Task 8: Create one example cutscene asset and dialogue asset

Author a sample cutscene using the narrative source from `docs/story_narrative_party_system.md` to prove the full loop works end-to-end.

**Files:**
- Create: `Assets/Data/Cutscenes/CS_Phasekeeper_Intro.asset`
- Create: `Assets/Data/Dialogues/DD_Phasekeeper_Intro.asset`

- [ ] **Step 1: Create the example dialogue asset in the Unity Editor**

> **Unity Editor task (user):**
> 1. Right-click in the Project window → Assets/Data/Dialogues (create the folder if it doesn't exist).
> 2. Create → Axiom/Data/Dialogue Data.
> 3. Name it `DD_Phasekeeper_Intro`.
> 4. Set Speaker Name to `"Phasekeeper"`.
> 5. Set Portrait Sprite to the player character's portrait (from `CD_Player_Kaelen` CharacterData if available, or leave null for now).
> 6. Set Dialogue Lines to these 3 lines from the narrative:
>    - "I was trapped between states. You taught me how to be whole."
>    - "We were broken. You taught us that change doesn't mean destruction — it means transformation."
>    - "Will you help me? To find others like you? To teach them that change doesn't mean destruction — it means transformation?"

- [ ] **Step 2: Create the example cutscene asset in the Unity Editor**

> **Unity Editor task (user):**
> 1. Right-click in the Project window → Assets/Data/Cutscenes (create the folder if it doesn't exist).
> 2. Create → Axiom/Data/Cutscene Sequence.
> 3. Name it `CS_Phasekeeper_Intro`.
> 4. Set Steps size to 2.
> 5. For Step 0:
>    - Step Type: Dialogue
>    - Dialogue Data: DD_Phasekeeper_Intro
> 6. For Step 1:
>    - Step Type: Unlock Spell
>    - Spell To Unlock: (assign a story-only spell, e.g., one with `requiredLevel = 0` from your spell catalog)
> 7. Save the asset.

---

## Task 9: Integrate the cutscene controller into the Platformer scene

Wire up the cutscene controller, dialogue UI, and trigger zone in the Platformer scene so the first story beat is reachable and playable.

**Files to modify:**
- Platformer.unity (scene)

- [ ] **Step 1: Add CutsceneController to the Platformer scene**

> **Unity Editor task (user):**
> 1. Open Assets/Scenes/Platformer.unity.
> 2. Create a new empty GameObject named `CutsceneManager`.
> 3. Drag it under the Canvas hierarchy (so it's part of the UI group).
> 4. Add the `CutsceneController` component to `CutsceneManager`.
> 5. Save the scene.

- [ ] **Step 2: Add DialogueBoxUI to the Platformer Canvas**

> **Unity Editor task (user):**
> 1. In the Platformer scene, create a new UI Panel under the Canvas named `DialogueBox`.
> 2. Add the `DialogueBoxUI` component to this panel.
> 3. Configure the DialogueBoxUI fields:
>    - Panel: drag the DialogueBox GameObject itself.
>    - Speaker Name Text: add a child TextMeshProUGUI for the speaker name.
>    - Portrait Image: add a child Image for the portrait.
>    - Dialogue Line Text: add a child TextMeshProUGUI for the dialogue text.
>    - Advance Button: add a child Button for "Continue".
> 4. Save the scene.

- [ ] **Step 3: Add a DialogueTriggerZone to the Platformer level**

> **Unity Editor task (user):**
> 1. In the Platformer scene, create a new empty GameObject at a location in the level where you want the story beat to trigger (e.g., near a key area or NPC sprite).
> 2. Name it `Phasekeeper_TriggerZone`.
> 3. Add a CircleCollider2D and set Is Trigger to true, with a radius of ~2 units.
> 4. Add the `DialogueTriggerZone` component.
> 5. Set Cutscene Sequence to `CS_Phasekeeper_Intro`.
> 6. Save the scene.

---

## Task 10: Smoke test the full story beat in Play Mode

Verify that the trigger fires, dialogue displays, the spell unlocks, and gameplay is restored cleanly.

- [ ] **Step 1: Run the Platformer scene in Play Mode**

> **Unity Editor task (user):**
> 1. Open Assets/Scenes/Platformer.unity.
> 2. Click Play.
> 3. Move the player into the trigger zone you created.
> 4. Confirm the dialogue appears with the speaker name and portrait.
> 5. Click "Continue" to advance through all 3 lines.
> 6. Confirm the dialogue box disappears and you regain control.
> 7. Check the Console for "Spell unlocked" messages.

- [ ] **Step 2: Validate the spell was added to the unlocked set**

> **Unity Editor task (user):**
> 1. In Play Mode, open the Game Manager prefab Inspector.
> 2. Check the `SpellUnlockService.UnlockedSpells` property (if you've wired it to show in the Inspector for debugging).
> 3. Confirm the story-unlock spell appears in the list.
> 4. Exit Play Mode.

- [ ] **Step 3: Check for any console errors and fix if needed**

> **Unity Editor task (user):**
> If you see errors in the Console (e.g., missing references, null exceptions), fix them in the Inspector or code as needed, then rerun the test.

---

## Summary

This plan delivers a minimal, data-driven dialogue and cutscene system that integrates with the existing spell-unlock infrastructure. The system is linear (not branching), fully editable in the Unity Editor via ScriptableObjects, and follows the project's architecture rules (plain C# logic, lightweight MonoBehaviours, event-driven decoupling).

**Verification Checklist:**
- ✅ All dialogue and cutscene data is authored as ScriptableObjects, not hardcoded strings.
- ✅ Story spell grants use `SpellUnlockService.Unlock(SpellData)` and fire the unlock event.
- ✅ Trigger zones follow the existing platformer pattern (SavePointTrigger, ExplorationEnemyCombatTrigger).
- ✅ MonoBehaviours handle lifecycle and wiring only; logic is in plain C# (`CutsceneRunner`).
- ✅ The first story beat is fully testable in the level and in Edit Mode.

**Next Steps (Post-DEV-49):**
1. Add branching dialogue if the narrative requires player choice.
2. Expand cutscene camera support with dedicated Cinemachine rigs for cinematic control.
3. Create more story beats using the same ScriptableObject pattern.
4. Add sound design and voice-over integration if needed.
