# DEV-23: Spell Input UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the voice-spell feedback UI that guides the player through the push-to-talk flow and communicates recognition outcomes (PTT prompt → listening indicator → spell name or "not recognized" error).

**Architecture:** A plain C# state machine (`SpellInputUILogic`) drives all state transitions and is unit-tested in Edit Mode. A thin MonoBehaviour (`SpellInputUI`) holds Unity/TMP references, reads the PTT `InputAction` independently for visual-only purposes, and subscribes exclusively to `BattleController` events — keeping it fully within the `Axiom.Battle` assembly. `BattleController` gains three new events and a voice spell phase boolean that prevents the action menu from accepting other inputs while the player is speaking. `SpellCastController` (Voice assembly) detects "final result with no match" and calls a new public method on `BattleController` to cross the assembly boundary cleanly.

**Tech Stack:** Unity 6 LTS · Unity Canvas + TextMeshPro · Unity Input System (`InputActionReference`) · NUnit (Unity Test Framework, Edit Mode) · UVCS for check-ins

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Battle/Battle.asmdef` | Add `Unity.InputSystem` reference |
| **Create** | `Assets/Scripts/Battle/UI/SpellInputUILogic.cs` | Plain C# state machine — no Unity types, fully testable |
| **Create** | `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs` | Edit Mode NUnit tests |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Add voice spell phase bool, 3 events, public notify method, `SpellInputUI` SerializeField, wire in `Initialize()` / `PlayerSpell()` / `OnSpellCast()` |
| Modify | `Assets/Scripts/Voice/SpellCastController.cs` | Detect final-result-no-match → call `BattleController.NotifySpellNotRecognized()` |
| **Create** | `Assets/Scripts/Battle/UI/SpellInputUI.cs` | MonoBehaviour wrapper — Unity lifecycle, TMP refs, PTT InputAction, coroutine auto-hide |

No new `.asmdef` files needed. `SpellInputUILogic` and `SpellInputUI` join the existing `Axiom.Battle` assembly. Tests join the existing `BattleTests` assembly.

---

## UI State Flow

```
[Idle]
  │  BattleController.OnSpellPhaseStarted
  ▼
[PromptVisible]   ← "Hold [Left Shift] and speak a spell name"
  │  PTT pressed (InputAction.started)
  ▼
[Listening]       ← "Listening..."
  │  PTT released (InputAction.canceled)
  ▼
[PromptVisible]   ← back to prompt while recognition processes
  │
  ├─ BattleController.OnSpellRecognized → [SpellRecognized] → auto-hide 2s → [Idle]
  │    "Hydrogen Blast!"
  │
  └─ BattleController.OnSpellNotRecognized → [NotRecognized] → auto-hide 2s → [PromptVisible]
       "Not recognized. Try again."

Any non-PlayerTurn BattleState change → cancel auto-hide → [Idle]
```

---

## Task 1 — Add Unity.InputSystem to Axiom.Battle Assembly

**Files:**
- Modify: `Assets/Scripts/Battle/Battle.asmdef`

- [ ] **Open `Assets/Scripts/Battle/Battle.asmdef`** and add `"Unity.InputSystem"` to the `references` array. The complete file should be:

```json
{
    "name": "Axiom.Battle",
    "references": [
        "Axiom.Data",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Verify Unity recompiles without errors** — switch to the Unity Editor after saving; the console should show no errors about missing `UnityEngine.InputSystem` types.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-23): add Unity.InputSystem reference to Axiom.Battle assembly`
  - `Assets/Scripts/Battle/Battle.asmdef`
  - `Assets/Scripts/Battle/Battle.asmdef.meta`

---

## Task 2 — Write Failing Tests for SpellInputUILogic

**Files:**
- Create: `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`

These tests drive the interface of `SpellInputUILogic` before a single line of implementation exists. The `BattleTests` assembly already references `Axiom.Battle`, so no asmdef changes are needed.

- [ ] **Create `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`** with the following content:

```csharp
using NUnit.Framework;
using Axiom.Battle;

[TestFixture]
public class SpellInputUILogicTests
{
    private SpellInputUILogic _logic;

    [SetUp]
    public void SetUp() => _logic = new SpellInputUILogic();

    // ── Initial state ─────────────────────────────────────────────────────────

    [Test]
    public void InitialState_IsIdle()
    {
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Idle));
    }

    [Test]
    public void InitialState_RecognizedSpellName_IsNull()
    {
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── ShowPrompt ────────────────────────────────────────────────────────────

    [Test]
    public void ShowPrompt_SetsPromptVisibleState()
    {
        _logic.ShowPrompt();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.PromptVisible));
    }

    [Test]
    public void ShowPrompt_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowPrompt();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── StartListening ────────────────────────────────────────────────────────

    [Test]
    public void StartListening_SetsListeningState()
    {
        _logic.StartListening();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Listening));
    }

    [Test]
    public void StartListening_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.StartListening();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── ShowResult ────────────────────────────────────────────────────────────

    [Test]
    public void ShowResult_SetsSpellRecognizedState()
    {
        _logic.ShowResult("Hydrogen Blast");
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.SpellRecognized));
    }

    [Test]
    public void ShowResult_StoresSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Hydrogen Blast"));
    }

    [Test]
    public void ShowResult_OverwritesPreviousSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowResult("Sodium Surge");
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Sodium Surge"));
    }

    // ── ShowError ─────────────────────────────────────────────────────────────

    [Test]
    public void ShowError_SetsNotRecognizedState()
    {
        _logic.ShowError();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.NotRecognized));
    }

    [Test]
    public void ShowError_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.ShowError();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── Hide ──────────────────────────────────────────────────────────────────

    [Test]
    public void Hide_ResetsToIdleState()
    {
        _logic.ShowPrompt();
        _logic.Hide();
        Assert.That(_logic.CurrentState, Is.EqualTo(SpellInputUILogic.State.Idle));
    }

    [Test]
    public void Hide_ClearsRecognizedSpellName()
    {
        _logic.ShowResult("Hydrogen Blast");
        _logic.Hide();
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }

    // ── State transitions ──────────────────────────────────────────────────────

    [Test]
    public void FullFlow_PromptToListeningToResult()
    {
        _logic.ShowPrompt();
        _logic.StartListening();
        _logic.ShowPrompt(); // PTT released, back to prompt while processing
        _logic.ShowResult("Sodium Surge");

        Assert.That(_logic.CurrentState,        Is.EqualTo(SpellInputUILogic.State.SpellRecognized));
        Assert.That(_logic.RecognizedSpellName, Is.EqualTo("Sodium Surge"));
    }

    [Test]
    public void FullFlow_PromptToListeningToError()
    {
        _logic.ShowPrompt();
        _logic.StartListening();
        _logic.ShowPrompt(); // PTT released
        _logic.ShowError();

        Assert.That(_logic.CurrentState,        Is.EqualTo(SpellInputUILogic.State.NotRecognized));
        Assert.That(_logic.RecognizedSpellName, Is.Null);
    }
}
```

- [ ] **Run tests in Unity Editor** — Window → General → Test Runner → EditMode tab → run `SpellInputUILogicTests`.
  Expected: **all tests FAIL** with `The type or namespace name 'SpellInputUILogic' could not be found` (or similar compile error). This confirms the tests are driving the implementation.

---

## Task 3 — Implement SpellInputUILogic

**Files:**
- Create: `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`

- [ ] **Create `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`:**

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Stateless (stateful but Unity-free) state machine for the spell input UI panel.
    /// Tracks which panel should be visible and what recognized spell name to display.
    /// Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Owned and driven by <see cref="SpellInputUI"/>.
    /// </summary>
    public class SpellInputUILogic
    {
        public enum State
        {
            Idle,
            PromptVisible,
            Listening,
            SpellRecognized,
            NotRecognized
        }

        /// <summary>The current display state of the spell input UI.</summary>
        public State  CurrentState       { get; private set; } = State.Idle;

        /// <summary>
        /// The name of the recognized spell, populated by <see cref="ShowResult"/>.
        /// Null in all other states.
        /// </summary>
        public string RecognizedSpellName { get; private set; }

        /// <summary>Transition to <see cref="State.PromptVisible"/>. Clears any stored spell name.</summary>
        public void ShowPrompt()
        {
            CurrentState        = State.PromptVisible;
            RecognizedSpellName = null;
        }

        /// <summary>Transition to <see cref="State.Listening"/>. Clears any stored spell name.</summary>
        public void StartListening()
        {
            CurrentState        = State.Listening;
            RecognizedSpellName = null;
        }

        /// <summary>Transition to <see cref="State.SpellRecognized"/> and store the spell name for display.</summary>
        public void ShowResult(string spellName)
        {
            CurrentState        = State.SpellRecognized;
            RecognizedSpellName = spellName;
        }

        /// <summary>Transition to <see cref="State.NotRecognized"/>. Clears any stored spell name.</summary>
        public void ShowError()
        {
            CurrentState        = State.NotRecognized;
            RecognizedSpellName = null;
        }

        /// <summary>Return to <see cref="State.Idle"/>. Clears any stored spell name.</summary>
        public void Hide()
        {
            CurrentState        = State.Idle;
            RecognizedSpellName = null;
        }
    }
}
```

- [ ] **Run tests in Unity Editor** — Window → General → Test Runner → EditMode → run `SpellInputUILogicTests`.
  Expected: **all 15 tests PASS**.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-23): add SpellInputUILogic state machine with Edit Mode tests`
  - `Assets/Scripts/Battle/UI/SpellInputUILogic.cs`
  - `Assets/Scripts/Battle/UI/SpellInputUILogic.cs.meta`
  - `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs`
  - `Assets/Tests/Editor/Battle/SpellInputUILogicTests.cs.meta`

---

## Task 4 — Extend BattleController with Voice Spell Phase

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

This task adds three new events, a voice spell phase boolean, a new `SpellInputUI` SerializeField, and modifies `PlayerSpell()` and `OnSpellCast()` to implement the actual PTT-then-speak flow (replacing the old Phase 2 placeholder behavior).

### Why `_isAwaitingVoiceSpell` is needed

Currently `PlayerSpell()` immediately sets `_isProcessingAction = true` and starts a coroutine. `OnSpellCast()` guards `if (_isProcessingAction) return;` — meaning voice results would be silently dropped while the placeholder is running. For Phase 3:

- `PlayerSpell()` enters a "waiting for voice" phase: sets `_isProcessingAction = true` (blocks attack/item/flee) but does **not** start a coroutine.
- `OnSpellCast()` is the one that actually starts `CompletePlayerAction` when a voice result arrives.
- `_isAwaitingVoiceSpell` is the guard that distinguishes "waiting for voice" from any other processing state.

- [ ] **Add the following declarations** directly after the existing `// ── UI Events` block (after `OnEnemyActionStarted`):

```csharp
/// <summary>
/// Fires when the player selects the Spell action, opening the voice spell phase.
/// SpellInputUI subscribes to show the PTT prompt panel.
/// </summary>
public event Action OnSpellPhaseStarted;

/// <summary>
/// Fires when Vosk returns a recognized spell name before execution resolves.
/// SpellInputUI subscribes to display the spell name briefly.
/// </summary>
public event Action<SpellData> OnSpellRecognized;

/// <summary>
/// Fires when Vosk returns a final result that does not match any unlocked spell.
/// SpellInputUI subscribes to show the "Not recognized" error panel.
/// Raised via <see cref="NotifySpellNotRecognized"/> (called from SpellCastController).
/// </summary>
public event Action OnSpellNotRecognized;
```

- [ ] **Add the following fields** in the `// ── Private fields` block, after `_enemySequenceComplete`:

```csharp
private bool _isAwaitingVoiceSpell;
```

- [ ] **Add the following SerializeField** in the Inspector region, after the `_battleHUD` SerializeField:

```csharp
[SerializeField]
[Tooltip("Assign the SpellInputUI component from the Battle Canvas.")]
private SpellInputUI _spellInputUI;
```

- [ ] **Add the `NotifySpellNotRecognized` public method** — place it after `OnSpellCast` in the player action methods region:

```csharp
/// <summary>
/// Called by <see cref="Axiom.Voice.SpellCastController"/> when Vosk returns a final
/// result that does not match any unlocked spell during the voice spell phase.
/// No-op outside the voice spell phase or outside PlayerTurn.
/// </summary>
public void NotifySpellNotRecognized()
{
    if (!_isAwaitingVoiceSpell) return;
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    OnSpellNotRecognized?.Invoke();
}
```

- [ ] **Replace the body of `Initialize()`** — add `_isAwaitingVoiceSpell = false;` after `_battleManager.StartBattle(startState);` and add `_spellInputUI?.Setup(this);` after `_battleHUD?.Setup(this, _playerStats, _enemyStats);`. The relevant section at the end of `Initialize()` should read:

```csharp
            _battleHUD?.Setup(this, _playerStats, _enemyStats);
            _spellInputUI?.Setup(this);

            // ... existing animation wiring block ...

            _battleManager.StartBattle(startState);
            _isAwaitingVoiceSpell = false;
```

- [ ] **Replace `PlayerSpell()`** — remove the placeholder call and coroutine start; instead fire `OnSpellPhaseStarted` and enter the awaiting state:

```csharp
/// <summary>
/// Called by ActionMenuUI when the player selects the Spell action.
/// Enters the voice spell phase: shows the PTT prompt and blocks other actions
/// until a spell is recognized via <see cref="OnSpellCast"/>.
/// No-op outside PlayerTurn or while an action is already processing.
/// </summary>
public void PlayerSpell()
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (_isProcessingAction) return;
    _isProcessingAction   = true;
    _isAwaitingVoiceSpell = true;
    OnSpellPhaseStarted?.Invoke();
}
```

- [ ] **Replace `OnSpellCast(SpellData)`** — guard by `_isAwaitingVoiceSpell` instead of `_isProcessingAction`, fire `OnSpellRecognized`, then start the completion coroutine:

```csharp
/// <summary>
/// Called by <see cref="Axiom.Voice.SpellCastController"/> when a recognized spell
/// name matches an unlocked spell during the voice spell phase.
/// Guards against calls outside the voice spell phase or outside PlayerTurn.
/// </summary>
public void OnSpellCast(SpellData spell)
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (!_isAwaitingVoiceSpell) return;
    _isAwaitingVoiceSpell     = false;
    _playerDamageVisualsFired = true; // No damage visuals for spells in Phase 3
    OnSpellRecognized?.Invoke(spell);
    Debug.Log($"[Battle] Voice spell cast: {spell.spellName}");
    StartCoroutine(CompletePlayerAction(targetDefeated: false));
}
```

- [ ] **Verify Unity compiles without errors.** Switch to Unity Editor; the console should be clean.

---

## Task 5 — Extend SpellCastController with Not-Recognized Forwarding

**Files:**
- Modify: `Assets/Scripts/Voice/SpellCastController.cs`

When Vosk returns a final result (non-empty `"text"` field) that does not match any unlocked spell, `SpellCastController` must forward this outcome to `BattleController` so the UI can show the error panel. The detection logic reuses `SpellResultMatcher.ExtractTextField`, which is already covered by existing tests.

- [ ] **Replace the `Update()` method** in `SpellCastController`:

```csharp
private void Update()
{
    while (_resultQueue.TryDequeue(out string voskJson))
    {
        SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);

        if (matched == null)
        {
            // Forward "final result, no match" to BattleController for UI feedback.
            // ExtractTextField returns empty for partial results (no "text" key),
            // so this only fires on genuine final results that didn't match a spell.
            string recognized = SpellResultMatcher.ExtractTextField(voskJson);
            if (!string.IsNullOrWhiteSpace(recognized) && _battleController != null)
                _battleController.NotifySpellNotRecognized();
            continue;
        }

        if (_battleController == null)
        {
            if (!_battleControllerWarningLogged)
            {
                Debug.LogError("[SpellCastController] BattleController is not assigned in the Inspector.", this);
                _battleControllerWarningLogged = true;
            }
            continue;
        }

        _battleController.OnSpellCast(matched);
    }
}
```

- [ ] **Verify Unity compiles without errors.**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-23): extend BattleController and SpellCastController for voice spell phase`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`
  - `Assets/Scripts/Voice/SpellCastController.cs`
  - `Assets/Scripts/Voice/SpellCastController.cs.meta`

---

## Task 6 — Implement SpellInputUI MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Battle/UI/SpellInputUI.cs`

`SpellInputUI` reads the same PTT `InputAction` that `MicrophoneInputHandler` uses — independently, for visual purposes only. It subscribes exclusively to `BattleController` events (same assembly). Coroutines handle the auto-hide delay after feedback states.

- [ ] **Create `Assets/Scripts/Battle/UI/SpellInputUI.cs`:**

```csharp
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
    ///   - PromptPanel    — visible in PromptVisible state ("Hold [Left Shift] and speak a spell name")
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
```

- [ ] **Verify Unity compiles without errors.**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-23): add SpellInputUI MonoBehaviour`
  - `Assets/Scripts/Battle/UI/SpellInputUI.cs`
  - `Assets/Scripts/Battle/UI/SpellInputUI.cs.meta`

---

## Task 7 — Build the Canvas Hierarchy (Unity Editor)

> **Unity Editor task (user):** All steps below are performed in the Unity Editor. No code changes.

**In the Battle scene (`Assets/Scenes/Battle.unity`):**

- [ ] **Open the Battle scene** — double-click `Assets/Scenes/Battle.unity` in the Project window.

- [ ] **Create the `SpellInputPanel` child on the Battle Canvas:**
  Hierarchy → right-click the existing Canvas → UI → Empty → rename to `SpellInputPanel`.
  Set its `RectTransform` to stretch across the full canvas (Anchor: stretch/stretch, Left/Right/Top/Bottom all 0). This is the root for all three sub-panels.

- [ ] **Create `PromptPanel` as a child of `SpellInputPanel`:**
  Right-click `SpellInputPanel` → UI → Panel → rename to `PromptPanel`.
  - Position: bottom-center of the canvas (Anchor: bottom-center, Pivot: 0.5/0, PosX: 0, PosY: 40, Width: 500, Height: 60).
  - Right-click `PromptPanel` → UI → Text - TextMeshPro → rename to `PromptText`.
  - Set `PromptText` content to: **"Hold [Left Shift] and speak a spell name"**
  - Font Size: 24, Alignment: Center/Middle, Color: white.
  - Set `PromptPanel` active: **false** (it starts hidden).

- [ ] **Create `ListeningPanel` as a child of `SpellInputPanel`:**
  Right-click `SpellInputPanel` → UI → Panel → rename to `ListeningPanel`.
  - Same anchor/position as `PromptPanel` (they overlap; only one is active at a time).
  - Right-click `ListeningPanel` → UI → Text - TextMeshPro → rename to `ListeningText`.
  - Set `ListeningText` content to: **"Listening..."**
  - Font Size: 24, Alignment: Center/Middle, Color: light blue (e.g., `#80C8FF`).
  - Set `ListeningPanel` active: **false**.

- [ ] **Create `FeedbackPanel` as a child of `SpellInputPanel`:**
  Right-click `SpellInputPanel` → UI → Panel → rename to `FeedbackPanel`.
  - Same anchor/position as the other two panels.
  - Right-click `FeedbackPanel` → UI → Text - TextMeshPro → rename to `FeedbackText`.
  - Leave `FeedbackText` content **empty** (filled at runtime by `SpellInputUI`).
  - Font Size: 28, Alignment: Center/Middle, Color: white.
  - Set `FeedbackPanel` active: **false**.

- [ ] **Attach `SpellInputUI` to `SpellInputPanel`:**
  Select `SpellInputPanel` in the Hierarchy → Add Component → search `SpellInputUI` → add it.

- [ ] **Save the scene** — Ctrl+S / Cmd+S.

---

## Task 8 — Wire Inspector References and Smoke-Test

> **Unity Editor task (user):** All steps below are performed in the Unity Editor.

- [ ] **Wire `SpellInputUI` SerializeFields** — select `SpellInputPanel` in the Hierarchy. In the Inspector:
  - **Push To Talk Action** → assign the same `InputActionReference` used by `MicrophoneInputHandler` (the PTT binding, e.g. `InputSystem_Actions → Voice → PushToTalk`).
  - **Prompt Panel** → drag `PromptPanel` from the Hierarchy.
  - **Listening Panel** → drag `ListeningPanel`.
  - **Feedback Panel** → drag `FeedbackPanel`.
  - **Feedback Text** → drag the `FeedbackText` TMP component from inside `FeedbackPanel`.
  - **Feedback Auto Hide Delay** → leave at `2` (seconds).

- [ ] **Wire `BattleController` SerializeField** — select the `BattleController` GameObject in the Hierarchy. In the Inspector:
  - **Spell Input UI** → drag the `SpellInputPanel` GameObject (which has the `SpellInputUI` component).

- [ ] **Play Mode smoke test:**
  1. Enter Play Mode (▶).
  2. Click **Spell** in the action menu → `PromptPanel` should appear ("Hold [Left Shift] and speak a spell name"). `ListeningPanel` and `FeedbackPanel` should be hidden.
  3. Hold the PTT key → `ListeningPanel` should appear, `PromptPanel` should hide.
  4. Release PTT → `PromptPanel` should return (Listening → PromptVisible transition).
  5. If Vosk is connected and returns a no-match result → `FeedbackPanel` appears with "Not recognized. Try again." then auto-hides after 2s and returns to `PromptPanel`.
  6. If a spell is recognized → `FeedbackPanel` appears with the spell name, then auto-hides and the turn advances to the enemy.
  7. Click **Attack** (not Spell) → none of the three panels should appear.

- [ ] **Save the scene** — Ctrl+S / Cmd+S.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-23): wire SpellInputUI in Battle scene`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/Battle.unity.meta`

---

## Self-Review Checklist

### Spec Coverage (DEV-23 Acceptance Criteria)

| AC | Covered by |
|----|-----------|
| Visual "listening" indicator appears while PTT key is held | `SpellInputUI.OnPTTStarted` → `_logic.StartListening()` → `ListeningPanel` visible |
| Push-to-talk prompt displayed when Spell is selected | `BattleController.OnSpellPhaseStarted` → `HandleSpellPhaseStarted` → `PromptPanel` visible |
| Recognized spell name displayed before execution resolves | `BattleController.OnSpellRecognized` → `HandleSpellRecognized` → `FeedbackPanel` with spell name |
| "Not recognized" error state shown on no-match | `SpellCastController` → `NotifySpellNotRecognized` → `BattleController.OnSpellNotRecognized` → `FeedbackPanel` with error text |
| All UI built with Unity Canvas + TextMeshPro | Tasks 6–7: Canvas panels + `TMP_Text` |
| Scripts placed under `Assets/Scripts/Battle/UI/` | `SpellInputUILogic.cs` and `SpellInputUI.cs` both placed there |

All six AC items are covered. ✓

### Placeholder Scan

No TBD, TODO, "implement later," "add appropriate error handling," or "similar to Task N" references found. ✓

### Type Consistency

| Type / Method | Defined in | Used in |
|---|---|---|
| `SpellInputUILogic.State` (enum) | Task 3 | Tasks 6 (SpellInputUI) |
| `SpellInputUILogic.ShowPrompt()` | Task 3 | Task 6 |
| `SpellInputUILogic.StartListening()` | Task 3 | Task 6 |
| `SpellInputUILogic.ShowResult(string)` | Task 3 | Task 6 |
| `SpellInputUILogic.ShowError()` | Task 3 | Task 6 |
| `SpellInputUILogic.Hide()` | Task 3 | Task 6 |
| `SpellInputUILogic.RecognizedSpellName` | Task 3 | Task 6 |
| `SpellInputUI.Setup(BattleController)` | Task 6 | Task 4 (wired in `Initialize()`) |
| `BattleController.OnSpellPhaseStarted` | Task 4 | Task 6 |
| `BattleController.OnSpellRecognized` | Task 4 | Task 6 |
| `BattleController.OnSpellNotRecognized` | Task 4 | Task 6 |
| `BattleController.NotifySpellNotRecognized()` | Task 4 | Task 5 |
| `SpellInputUILogic.State.PromptVisible` | Task 3 | Tasks 2 (tests), 6 |
| `SpellInputUILogic.State.Listening` | Task 3 | Tasks 2 (tests), 6 |
| `SpellInputUILogic.State.SpellRecognized` | Task 3 | Tasks 2 (tests), 6 |
| `SpellInputUILogic.State.NotRecognized` | Task 3 | Tasks 2 (tests), 6 |
| `SpellInputUILogic.State.Idle` | Task 3 | Tasks 2 (tests), 6 |

All consistent. ✓

### UVCS Staged File Audit

| Check-in | Files staged |
|---|---|
| Task 1 | `Battle.asmdef` + `.meta` |
| Task 3 | `SpellInputUILogic.cs` + `.meta`, `SpellInputUILogicTests.cs` + `.meta` |
| Task 5 | `BattleController.cs` + `.meta`, `SpellCastController.cs` + `.meta` |
| Task 6 | `SpellInputUI.cs` + `.meta` |
| Task 8 | `Battle.unity` + `.meta` |

All created/modified files covered. ✓

### Unity Editor Task Isolation

Tasks 7 and 8 are fully marked as `> Unity Editor task (user):` with no code steps mixed in. ✓

### MonoBehaviour Separation Rule

- `SpellInputUILogic` — plain C#, no Unity types, no `MonoBehaviour` ✓
- `SpellInputUI` — `MonoBehaviour`, handles Unity lifecycle (`OnEnable`, `OnDisable`, `OnDestroy`), delegates all state decisions to `_logic` ✓
- `BattleController` — existing pattern maintained; new code only adds field declarations and method calls in lifecycle and action methods ✓
