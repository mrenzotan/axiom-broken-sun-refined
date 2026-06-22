# Battle Message Readability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` together with `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the passive two-line battle history with one typewritten, acknowledgment-gated message at a time, while preventing battle actions and turn progression until required narration is acknowledged.

**Architecture:** `StatusMessageQueue` becomes the plain-C# presentation state machine and reuses `Axiom.Core.TypewriterEffect`; `StatusMessageUI` remains the Unity lifecycle/input wrapper. A small plain-C# `BattleMessageFlowGate` holds controller continuations while the queue is busy, so animation completion, condition processing, and terminal transitions cannot outrun unread messages. `BattleMessageFormatter` owns condition wording, and condition turn results retain individual damage-tick identities instead of collapsing them into an unnamed total.

**Tech Stack:** Unity 6 LTS, C#, uGUI `Button`/`EventSystem`, TextMeshPro, Unity Input System UI module, NUnit Edit Mode tests, Unity Version Control (UVCS)

**Jira:** [DEV-123](https://axiombrokensunrefined.atlassian.net/browse/DEV-123) — Improve Battle Scene Message Log Readability

**Confirmed UX:** Reuse the current `MessageLog` footprint as a wrapped two-line narration panel. Show one message at a time with a visible Continue button. Pressing Continue during reveal completes that message; pressing it again advances. Keep the action menu visible but disabled, focus Continue while narration is pending, and restore the first valid action afterward. Do not narrate routine turn changes; the existing turn indicator already does that.

---

## File map

- Modify `Assets/Scripts/Battle/UI/StatusMessageQueue.cs`: queued-message and typewriter state machine.
- Modify `Assets/Scripts/Battle/UI/StatusMessageUI.cs`: drive the state machine from `Update`, bind Continue, and expose busy-state changes.
- Modify `Assets/Scripts/Battle/UI/ActionMenuUI.cs`: snapshot/restore button interactability during message blocking.
- Modify `Assets/Scripts/Battle/UI/BattleHUD.cs`: remove routine turn messages, connect message blocking, and post condition-specific narration.
- Create `Assets/Scripts/Battle/BattleMessageFlowGate.cs`: defer battle continuations while narration is busy.
- Create `Assets/Scripts/Battle/BattleMessageFormatter.cs`: canonical applied-condition and damage-tick wording.
- Modify `Assets/Scripts/Battle/BattleController.cs`: feed the gate, emit applied-condition details, and defer all progression boundaries.
- Modify `Assets/Scripts/Battle/ConditionTurnResult.cs`: carry individual condition damage ticks.
- Modify `Assets/Scripts/Battle/CharacterStats.cs`: record per-condition damage while preserving aggregate damage.
- Modify `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs`: queue/reveal/advance/busy transition coverage.
- Create `Assets/Tests/Editor/UI/ActionMenuUIMessageBlockTests.cs`: message-lock snapshot/restore coverage.
- Create `Assets/Tests/Editor/Battle/BattleMessageFlowGateTests.cs`: continuation ordering and re-blocking coverage.
- Create `Assets/Tests/Editor/Battle/BattleMessageFormatterTests.cs`: condition wording coverage.
- Modify `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`: per-condition tick identity coverage.
- Modify `Assets/Scenes/Battle.unity`: resize the existing message area and add/wire its Continue button.

No new assembly definition is needed: `Axiom.Battle` already references `Axiom.Core`, TextMeshPro, uGUI, and Input System; the existing `BattleTests` and `UITests` assemblies cover the new tests.

### Task 1: Replace the rolling history with a typewriter queue

**Files:**
- Modify: `Assets/Scripts/Battle/UI/StatusMessageQueue.cs`
- Modify: `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs`

- [ ] **Step 1: Replace the rolling-buffer tests with intent-focused failing tests**

Cover these exact behaviors in `StatusMessageQueueTests`: posting the first non-empty message changes `IsBusy` from false to true once; messages remain FIFO; `Update` reveals characters through the shared typewriter; Continue during reveal calls the skip path without dequeuing; the next Continue advances; acknowledging the final message changes `IsBusy` to false once; posting null, empty, or whitespace throws `ArgumentException`; and a newly displayed queued message starts unrevealed.

```csharp
[Test]
public void Continue_WhileRevealing_CompletesCurrentWithoutAdvancing()
{
    var queue = new StatusMessageQueue(charsPerSecond: 1f);
    queue.Post("First");
    queue.Post("Second");

    queue.Continue();

    Assert.AreEqual("First", queue.VisibleText);
    Assert.IsTrue(queue.IsCurrentMessageComplete);
    Assert.AreEqual(2, queue.PendingCount);
}

[Test]
public void Continue_AfterReveal_AdvancesInFifoOrder()
{
    var queue = new StatusMessageQueue(charsPerSecond: 30f);
    queue.Post("First");
    queue.Post("Second");
    queue.Continue();
    queue.Continue();

    Assert.AreEqual("Second", queue.CurrentMessage);
    Assert.AreEqual(string.Empty, queue.VisibleText);
    Assert.AreEqual(1, queue.PendingCount);
}
```

- [ ] **Step 2: Run the focused Edit Mode tests and verify the old implementation fails**

> **Unity Editor task (user):** Open Window → General → Test Runner → EditMode, run `Axiom.Tests.UI.StatusMessageQueueTests`. Expected: compile/test failures because the new API and semantics do not exist.

- [ ] **Step 3: Implement the minimal queue state machine**

Replace the two-line fields with `Queue<string>` plus one injected `TypewriterEffect`. Expose `CurrentMessage`, `VisibleText`, `IsBusy`, `IsCurrentMessageComplete`, and `PendingCount`. `Post` validates text, starts the typewriter only on empty-to-busy, and fires `BusyStateChanged(true)` exactly once. `Update(float)` delegates to the typewriter. `Continue()` skips an incomplete reveal; otherwise it dequeues, starts the next message at zero visible characters, or fires `BusyStateChanged(false)` after the final item.

```csharp
public sealed class StatusMessageQueue
{
    private readonly Queue<string> _messages = new Queue<string>();
    private readonly TypewriterEffect _typewriter = new TypewriterEffect();
    private readonly float _charsPerSecond;

    public event Action<bool> BusyStateChanged;
    public string CurrentMessage => _messages.Count == 0 ? string.Empty : _messages.Peek();
    public string VisibleText => _typewriter.VisibleText;
    public bool IsBusy => _messages.Count > 0;
    public bool IsCurrentMessageComplete => !IsBusy || _typewriter.IsComplete;
    public int PendingCount => _messages.Count;

    public StatusMessageQueue(float charsPerSecond = 30f)
    {
        _charsPerSecond = Math.Max(0.01f, charsPerSecond);
    }

    public void Post(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Battle messages cannot be empty.", nameof(message));

        bool wasBusy = IsBusy;
        _messages.Enqueue(message);
        if (wasBusy) return;
        _typewriter.Start(message, _charsPerSecond);
        BusyStateChanged?.Invoke(true);
    }

    public void Update(float deltaTime)
    {
        if (IsBusy) _typewriter.Update(deltaTime);
    }

    public void Continue()
    {
        if (!IsBusy) return;
        if (!_typewriter.IsComplete)
        {
            _typewriter.SkipToEnd();
            return;
        }

        _messages.Dequeue();
        if (IsBusy) _typewriter.Start(_messages.Peek(), _charsPerSecond);
        else BusyStateChanged?.Invoke(false);
    }
}
```

- [ ] **Step 4: Re-run `StatusMessageQueueTests`**

> **Unity Editor task (user):** Run `Axiom.Tests.UI.StatusMessageQueueTests`. Expected: all tests pass, with no skipped tests.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-123): replace battle history with message queue`
- `Assets/Scripts/Battle/UI/StatusMessageQueue.cs`
- `Assets/Scripts/Battle/UI/StatusMessageQueue.cs.meta`
- `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs`
- `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs.meta`

### Task 2: Bind typewriter progression, Continue focus, and action-menu blocking

**Files:**
- Modify: `Assets/Scripts/Battle/UI/StatusMessageUI.cs`
- Modify: `Assets/Scripts/Battle/UI/ActionMenuUI.cs`
- Create: `Assets/Tests/Editor/UI/ActionMenuUIMessageBlockTests.cs`

- [ ] **Step 1: Write failing action-menu lock tests**

Build the component with five test Buttons and verify `SetMessageBlocked(true)` makes all five non-interactable, a second true call is idempotent, and `SetMessageBlocked(false)` restores each button's exact prior state rather than enabling tutorial-disabled actions. Also verify unblocking focuses the first restored active/interactable button when an EventSystem exists.

- [ ] **Step 2: Run the focused UI tests and verify failure**

> **Unity Editor task (user):** Run `Axiom.Tests.UI.ActionMenuUIMessageBlockTests`. Expected: compile failure because `SetMessageBlocked` does not exist.

- [ ] **Step 3: Add a dedicated reversible message lock to `ActionMenuUI`**

Store the five interactable values only on the false-to-true edge. Disable all five while blocked. Restore the snapshot only on the true-to-false edge, clear the snapshot flag, and then call `FocusFirstInteractable()`. Do not call `SetInteractable(true)` when unblocking because that would overwrite tutorial and spell-phase restrictions.

- [ ] **Step 4: Turn `StatusMessageUI` into lifecycle/wiring only**

Add serialized `_continueButton` and `_charactersPerSecond = 30f`. Construct the plain queue in `Awake`, subscribe to its busy event, add the button listener in `OnEnable`, remove it in `OnDisable`, and call `_queue.Update(Time.deltaTime)` plus `_text.text = _queue.VisibleText` in `Update`. On busy, show/enable Continue and select it through `EventSystem.current`; on idle, hide it. Expose `event Action<bool> BusyStateChanged`, `bool IsBusy`, `Post(string)`, and a public `Continue()` used by the Button. Do not poll Space/Enter directly: the focused uGUI Button already receives Submit from the scene's `InputSystemUIInputModule`.

- [ ] **Step 5: Re-run the focused UI tests**

> **Unity Editor task (user):** Run `Axiom.Tests.UI.StatusMessageQueueTests` and `ActionMenuUIMessageBlockTests`. Expected: all pass.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-123): add battle message progression controls`
- `Assets/Scripts/Battle/UI/StatusMessageUI.cs`
- `Assets/Scripts/Battle/UI/StatusMessageUI.cs.meta`
- `Assets/Scripts/Battle/UI/ActionMenuUI.cs`
- `Assets/Scripts/Battle/UI/ActionMenuUI.cs.meta`
- `Assets/Tests/Editor/UI/ActionMenuUIMessageBlockTests.cs`
- `Assets/Tests/Editor/UI/ActionMenuUIMessageBlockTests.cs.meta` (generated by Unity; do not create manually)

### Task 3: Gate battle progression behind acknowledged messages

**Files:**
- Create: `Assets/Scripts/Battle/BattleMessageFlowGate.cs`
- Create: `Assets/Tests/Editor/Battle/BattleMessageFlowGateTests.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

- [ ] **Step 1: Write failing gate tests**

Test immediate execution while unblocked, `ArgumentNullException` for a null continuation, deferred execution while blocked, FIFO release, no release on repeated `SetBlocked(true)`, and the critical re-entrant case: the first released continuation blocks the gate again, so later continuations remain queued until a later unblock.

```csharp
[Test]
public void SetBlocked_False_StopsDrainingWhenContinuationReblocks()
{
    var gate = new BattleMessageFlowGate();
    var calls = new List<int>();
    gate.SetBlocked(true);
    gate.ContinueWhenReady(() => { calls.Add(1); gate.SetBlocked(true); });
    gate.ContinueWhenReady(() => calls.Add(2));

    gate.SetBlocked(false);
    CollectionAssert.AreEqual(new[] { 1 }, calls);
    gate.SetBlocked(false);
    CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
}
```

- [ ] **Step 2: Run the gate tests and verify failure**

> **Unity Editor task (user):** Run `Axiom.Tests.Battle.BattleMessageFlowGateTests`. Expected: compile failure because the class does not exist.

- [ ] **Step 3: Implement the plain-C# continuation gate**

```csharp
public sealed class BattleMessageFlowGate
{
    private readonly Queue<Action> _continuations = new Queue<Action>();
    public bool IsBlocked { get; private set; }

    public void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
        while (!IsBlocked && _continuations.Count > 0)
            _continuations.Dequeue().Invoke();
    }

    public void ContinueWhenReady(Action continuation)
    {
        if (continuation == null) throw new ArgumentNullException(nameof(continuation));
        if (!IsBlocked) continuation();
        else _continuations.Enqueue(continuation);
    }
}
```

- [ ] **Step 4: Connect `BattleHUD` busy changes without adding turn narration**

During `Setup`, subscribe to `_statusMessageUI.BusyStateChanged`; during `Unsubscribe`, remove it. The handler must call `_actionMenuUI.SetMessageBlocked(isBusy)` and `_battleController.SetBattleMessagesBlocked(isBusy)`. Remove only the two posts `"Your turn."` and `"{enemy}'s turn."`; retain turn-indicator targeting and all meaningful event messages.

- [ ] **Step 5: Route every continuation boundary through the gate**

Instantiate one gate in `BattleController.Initialize` before `_battleHUD.Setup(...)`. Add `SetBattleMessagesBlocked(bool)` as the HUD wiring entry point. At the end of `CompletePlayerAction` and `CompleteEnemyAction`, call `ContinueWhenReady` around the existing flag reset and `BattleManager` completion. At player/enemy turn start, emit condition messages first, then defer Frozen skip completion, enemy execution/morph, or condition-caused defeat transition. In `HandleStateChanged`, keep `Fled` immediate, but defer Victory and Defeat persistence/post-battle flow until narration drains. Preserve the existing animation waits and fallbacks before registering each continuation.

- [ ] **Step 6: Run gate and existing battle-state tests**

> **Unity Editor task (user):** Run the entire `BattleTests` Edit Mode assembly. Expected: all tests pass with no skips. Confirm the re-entrant test proves a newly posted Victory message can re-block terminal flow while earlier action messages are draining.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-123): pause battle flow for unread messages`
- `Assets/Scripts/Battle/BattleMessageFlowGate.cs`
- `Assets/Scripts/Battle/BattleMessageFlowGate.cs.meta` (generated by Unity; do not create manually)
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleController.cs.meta`
- `Assets/Scripts/Battle/UI/BattleHUD.cs`
- `Assets/Scripts/Battle/UI/BattleHUD.cs.meta`
- `Assets/Tests/Editor/Battle/BattleMessageFlowGateTests.cs`
- `Assets/Tests/Editor/Battle/BattleMessageFlowGateTests.cs.meta` (generated by Unity; do not create manually)

### Task 4: Preserve condition identity and narrate applications clearly

**Files:**
- Modify: `Assets/Scripts/Battle/ConditionTurnResult.cs`
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Create: `Assets/Scripts/Battle/BattleMessageFormatter.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`
- Modify: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Create: `Assets/Tests/Editor/Battle/BattleMessageFormatterTests.cs`

- [ ] **Step 1: Write failing per-condition outcome tests**

Extend `CharacterStatsTests` to assert Burning reports one `ConditionDamageTick(Burning, 5)`, Corroded reports its escalated amount, Frozen reports no damage tick plus `ActionSkipped`, no conditions returns an empty tick list, and simultaneous Burning plus Corroded preserves both condition names and amounts while `TotalDamageDealt` equals their sum.

- [ ] **Step 2: Write failing wording tests**

Test exact canonical output:

```csharp
Assert.AreEqual(
    "Void Wraith was Frozen! It will skip its next action.",
    BattleMessageFormatter.ConditionApplied("Void Wraith", ChemicalCondition.Frozen));
Assert.AreEqual(
    "Void Wraith takes 5 damage from Burning.",
    BattleMessageFormatter.ConditionDamage("Void Wraith", ChemicalCondition.Burning, 5));
```

For non-Frozen conditions, use `"{name} was {condition}!"`. Guard null/blank character names with `ArgumentException`, reject `ChemicalCondition.None`, and reject non-positive damage in `ConditionDamage`.

- [ ] **Step 3: Run the focused tests and verify failure**

> **Unity Editor task (user):** Run the new formatter tests and the `ProcessConditionTurn` fixture in `CharacterStatsTests`. Expected: failure because tick identity and formatter APIs do not exist.

- [ ] **Step 4: Add the per-condition result value**

Define `ConditionDamageTick` beside `ConditionTurnResult` with read-only `Condition` and `Damage` properties and a validating constructor. Add `IReadOnlyList<ConditionDamageTick> DamageTicks` to `ConditionTurnResult`. In `ProcessConditionTurn`, create one list per call, append immediately when each DoT amount is computed, preserve `TotalDamageDealt`, and return an empty list—not null—when no DoT fires.

- [ ] **Step 5: Implement `BattleMessageFormatter`**

Create a static plain-C# formatter with only `ConditionApplied(string, ChemicalCondition)` and `ConditionDamage(string, ChemicalCondition, int)`. Keep Frozen's one-time mechanic explanation in `ConditionApplied`; tick messages remain concise.

- [ ] **Step 6: Emit detailed condition events and messages**

Add `OnConditionApplied(CharacterStats, ChemicalCondition)` to `BattleController`. After `SpellEffectResolver.Resolve`, invoke it only when `result.ConditionApplied != ChemicalCondition.None`, passing `_enemyStats` for Damage spells and `_playerStats` for Heal/Shield spells to match `SpellEffectResolver`'s effect-target rule. Replace each aggregate `OnConditionDamageTick(..., None)` call with one event per `result.DamageTicks`. In `BattleHUD`, subscribe/unsubscribe to the new event and use the formatter for both application and tick text. Keep `OnConditionsChanged` responsible only for badge refresh. Keep the existing Frozen action-skip message, because it reports the later skip; the application message is the one-time explanation.

- [ ] **Step 7: Run all Battle and UI Edit Mode tests**

> **Unity Editor task (user):** Run the `BattleTests` and `UITests` assemblies. Expected: all pass, none skipped.

- [ ] **Step 8: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-123): narrate battle conditions by name`
- `Assets/Scripts/Battle/ConditionTurnResult.cs`
- `Assets/Scripts/Battle/ConditionTurnResult.cs.meta`
- `Assets/Scripts/Battle/CharacterStats.cs`
- `Assets/Scripts/Battle/CharacterStats.cs.meta`
- `Assets/Scripts/Battle/BattleMessageFormatter.cs`
- `Assets/Scripts/Battle/BattleMessageFormatter.cs.meta` (generated by Unity; do not create manually)
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleController.cs.meta`
- `Assets/Scripts/Battle/UI/BattleHUD.cs`
- `Assets/Scripts/Battle/UI/BattleHUD.cs.meta`
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs.meta`
- `Assets/Tests/Editor/Battle/BattleMessageFormatterTests.cs`
- `Assets/Tests/Editor/Battle/BattleMessageFormatterTests.cs.meta` (generated by Unity; do not create manually)

### Task 5: Reconfigure the Battle scene narration panel

**Files:**
- Modify: `Assets/Scenes/Battle.unity`

- [ ] **Step 1: Import new scripts before scene wiring**

> **Unity Editor task (user):** Open the project and wait for Unity to finish importing/compiling. Confirm the Console has no compile errors and Unity generated `.meta` files for all new scripts/tests. Do not create `.meta` files manually.

- [ ] **Step 2: Adapt the existing MessageLog area**

> **Unity Editor task (user):** In `Assets/Scenes/Battle.unity`, reuse `Battle Canvas/MessageLog` and `MessageLogBG`. Resize the narration region enough for two wrapped lines, keep one TMP message field, and reserve stable space for a Continue button so text does not shift when the button appears. Do not add a scroll view or persistent history.

- [ ] **Step 3: Add and wire the dedicated Continue button**

> **Unity Editor task (user):** Add a uGUI Button named `MessageContinueButton` under the MessageLog panel, label it `Continue`, make it visibly selectable, and assign it to `StatusMessageUI._continueButton`. Leave its OnClick list empty because `StatusMessageUI` wires it at runtime. Confirm the existing TMP field remains assigned to `_text` and set `_charactersPerSecond` to `30`.

- [ ] **Step 4: Verify navigation**

> **Unity Editor task (user):** Confirm the scene's existing EventSystem and `InputSystemUIInputModule` can Submit the focused Continue button with keyboard/controller. In Play Mode, verify Continue gains focus when the first message appears and the first currently valid action regains focus after the final acknowledgment.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-123): configure battle narration panel`
- `Assets/Scenes/Battle.unity`
- Unity-generated `.meta` files for the new scripts/tests from Tasks 2–4

### Task 6: End-to-end acceptance verification

- [ ] **Step 1: Run the full Edit Mode suite**

> **Unity Editor task (user):** Test Runner → EditMode → Run All. Expected: every test passes; report any skipped test rather than claiming full success.

- [ ] **Step 2: Verify player-action sequencing in Play Mode**

> **Unity Editor task (user):** Attack once. Confirm animation/hit resolution finishes, the attack/damage message reveals one character stream at a time, actions remain visible but disabled, first Continue completes reveal, second advances, and EnemyTurn does not begin until the queue drains.

- [ ] **Step 3: Verify enemy and Frozen sequencing**

> **Unity Editor task (user):** Let an enemy attack and confirm PlayerTurn does not begin until its messages drain. Apply Frozen and confirm the application says it will skip the next action; on the affected turn, confirm the skip is narrated and the following turn does not begin until acknowledged.

- [ ] **Step 4: Verify multiple conditions and terminal flow**

> **Unity Editor task (user):** Exercise two queued messages and at least one Burning/Corroded tick. Confirm FIFO ordering, no overlap, and condition names in tick text. Finish a battle and confirm defeat/victory narration is acknowledged before post-battle transition UI or scene flow proceeds.

- [ ] **Step 5: Final UVCS audit**

> **Unity Editor task (user):** In Pending Changes, confirm only DEV-123 files are included, every new script/test has its Unity-generated `.meta`, `Battle.unity` is present, and no unrelated scene/prefab changes are staged. Check in any verification-only correction with `fix(DEV-123): correct battle message sequencing`.
