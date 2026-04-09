# DEV-25: Voice System Fallback & Edge Cases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Patch three code gaps in the voice spell system so all DEV-25 acceptance-criteria scenarios produce correct UI feedback, correct animator state, and no stuck turn states.

**Architecture:** Three targeted changes to existing MonoBehaviours — (1) `BattleVoiceBootstrap` checks mic availability and disables the Spell button on any voice-init failure; (2) `BattleController` adds an `OnSpellChargeAborted` event that resets `IsCharging` on PTT release without a spell match; (3) `BattleController` adds a timeout coroutine so the turn always advances even if `AnimEvent_OnSpellFire` is missing from the animation clip.

**Tech Stack:** Unity 6 LTS · C# · UnityEngine.UI (Button) · Axiom.Battle · Axiom.Voice

---

## Already Satisfied — No Code Needed

The following AC items are correctly handled by the existing implementation. Verified by reading source.

| AC item | How it is handled |
|---------|------------------|
| PTT released before speech | `StopCapture()` → `RequestFinalResult()` → `{"text":""}` → `SpellResultMatcher.Match` returns null → no dispatch → `_isAwaitingVoiceSpell` stays `true` — player retries freely |
| Recognized word not in spell list | `SpellCastController.Update()` calls `NotifySpellNotRecognized()` → `OnSpellNotRecognized` event → `SpellInputUI` shows "Not recognized. Try again." — player retries |
| Vosk result dequeued outside player-turn | `BattleController.OnSpellCast` and `NotifySpellNotRecognized` both guard `if (_battleManager.CurrentState != BattleState.PlayerTurn) return` |
| Insufficient MP | `BattleController.OnSpellCast` fires `OnSpellCastRejected`; `SpellInputUI.HandleSpellCastRejected` shows rejection panel and hides listening state; turn not consumed |

---

## Remaining Gaps

| AC item | Root cause |
|---------|-----------|
| **Vosk model not found / no mic → Spell button must be disabled** | `BattleVoiceBootstrap.Start()` logs errors but never calls any UI method; player can click Spell and enter a permanently stuck `_isAwaitingVoiceSpell = true` state with no way out |
| **PTT released during charge → `IsCharging` must be reset** | `SpellCastController` silently discards empty Vosk results (`{"text":""}`); `PlayerBattleAnimator` `IsCharging` stays `true` — player sprite shows charging animation indefinitely |
| **`AnimEvent_OnSpellFire` never fires → turn is stuck** | When `_animationService != null`, `FireSpellVisuals()` depends entirely on the animation event callback; no timeout coroutine exists — `_pendingSpell` is never cleared and `CompletePlayerAction()` is never called |

---

## File Map

| File | Change |
|------|--------|
| `Assets/Scripts/Battle/UI/ActionMenuUI.cs` | Add `SetSpellInteractable(bool)` — disables the Spell button independently of the other three buttons |
| `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` | Add `[SerializeField] ActionMenuUI _actionMenuUI`; add `DisableSpell()` helper; call it on every early-exit (no mic, model missing, load failed, recognizer failed, empty spell list) |
| `Assets/Scripts/Battle/PlayerBattleAnimator.cs` | Add `TriggerResetCharge()` — sets `IsCharging = false` on the Animator |
| `Assets/Scripts/Battle/BattleController.cs` | Add `OnSpellChargeAborted` event; add `NotifyVoiceResultEmpty()` method; fire `OnSpellChargeAborted` from `NotifySpellNotRecognized()`; wire/unwire `OnSpellChargeAborted` in `Initialize()` and `OnDestroy()`; add `_spellFireTimeout` field, `_spellFireTimeoutCoroutine` field, `SpellFireTimeoutCoroutine()` coroutine; start coroutine in `OnSpellCast()` when `_animationService != null`; cancel coroutine at top of `FireSpellVisuals()` |
| `Assets/Scripts/Voice/SpellCastController.cs` | In `Update()`: call `_battleController.NotifyVoiceResultEmpty()` when dequeued result has empty text (currently silently skipped) |

> **Why no new Edit Mode tests?** All new logic lives in MonoBehaviour methods (`BattleController`, `BattleVoiceBootstrap`, `SpellCastController`). These depend on Unity lifecycle and Animator components that cannot be exercised outside a scene. The acceptance criteria are verified via the manual test matrix in Task 4.

---

## Task 1 — Spell button disabled when voice init fails

**Files:**
- Modify: `Assets/Scripts/Battle/UI/ActionMenuUI.cs`
- Modify: `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`

---

- [ ] **Add `SetSpellInteractable(bool)` to `ActionMenuUI`**

Open `Assets/Scripts/Battle/UI/ActionMenuUI.cs`. Add this method immediately after `SetInteractable`:

```csharp
/// <summary>
/// Enables or disables only the Spell button independently of the other three actions.
/// Call with false when voice recognition is unavailable (model missing, no mic device).
/// </summary>
public void SetSpellInteractable(bool interactable)
{
    _spellButton.interactable = interactable;
}
```

---

- [ ] **Add `using Axiom.Battle;` import to `BattleVoiceBootstrap`**

Open `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`. Add this import alongside the existing `using Axiom.Data;` line:

```csharp
using Axiom.Battle;
using Axiom.Data;
```

---

- [ ] **Add `_actionMenuUI` field to `BattleVoiceBootstrap`**

Open `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`. Add this field after `_sampleRate`:

```csharp
[SerializeField]
[Tooltip("ActionMenuUI in the Battle scene. Assign to disable the Spell button when voice is unavailable.")]
private ActionMenuUI _actionMenuUI;
```

---

- [ ] **Replace `BattleVoiceBootstrap.Start()` and add `DisableSpell()`**

Replace the entire `Start()` coroutine body and add a `DisableSpell()` helper at the bottom of the class (before `OnDestroy`). The complete `Start()` and `DisableSpell()` methods should read:

```csharp
private IEnumerator Start()
{
    if (Microphone.devices.Length == 0)
    {
        Debug.LogWarning("[BattleVoiceBootstrap] No microphone device detected — Spell button disabled.", this);
        DisableSpell();
        yield break;
    }

    string modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);

    if (!Directory.Exists(modelPath))
    {
        Debug.LogError(
            $"[BattleVoiceBootstrap] Vosk model not found at: {modelPath}\n" +
            "Place vosk-model-en-us-0.22-lgraph inside StreamingAssets/VoskModels/.", this);
        DisableSpell();
        yield break;
    }

    Task<Model> modelTask = Task.Run(() => new Model(modelPath));
    yield return new WaitUntil(() => modelTask.IsCompleted);

    if (modelTask.IsFaulted)
    {
        Debug.LogError(
            $"[BattleVoiceBootstrap] Failed to load Vosk model: " +
            $"{modelTask.Exception?.InnerException?.Message}", this);
        DisableSpell();
        yield break;
    }

    SpellData[] spells = _unlockedSpells ?? Array.Empty<SpellData>();

    Task<VoskRecognizer> recognizerTask =
        SpellVocabularyManager.RebuildRecognizerAsync(modelTask.Result, _sampleRate, spells);
    yield return new WaitUntil(() => recognizerTask.IsCompleted);

    if (recognizerTask.IsFaulted)
    {
        modelTask.Result.Dispose();
        Debug.LogError(
            $"[BattleVoiceBootstrap] Failed to build Vosk recognizer: " +
            $"{recognizerTask.Exception?.InnerException?.Message}", this);
        DisableSpell();
        yield break;
    }

    if (recognizerTask.Result == null)
    {
        Debug.LogWarning(
            "[BattleVoiceBootstrap] Spell list is empty — voice recognition not started.\n" +
            "Assign at least one SpellData asset to the Unlocked Spells field.", this);
        DisableSpell();
        yield break;
    }

    var inputQueue  = new ConcurrentQueue<short[]>();
    var resultQueue = new ConcurrentQueue<string>();

    _recognizerService = new VoskRecognizerService(recognizerTask.Result, inputQueue, resultQueue);
    _recognizerService.Start();

    _microphoneInputHandler.Inject(inputQueue, _recognizerService);
    _spellCastController.Inject(resultQueue, spells);

    Debug.Log("[BattleVoiceBootstrap] Vosk pipeline ready.");
}

private void DisableSpell()
{
    _actionMenuUI?.SetSpellInteractable(false);
}
```

---

- [ ] **Wire Inspector reference (Unity Editor task)**

> **Unity Editor task (user):** Open the Battle scene. Select the GameObject that holds `BattleVoiceBootstrap`. In the Inspector, assign the `ActionMenuUI` component from the Battle Canvas to the new **Action Menu UI** field.

---

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-25): disable spell button when voice init fails`
  - `Assets/Scripts/Battle/UI/ActionMenuUI.cs`
  - `Assets/Scripts/Battle/UI/ActionMenuUI.cs.meta`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs.meta`

---

## Task 2 — Charge animation reset on PTT release without spell match

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Voice/SpellCastController.cs`

---

- [ ] **Add `TriggerResetCharge()` to `PlayerBattleAnimator`**

Open `Assets/Scripts/Battle/PlayerBattleAnimator.cs`. Add this method immediately after `TriggerCast()`:

```csharp
/// <summary>
/// Resets the charge animation when the voice spell phase ends without a cast.
/// Called by BattleController via the OnSpellChargeAborted event.
/// </summary>
public void TriggerResetCharge() => _animator.SetBool(IsChargingHash, false);
```

---

- [ ] **Add `OnSpellChargeAborted` event to `BattleController`**

Open `Assets/Scripts/Battle/BattleController.cs`. In the UI Events region, add this after `OnSpellCastRejected`:

```csharp
/// <summary>
/// Fires when the voice spell phase ends without a cast being dispatched —
/// either because Vosk returned empty text (silent PTT release) or because
/// the recognized word did not match any unlocked spell.
/// <see cref="PlayerBattleAnimator"/> subscribes via <see cref="Initialize"/> to reset IsCharging.
/// </summary>
public event Action OnSpellChargeAborted;
```

---

- [ ] **Add `NotifyVoiceResultEmpty()` to `BattleController`**

Add this method immediately after `NotifySpellNotRecognized()`:

```csharp
/// <summary>
/// Called by <see cref="Axiom.Voice.SpellCastController"/> when Vosk returns a final
/// result with empty text (e.g. PTT released without speaking). Resets the charge
/// animation if the player is still in the voice spell phase.
/// No-op outside the voice spell phase or outside PlayerTurn.
/// </summary>
public void NotifyVoiceResultEmpty()
{
    if (!_isAwaitingVoiceSpell) return;
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    OnSpellChargeAborted?.Invoke();
}
```

---

- [ ] **Fire `OnSpellChargeAborted` from `NotifySpellNotRecognized()`**

In `BattleController.NotifySpellNotRecognized()`, add one line after `OnSpellNotRecognized?.Invoke()`. The complete method should read:

```csharp
public void NotifySpellNotRecognized()
{
    if (!_isAwaitingVoiceSpell) return;
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    OnSpellNotRecognized?.Invoke();
    OnSpellChargeAborted?.Invoke();
}
```

---

- [ ] **Unwire `OnSpellChargeAborted` in `BattleController.Initialize()` cleanup block**

The cleanup section at the top of `Initialize()` ends with `if (_battleManager != null) _battleManager.OnStateChanged -= HandleStateChanged;`. Add the charge-abort unwire **after** that line (not inside any `if` block):

```csharp
if (_battleManager != null)
    _battleManager.OnStateChanged -= HandleStateChanged;

if (_playerAnimator != null)
    OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;
```

---

- [ ] **Wire `OnSpellChargeAborted` in `BattleController.Initialize()` wiring block**

Inside the `if (_playerAnimator != null && _enemyAnimator != null)` block, add one line at the very end (after `_playerAnimator.OnSpellFireFrame += FireSpellVisuals;`):

```csharp
OnSpellChargeAborted += _playerAnimator.TriggerResetCharge;
```

---

- [ ] **Unwire `OnSpellChargeAborted` in `BattleController.OnDestroy()`**

Add this line to `OnDestroy()`, alongside the other `_playerAnimator` unwires:

```csharp
if (_playerAnimator != null) OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;
```

---

- [ ] **Update `SpellCastController.Update()` to call `NotifyVoiceResultEmpty()` for empty results**

Open `Assets/Scripts/Voice/SpellCastController.cs`. Replace the no-match block inside `Update()`:

**Old:**
```csharp
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
```

**New:**
```csharp
if (matched == null)
{
    if (_battleController == null) { continue; }

    // VoskRecognizerService only enqueues Result() and FinalResult() — both have a
    // "text" field. Non-empty text → word heard but not in spell list (not recognized).
    // Empty text → PTT released without any speech (silent release).
    string recognized = SpellResultMatcher.ExtractTextField(voskJson);
    if (!string.IsNullOrWhiteSpace(recognized))
        _battleController.NotifySpellNotRecognized();
    else
        _battleController.NotifyVoiceResultEmpty();
    continue;
}
```

---

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-25): reset charge anim when PTT released without spell match`
  - `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
  - `Assets/Scripts/Battle/PlayerBattleAnimator.cs.meta`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`
  - `Assets/Scripts/Voice/SpellCastController.cs`
  - `Assets/Scripts/Voice/SpellCastController.cs.meta`

---

## Task 3 — AnimEvent_OnSpellFire timeout fallback

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

---

- [ ] **Add timeout fields to `BattleController`**

In the private fields region, add after `_pendingSpellResult`:

```csharp
[SerializeField]
[Tooltip("Seconds to wait for AnimEvent_OnSpellFire before forcing spell resolution. " +
         "Set to a value greater than your longest cast animation clip length.")]
private float _spellFireTimeout = 3f;

private Coroutine _spellFireTimeoutCoroutine;
```

---

- [ ] **Cancel timeout at the top of `FireSpellVisuals()`**

Replace the opening of `FireSpellVisuals()`:

**Old first two lines:**
```csharp
private void FireSpellVisuals()
{
    if (_pendingSpell == null) return;
```

**New:**
```csharp
private void FireSpellVisuals()
{
    // Cancel the safety-net timeout — either the animation event fired on time,
    // or the timeout itself called us. Either way the coroutine is no longer needed.
    if (_spellFireTimeoutCoroutine != null)
    {
        StopCoroutine(_spellFireTimeoutCoroutine);
        _spellFireTimeoutCoroutine = null;
    }

    if (_pendingSpell == null) return;
```

---

- [ ] **Start timeout coroutine in `OnSpellCast()` when animators are wired**

In `BattleController.OnSpellCast()`, replace:

```csharp
if (_animationService == null)
{
    FireSpellVisuals();
}
// else: FireSpellVisuals() is called by the OnSpellFireFrame animation event.
```

With:

```csharp
if (_animationService == null)
{
    FireSpellVisuals();
}
else
{
    // Safety net: if AnimEvent_OnSpellFire never fires (missing animation event or
    // clip interrupted), resolve spell visuals after the timeout so the turn advances.
    _spellFireTimeoutCoroutine = StartCoroutine(SpellFireTimeoutCoroutine());
}
```

---

- [ ] **Add `SpellFireTimeoutCoroutine()`**

Add this private coroutine anywhere in `BattleController`'s private section:

```csharp
private System.Collections.IEnumerator SpellFireTimeoutCoroutine()
{
    yield return new WaitForSeconds(_spellFireTimeout);
    _spellFireTimeoutCoroutine = null;
    Debug.LogWarning(
        "[Battle] AnimEvent_OnSpellFire did not fire within timeout — resolving spell via fallback.",
        this);
    FireSpellVisuals();
}
```

---

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-25): add timeout fallback for AnimEvent_OnSpellFire`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`

---

## Task 4 — Manual test matrix

Run each scenario in Play Mode in the Battle scene. Restore any temporarily modified values after testing.

| # | Scenario | Setup | Expected result |
|---|----------|-------|-----------------|
| 1 | No Vosk model | Rename `StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph` to `…-MISSING` → Enter Play Mode | Spell button is disabled (greyed out). No NRE in Console. |
| 2 | No microphone device | Disconnect or disable mic → Enter Play Mode | Spell button is disabled. No NRE in Console. |
| 3 | PTT released without speaking | Click Spell → hold PTT → release without speaking | `SpellInputUI` returns to PromptVisible. Player sprite returns to Idle (`IsCharging = false`). No error logged. Turn not consumed. |
| 4 | Recognized word not in spell list | Click Spell → PTT → say "hello world" → release | "Not recognized. Try again." shown. Player sprite returns to Idle. Turn not consumed. |
| 5 | Normal successful spell cast | Normal voice spell | Spell resolves. Turn advances. No "[Battle] AnimEvent_OnSpellFire did not fire" warning in Console. |
| 6 | `AnimEvent_OnSpellFire` missing | Temporarily remove the `AnimEvent_OnSpellFire` animation event from the cast clip in the Animator → cast a spell | After 3 seconds: "[Battle] AnimEvent_OnSpellFire did not fire within timeout" warning logged. Spell resolves. Turn advances normally. |
| 7 | Insufficient MP | Set player MP to 0 in the Inspector → attempt a spell cast | Cast rejected. SpellInputUI shows rejection message then hides. Turn not consumed. No stuck state. |
| 8 | Vosk result during enemy turn | Speak during the enemy's turn | Result is discarded. No out-of-turn spell dispatch. No NRE. |

> After test #1, restore the model folder name before testing #2–8.
> After test #6, restore the animation event before shipping.
