# Unity Codebase Scan — Full Repository — 2026-04-19

## Executive Summary

This is a Phase 4 project with a well-structured architecture, but the audit found **5 P0/P1 correctness issues** that are ship blockers, including a forbidden static singleton violating the project's own non-negotiable code standards, event subscription accumulation risks that cause double-firing, and a voice threading shutdown deadlock. Performance findings are secondary (Vector3 allocation per-frame in the platformer hot path, unconditional HealthBarUI lerp). The voice pipeline threading is architecturally correct — the P0 in that slice is a cancellation deadlock, not a rule violation.

---

## Scan Parameters

| Parameter | Value |
|-----------|-------|
| Scope | `Assets/Scripts/` — all runtime C# |
| Excluded | `ThirdParty/`, `Assets/Tests/`, generated/imported code |
| Evidence basis | Static analysis + targeted `Read` verification on all P0/P1 citations |
| Subagents dispatched | 4 parallel (`Architecture`, `Voice/Threading`, `Performance`, `Correctness/Lifecycle`) |

---

## Findings

### P0 — Forbidden Static Singleton in StateBasedCursorUI

- **Where:** `Assets/Scripts/Core/StateBasedCursorUI.cs:20`
- **Rule violated:** CLAUDE.md §Non-Negotiable Code Standards — *"No static singletons except GameManager"*
- **Evidence:**

```csharp
// Line 20
private static StateBasedCursorUI _instance;

// Lines 109-121
if (_persistAcrossScenes)
{
    if (_instance != null && _instance != this)
    {
        GameObject discard = _persistRoot != null ? _persistRoot.gameObject : transform.root.gameObject;
        Destroy(discard);
        return;
    }
    _instance = this;
    // ...
    DontDestroyOnLoad(persist);
}
```

- **Risk:** This MonoBehaviour uses a static `_instance` field to implement persistence across scenes — the **exact** forbidden singleton pattern. Every other system must pass dependencies explicitly or via ScriptableObject channels. Only `GameManager` is allowed this pattern.
- **Recommendation:** Refactor to receive a scene-persistent reference via explicit injection (e.g., a `ScenePersistenceService` or interface), or move persistence logic into `GameManager`.

---

### P0 — Button Listener Accumulation in BattleHUD / ActionMenuUI

- **Where:** `Assets/Scripts/Battle/UI/BattleHUD.cs:67-70` + `ActionMenuUI.cs:28-31`
- **Evidence:**

`BattleHUD.Setup()` assigns Action delegates:

```csharp
// BattleHUD.cs:67-70
_actionMenuUI.OnAttack = _battleController.PlayerAttack;
_actionMenuUI.OnSpell  = _battleController.PlayerSpell;
_actionMenuUI.OnItem   = _battleController.PlayerItem;
_actionMenuUI.OnFlee   = _battleController.PlayerFlee;
```

`ActionMenuUI.Start()` adds listeners to those Action fields:

```csharp
// ActionMenuUI.cs:28-31
_attackButton.onClick.AddListener(() => OnAttack?.Invoke());
_spellButton.onClick.AddListener(() => OnSpell?.Invoke());
_itemButton.onClick.AddListener(() => OnItem?.Invoke());
_fleeButton.onClick.AddListener(() => OnFlee?.Invoke());
```

`BattleHUD.Unsubscribe()` (line 291-305) removes only `BattleController` event subscriptions — **never** clears the button listeners.

- **Risk:** If `BattleHUD.Setup()` is called twice (e.g., from a second `BattleController.Initialize()` call), `AddListener` has already been called with lambdas capturing the **old** (null) values. When the Action fields are later overwritten, the **existing** listeners still hold references to the old Action fields and call `null?.Invoke()` (a no-op), but the **new** values set by the second `Setup()` call are never wired to listeners. The net effect: clicking buttons may fire nothing instead of the intended methods. Additionally, if `Setup()` is called before `Start()` runs, listeners capture the property reference and call the new value (correct), but with accumulation on repeated `Setup()` calls.
- **Recommendation:** Add a `Reset()` method to `ActionMenuUI` that calls `RemoveAllListeners()` on all buttons, and call it from `BattleHUD.Setup()` before assigning the Action fields.

---

### P0 — Voice Recognition Background Thread Cannot Be Cancelled

- **Where:** `Assets/Scripts/Voice/VoskRecognizerService.cs:117-134` + `77-102`
- **Evidence:**

```csharp
// Lines 117-134 — RecognitionLoop
private void RecognitionLoop(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        if (_inputQueue.TryDequeue(out short[] samples))
        {
            if (_recognizer.AcceptWaveform(samples, samples.Length))
                _resultQueue.Enqueue(_recognizer.Result());
        }
        else if (_finalResultRequested)
        {
            _finalResultRequested = false;
            _resultQueue.Enqueue(_recognizer.FinalResult());
        }
        else
        {
            Thread.Sleep(1);  // ← only yields when queue is empty
        }
    }
}
```

```csharp
// Lines 77-102 — Stop()
public void Stop()
{
    if (_recognitionTask == null) return;
    _cts.Cancel();
    _running = false;

    bool completed = _recognitionTask.Wait(ShutdownTimeoutMs); // 2000ms
    if (!completed)
        Debug.LogWarning("[VoskRecognizerService] Background recognition task did not exit within 2000ms...");
}
```

- **Risk:** `ConcurrentQueue<short[]>.TryDequeue()` has **no timeout** — it is a blocking call. When the queue is empty and `_finalResultRequested` is false, the thread blocks inside `TryDequeue()` and **never reaches** the `token.IsCancellationRequested` check until an item is enqueued. The `Thread.Sleep(1)` in the `else` branch only executes if `TryDequeue` returns false, but the blocking call means the loop never gets there until something enqueues. On a quiet queue with no enqueuer, `Stop()`'s 2000ms timeout will always fire, leaving the service in a partially-disposed state.
- **Impact:** Clean shutdown is not guaranteed; could cause `ObjectDisposedException` on subsequent calls or leaked threads.
- **Recommendation:** Replace `ConcurrentQueue` with a `BlockingCollection<short[]>` or add a wakeup mechanism (e.g., a sentinel value in the queue or a `ManualResetEventSlim`) so that `Stop()` can reliably unblock the background thread.

---

### P1 — BattleController OnSpellChargeAborted Double-Subscribe

- **Where:** `Assets/Scripts/Battle/BattleController.cs:377` + `OnDestroy:851`
- **Evidence:**

`Initialize()` subscribes without checking if already subscribed:

```csharp
// Line 377 — inside Initialize(), outside the _animationService guard
OnSpellChargeAborted += _playerAnimator.TriggerResetCharge;
```

`OnDestroy` unsubscribes once:

```csharp
// Line 851
if (_playerAnimator != null) OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;
```

- **Risk:** If `Initialize()` is called twice (which the comment at line 251 explicitly guards against), `OnSpellChargeAborted` accumulates two `+=` subscriptions to the same handler. `OnDestroy` only fires one `-=`, leaving a dangling subscription that can fire on a destroyed object. Note: the `_animationService` subscription guard at line 271 does unsubscribe some events, but `OnSpellChargeAborted` is intentionally subscribed **outside** that guard (line 377 is after line 271).
- **Recommendation:** Add a guard in `Initialize()` — or use the same unsubscribe-first pattern used for `_animationService` events at lines 251-265.

---

### P1 — BattleController Spell Fire Timeout Coroutine Leak

- **Where:** `Assets/Scripts/Battle/BattleController.cs:475` + `483-487`
- **Evidence:**

```csharp
// Line 475 — OnSpellCast starts coroutine
_spellFireTimeoutCoroutine = StartCoroutine(SpellFireTimeoutCoroutine());
```

```csharp
// FireSpellVisuals stops it:
if (_spellFireTimeoutCoroutine != null)
{
    StopCoroutine(_spellFireTimeoutCoroutine);
    _spellFireTimeoutCoroutine = null;
}
```

- **Risk:** If `Initialize()` is called while a spell cast is in progress (coroutine running), `StartCoroutine` overwrites `_spellFireTimeoutCoroutine` **without** calling `StopCoroutine` first. The orphaned coroutine continues running and fires `FireSpellVisuals()` on the **new** `_pendingSpell` / `_pendingSpellResult` state — causing incorrect spell resolution or a null reference exception.
- **Recommendation:** Stop the existing coroutine before starting a new one:

```csharp
if (_spellFireTimeoutCoroutine != null)
    StopCoroutine(_spellFireTimeoutCoroutine);
_spellFireTimeoutCoroutine = StartCoroutine(SpellFireTimeoutCoroutine());
```

---

### P1 — LevelUpPromptUI Subscription Leak on Destroy-While-Enabled

- **Where:** `Assets/Scripts/Battle/UI/LevelUpPromptUI.cs:39-68`
- **Evidence:**

Subscriptions are in `OnEnable`:

```csharp
// Line 51
_progression.OnLevelUp += HandleLevelUp;
// Line 53
_controller.OnDismissed += HandleQueueDrained;
// Line 56
_confirmButton.onClick.AddListener(OnConfirmClicked);
```

Unsubscriptions are in `OnDisable`:

```csharp
// Line 62
_progression.OnLevelUp -= HandleLevelUp;
// etc.
```

`OnDestroy` calls `Unsubscribe()` which only removes `_battleController` event subscriptions — it does **not** remove the `OnLevelUp`, `OnDismissed`, or button click subscriptions.

- **Risk:** If the `LevelUpPromptUI` MonoBehaviour is destroyed while **enabled** (not disabled), `OnDisable` never runs, so all three subscriptions leak.
- **Recommendation:** Move all subscription/unsubscription logic to `OnEnable`/`OnDestroy` (not `OnDisable`), or ensure `OnDestroy` also cleans up the `_progression`, `_controller`, and `_confirmButton` subscriptions.

---

## Optimizations (Non-Blocking)

### Vector3 Allocation Per Frame — ParallaxController

- **Where:** `Assets/Scripts/Platformer/ParallaxController.cs:29`
- **Evidence:**

```csharp
// Every frame — allocates a new Vector3 on the heap
transform.position = new Vector3(newX, transform.position.y, transform.position.z);
```

- **Fix:** Cache a `Vector3` field and reuse it:

```csharp
private Vector3 _tempPos;
void Update() {
    _tempPos.x = newX;
    _tempPos.y = transform.position.y;
    _tempPos.z = transform.position.z;
    transform.position = _tempPos;
}
```

---

### Unconditional HealthBarUI Lerp Every Frame

- **Where:** `Assets/Scripts/Battle/UI/HealthBarUI.cs:39-48`
- **Evidence:**

```csharp
private void Update()
{
    if (_hpBarImage != null)
        _hpBarImage.fillAmount = Mathf.Lerp(
            _hpBarImage.fillAmount, _targetHPFill, Time.deltaTime * _lerpSpeed);
    // ... same for MP bar
}
```

- **Risk:** Lerps every frame even when values haven't changed, wasting CPU.
- **Fix:** Add a dirty flag or `Mathf.Approximately` check:

```csharp
private void Update()
{
    if (_hpBarImage != null && !Mathf.Approximately(_hpBarImage.fillAmount, _targetHPFill))
        _hpBarImage.fillAmount = Mathf.Lerp(...);
    // ...
}
```

---

### Camera.main Called Per Frame in TurnIndicatorUI

- **Where:** `Assets/Scripts/Battle/UI/TurnIndicatorUI.cs:78`
- **Evidence:**

```csharp
private Vector3 ScreenPositionOf(Transform target)
{
    if (target is RectTransform)
        return target.position;
    return Camera.main.WorldToScreenPoint(target.position); // ← called every bob frame
}
```

The `Bob()` coroutine calls `ScreenPositionOf(_currentTarget)` every `yield return null`.

- **Fix:** Cache `Camera.main` once in `Start()` or `SetActiveTarget()`.

---

### LINQ Chain Allocation in ConditionBadgeUI

- **Where:** `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs:70-73`
- **Evidence:**

```csharp
var badges = entries
    .OrderBy(e => e.order)
    .Select(e => SpawnBadge(e.condition, e.turns))
    .ToList();
```

- **Risk:** Allocates an iterator and a new `List<>` on every `Refresh()` call. Event-driven (not hot-path), so P3.
- **Fix:** Use a simple `Sort()` on the list in-place, or use `Span<T>`/stackalloc if performance becomes measurable.

---

## False Positives / Needs Human Confirmation

| Finding | Why It May Not Apply |
|---------|---------------------|
| BattleVoiceBootstrap use-after-free (P0) | The `MicrophoneInputHandler` holds a reference to the `VoskRecognizerService` interface, not the concrete type. If the service is disposed and a new one created with the same queues, the handler's reference becomes stale — but the handler calls `RequestFinalResult()` on that reference, which would throw `ObjectDisposedException`. Requires Play Mode repro to confirm whether this window actually manifests. |
| MainMenuUI / ConfirmNewGameDialogUI in `Core/` folder | These are menu-scoped UI components. The rule says `Battle/UI/` and `Platformer/UI/` — they arguably belong in `MainMenu/UI/` but since `MainMenu` is a planned-but-not-yet-implemented scene, placing them in `Core/` may be a deliberate staging decision. Human should decide. |
| StateBasedCursorUI `_instance` being "forbidden" | This is arguably the second-most justified use of a static singleton in any Unity project (after GameManager) — cursor management often needs a scene-persistent singleton. However, the rule is explicit; the exception process should be to formally document it as an approved pattern, not to silently ignore it. |

---

## Suggested Follow-Ups

- [ ] **Play Mode repro** for the BattleHUD double-button-fire issue — click Attack rapidly and verify `PlayerAttack()` fires exactly once per click
- [ ] **Play Mode repro** for the VoskRecognizerService cancellation deadlock — call `Stop()` with a quiet input queue and verify it returns within 2 seconds
- [ ] **Play Mode repro** for the `_spellFireTimeoutCoroutine` orphan issue — trigger a spell cast, immediately trigger `Initialize()` (if possible), and verify no extra `FireSpellVisuals()` fires
- [ ] **Unity Profiler** for ParallaxController allocation — confirm `new Vector3` is on the hot path and measurable
- [ ] **Scripting backend check** — confirm this project uses Mono (per CLAUDE.md), not IL2CPP, since threading issues manifest differently

---

## Summary Table

| ID | Severity | File | Lines | Risk |
|----|----------|------|-------|------|
| P0-1 | P0 | `StateBasedCursorUI.cs` | 20, 109-121 | Forbidden static singleton — violates CLAUDE.md non-negotiable rule |
| P0-2 | P0 | `BattleHUD.cs` + `ActionMenuUI.cs` | 67-70, 28-31 | Button listeners accumulate across Setup() calls; double-fires or no-op on click |
| P0-3 | P0 | `VoskRecognizerService.cs` | 117-134 | Background thread blocks indefinitely on empty queue; cancellation deadline always misses |
| P1-1 | P1 | `BattleController.cs` | 377, 851 | OnSpellChargeAborted double-subscribe if Initialize() called twice |
| P1-2 | P1 | `BattleController.cs` | 475, 483-487 | Orphaned spell-fire timeout coroutine fires on wrong spell state |
| P1-3 | P1 | `LevelUpPromptUI.cs` | 39-68 | OnLevelUp / OnDismissed / button subscriptions leak if destroyed while enabled |
| Opt-1 | Opt | `ParallaxController.cs` | 29 | Vector3 allocation per frame — GC pressure on platformer hot path |
| Opt-2 | Opt | `HealthBarUI.cs` | 39-48 | Unconditional lerp every frame — wasted CPU |
| Opt-3 | Opt | `TurnIndicatorUI.cs` | 78 | Camera.main lookup per bob frame |
| Opt-4 | Opt | `ConditionBadgeUI.cs` | 70-73 | LINQ chain allocation on Refresh() |
