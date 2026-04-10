# Design: Charge Animation Fix & Cancel Spell

**Date:** 2026-04-09
**Scope:** Fix player sprite stuck in Charging animation after aborted voice spell; add Esc-to-cancel spell casting in Battle scene.

---

## Problem Summary

Two related issues:

1. **Stuck Charging animation** — after a voice spell attempt where PTT is released without speaking, `OnSpellChargeAborted` fires and `TriggerResetCharge()` correctly sets `IsCharging = false`, but the Animator Controller has no `Charging → Idle` transition, so the sprite stays frozen in the charging pose for the remainder of the turn.

2. **No pre-PTT cancel** — once the player clicks Spell, they are committed. There is no way to cancel the spell phase before pressing PTT for the first time. The dev note in `docs/dev-notes/minor-stuff-to-implement.md` specifies Esc as the cancel key and that the prompt text should mention it.

---

## Approach

**Approach A — Proper Animator fix + minimal cancel code.** Fix the Animator Controller with the correct transition, add one new `CancelSpell()` method to `BattleController`, and wire Esc in `SpellInputUI`. Reuses the existing `OnSpellChargeAborted` event and `TriggerResetCharge()` — no new events, no new states in `SpellInputUILogic`.

---

## Design

### 1. Animator Controller (Unity Editor)

File: Player Battle Animator Controller asset.

**Add transition: `Charging → Idle`**
- Condition: `IsCharging = false`
- Has Exit Time: off
- Transition Duration: 0

This is the root fix. Both the existing abort paths (`NotifySpellNotRecognized`, `NotifyVoiceResultEmpty`) and the new cancel path all flow through `TriggerResetCharge()` → `SetBool(IsChargingHash, false)`, so this single transition fixes all three cases.

**Verify Attack / Hurt / Defeat reachability from Charging:**
Check whether the Attack, Hurt, and Defeat triggers are already on "Any State" transitions. If yes, no change needed — they fire from any state including Charging. If they are only wired from specific states, add `Charging → Attack`, `Charging → Hurt`, and `Charging → Defeat` transitions so that an attack or damage event immediately following a cancel does not get stranded.

---

### 2. `BattleController.CancelSpell()` (new method)

File: `Assets/Scripts/Battle/BattleController.cs`

```csharp
/// <summary>
/// Called by SpellInputUI when the player presses Esc during the PromptVisible state.
/// Exits the voice spell phase without advancing the turn — the player can choose a
/// different action.
/// No-op outside the voice spell phase or outside PlayerTurn.
/// </summary>
public void CancelSpell()
{
    if (!_isAwaitingVoiceSpell) return;
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    _isAwaitingVoiceSpell = false;
    _isProcessingAction   = false;
    OnSpellChargeAborted?.Invoke();
}
```

Same pattern as `NotifyVoiceResultEmpty`. `OnSpellChargeAborted` is already subscribed by `_playerAnimator.TriggerResetCharge` — no wiring changes needed.

---

### 3. `SpellInputUI` changes

File: `Assets/Scripts/Battle/UI/SpellInputUI.cs`

**New serialized field:**
```csharp
[SerializeField]
[Tooltip("InputAction for cancelling the spell phase. Bind to Esc in the Input Actions asset.")]
private InputActionReference _cancelAction;
```

**Subscribe/unsubscribe** in `OnEnable` / `OnDisable`, matching the existing PTT pattern:
```csharp
// OnEnable
if (_cancelAction != null)
    _cancelAction.action.performed += OnCancelPerformed;

// OnDisable
if (_cancelAction != null)
    _cancelAction.action.performed -= OnCancelPerformed;
```

**Handler** — fires only in `PromptVisible` state:
```csharp
private void OnCancelPerformed(InputAction.CallbackContext _)
{
    if (_logic.CurrentState != SpellInputUILogic.State.PromptVisible) return;
    _battleController.CancelSpell();
    _logic.Hide();
    Refresh();
}
```

`SpellInputUI` hides itself directly here rather than via an event because it is the sole caller — no other subscriber needs to react to cancel differently than it already does via `OnSpellChargeAborted`.

**Prompt label text:**
Update the prompt panel's TMP label (Inspector or wherever the string is authored) to:
```
Hold [Left Shift] and speak a spell name.
Press [Esc] to cancel.
```

---

### 4. Input Actions asset

File: `Assets/InputSystem_Actions.inputactions`

Add a `CancelSpell` action (Button type) with an Esc binding. Assign the resulting `InputActionReference` to the `_cancelAction` field on the `SpellInputUI` component in the Battle scene Inspector.

---

## What does NOT change

- `SpellInputUILogic` — `Hide()` already handles the Idle transition; no new state needed.
- `OnSpellChargeAborted` event and its subscribers — cancel reuses this event as-is.
- `MicrophoneInputHandler`, `SpellCastController`, `VoskRecognizerService` — cancel happens before PTT is ever pressed; the voice pipeline is untouched.

---

## Scope: Cancel works only in `PromptVisible` state

Esc cancel is intentionally limited to the `PromptVisible` state (prompt showing, PTT not yet pressed). If the player is in `Listening` state (PTT held), they release PTT first — the empty-result path handles that — and can then press Esc. Pressing Esc while holding PTT simultaneously is counter-intuitive and not supported.
