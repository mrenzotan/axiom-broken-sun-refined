# Spell Cast Animations — Design Spec

**Date:** 2026-04-09  
**Status:** Approved  
**Scope:** Integrate charge, cast, and enemy hurt animations into the spell casting flow in the Battle scene.

---

## Summary

Three animation gaps exist in the current spell casting flow:

1. No animation plays while the player is waiting to speak a spell (voice input phase).
2. No cast animation plays when the player's spell is recognized and executed.
3. The enemy does not play its hurt animation when damaged by a spell (it already plays on physical attack).

All three are resolved by extending `PlayerBattleAnimator` with two new triggers and one new animation event, and restructuring `BattleController.OnSpellCast()` to defer VFX and damage resolution until the cast animation's fire frame.

---

## Animation Clips

| Clip | Path | Loop |
|------|------|------|
| `playerChargeRight` | `Assets/Animations/Player/playerChargeRight.anim` | Yes — loops while waiting for voice |
| `playerCastRight` | `Assets/Animations/Player/playerCastRight.anim` | No — plays once, fire event near last frame |
| `iceSlimeHurtRight` | `Assets/Animations/Enemies/Ice Slime/iceSlimeHurtRight.anim` | No — already wired, no changes needed |

---

## Flow

```
Player clicks Spell
  → BattleController.PlayerSpell()
  → _playerAnimator.TriggerCharge()        ← NEW
  → playerChargeRight.anim loops

Voice recognized → OnSpellCast(spell) called
  → Store spell in _pendingSpell            ← NEW
  → _playerAnimator.TriggerCast()          ← NEW (snaps immediately, no idle gap)
  → playerCastRight.anim plays

Cast animation event fires (near last frame)
  → AnimEvent_OnSpellFire()                ← NEW animation event method
  → OnSpellFireFrame event                 ← NEW event
  → BattleController.FireSpellVisuals()    ← NEW method

FireSpellVisuals()
  → Spawn VFX (SpellVFXController.Play)
  → Resolve spell (_resolver.Resolve)
  → Fire OnSpellRecognized, OnDamageDealt / OnSpellHealed / OnShieldApplied
  → OnDamageDealt → BattleAnimationService → enemy TriggerHurt()  ← reused
  → StartCoroutine(CompletePlayerAction)   → turn advances
```

---

## Code Changes

### `PlayerBattleAnimator` (Assets/Scripts/Battle/PlayerBattleAnimator.cs)

**New Animator parameter hashes:**
```csharp
private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
private static readonly int CastHash       = Animator.StringToHash("Cast");
```

**New public surface:**
```csharp
public event System.Action OnSpellFireFrame;
public void TriggerCharge() => _animator.SetBool(IsChargingHash, true);
public void TriggerCast()   { _animator.SetBool(IsChargingHash, false); _animator.SetTrigger(CastHash); }
public void AnimEvent_OnSpellFire() => OnSpellFireFrame?.Invoke();
```

No coroutine needed for cast — the fire frame is near the last frame and acts as the sequence completion point.

### `BattleController` (Assets/Scripts/Battle/BattleController.cs)

**New fields:**
```csharp
private SpellData   _pendingSpell;
private SpellResult _pendingSpellResult;
```

**`PlayerSpell()` addition:**
```csharp
_playerAnimator?.TriggerCharge();
```
Added after `_isAwaitingVoiceSpell = true`.

**`OnSpellCast()` restructure:**
- MP deduction stays here (guard still runs first — reject if insufficient MP)
- Fire `OnSpellRecognized` here (so the spell name appears in SpellInputUI during the cast animation)
- Store spell: `_pendingSpell = spell`
- Trigger cast: `_playerAnimator?.TriggerCast()`
- Remove immediate VFX spawn, `_resolver.Resolve()`, and damage/condition event firing — moved to `FireSpellVisuals()`
- If no animator wired: call `FireSpellVisuals()` immediately (fallback, same pattern as attack)

**New `FireSpellVisuals()` method:**
- Guard: `if (_pendingSpell == null) return;`
- Spawn VFX via `_spellVfxController` (same logic as before)
- Resolve: `_pendingSpellResult = _resolver.Resolve(_pendingSpell, _playerStats, _enemyStats)`
- Fire `OnDamageDealt` / `OnSpellHealed` / `OnShieldApplied`, `OnCharacterDefeated`, `OnConditionsChanged`, MP bar ping
- `StartCoroutine(CompletePlayerAction(_pendingSpellResult.TargetDefeated))`
- Clear: `_pendingSpell = null`

**Event wiring in `Initialize()`:**
```csharp
_playerAnimator.OnSpellFireFrame += FireSpellVisuals;
```

**Unwiring in `Initialize()` re-init guard and `OnDestroy()`:**
```csharp
_playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
```

---

## Unity Editor Changes (no code)

### Player Animator Controller

1. Add **`IsCharging` bool** parameter and **`Cast` trigger** parameter.
2. Add **Charge state** using `playerChargeRight.anim`, loop time = true.
3. Add transitions:
   - `Any State → Charge`: condition `IsCharging == true`
   - `Charge → Cast`: condition `IsCharging == false` + `Cast` trigger, exit time = 0, transition duration = 0
   - `Cast → Idle`: exit time = 1, transition duration = 0
4. Add **Animation Event** on `playerCastRight.anim` near the last frame, function name = `AnimEvent_OnSpellFire`.

### Enemy Animator Controller

No changes — `Hurt` trigger and `iceSlimeHurtRight.anim` are already wired.

---

## What Is Not Changing

- `BattleAnimationService` — no changes; enemy hurt fires automatically via the existing `OnDamageDealt → _enemyHurt` path.
- `EnemyBattleAnimator` — no changes.
- `SpellVFXController` — no changes; called with the same arguments as before, just deferred to `FireSpellVisuals()`.
- `SpellEffectResolver` — no changes.
- `CompletePlayerAction` coroutine — no changes; called from `FireSpellVisuals()` exactly as before.

---

## Out of Scope

- Left-facing variants (`playerChargeLeft.anim`, `playerCastLeft.anim`) — not needed until directional facing is implemented.
- Additional enemy types — same hurt trigger pattern applies; no per-enemy changes required.
