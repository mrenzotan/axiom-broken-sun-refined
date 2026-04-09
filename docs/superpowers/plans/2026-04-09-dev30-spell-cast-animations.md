# Spell Cast Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate charge, cast, and enemy hurt animations into the spell casting flow so that voice-spell actions feel as weighty and responsive as physical attacks.

**Architecture:** `PlayerBattleAnimator` gains two new Animator parameters (`IsCharging` bool, `Cast` trigger), an `OnSpellFireFrame` event fired by an Animation Event, and two trigger methods. `BattleAnimationService` is the established bridge between `BattleController` events and animator trigger methods — all outgoing animation triggers, including charge and cast, route through it. `BattleController` adds two new events (`OnSpellChargeStarted`, `OnSpellCastStarted`) that the service subscribes to; direct animator calls are removed from `BattleController`. VFX and damage resolution live in `FireSpellVisuals()`, called at the animation's fire frame via `OnSpellFireFrame` (animator → controller direction; does not route through the service).

**Tech Stack:** Unity 6 LTS, C# (NUnit via Unity Test Framework for Edit Mode tests), Unity Animator Controller.

---

## File Map

| Action | File |
|--------|------|
| ~~Modify~~ ✓ Done | `Assets/Scripts/Battle/PlayerBattleAnimator.cs` |
| Modify | `Assets/Scripts/Battle/BattleAnimationService.cs` |
| Modify | `Assets/Scripts/Battle/BattleController.cs` |
| Modify | `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs` |
| Editor | `Assets/Animations/Player/playerCastRight.anim` — add Animation Event at last frame |
| Editor | Player Animator Controller — add `IsCharging` bool, `Cast` trigger, Charge state, transitions |

---

## Task 1: Extend `PlayerBattleAnimator` with Charge/Cast triggers and fire event — DONE

All code changes in this task are already implemented in the codebase.

- [x] **Step 1:** Added `IsChargingHash` and `CastHash` parameter hash fields
- [x] **Step 2:** Added `OnSpellFireFrame` event, `TriggerCharge()`, `TriggerCast()`, and `AnimEvent_OnSpellFire()` methods
- [x] **Step 3:** Verified the file compiles — no errors in Unity Console
- [x] **Step 4:** Checked in via UVCS

---

## Task 2: Extend `BattleAnimationService` with charge/cast delegates

**Files:**
- Modify: `Assets/Scripts/Battle/BattleAnimationService.cs`

**Why:** All outgoing animation triggers must route through `BattleAnimationService` — the established bridge between `BattleController` events and animator trigger methods. Adding charge/cast here keeps `BattleController` free of direct animator calls and makes the service the single, complete picture of all animation signals.

### 2a — Add private delegate fields

- [ ] **Step 1: Add `_playerCharge` and `_playerCast` fields**

In `BattleAnimationService.cs`, after `private readonly Action _enemyDefeat;`, add:

```csharp
private readonly Action _playerCharge;
private readonly Action _playerCast;
```

### 2b — Extend the constructor

- [ ] **Step 2: Update the constructor signature and body**

Replace the constructor with:

```csharp
public BattleAnimationService(
    CharacterStats playerStats,
    CharacterStats enemyStats,
    Action playerAttack,
    Action playerHurt,
    Action playerDefeat,
    Action playerCharge,
    Action playerCast,
    Action enemyAttack,
    Action enemyHurt,
    Action enemyDefeat)
{
    _playerStats  = playerStats;
    _enemyStats   = enemyStats;
    _playerAttack = playerAttack;
    _playerHurt   = playerHurt;
    _playerDefeat = playerDefeat;
    _playerCharge = playerCharge;
    _playerCast   = playerCast;
    _enemyAttack  = enemyAttack;
    _enemyHurt    = enemyHurt;
    _enemyDefeat  = enemyDefeat;
}
```

### 2c — Add the two new public methods

- [ ] **Step 3: Add `OnSpellChargeStarted()` and `OnSpellCastStarted()`**

After `public void OnPlayerActionStarted() => _playerAttack?.Invoke();`, add:

```csharp
/// <summary>Call when the player enters the charge animation state (waiting for voice input).</summary>
public void OnSpellChargeStarted() => _playerCharge?.Invoke();

/// <summary>Call when a spell is recognized and the cast animation begins.</summary>
public void OnSpellCastStarted()   => _playerCast?.Invoke();
```

- [ ] **Step 4: Verify the file compiles — check Unity Console for errors**

Expected: `BattleAnimationService.cs` compiles cleanly. `BattleController.cs` will show a compile error because the constructor call still passes the old number of arguments — that is expected and will be fixed in Task 3.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-30): extend BattleAnimationService with playerCharge and playerCast delegates`
  - `Assets/Scripts/Battle/BattleAnimationService.cs`
  - `Assets/Scripts/Battle/BattleAnimationService.cs.meta`

---

## Task 3: Update `BattleController` — add events, wire through service, remove direct animator calls

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

Read `BattleController.cs` in full before making changes — guard flags, coroutine patterns, and event wiring must remain consistent with the existing patterns.

### 3a — Completed restructure steps (already done)

- [x] **Step 1:** Added `_pendingSpell` and `_pendingSpellResult` private fields
- [x] **Step 2:** Added `_playerAnimator.OnSpellFireFrame -= FireSpellVisuals` in the re-init guard inside `Initialize()`
- [x] **Step 3:** Added `_playerAnimator.OnSpellFireFrame += FireSpellVisuals` in the animator block inside `Initialize()`
- [x] **Step 4:** Added `_playerAnimator.OnSpellFireFrame -= FireSpellVisuals` in `OnDestroy()`
- [x] **Step 5:** Added `_playerAnimator?.TriggerCharge()` in `PlayerSpell()` — **will be replaced in Step 10**
- [x] **Step 6:** Rewrote `OnSpellCast()` with `_playerAnimator.TriggerCast()` direct call — **will be replaced in Step 11**
- [x] **Step 7:** Added `FireSpellVisuals()` method
- [x] **Step 8:** Verified compile, no errors
- [x] **Step 9:** Checked in via UVCS

### 3b — Add two new events to `BattleController`

- [ ] **Step 10: Add `OnSpellChargeStarted` and `OnSpellCastStarted` events**

In the UI Events region, after `public event Action OnSpellPhaseStarted;`, add:

```csharp
/// <summary>
/// Fires when the player enters the spell charge state (waiting for voice input).
/// BattleAnimationService subscribes to route this to PlayerBattleAnimator.TriggerCharge().
/// </summary>
public event Action OnSpellChargeStarted;

/// <summary>
/// Fires when a spell is recognized and the cast animation begins.
/// BattleAnimationService subscribes to route this to PlayerBattleAnimator.TriggerCast().
/// </summary>
public event Action OnSpellCastStarted;
```

### 3c — Update `BattleAnimationService` construction in `Initialize()`

- [ ] **Step 11: Pass the two new delegates in the constructor call**

Inside `Initialize()`, replace the `new BattleAnimationService(...)` call with:

```csharp
_animationService = new BattleAnimationService(
    _playerStats, _enemyStats,
    _playerAnimator.TriggerAttack, _playerAnimator.TriggerHurt, _playerAnimator.TriggerDefeat,
    _playerAnimator.TriggerCharge, _playerAnimator.TriggerCast,
    _enemyAnimator.TriggerAttack,  _enemyAnimator.TriggerHurt,  _enemyAnimator.TriggerDefeat);
```

### 3d — Wire new service subscriptions in `Initialize()`

- [ ] **Step 12: Subscribe the two new service methods to the new events**

In `Initialize()`, after `OnCharacterDefeated += _animationService.OnCharacterDefeated;`, add:

```csharp
OnSpellChargeStarted += _animationService.OnSpellChargeStarted;
OnSpellCastStarted   += _animationService.OnSpellCastStarted;
```

### 3e — Unwire in the re-init guard and `OnDestroy()`

- [ ] **Step 13: Unwire in the re-init guard**

Inside the `if (_animationService != null)` block at the top of `Initialize()`, after `OnCharacterDefeated -= _animationService.OnCharacterDefeated;`, add:

```csharp
OnSpellChargeStarted -= _animationService.OnSpellChargeStarted;
OnSpellCastStarted   -= _animationService.OnSpellCastStarted;
```

- [ ] **Step 14: Unwire in `OnDestroy()`**

Inside the `if (_animationService != null)` block in `OnDestroy()`, after `OnCharacterDefeated -= _animationService.OnCharacterDefeated;`, add:

```csharp
OnSpellChargeStarted -= _animationService.OnSpellChargeStarted;
OnSpellCastStarted   -= _animationService.OnSpellCastStarted;
```

### 3f — Replace direct `TriggerCharge()` call in `PlayerSpell()`

- [ ] **Step 15: Replace direct animator call with event**

In `PlayerSpell()`, replace:

```csharp
_playerAnimator?.TriggerCharge();
```

with:

```csharp
OnSpellChargeStarted?.Invoke();
```

### 3g — Replace direct `TriggerCast()` block in `OnSpellCast()`

- [ ] **Step 16: Replace direct animator call and update fallback logic**

In `OnSpellCast()`, replace:

```csharp
if (_playerAnimator != null)
{
    _playerAnimator.TriggerCast();
    // FireSpellVisuals() is called by the OnSpellFireFrame animation event.
}
else
{
    FireSpellVisuals();
}
```

with:

```csharp
OnSpellCastStarted?.Invoke();

if (_animationService == null)
{
    FireSpellVisuals();
}
// else: FireSpellVisuals() is called by the OnSpellFireFrame animation event.
```

> **Why `_animationService == null` instead of `_playerAnimator == null`:** `_animationService` is null when either animator is not assigned in the Inspector — the same condition under which no `OnSpellFireFrame` event will ever fire. This keeps the fallback consistent with the service-centric pattern.

- [ ] **Step 17: Verify the file compiles — check Unity Console for errors**

Expected: no compile errors. Confirm that `PlayerSpell()` no longer references `_playerAnimator` for animation triggers, and that `OnSpellCast()` no longer references `_playerAnimator` directly. The only remaining direct `_playerAnimator` references in `BattleController` should be the `OnSpellFireFrame` subscription lines (animator → controller direction) and the null checks inside `FireSpellVisuals()` for VFX position.

- [ ] **Step 18: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-30): route charge and cast triggers through BattleAnimationService; remove direct animator calls from BattleController`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`

---

## Task 4: Update `BattleAnimationServiceTests` — fix broken helper and add new tests

**Files:**
- Modify: `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`

**Why this task is required:** The `BattleAnimationService` constructor gains two new parameters (`playerCharge`, `playerCast`). This breaks two things in the existing test file:
1. The `MakeService` helper — it calls the constructor with the old 8-argument signature.
2. `NullDelegates_DoNotThrow` — it calls the constructor directly with 6 positional nulls (now needs 8).

Both must be fixed before any test can run. Two new test cases are also added to cover the new methods.

### 4a — Update the `MakeService` helper

- [ ] **Step 1: Add `playerCharge` and `playerCast` optional params to `MakeService`**

Replace the existing `MakeService` helper with:

```csharp
private static BattleAnimationService MakeService(
    CharacterStats player, CharacterStats enemy,
    System.Action playerAttack = null, System.Action playerHurt = null, System.Action playerDefeat = null,
    System.Action playerCharge = null, System.Action playerCast   = null,
    System.Action enemyAttack  = null, System.Action enemyHurt   = null, System.Action enemyDefeat = null)
{
    return new BattleAnimationService(
        player, enemy,
        playerAttack ?? (() => {}), playerHurt ?? (() => {}), playerDefeat ?? (() => {}),
        playerCharge ?? (() => {}), playerCast  ?? (() => {}),
        enemyAttack  ?? (() => {}), enemyHurt  ?? (() => {}), enemyDefeat  ?? (() => {}));
}
```

> **Note:** All existing tests use named parameters (e.g. `playerHurt: () => called = true`) so their call sites do not need to change.

### 4b — Fix `NullDelegates_DoNotThrow`

- [ ] **Step 2: Update the direct constructor call to pass 8 nulls instead of 6**

Replace the constructor call inside `NullDelegates_DoNotThrow` with:

```csharp
var svc = new BattleAnimationService(player, enemy, null, null, null, null, null, null, null, null);
```

Also extend the `Assert.DoesNotThrow` block to cover the new methods:

```csharp
Assert.DoesNotThrow(() => svc.OnSpellChargeStarted());
Assert.DoesNotThrow(() => svc.OnSpellCastStarted());
```

### 4c — Add tests for the two new methods

- [ ] **Step 3: Add `OnSpellChargeStarted_InvokesPlayerCharge`**

In the Attack animations region, after `OnEnemyActionStarted_DoesNotInvokePlayerAttack`, add:

```csharp
[Test]
public void OnSpellChargeStarted_InvokesPlayerCharge()
{
    bool called = false;
    var player = MakeStats("Player");
    var enemy  = MakeStats("Enemy");
    var svc = MakeService(player, enemy, playerCharge: () => called = true);

    svc.OnSpellChargeStarted();

    Assert.IsTrue(called);
}

[Test]
public void OnSpellCastStarted_InvokesPlayerCast()
{
    bool called = false;
    var player = MakeStats("Player");
    var enemy  = MakeStats("Enemy");
    var svc = MakeService(player, enemy, playerCast: () => called = true);

    svc.OnSpellCastStarted();

    Assert.IsTrue(called);
}
```

- [ ] **Step 4: Verify in Unity Test Runner — all existing tests still pass, two new tests pass**

> **Unity Editor task (user):** Unity Editor → Window → General → Test Runner → EditMode tab → filter to `BattleAnimationServiceTests` → Run. Confirm all tests green, including `OnSpellChargeStarted_InvokesPlayerCharge` and `OnSpellCastStarted_InvokesPlayerCast`.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-30): update BattleAnimationServiceTests for charge/cast delegates; add two new test cases`
  - `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`
  - `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs.meta`

---

## Task 5: Unity Editor — Animator Controller wiring

> **Note:** These are manual Unity Editor steps. No code changes. Complete these in the Unity Editor with the project open.

**Files:**
- Editor: Player Animator Controller (find via the player character's Animator component in the Battle scene)
- Editor: `Assets/Animations/Player/playerCastRight.anim`

### 5a — Add Animator parameters

- [ ] **Step 1: Open the Player Animator Controller**

> **Unity Editor task (user):** Open the Battle scene → select the player GameObject → in the Inspector, click the Animator Controller asset to open the Animator window.

- [ ] **Step 2: Add `IsCharging` bool parameter**

> **Unity Editor task (user):** In the Animator window Parameters tab, click `+` → Bool → name it exactly `IsCharging`.

- [ ] **Step 3: Add `Cast` trigger parameter**

> **Unity Editor task (user):** In the Animator window Parameters tab, click `+` → Trigger → name it exactly `Cast`.

### 5b — Add the Charge state

- [ ] **Step 4: Create the Charge state**

> **Unity Editor task (user):** Right-click in the Animator grid → Create State → Empty. Rename it `Charge`. In the Inspector for the Charge state, set Motion to `playerChargeRight` (`Assets/Animations/Player/playerChargeRight.anim`). Select the `.anim` asset in the Project window → Inspector → enable **Loop Time** if not already set.

### 5c — Add transitions

- [ ] **Step 5: Add Any State → Charge transition**

> **Unity Editor task (user):** Right-click `Any State` → Make Transition → click the `Charge` state. Select the transition arrow. In Inspector: uncheck **Has Exit Time**, set **Transition Duration** to `0`, add condition `IsCharging = true`.

- [ ] **Step 6: Add Charge → Cast transition**

> **Unity Editor task (user):** Right-click the `Charge` state → Make Transition → click the `Cast` state. Select the transition arrow. In Inspector: uncheck **Has Exit Time**, set **Transition Duration** to `0`, add conditions `IsCharging = false` AND `Cast` (trigger).

- [ ] **Step 7: Add Cast → Idle transition**

> **Unity Editor task (user):** Right-click the `Cast` state → Make Transition → click the `Idle` state. Select the transition arrow. In Inspector: enable **Has Exit Time**, set Exit Time to `1`, set **Transition Duration** to `0`, no conditions.

### 5d — Add Animation Event on `playerCastRight.anim`

- [ ] **Step 8: Add the Animation Event**

> **Unity Editor task (user):** Select the player GameObject in the Battle scene. Open Window → Animation → Animation. From the clip dropdown, select `playerCastRight`. Scrub to the last frame (or near-last frame where the cast fires). Click the **Add Event** button (white marker icon in the timeline). In the Inspector, set **Function** to exactly `AnimEvent_OnSpellFire`. No parameters.

- [ ] **Step 9: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-30): wire Charge and Cast states in Player Animator Controller; add Animation Event on cast clip`
  - `Assets/Animations/Player/[YourAnimatorController].controller`
  - `Assets/Animations/Player/playerCastRight.anim`

### 5e — Play Mode verification

- [ ] **Step 10: Verify the full sequence in Play Mode**

> **Unity Editor task (user):** Enter Play Mode in the Battle scene. Select the Spell action and speak a valid spell. Confirm sequence:
> 1. Player plays the charge loop animation while waiting for voice.
> 2. On voice recognition, the charge animation snaps directly to the cast animation (no idle gap).
> 3. At the cast fire frame: VFX spawns on the enemy, enemy takes damage, enemy plays the hurt animation.
> 4. After the action delay, the turn advances normally.

---

## Task 6: Verify all battle tests pass

**Files:**
- `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs`
- `Assets/Tests/Editor/Battle/BattleManagerTests.cs`
- `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs`
- `Assets/Tests/Editor/Battle/SpellEffectResolverTests.cs`

- [ ] **Step 1: Run all battle Edit Mode tests**

> **Unity Editor task (user):** Unity Editor → Window → General → Test Runner → EditMode tab → Run All (or filter to `Battle`). Expected: all tests pass. No regressions.

- [ ] **Step 2: Confirm `_playerDamageVisualsFired` guard is in place on the spell path**

Verify that `_playerDamageVisualsFired = true;` is still present in `OnSpellCast()` after the Task 3 edits. This prevents `CompletePlayerAction`'s safety-net call to `FirePlayerDamageVisuals()` from running against a null `_pendingPlayerAttack` on the spell path.

---

## Spec Coverage Checklist

| Spec requirement | Task |
|---|---|
| TriggerCharge() — player loops charge anim while waiting for voice | Task 1 + Task 3f + Task 5b/5c |
| TriggerCast() — snaps to cast animation, no idle gap | Task 1 + Task 3g + Task 5c |
| AnimEvent_OnSpellFire() fires OnSpellFireFrame event | Task 1 + Task 5d |
| FireSpellVisuals() spawns VFX, resolves spell, fires all damage/condition events | Task 3a (done) |
| OnSpellRecognized fires early (spell name visible during cast animation) | Task 3a (done) |
| MP deduction and rejection guard remain in OnSpellCast() | Task 3a (done) |
| Fallback: FireSpellVisuals() called immediately when no animators are wired | Task 3g |
| Event wiring/unwiring in Initialize() and OnDestroy() | Task 3d/3e |
| Enemy hurt fires via existing OnDamageDealt → BattleAnimationService path | No change needed |
| CompletePlayerAction called from FireSpellVisuals() | Task 3a (done) |
| _playerDamageVisualsFired set on spell path to prevent safety-net misfire | Task 3a (done) + Task 6 step 2 |
| All outgoing animation triggers route through BattleAnimationService | Task 2 + Task 3 |
| New service methods covered by Edit Mode tests | Task 4 |
