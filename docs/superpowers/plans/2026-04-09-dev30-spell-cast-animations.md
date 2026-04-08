# Spell Cast Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate charge, cast, and enemy hurt animations into the spell casting flow so that voice-spell actions feel as weighty and responsive as physical attacks.

**Architecture:** `PlayerBattleAnimator` gains two new Animator parameters (`IsCharging` bool, `Cast` trigger), an `OnSpellFireFrame` event fired by an Animation Event, and two trigger methods. `BattleController.OnSpellCast()` is restructured to store the pending spell and trigger the cast animation; VFX and damage resolution move to a new `FireSpellVisuals()` method that runs at the animation's fire frame.

**Tech Stack:** Unity 6 LTS, C# (NUnit via Unity Test Framework for Edit Mode tests), Unity Animator Controller.

---

## File Map

| Action | File |
|--------|------|
| Modify | `Assets/Scripts/Battle/PlayerBattleAnimator.cs` |
| Modify | `Assets/Scripts/Battle/BattleController.cs` |
| Modify | `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs` — no changes needed; included to confirm existing tests still pass after BattleController restructure |
| Editor | `Assets/Animations/Player/playerCastRight.anim` — add Animation Event at last frame |
| Editor | Player Animator Controller — add `IsCharging` bool, `Cast` trigger, Charge state, transitions |

---

## Task 1: Extend `PlayerBattleAnimator` with Charge/Cast triggers and fire event

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerBattleAnimator.cs`

- [ ] **Step 1: Add the two new Animator parameter hashes**

Open `Assets/Scripts/Battle/PlayerBattleAnimator.cs`. After the existing hash fields (line 25–29), add:

```csharp
private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
private static readonly int CastHash       = Animator.StringToHash("Cast");
```

- [ ] **Step 2: Add the `OnSpellFireFrame` event and the three new public methods**

After `public event System.Action OnAttackSequenceComplete;` (line 43), add:

```csharp
/// <summary>
/// Fired by Unity Animation Event on the fire frame of the cast clip.
/// BattleController subscribes to spawn VFX and resolve spell damage at the right moment.
/// </summary>
public event System.Action OnSpellFireFrame;
```

After `public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);` (line 58), add:

```csharp
public void TriggerCharge() => _animator.SetBool(IsChargingHash, true);
public void TriggerCast()   { _animator.SetBool(IsChargingHash, false); _animator.SetTrigger(CastHash); }

/// <summary>
/// Called by Unity Animation Event on the cast clip's fire frame.
/// The method name must match exactly what is set in the Animation Event inspector.
/// </summary>
public void AnimEvent_OnSpellFire() => OnSpellFireFrame?.Invoke();
```

- [ ] **Step 3: Verify the file compiles — open Unity Editor and check the Console for errors**

Expected: no compile errors related to `PlayerBattleAnimator`.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/Battle/PlayerBattleAnimator.cs
git commit -m "feat(DEV-30): add TriggerCharge, TriggerCast, and OnSpellFireFrame to PlayerBattleAnimator"
```

---

## Task 2: Restructure `BattleController` — defer spell resolution to the fire frame

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

This task has the most surface area. Read `BattleController.cs` in full before touching it — there are guard flags and coroutine patterns that must remain consistent.

### 2a — Add pending spell fields

- [ ] **Step 1: Add `_pendingSpell` and `_pendingSpellResult` private fields**

After `private SpellEffectResolver _resolver;` (line 163), add:

```csharp
private SpellData   _pendingSpell;
private SpellResult _pendingSpellResult;
```

### 2b — Wire/unwire `OnSpellFireFrame` in `Initialize()` and `OnDestroy()`

- [ ] **Step 2: Add unwire call inside the re-init guard at the top of `Initialize()`**

Inside the `if (_animationService != null)` block in `Initialize()` (lines 175–186), add before `_animationService = null;`:

```csharp
_playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
```

- [ ] **Step 3: Add wire call inside the animator assignment block in `Initialize()`**

Inside the `if (_playerAnimator != null && _enemyAnimator != null)` block (lines 216–231), add after `_enemyAnimator.OnAttackSequenceComplete += OnEnemySequenceComplete;`:

```csharp
_playerAnimator.OnSpellFireFrame += FireSpellVisuals;
```

- [ ] **Step 4: Add unwire call in `OnDestroy()`**

Inside `OnDestroy()` (lines 503–520), after `if (_playerAnimator != null) _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;`, add:

```csharp
if (_playerAnimator != null) _playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
```

### 2c — Call `TriggerCharge()` from `PlayerSpell()`

- [ ] **Step 5: Add charge trigger call in `PlayerSpell()`**

In `PlayerSpell()` (lines 280–287), after `_isAwaitingVoiceSpell = true;`, add:

```csharp
_playerAnimator?.TriggerCharge();
```

### 2d — Restructure `OnSpellCast()` and add `FireSpellVisuals()`

- [ ] **Step 6: Rewrite `OnSpellCast()` to store the pending spell and trigger the cast animation**

Replace the body of `OnSpellCast()` from line 296 onward with:

```csharp
public void OnSpellCast(SpellData spell)
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (!_isAwaitingVoiceSpell) return;

    if (!_playerStats.SpendMP(spell.mpCost))
    {
        _isAwaitingVoiceSpell = false;
        _isProcessingAction   = false;
        OnSpellCastRejected?.Invoke($"Not enough MP to cast {spell.spellName}.");
        Debug.Log($"[Battle] Spell rejected — insufficient MP for {spell.spellName}.");
        return;
    }

    _isAwaitingVoiceSpell = false;
    _pendingSpell         = spell;

    // Show the spell name in SpellInputUI during the cast animation.
    OnSpellRecognized?.Invoke(spell);

    if (_playerAnimator != null)
    {
        _playerAnimator.TriggerCast();
        // FireSpellVisuals() is called by the OnSpellFireFrame animation event.
    }
    else
    {
        FireSpellVisuals();
    }
}
```

- [ ] **Step 7: Add the `FireSpellVisuals()` method**

Add the following private method after `OnSpellCast()` (before `NotifySpellNotRecognized()`):

```csharp
private void FireSpellVisuals()
{
    if (_pendingSpell == null) return;
    SpellData spell = _pendingSpell;
    _pendingSpell = null;

    if (_spellVfxController != null)
    {
        Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
            ? (_enemyAnimator  != null ? _enemyAnimator.transform.position  : Vector3.zero)
            : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
        _spellVfxController.Play(spell, vfxPosition);
    }

    SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);

    switch (result.EffectType)
    {
        case SpellEffectType.Damage:
            OnDamageDealt?.Invoke(_enemyStats, result.Amount, false);
            if (result.TargetDefeated)
                OnCharacterDefeated?.Invoke(_enemyStats);
            break;
        case SpellEffectType.Heal:
            OnSpellHealed?.Invoke(_playerStats, result.Amount);
            break;
        case SpellEffectType.Shield:
            OnShieldApplied?.Invoke(_playerStats, result.Amount);
            break;
    }

    OnConditionsChanged?.Invoke(_playerStats);
    OnConditionsChanged?.Invoke(_enemyStats);

    // Zero-damage ping so BattleHUD refreshes MP bar after the spend.
    OnDamageDealt?.Invoke(_playerStats, 0, false);

    Debug.Log($"[Battle] Spell cast: {spell.spellName} → {result.EffectType} {result.Amount}" +
              $"{(result.ReactionTriggered ? " [REACTION]" : string.Empty)}");

    StartCoroutine(CompletePlayerAction(result.TargetDefeated));
}
```

- [ ] **Step 8: Verify the file compiles — check Unity Console for errors**

Expected: no compile errors. The `_playerDamageVisualsFired` flag that was set in the old `OnSpellCast()` is no longer set here; that flag guards physical attack visuals only and is not needed for the spell path — verify the flag is still set correctly for physical attacks (it is, in `PlayerAttack()`).

- [ ] **Step 9: Commit**

```
git add Assets/Scripts/Battle/BattleController.cs
git commit -m "feat(DEV-30): restructure OnSpellCast to defer VFX and damage to FireSpellVisuals on cast fire frame"
```

---

## Task 3: Unity Editor — Animator Controller wiring

> **Note:** These are manual Unity Editor steps. No code changes. Complete these in the Unity Editor with the project open.

**Files:**
- Editor: Player Animator Controller (find via the player character's Animator component in the Battle scene)
- Editor: `Assets/Animations/Player/playerCastRight.anim`

### 3a — Add Animator parameters

- [ ] **Step 1: Open the Player Animator Controller**

In the Unity Editor: open the Battle scene → select the player GameObject → in the Inspector, click the Animator Controller asset to open the Animator window.

- [ ] **Step 2: Add `IsCharging` bool parameter**

In the Animator window Parameters tab, click `+` → Bool → name it exactly `IsCharging`.

- [ ] **Step 3: Add `Cast` trigger parameter**

In the Animator window Parameters tab, click `+` → Trigger → name it exactly `Cast`.

### 3b — Add the Charge state

- [ ] **Step 4: Create the Charge state**

Right-click in the Animator grid → Create State → Empty. Rename it `Charge`. In the Inspector for Charge state: set Motion to `playerChargeRight` (from `Assets/Animations/Player/playerChargeRight.anim`). Enable **Loop Time** on the animation clip if not already set (select the `.anim` asset → Inspector → enable Loop Time).

### 3c — Add transitions

- [ ] **Step 5: Add Any State → Charge transition**

Right-click `Any State` → Make Transition → click the `Charge` state. Select the transition arrow. In Inspector:
- Uncheck **Has Exit Time**
- Set **Transition Duration** to 0
- Add condition: `IsCharging` = `true`

- [ ] **Step 6: Add Charge → Cast transition**

Right-click `Charge` state → Make Transition → click the `Cast` state. Select the transition arrow. In Inspector:
- Uncheck **Has Exit Time**
- Set **Transition Duration** to 0
- Add conditions: `IsCharging` = `false` AND `Cast` (trigger)

- [ ] **Step 7: Add Cast → Idle transition**

Right-click `Cast` state → Make Transition → click the `Idle` state. Select the transition arrow. In Inspector:
- Enable **Has Exit Time**, set Exit Time to `1`
- Set **Transition Duration** to 0
- No conditions

### 3d — Add Animation Event on `playerCastRight.anim`

- [ ] **Step 8: Open the Animation clip in the Animation window**

Select the player GameObject in the Battle scene. Open Window → Animation → Animation. From the clip dropdown, select `playerCastRight`.

- [ ] **Step 9: Add the Animation Event**

Scrub to the last frame (or near-last frame where the cast "fires"). Click the **Add Event** button (white marker icon in the timeline). In the Inspector that appears, set **Function** to exactly `AnimEvent_OnSpellFire`. No parameters needed.

- [ ] **Step 10: Save and verify in Play Mode**

Enter Play Mode in the Battle scene. Open the Spell action, speak a valid spell. Confirm sequence:
1. Player plays charge loop while waiting for voice.
2. On voice recognition, charge animation snaps to cast animation (no idle gap).
3. At the cast fire frame: VFX spawns, enemy takes damage, enemy plays hurt animation.
4. After the action delay, the turn advances normally.

---

## Task 4: Verify existing tests still pass

**Files:**
- Test: `Assets/Tests/Editor/Battle/BattleAnimationServiceTests.cs` (read-only verification)

- [ ] **Step 1: Run existing battle tests in Unity Test Runner**

Unity Editor → Window → General → Test Runner → EditMode tab → Run All (or filter to `Battle`).

Expected: all tests in `BattleAnimationServiceTests`, `BattleManagerTests`, `PlayerActionHandlerTests`, `SpellEffectResolverTests` pass. No new failures.

- [ ] **Step 2: Confirm `_playerDamageVisualsFired` is not used in the spell path**

The `_playerDamageVisualsFired` flag was set to `true` in the old `OnSpellCast()` to prevent `CompletePlayerAction`'s safety-net call to `FirePlayerDamageVisuals()` from double-firing. In the new flow, `FireSpellVisuals()` is not guarded by that flag — instead it uses `_pendingSpell == null` as its own idempotency guard. The `CompletePlayerAction` safety-net still calls `FirePlayerDamageVisuals()`, which checks `_playerDamageVisualsFired`. This flag is **never set for the spell path**, so `FirePlayerDamageVisuals()` will run with a null/default `_pendingPlayerAttack`.

**Action required:** Set `_playerDamageVisualsFired = true` early in `OnSpellCast()` (right after `_isAwaitingVoiceSpell = false`) to prevent the safety net from calling `FirePlayerDamageVisuals()` on a null attack result. Add this line:

```csharp
_playerDamageVisualsFired = true; // Spell path does not go through FirePlayerDamageVisuals
```

Place it after `_pendingSpell = spell;` in `OnSpellCast()`.

- [ ] **Step 3: Commit the fix if Step 2 required a change**

```
git add Assets/Scripts/Battle/BattleController.cs
git commit -m "fix(DEV-30): set _playerDamageVisualsFired on spell path to prevent null attack safety-net call"
```

---

## Spec Coverage Checklist

| Spec requirement | Task |
|---|---|
| TriggerCharge() — player loops charge anim while waiting for voice | Task 1 + Task 2c + Task 3b/3c |
| TriggerCast() — snaps to cast animation, no idle gap | Task 1 + Task 2d + Task 3c |
| AnimEvent_OnSpellFire() fires OnSpellFireFrame event | Task 1 + Task 3d |
| FireSpellVisuals() spawns VFX, resolves spell, fires all damage/condition events | Task 2d |
| OnSpellRecognized fires early (spell name visible during cast) | Task 2d |
| MP deduction and rejection guard remain in OnSpellCast() | Task 2d |
| No animator fallback: FireSpellVisuals() called immediately | Task 2d |
| Event wiring/unwiring in Initialize() and OnDestroy() | Task 2b |
| Enemy hurt fires via existing OnDamageDealt → BattleAnimationService path | No change needed |
| CompletePlayerAction called from FireSpellVisuals() | Task 2d |
| _playerDamageVisualsFired set correctly for spell path | Task 4 |
