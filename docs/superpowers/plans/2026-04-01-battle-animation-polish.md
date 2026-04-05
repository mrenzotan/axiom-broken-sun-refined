# Battle Animation Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three polish improvements to the Battle animation system: frame-precise damage timing via Animation Events, brief step-in movement when attacking, and the turn indicator repositioned above character sprites instead of HP panels.

**Architecture:** All three improvements layer onto the existing `BattleController` / `PlayerBattleAnimator` / `EnemyBattleAnimator` / `TurnIndicatorUI` / `BattleHUD` classes from DEV-17. No new classes are introduced — only targeted additions to existing ones. `BattleController` gains deferred damage-visual firing; the animator MonoBehaviours gain a hit-frame event and a step-in coroutine; `TurnIndicatorUI` gains world-to-screen conversion; `BattleHUD` gains sprite Transform references.

**Tech Stack:** Unity 6 LTS · C# · Unity Animator (Animation Events) · Unity Coroutines · Camera.WorldToScreenPoint

---

## File Map

| Action | Path | Change |
|--------|------|--------|
| **Modify** | `Assets/Scripts/Battle/PlayerBattleAnimator.cs` | Add `OnHitFrame` event + `AnimEvent_OnHit()` + step-in coroutine |
| **Modify** | `Assets/Scripts/Battle/EnemyBattleAnimator.cs` | Same as above (step direction reversed) |
| **Modify** | `Assets/Scripts/Battle/BattleController.cs` | Store pending attack results; defer `OnDamageDealt`/`OnCharacterDefeated` to hit frame; safety-net fallback in coroutine |
| **Modify** | `Assets/Scripts/Battle/UI/TurnIndicatorUI.cs` | Change `SetActiveTarget` param from `RectTransform` to `Transform`; add world-to-screen conversion in `SetActiveTarget` and `Bob()` |
| **Modify** | `Assets/Scripts/Battle/UI/BattleHUD.cs` | Add `[SerializeField] Transform` refs for sprite positions; update `HandleStateChanged` to pass them |

No new `.asmdef` files needed — all files are in existing assemblies.

---

## Task 1: Frame-Precise Damage Timing via Animation Events

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- Modify: `Assets/Scripts/Battle/EnemyBattleAnimator.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`

### Why this matters

Currently `BattleController.PlayerAttack()` fires `OnDamageDealt` (which drives HP bar, floating numbers, and status message) the moment the attack is called — before the attack animation reaches the hit frame. This task defers those visual events until the animation clip fires a Unity Animation Event on the hit frame. A `_playerDamageVisualsFired` guard ensures they fire exactly once; a safety-net in the coroutine fires them at the end of `_actionDelay` if the animation event was never called (e.g. editor setup not complete).

- [ ] **Step 1: Add `OnHitFrame` event and `AnimEvent_OnHit()` to `PlayerBattleAnimator`**

Read `Assets/Scripts/Battle/PlayerBattleAnimator.cs` first, then add below the three existing hash fields:

```csharp
/// <summary>
/// Fired by Unity Animation Event on the hit frame of the attack clip.
/// BattleController subscribes to trigger damage visual feedback at the right moment.
/// </summary>
public event System.Action OnHitFrame;

/// <summary>
/// Called by Unity Animation Event on the attack clip's hit frame.
/// The method name must match exactly what is set in the Animation Event inspector.
/// </summary>
public void AnimEvent_OnHit() => OnHitFrame?.Invoke();
```

- [ ] **Step 2: Add `OnHitFrame` event and `AnimEvent_OnHit()` to `EnemyBattleAnimator`**

Apply the identical addition to `Assets/Scripts/Battle/EnemyBattleAnimator.cs`.

- [ ] **Step 3: Extend `BattleController` — store pending results and defer visual events**

Read `Assets/Scripts/Battle/BattleController.cs` first. Apply these changes:

**3a. Add two private fields** in the `// ── Private fields ─` block, after `_animationService`:

```csharp
private AttackResult _pendingPlayerAttack;
private AttackResult _pendingEnemyAttack;
private bool _playerDamageVisualsFired;
private bool _enemyDamageVisualsFired;
```

**3b. Add two private methods** near the end of the class, before `OnDestroy`:

```csharp
private void FirePlayerDamageVisuals()
{
    if (_playerDamageVisualsFired) return;
    _playerDamageVisualsFired = true;
    OnDamageDealt?.Invoke(_enemyStats, _pendingPlayerAttack.Damage, _pendingPlayerAttack.IsCrit);
    if (_pendingPlayerAttack.TargetDefeated)
        OnCharacterDefeated?.Invoke(_enemyStats);
}

private void FireEnemyDamageVisuals()
{
    if (_enemyDamageVisualsFired) return;
    _enemyDamageVisualsFired = true;
    OnDamageDealt?.Invoke(_playerStats, _pendingEnemyAttack.Damage, _pendingEnemyAttack.IsCrit);
    if (_pendingEnemyAttack.TargetDefeated)
        OnCharacterDefeated?.Invoke(_playerStats);
}
```

**3c. Replace `PlayerAttack()`** with this version (store result, defer visuals; fire immediately only when no animators):

```csharp
public void PlayerAttack()
{
    if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
    if (_isProcessingAction) return;
    _isProcessingAction = true;

    OnPlayerActionStarted?.Invoke();

    _pendingPlayerAttack      = _actionHandler.ExecuteAttack();
    _playerDamageVisualsFired = false;

    // Immediate fallback: fire visuals now if animators are not wired (no hit frame will come).
    if (_animationService == null)
        FirePlayerDamageVisuals();

    StartCoroutine(CompletePlayerAction(_pendingPlayerAttack.TargetDefeated));
}
```

**3d. Replace `CompletePlayerAction()`** with this version (safety-net + flag reset):

```csharp
private System.Collections.IEnumerator CompletePlayerAction(bool targetDefeated)
{
    yield return new WaitForSeconds(_actionDelay);
    // Safety net: fires if the animation event was never triggered (e.g. event not yet set up in editor).
    FirePlayerDamageVisuals();
    _playerDamageVisualsFired = false;
    _isProcessingAction = false;
    _battleManager.OnPlayerActionComplete(targetDefeated);
}
```

**3e. Replace `ExecuteEnemyTurn()`** with this version:

```csharp
private void ExecuteEnemyTurn()
{
    OnEnemyActionStarted?.Invoke();

    _pendingEnemyAttack      = _enemyActionHandler.ExecuteAttack();
    _enemyDamageVisualsFired = false;

    // Immediate fallback: fire visuals now if animators are not wired.
    if (_animationService == null)
        FireEnemyDamageVisuals();

    StartCoroutine(CompleteEnemyAction(_pendingEnemyAttack.TargetDefeated));
}
```

**3f. Replace `CompleteEnemyAction()`** with this version:

```csharp
private System.Collections.IEnumerator CompleteEnemyAction(bool targetDefeated)
{
    yield return new WaitForSeconds(_actionDelay);
    FireEnemyDamageVisuals();
    _enemyDamageVisualsFired = false;
    _battleManager.OnEnemyActionComplete(targetDefeated);
}
```

**3g. Subscribe to hit frame events in `Initialize()`** — add inside the `if (_playerAnimator != null && _enemyAnimator != null)` block, after the existing animation service subscriptions:

```csharp
_playerAnimator.OnHitFrame += FirePlayerDamageVisuals;
_enemyAnimator.OnHitFrame  += FireEnemyDamageVisuals;
```

**3h. Unsubscribe in the teardown guard at the top of `Initialize()`** — add inside the `if (_animationService != null)` block, after the existing unwires:

```csharp
_playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
_enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
```

**3i. Unsubscribe in `OnDestroy()`** — add inside the `if (_animationService != null)` block, after the existing unwires:

```csharp
if (_playerAnimator != null) _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
if (_enemyAnimator  != null) _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
```

- [ ] **Step 4: Verify compile — no errors in Unity Editor console**

- [ ] **Step 5: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs`
- `Assets/Scripts/Battle/BattleController.cs`

Check in with message: `feat: defer damage visuals to animation hit frame with safety-net fallback`

---

> **Unity Editor task (user) — Step 6: Add Animation Events to attack clips**

Open each attack clip in the Animation window (select the character's sprite GameObject, open Window → Animation → Animation, choose the attack clip from the dropdown).

At the frame where the hit should land:
1. Click **Add Event** (the flag icon in the timeline toolbar).
2. In the Inspector for the new event, set **Function** to `AnimEvent_OnHit`.
3. The method is on `PlayerBattleAnimator` (for the player clip) and `EnemyBattleAnimator` (for the enemy clip) — make sure the component is on the same GameObject as the Animator.

Do this for:
- `playerAttackRight` clip → event on player's `PlayerBattleAnimator`
- `enemyAttackRight` clip → event on enemy's `EnemyBattleAnimator`

**Check in:**
Unity Version Control → stage modified `.anim` files.
Check in with message: `assets: add hit-frame Animation Events to player and enemy attack clips`

---

> **Play Mode smoke test — Step 7**

Press **Play**. Click Attack.

| Expected | If wrong |
|---|---|
| Floating damage number appears at the hit frame, not at turn start | Animation Event not set up — check function name matches exactly `AnimEvent_OnHit` |
| Enemy hurt animation plays at the same time as floating number | Event is on wrong frame — move it earlier/later in the timeline |
| HP bar updates at hit frame | Same as above |
| If you remove the animation event entirely, damage still appears (safety net fires at end of delay) | `_animationService == null` guard in `PlayerAttack()` is wrong — recheck |

---

## Task 2: Step-In Attack Movement

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- Modify: `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

The step-in coroutine moves the sprite child horizontally toward the enemy (player steps right, enemy steps left) when `TriggerAttack()` is called, then returns to the original position. Direction is hardcoded since the battle layout is fixed: player always left, enemy always right.

- [ ] **Step 1: Add step-in fields and coroutine to `PlayerBattleAnimator`**

Read the file first, then add:

**New serialized fields** after the existing `[SerializeField] private Animator _animator`:

```csharp
[SerializeField]
[Tooltip("World units to step toward the enemy when attacking.")]
private float _stepDistance = 0.5f;

[SerializeField]
[Tooltip("Seconds to travel the step distance (each way).")]
private float _stepDuration = 0.1f;
```

**New private field** after the hash constants:

```csharp
private Vector3 _originalLocalPosition;
```

**New `Start()` method** (if one doesn't exist; if it does, add this line to it):

```csharp
private void Start()
{
    _originalLocalPosition = transform.localPosition;
}
```

**Replace `TriggerAttack()`** to start the step coroutine:

```csharp
public void TriggerAttack()
{
    _animator.SetTrigger(AttackHash);
    StartCoroutine(StepSequence(Vector3.right));
}
```

**New private coroutine** at the end of the class:

```csharp
private System.Collections.IEnumerator StepSequence(Vector3 direction)
{
    // Step toward enemy
    float elapsed = 0f;
    Vector3 stepTarget = _originalLocalPosition + direction * _stepDistance;
    while (elapsed < _stepDuration)
    {
        elapsed += Time.deltaTime;
        transform.localPosition = Vector3.Lerp(_originalLocalPosition, stepTarget, elapsed / _stepDuration);
        yield return null;
    }
    transform.localPosition = stepTarget;

    // Brief hold at the extended position
    yield return new WaitForSeconds(_stepDuration);

    // Return to origin
    elapsed = 0f;
    while (elapsed < _stepDuration)
    {
        elapsed += Time.deltaTime;
        transform.localPosition = Vector3.Lerp(stepTarget, _originalLocalPosition, elapsed / _stepDuration);
        yield return null;
    }
    transform.localPosition = _originalLocalPosition;
}
```

- [ ] **Step 2: Apply the same changes to `EnemyBattleAnimator`**

Identical fields and coroutine, except `TriggerAttack()` steps in the opposite direction:

```csharp
public void TriggerAttack()
{
    _animator.SetTrigger(AttackHash);
    StartCoroutine(StepSequence(Vector3.left));  // ← enemy steps left toward player
}
```

- [ ] **Step 3: Verify compile — no errors in Unity Editor console**

- [ ] **Step 4: Play Mode smoke test**

Press **Play**. Click Attack. Verify:
- Player briefly slides right toward the enemy before returning to original position
- Enemy briefly slides left toward the player on their turn
- Neither character leaves its side of the screen
- Original positions are correctly restored after each attack (attack again and again — no drift)

If character drifts over time, `_originalLocalPosition` is being captured after a previous step; check that `Start()` captures it correctly before any attack.

- [ ] **Step 5: Check in**

Unity Version Control → stage:
- `Assets/Scripts/Battle/PlayerBattleAnimator.cs`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

Check in with message: `feat: add step-in attack movement to player and enemy battle animators`

---

## Task 3: Turn Indicator Above Sprites

**Files:**
- Modify: `Assets/Scripts/Battle/UI/TurnIndicatorUI.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`

Currently `TurnIndicatorUI.SetActiveTarget(RectTransform)` receives HP-panel RectTransforms from `BattleHUD`. The fix changes the parameter to `Transform` (base class of `RectTransform` — backward compatible) and adds world-to-screen conversion so sprite Transforms can be passed directly. `BattleHUD` gets two new `[SerializeField] Transform` fields for the sprite GameObjects and passes them instead.

- [ ] **Step 1: Update `TurnIndicatorUI`**

Read the file first, then apply:

**Change `_currentTarget` field type** from `RectTransform` to `Transform`:

```csharp
private Transform _currentTarget;
```

**Replace `SetActiveTarget`** with the world-to-screen–aware version:

```csharp
/// <summary>
/// Moves the arrow above the given target and (re)starts the bob.
/// Accepts both Canvas RectTransforms and world-space Transforms.
/// Pass null to hide the indicator.
/// </summary>
public void SetActiveTarget(Transform target)
{
    _currentTarget = target;

    if (_bobCoroutine != null)
        StopCoroutine(_bobCoroutine);

    if (target == null)
    {
        _arrowRect.gameObject.SetActive(false);
        return;
    }

    _arrowRect.gameObject.SetActive(true);
    _arrowRect.position = ScreenPositionOf(target) + Vector3.up * _yOffset;
    _bobCoroutine = StartCoroutine(Bob());
}
```

**Replace `Bob()`** to re-evaluate world position each frame (handles world-space targets that could move):

```csharp
private IEnumerator Bob()
{
    float elapsed = 0f;

    while (true)
    {
        elapsed += Time.deltaTime;
        Vector3 baseScreen = ScreenPositionOf(_currentTarget);
        _arrowRect.position = baseScreen + Vector3.up * (_yOffset + _bobHeight * Mathf.Sin(elapsed * _bobSpeed));
        yield return null;
    }
}
```

**Add helper method** at the end of the class:

```csharp
/// <summary>
/// Returns the screen-space position of a transform.
/// Canvas RectTransforms (Screen Space – Overlay) already use screen coordinates;
/// world-space Transforms are converted via the main camera.
/// </summary>
private Vector3 ScreenPositionOf(Transform target)
{
    if (target is RectTransform)
        return target.position;

    return Camera.main.WorldToScreenPoint(target.position);
}
```

- [ ] **Step 2: Update `BattleHUD`**

Read the file first, then add two new serialized fields in the `[Header("Party Panel")]` and `[Header("Enemy Panel")]` sections respectively:

```csharp
[Header("Sprite Transforms (for turn indicator)")]
[SerializeField] private Transform _playerSpriteTransform;
[SerializeField] private Transform _enemySpriteTransform;
```

In `HandleStateChanged`, update the two `SetActiveTarget` calls:

```csharp
if (state == BattleState.PlayerTurn)
{
    _turnIndicatorUI.SetActiveTarget(_playerSpriteTransform);
    _statusMessageUI.Post("Your turn.");
}
else if (state == BattleState.EnemyTurn)
{
    _turnIndicatorUI.SetActiveTarget(_enemySpriteTransform);
    _statusMessageUI.Post($"{_enemyStats.Name}'s turn.");
}
```

- [ ] **Step 3: Verify compile — no errors in Unity Editor console**

- [ ] **Step 4: Check in**

Unity Version Control → stage:
- `Assets/Scripts/Battle/UI/TurnIndicatorUI.cs`
- `Assets/Scripts/Battle/UI/BattleHUD.cs`

Check in with message: `feat: turn indicator accepts world-space transforms for sprite-level positioning`

---

> **Unity Editor task (user) — Step 5: Assign sprite transforms in BattleHUD Inspector**

1. Select the BattleHUD GameObject in the Hierarchy.
2. In the Inspector, locate the new **Player Sprite Transform** and **Enemy Sprite Transform** fields.
3. Assign the sprite child GameObjects (the ones holding the SpriteRenderer + Animator) to each field — not the root character GameObjects, not the Canvas elements.

> **Play Mode smoke test — Step 6**

Press **Play**. Verify:
- The ▼ arrow bobs above Kaelen's sprite on the player's turn
- The ▼ arrow bobs above the enemy's sprite on the enemy's turn
- Arrow disappears on Victory/Defeat/Fled
- Arrow correctly tracks position if sprites were moved in the Scene

---

## Self-Review

### Spec Coverage

| Improvement | Covered by |
|---|---|
| Floating numbers / HP bar appear at hit frame, not turn start | Task 1 (deferred `OnDamageDealt` to animation event hit frame) |
| Enemy hurt animation plays at hit frame | Task 1 (hurt is triggered by `OnDamageDealt` → `BattleAnimationService.OnDamageDealt`, so it automatically defers) |
| Safety net if animation event not set up | Task 1 (`FirePlayerDamageVisuals()` safety-net call in `CompletePlayerAction`) |
| Player steps toward enemy when attacking | Task 2 (`PlayerBattleAnimator.StepSequence(Vector3.right)`) |
| Enemy steps toward player when attacking | Task 2 (`EnemyBattleAnimator.StepSequence(Vector3.left)`) |
| No position drift over repeated attacks | Task 2 (`_originalLocalPosition` captured in `Start()`) |
| Turn indicator above sprite, not HP panel | Task 3 (`TurnIndicatorUI` world-to-screen + `BattleHUD` sprite Transforms) |
| Backward compatible with existing RectTransform callers | Task 3 (`RectTransform is Transform` — `ScreenPositionOf` returns `.position` directly for Canvas elements) |

### Placeholder Scan

No TBDs or incomplete steps found.

### Type Consistency

- `FirePlayerDamageVisuals` / `FireEnemyDamageVisuals` are `void` — match the `System.Action` delegate signature required for `_playerAnimator.OnHitFrame += ...` subscriptions. ✓
- `TurnIndicatorUI.SetActiveTarget(Transform)` — `RectTransform` IS-A `Transform`, so existing `BattleHUD` callers passing `_partySlotRect`/`_enemySlotRect` remain valid even if not updated. ✓
- `ScreenPositionOf(Transform)` returns `Vector3` — consistent with existing `_arrowRect.position` assignment (Vector3). ✓
- `_stepDistance` / `_stepDuration` are `float` — `Vector3.Lerp` and `WaitForSeconds` both take `float`. ✓
