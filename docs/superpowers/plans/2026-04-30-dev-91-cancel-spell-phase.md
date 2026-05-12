# DEV-91: Cancel Spell Phase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player abort the voice spell phase before committing — restoring the action menu without consuming the turn or any MP, and resetting the player animator from Charging → Idle.

**Architecture:** One new public method on `BattleController` (`CancelSpellPhase()`) plus a new `OnSpellPhaseCancelled` event. Cancel is wired through two paths in `SpellInputUI`: a new `CancelSpell` `InputActionReference` (Esc / Gamepad B) **and** a visible Cancel `Button` child of the SpellInputPanel. Both routes call the same single API, so the guard logic lives in one place. Animator reset reuses the existing `OnSpellChargeAborted → PlayerBattleAnimator.TriggerResetCharge` wiring authored in DEV-25 — no animator changes required.

**Tech Stack:** Unity 6.0.4 LTS · C# · Axiom.Battle · Axiom.Voice · Unity Input System (`InputActionReference`) · UnityEngine.UI (`Button`) · NUnit Edit Mode tests · Unity Version Control (UVCS).

**Jira:** [DEV-91](https://axiombrokensunrefined.atlassian.net/browse/DEV-91)
**Branch:** `dev` (current)

---

## Pre-flight check

> **Required before starting:** the working tree currently has uncommitted DEV-90 changes (`BattleController.cs`, `BattleVoiceBootstrap.cs`, `BattleControllerSpellPhaseTests.cs`). DEV-90 must be checked into UVCS first, otherwise the diffs in this plan will collide with DEV-90's pending changes.
>
> Confirm via Unity Version Control → Pending Changes shows zero pending items before running Task 1.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs` | Modify | Add seven Edit Mode tests covering `CancelSpellPhase()` paths (TDD red phase) |
| `Assets/Scripts/Battle/BattleController.cs` | Modify | Add `OnSpellPhaseCancelled` event + `CancelSpellPhase()` public method (TDD green phase) |
| `Assets/InputSystem_Actions.inputactions` | Modify | Add `CancelSpell` Button action to the existing `Voice` action map with `<Keyboard>/escape` + `<Gamepad>/buttonEast` bindings |
| `Assets/Scripts/Battle/UI/SpellInputUI.cs` | Modify | Subscribe to `OnSpellPhaseCancelled`, read `_cancelSpellAction.performed`, wire optional `_cancelButton.onClick` — all three routes call `_battleController.CancelSpellPhase()` |
| `Assets/Scenes/Battle.unity` | Modify | Add Cancel `Button` child under SpellInputPanel; update PromptPanel hint TMP text; assign `_cancelSpellAction` and `_cancelButton` on the SpellInputUI component |

> **Why no animator changes:** DEV-25 already wired `OnSpellChargeAborted → PlayerBattleAnimator.TriggerResetCharge` in `BattleController.Initialize`/`OnDestroy` (see `BattleController.cs:425` and `:933`). `CancelSpellPhase()` reuses that path by firing `OnSpellChargeAborted` itself — there is no second animator subscription to add.

---

## Task 1 — Add failing Edit Mode tests for `CancelSpellPhase`

**Files:**
- Modify: `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs`

This task adds seven NUnit tests to the existing test class. The class already exposes private-field reflection helpers (`SetField`, `GetField`) — reuse them. Each test is independent: the `[SetUp]` creates a fresh `BattleController` MonoBehaviour and `[TearDown]` destroys it.

The tests intentionally exercise *only* the public `CancelSpellPhase()` API + event surface. They do not touch `MicrophoneInputHandler`, `SpellInputUI`, or any animator — those layers are exercised manually in Task 5's Editor walkthrough.

- [ ] **Step 1: Add `using Axiom.Core;` to the existing test file**

The new MP-not-consumed test needs `CharacterStats`, which lives in `Axiom.Core`. Open `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs` and add the import next to the existing ones:

```csharp
using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;
```

- [ ] **Step 2: Append the seven new tests**

Add these methods inside the existing `BattleControllerSpellPhaseTests` class, immediately *before* the `// ── Reflection helpers ──` comment band so they sit alongside the existing `NotifyVoiceResultEmpty_*` tests. The reflection helpers at the bottom are reused unchanged.

```csharp
[Test]
public void CancelSpellPhase_OnPlayerTurn_DuringSpellPhase_ResetsAwaitingFlag()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged); // → PlayerTurn

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", true);
    SetField(_controller, "_isProcessingAction", false);

    _controller.CancelSpellPhase();

    Assert.IsFalse((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
        "Cancel must exit the voice spell phase so the player can choose another action.");
    Assert.IsFalse((bool)GetField(_controller, "_isProcessingAction"),
        "Cancel must release the action lock so PlayerAttack/PlayerItem/PlayerFlee work after.");
}

[Test]
public void CancelSpellPhase_FiresOnSpellPhaseCancelled()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged);

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", true);

    int cancelledFired = 0;
    _controller.OnSpellPhaseCancelled += () => cancelledFired++;

    _controller.CancelSpellPhase();

    Assert.AreEqual(1, cancelledFired,
        "SpellInputUI listens for OnSpellPhaseCancelled to hide all panels — it must fire exactly once per cancel.");
}

[Test]
public void CancelSpellPhase_FiresOnSpellChargeAborted()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged);

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", true);

    int abortFired = 0;
    _controller.OnSpellChargeAborted += () => abortFired++;

    _controller.CancelSpellPhase();

    Assert.AreEqual(1, abortFired,
        "Cancel must reset the player animator from Charging → Idle. " +
        "Animator wiring is OnSpellChargeAborted → PlayerBattleAnimator.TriggerResetCharge.");
}

[Test]
public void CancelSpellPhase_DoesNotFireOnSpellNotRecognized()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged);

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", true);

    int notRecognizedFired = 0;
    _controller.OnSpellNotRecognized += () => notRecognizedFired++;

    _controller.CancelSpellPhase();

    Assert.AreEqual(0, notRecognizedFired,
        "Cancel is not an error path — the 'Not recognized. Try again.' feedback panel must NOT show.");
}

[Test]
public void CancelSpellPhase_OutsideSpellPhase_IsNoOp()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged);

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", false);

    int cancelledFired = 0;
    int abortFired = 0;
    _controller.OnSpellPhaseCancelled += () => cancelledFired++;
    _controller.OnSpellChargeAborted  += () => abortFired++;

    Assert.DoesNotThrow(() => _controller.CancelSpellPhase());

    Assert.AreEqual(0, cancelledFired,
        "Pressing Cancel from the action menu (panel hidden) must not raise the cancel event.");
    Assert.AreEqual(0, abortFired,
        "Pressing Cancel from the action menu must not retrigger the animator reset.");
}

[Test]
public void CancelSpellPhase_OutsidePlayerTurn_IsNoOp()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Surprised); // → EnemyTurn

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_isAwaitingVoiceSpell", true); // pathological state

    int cancelledFired = 0;
    _controller.OnSpellPhaseCancelled += () => cancelledFired++;

    _controller.CancelSpellPhase();

    Assert.IsTrue((bool)GetField(_controller, "_isAwaitingVoiceSpell"),
        "Cancel during EnemyTurn must not mutate spell-phase state.");
    Assert.AreEqual(0, cancelledFired,
        "Cancel during EnemyTurn must not raise the cancel event.");
}

[Test]
public void CancelSpellPhase_DoesNotConsumeMP()
{
    var bm = new BattleManager();
    bm.StartBattle(CombatStartState.Advantaged);

    var playerStats = new CharacterStats { Name = "Test", MaxHP = 30, MaxMP = 20, ATK = 1, DEF = 1, SPD = 1 };
    playerStats.Initialize();

    SetField(_controller, "_battleManager", bm);
    SetField(_controller, "_playerStats", playerStats);
    SetField(_controller, "_isAwaitingVoiceSpell", true);

    int mpBefore = playerStats.CurrentMP;
    _controller.CancelSpellPhase();

    Assert.AreEqual(mpBefore, playerStats.CurrentMP,
        "Cancel returns to the action menu without consuming the turn or any MP (DEV-91 AC).");
}
```

- [ ] **Step 3: Run the new tests in Unity Test Runner — confirm they FAIL**

> **Unity Editor task (user):**
> 1. In Unity Editor: **Window → General → Test Runner**.
> 2. Switch to the **EditMode** tab.
> 3. Expand the `BattleTests` assembly tree until you see the seven `CancelSpellPhase_*` tests.
> 4. Right-click the group and **Run Selected**.
> 5. Expected result: all seven FAIL with compile error `'BattleController' does not contain a definition for 'CancelSpellPhase'` (and similarly for `OnSpellPhaseCancelled`). This is the TDD red phase — proceed to Task 2 to make them pass.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-91): add failing CancelSpellPhase Edit Mode tests`
- `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs`

> The `.cs.meta` for this file is already tracked from DEV-90 — do not include it again. If `git status`-equivalent shows the meta as new (i.e. DEV-90 was not yet checked in), include `Assets/Tests/Editor/Battle/BattleControllerSpellPhaseTests.cs.meta` as well.

---

## Task 2 — Implement `CancelSpellPhase` + `OnSpellPhaseCancelled` event in `BattleController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

This is the minimal-green step. Add one event and one public method. No other changes — no animator subscriptions to wire (existing `OnSpellChargeAborted` wiring at line ~425 already covers the animator), no `OnDestroy` cleanup needed for the new event (events on `BattleController` don't require explicit teardown — when `BattleController` is destroyed all subscribers' delegate references are released with it; this matches the pattern used by every other event on this class, e.g. `OnSpellPhaseStarted`, `OnSpellNotRecognized`).

- [ ] **Step 1: Add the `OnSpellPhaseCancelled` event declaration**

Open `Assets/Scripts/Battle/BattleController.cs`. Find the `OnSpellChargeAborted` event declaration (around line 154):

```csharp
        /// <summary>
        /// Fires when the voice spell phase ends without a cast being dispatched —
        /// either because Vosk returned empty text (silent PTT release) or because
        /// the recognized word did not match any unlocked spell.
        /// <see cref="PlayerBattleAnimator"/> subscribes via <see cref="Initialize"/> to reset IsCharging.
        /// </summary>
        public event Action OnSpellChargeAborted;
```

Immediately *after* it, add the new event:

```csharp
        /// <summary>
        /// Fires when the player explicitly cancels the voice spell phase via
        /// <see cref="CancelSpellPhase"/>. Distinct from <see cref="OnSpellChargeAborted"/>:
        /// charge-aborted only resets the animator, while spell-phase-cancelled also tells
        /// <see cref="SpellInputUI"/> to hide every panel and return the player to the
        /// action menu. Cancel does not fire <see cref="OnSpellNotRecognized"/> — it is
        /// a deliberate opt-out, not an error path.
        /// </summary>
        public event Action OnSpellPhaseCancelled;
```

- [ ] **Step 2: Add the `CancelSpellPhase()` public method**

Find `NotifyVoiceResultEmpty()` in the same file (around line 624). Immediately *after* its closing brace, add the new public method:

```csharp
        /// <summary>
        /// Aborts the voice spell phase at the player's request, returning to the action
        /// menu without consuming the turn or any MP. Fires <see cref="OnSpellChargeAborted"/>
        /// so the player animator returns to Idle, and <see cref="OnSpellPhaseCancelled"/>
        /// so <see cref="SpellInputUI"/> hides every panel.
        ///
        /// No-op outside the voice spell phase or outside <see cref="BattleState.PlayerTurn"/> —
        /// satisfying the DEV-91 AC that the cancel input does nothing when pressed from the
        /// action menu or during the enemy's turn.
        /// </summary>
        public void CancelSpellPhase()
        {
            if (!_isAwaitingVoiceSpell) return;
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;

            _isAwaitingVoiceSpell = false;
            _isProcessingAction   = false;
            OnSpellPhaseCancelled?.Invoke();
            OnSpellChargeAborted?.Invoke();
        }
```

> **Guard order rationale:** the `_isAwaitingVoiceSpell` guard runs first because it is the cheapest check and covers the common case (player presses Esc with the action menu visible — the panel is already hidden, nothing to do). The `PlayerTurn` guard is a safety net for the pathological case where `_isAwaitingVoiceSpell` was somehow left `true` across a state transition; production code does not produce that state, but the guard is cheap and the test pins the behavior.

- [ ] **Step 3: Re-run the seven tests in Unity Test Runner — confirm they PASS**

> **Unity Editor task (user):**
> 1. Window → General → Test Runner → EditMode tab.
> 2. Right-click the `CancelSpellPhase_*` group → **Run Selected**.
> 3. Expected result: all seven PASS. Also re-run the existing `NotifyVoiceResultEmpty_*` tests in the same class to confirm no regression.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-91): add BattleController.CancelSpellPhase + OnSpellPhaseCancelled event`
- `Assets/Scripts/Battle/BattleController.cs`

---

## Task 3 — Add `CancelSpell` action to the Input System

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions`

The project's input asset already has a `Voice` action map containing `PushToTalk`. Add a sibling `CancelSpell` Button action with two bindings: `<Keyboard>/escape` and `<Gamepad>/buttonEast` (B button on Xbox / Circle on PlayStation). Authoring through the Unity Input Actions editor is required because Unity normalizes the JSON and assigns GUIDs — direct text edits work but get reformatted on the next Editor save.

> **Conflict check before authoring:** if your project later wires Esc to a pause menu (DEV-78 area), both actions will fire on the same key. Until that lands, `<Keyboard>/escape` is unused. Verify the project's Input Actions editor has no other `Esc` binding before saving.

- [ ] **Step 1: Open the Input Actions editor**

> **Unity Editor task (user):**
> 1. In the Project window, double-click `Assets/InputSystem_Actions.inputactions`. The Input Actions editor window opens.
> 2. In the left **Action Maps** column, select **Voice**. The middle **Actions** column should show `PushToTalk`.

- [ ] **Step 2: Create the `CancelSpell` action**

> **Unity Editor task (user):**
> 1. In the Actions column, click the **+** button to add a new action.
> 2. Rename the new action to `CancelSpell` (single word, PascalCase — matches `PushToTalk`).
> 3. With `CancelSpell` selected, set its properties in the right pane:
>    - **Action Type:** `Button`
>    - **Initial State Check:** unchecked (matches `PushToTalk`)
>    - Leave **Interactions** and **Processors** empty.

- [ ] **Step 3: Add the keyboard binding**

> **Unity Editor task (user):**
> 1. With `CancelSpell` selected, click the **+** beside it and choose **Add Binding**.
> 2. Select the new `<No Binding>` row underneath.
> 3. In the right pane's **Path** dropdown, search for `Escape` and choose **Keyboard → Escape** (`<Keyboard>/escape`).
> 4. Leave **Use in control scheme** boxes at default (all schemes enabled).

- [ ] **Step 4: Add the gamepad binding**

> **Unity Editor task (user):**
> 1. With `CancelSpell` still selected, click **+** → **Add Binding** again.
> 2. Select the second `<No Binding>` row.
> 3. In the **Path** dropdown, search for `Button East` and choose **Gamepad → Button East** (`<Gamepad>/buttonEast`).

- [ ] **Step 5: Save and verify**

> **Unity Editor task (user):**
> 1. Click **Save Asset** at the top of the Input Actions editor (or press Ctrl/Cmd+S).
> 2. Close and reopen the asset to verify both bindings persisted.
> 3. Open `Assets/InputSystem_Actions.inputactions` in a text editor as a sanity check — search for `"name": "CancelSpell"`. The `Voice` map's `actions` array should now contain two entries (`PushToTalk`, `CancelSpell`) and its `bindings` array should contain three entries (PushToTalk's leftShift + CancelSpell's escape + CancelSpell's buttonEast).

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-91): add CancelSpell input action (Esc / Gamepad B) to Voice map`
- `Assets/InputSystem_Actions.inputactions`

> The `.inputactions.meta` file should not change (only the asset's contents change). If UVCS lists it as modified anyway, include it in the same check-in.

---

## Task 4 — Wire `SpellInputUI` to the cancel input action, cancel button, and cancel event

**Files:**
- Modify: `Assets/Scripts/Battle/UI/SpellInputUI.cs`

`SpellInputUI` is the natural owner of cancel input because it already gates on the SpellInputPanel being visible. Three call sites (`InputAction.performed`, `Button.onClick`, and a fallback no-op when nothing is wired) all reduce to a single `_battleController.CancelSpellPhase()` call — the guard logic on `BattleController` ensures every path is safe.

`SpellInputUI` enables/disables the action in `OnEnable`/`OnDisable` (matching the `MicrophoneInputHandler` pattern at line 76–86 of that file). The `Button` reference is optional — if the Editor wiring in Task 5 is skipped, `SpellInputUI` falls back to input-only.

- [ ] **Step 1: Add the new SerializeFields**

Open `Assets/Scripts/Battle/UI/SpellInputUI.cs`. Find the existing `_pushToTalkAction` field and the `Header("Panels — assign child GameObjects from the Battle Canvas")` block (around line 22–32). Add two new fields immediately *after* the existing `_feedbackAutoHideDelay` field (around line 39):

```csharp
        [Header("Cancel input — DEV-91")]

        [SerializeField]
        [Tooltip("Cancel InputAction (Esc on keyboard, B on gamepad). Wired to BattleController.CancelSpellPhase. Required for keyboard/gamepad cancel — leave unassigned only if the Cancel button is the sole cancel route.")]
        private InputActionReference _cancelSpellAction;

        [SerializeField]
        [Tooltip("Optional. Visible Cancel button child of the SpellInputPanel. Provides discoverable cancel for mouse/touch users; clicks call the same path as the Cancel input action.")]
        private UnityEngine.UI.Button _cancelButton;
```

- [ ] **Step 2: Subscribe to `OnSpellPhaseCancelled` in `Setup`**

Find the `Setup` method (around line 51). Inside the body, immediately *after* the `OnBattleStateChanged` subscription line, add:

```csharp
            _battleController.OnSpellPhaseCancelled += HandleSpellPhaseCancelled;
```

The full block now reads:

```csharp
            _battleController = battleController;
            _battleController.OnSpellPhaseStarted  += HandleSpellPhaseStarted;
            _battleController.OnSpellRecognized    += HandleSpellRecognized;
            _battleController.OnSpellNotRecognized += HandleSpellNotRecognized;
            _battleController.OnSpellCastRejected  += HandleSpellCastRejected;
            _battleController.OnBattleStateChanged += HandleBattleStateChanged;
            _battleController.OnSpellPhaseCancelled += HandleSpellPhaseCancelled;
```

- [ ] **Step 3: Add the matching unsubscribe to `Unsubscribe`**

Find the `Unsubscribe` method at the bottom of the file (around line 205). Add the matching `-=` line at the end of the block, mirroring the `Setup` order:

```csharp
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
```

- [ ] **Step 4: Wire `_cancelSpellAction` and `_cancelButton` in `OnEnable`/`OnDisable`**

Replace the existing `OnEnable` and `OnDisable` methods (around line 73–85) with the versions below. Key changes: enable/disable the cancel action (matches the MicrophoneInputHandler ownership pattern — `SpellInputUI` is the sole consumer of `CancelSpell` so it owns the action's lifecycle), and wire/unwire the optional cancel button.

```csharp
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
```

- [ ] **Step 5: Add the three handler methods**

Add these three methods at the bottom of the class, immediately *before* `private void Unsubscribe()`:

```csharp
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
```

> **Why a no-op when the panel is already hidden is fine:** `BattleController.CancelSpellPhase()` itself guards on `_isAwaitingVoiceSpell` and `PlayerTurn`. If the player presses Esc from the action menu, `CancelSpellPhase` exits early and the cancel event never fires, so `HandleSpellPhaseCancelled` is never called — no panel state churn. This satisfies the DEV-91 AC "Outside the spell phase the cancel input is a no-op."

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-91): wire SpellInputUI to cancel input + button + event`
- `Assets/Scripts/Battle/UI/SpellInputUI.cs`

---

## Task 5 — Battle scene wiring + manual Play Mode verification

**Files:**
- Modify: `Assets/Scenes/Battle.unity`

Three Editor changes: add a Cancel `Button` child under SpellInputPanel, update the PromptPanel hint text, and assign the `_cancelSpellAction` + `_cancelButton` Inspector fields on the SpellInputUI component. Then a six-scenario manual matrix to verify acceptance criteria.

- [ ] **Step 1: Add a Cancel button under the SpellInputPanel**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. In the Hierarchy, expand **Battle Canvas → SpellInputPanel**.
> 3. Right-click `SpellInputPanel` → **UI → Button - TextMeshPro**. Rename the new GameObject to `CancelButton`.
> 4. In the Inspector, set the RectTransform anchors so the button sits in the bottom-right corner of the SpellInputPanel (e.g. anchor preset bottom-right, offsets `-20, 20`, size `120 × 40`). Match the visual style of the project's existing menu buttons — the simplest path is to duplicate the look of an `ActionMenuUI` button and tweak the colors to a muted grey/red.
> 5. Expand `CancelButton` and select its child **Text (TMP)** GameObject. Set the TMP component's text to `Cancel`.

- [ ] **Step 2: Update the PromptPanel hint text**

> **Unity Editor task (user):**
> 1. Still in `Battle.unity`, expand **Battle Canvas → SpellInputPanel → PromptPanel**.
> 2. Locate the TMP child that currently shows the prompt copy (the existing string is similar to "Hold [Shift] and speak a spell name").
> 3. Edit the TMP text to: `Hold [Shift] to speak · [Esc] to cancel`.
> 4. If the new text overflows, increase the PromptPanel width or shrink the TMP font size by 1–2 points until it fits on a single line at the panel's reference resolution.

- [ ] **Step 3: Assign the Cancel input action reference**

> **Unity Editor task (user):**
> 1. In the Hierarchy, select the GameObject carrying the `SpellInputUI` component (typically the `SpellInputPanel` itself, or a sibling — same GameObject that already has `_pushToTalkAction` assigned).
> 2. In the Inspector, locate the new `Cancel Spell Action` field added in Task 4.
> 3. Click the field's circle picker. In the picker, navigate to `InputSystem_Actions / Voice / CancelSpell` and select it. The field should now display a reference to the `CancelSpell` action.

- [ ] **Step 4: Assign the Cancel button reference**

> **Unity Editor task (user):**
> 1. With the `SpellInputUI` GameObject still selected, locate the new `Cancel Button` field.
> 2. Drag the `CancelButton` GameObject (created in Step 1) from the Hierarchy onto the `Cancel Button` field. The field should now display the button.

- [ ] **Step 5: Save the scene**

> **Unity Editor task (user):**
> File → Save (Ctrl/Cmd+S).

- [ ] **Step 6: Manual Play Mode verification (six scenarios)**

> **Unity Editor task (user):**
> Enter Play Mode in `Battle.unity`. Run the matrix below — every row must pass before the work is considered shippable.

| # | Scenario | Expected behavior |
|---|---|---|
| 1 | Click **Spell** → press **Esc** | SpellInputPanel hides; action menu re-enables; player animator transitions Charging → Idle within ~1 frame; no "Not recognized" toast appears |
| 2 | Click **Spell** → click **Cancel** button | Same outcome as #1 |
| 3 | Click **Spell** → hold **Shift** → release before speaking → press **Esc** | Same outcome as #1; verifies cancel works after the DEV-90 silent-PTT path that keeps the panel armed |
| 4 | Click **Spell** → press **Esc** → click **Spell** again → speak a spell | Spell recognizes and casts normally; verifies `_isAwaitingVoiceSpell` and `_isProcessingAction` were both released by cancel |
| 5 | Press **Esc** while the action menu is visible (no spell phase active) | Nothing happens — no panel toggles, no log spam, MP and HP unchanged |
| 6 | Click **Spell** → wait for enemy turn to interrupt? *(not actually possible — player turn has no time pressure; replace with:)* During EnemyTurn, press **Esc** | Nothing happens — guard prevents any state mutation |

Verify after each test that `_playerStats.CurrentMP` (visible on the MP bar) is unchanged from the start of the turn.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-91): wire Cancel button + Esc hint in Battle scene`
- `Assets/Scenes/Battle.unity`

---

## Acceptance Criteria Coverage

| AC item | Covered by |
|---|---|
| Cancel returns to action menu without consuming turn or MP | Task 1 Step 2 (`CancelSpellPhase_OnPlayerTurn_DuringSpellPhase_ResetsAwaitingFlag`, `CancelSpellPhase_DoesNotConsumeMP`) + Task 5 manual scenario #4 |
| Player animator returns to Idle after cancel | Task 1 Step 2 (`CancelSpellPhase_FiresOnSpellChargeAborted`) + Task 5 manual scenarios #1–#3 |
| Cancel does not fire `OnSpellNotRecognized` or any error feedback | Task 1 Step 2 (`CancelSpellPhase_DoesNotFireOnSpellNotRecognized`) + Task 5 scenario #1 visual check |
| Cancel input is hinted on the spell prompt panel | Task 5 Step 2 (PromptPanel TMP edit) |
| Outside the spell phase the cancel input is a no-op | Task 1 Step 2 (`CancelSpellPhase_OutsideSpellPhase_IsNoOp`, `CancelSpellPhase_OutsidePlayerTurn_IsNoOp`) + Task 5 scenarios #5, #6 |
| Edit Mode tests cover the public API path | Task 1 (seven new tests in `BattleControllerSpellPhaseTests.cs`) |
