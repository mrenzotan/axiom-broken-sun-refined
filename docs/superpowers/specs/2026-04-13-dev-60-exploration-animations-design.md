# DEV-60: Exploration Animations — Player Attack & Enemy Movement

**Date:** 2026-04-13
**Status:** Approved

## Goal

Wire existing animation clips into the exploration (platformer) scene for two entity types:

1. **Player** — play the attack animation when the player triggers an exploration attack, lock movement for its duration, and defer the battle scene transition until the clip ends.
2. **Enemy (Ice Slime)** — drive idle/move animations from patrol state in the exploration scene.

No new animation clips are required — all referenced clips already exist.

---

## Sub-system 1: Player Attack Animation

### Animator Controller (`Assets/Animations/Player/Player.controller`)

- Add an `Attack` trigger parameter.
- Add two new states: `AttackRight` and `AttackLeft`, playing `playerAttackRight.anim` and `playerAttackLeft.anim` respectively.
- Transitions from any movement state fire when `Attack` is triggered and `IsFacingRight` matches the state. `Exit Time` is disabled on these transitions — trigger-driven only.
- Both attack states transition back to the blend tree on clip end (exit time enabled for the return transition).

### Animation Event Bridge

The `Animator` component lives on the `Animator` child GameObject of the Player (not the root). Unity Animation Events fire on MonoBehaviours on the **same GameObject as the Animator**, so a bridge component is required.

- Add a new `PlayerAnimationEventReceiver : MonoBehaviour` to the `Animator` child.
- In `Start()`, it calls `GetComponentInParent<PlayerController>()` to obtain a reference.
- Both `playerAttackLeft.anim` and `playerAttackRight.anim` get an Animation Event at their last frame calling `OnAttackEnd()` on this receiver.
- `OnAttackEnd()` delegates to `PlayerController.OnAttackAnimationEnd()`.

### `PlayerAnimator` changes

- Add a `TriggerAttack()` method that sets the `Attack` trigger on the Animator.

### `PlayerMovement` changes

- Add `SetMovementLocked(bool locked)`. When locked, `Move()` applies zero horizontal velocity regardless of input.

### `PlayerController` changes

- Add `BeginAttack(ExplorationEnemyCombatTrigger pending)`:
  - Calls `_movement.SetMovementLocked(true)`
  - Calls `_playerAnimator.TriggerAttack()`
  - Stores `pending` in a `_pendingAttackTrigger` field
- Add `OnAttackAnimationEnd()` (called by `PlayerAnimationEventReceiver`):
  - Calls `_movement.SetMovementLocked(false)`
  - Calls `_pendingAttackTrigger?.TriggerAdvantagedBattle()`
  - Clears `_pendingAttackTrigger`

### `PlayerExplorationAttack` changes

- Replace the direct `trigger?.TriggerAdvantagedBattle()` call with `GetComponent<PlayerController>().BeginAttack(trigger)`.
- The scene transition is now deferred to the end of the attack animation.

### Attack flow

```
Player presses Attack
  → PlayerExplorationAttack detects enemy in range
  → PlayerController.BeginAttack(trigger)
      → movement locked
      → attack animation plays
  → clip ends → Animation Event fires
  → PlayerAnimationEventReceiver.OnAttackEnd()
  → PlayerController.OnAttackAnimationEnd()
      → movement unlocked
      → SceneManager.LoadScene("Battle") via TriggerAdvantagedBattle()
```

---

## Sub-system 2: Enemy Exploration Animation

### Prefab update (`Assets/Prefabs/Enemies/Enemy.prefab`)

- Rename the `Visual` child GameObject → `Animator`.
- Add an `Animator` component to the `Animator` child (currently only has `SpriteRenderer`).
- Assign `IceSlimeExploration.controller` to it.
- Update the doc comment in `EnemyController.cs` to reflect the renamed child.

### New Animator Controller (`Assets/Animations/Enemies/Ice Slime/IceSlimeExploration.controller`)

- Single `IsMoving` bool parameter — this is the **parameter contract** all exploration enemy controllers must honour.
- Two states:
  - `Idle` — plays `iceSlimeIdleRight.anim`
  - `Move` — plays `iceSlimeMoveRight.anim`
- Transitions: `IsMoving == true` → Move; `IsMoving == false` → Idle.
- Sprite direction (left vs right) is handled by `EnemyController` flipping `visualTransform.localScale.x` — no separate left-facing state or `iceSlimeMoveLeft.anim` clip needed in this controller. `iceSlimeMoveLeft.anim` is retained for the battle controller.

### New `EnemyAnimator` plain C# class (`Assets/Scripts/Platformer/EnemyAnimator.cs`)

Mirrors the existing `PlayerAnimator` pattern — plain C#, no MonoBehaviour.

```
Constructor: EnemyAnimator(Animator animator)
Tick(float xVelocity): sets IsMoving = Abs(xVelocity) > 0.01f
```

Reusable across all exploration enemies. Each enemy type supplies its own Animator Controller that exposes the `IsMoving` bool parameter.

### `EnemyController` changes

- Add `[SerializeField] private Animator _animator;` under the Visual header (assigned in Inspector to the `Animator` child).
- Create `_enemyAnimator = new EnemyAnimator(_animator)` in `Awake()`.
- Call `_enemyAnimator.Tick(xVel)` in `FixedUpdate()` after computing `xVel`.

---

## File Map

| Action | Path | Notes |
|--------|------|-------|
| Modify | `Assets/Animations/Player/Player.controller` | Add Attack trigger + attack states |
| Modify | `Assets/Animations/Player/playerAttackLeft.anim` | Add Animation Event at last frame |
| Modify | `Assets/Animations/Player/playerAttackRight.anim` | Add Animation Event at last frame |
| Create | `Assets/Scripts/Platformer/PlayerAnimationEventReceiver.cs` | Bridge on Visual child |
| Modify | `Assets/Scripts/Platformer/PlayerAnimator.cs` | Add TriggerAttack() |
| Modify | `Assets/Scripts/Platformer/PlayerMovement.cs` | Add SetMovementLocked() |
| Modify | `Assets/Scripts/Platformer/PlayerController.cs` | Add BeginAttack(), OnAttackAnimationEnd() |
| Modify | `Assets/Scripts/Platformer/PlayerExplorationAttack.cs` | Defer transition via BeginAttack() |
| Modify | `Assets/Prefabs/Enemies/Enemy.prefab` | Rename Visual→Animator, add Animator component |
| Create | `Assets/Animations/Enemies/Ice Slime/IceSlimeExploration.controller` | Exploration Animator Controller |
| Create | `Assets/Scripts/Platformer/EnemyAnimator.cs` | Plain C# animation driver |
| Modify | `Assets/Scripts/Platformer/EnemyController.cs` | Add _animator field + EnemyAnimator wiring |

---

## Architecture Notes

- `EnemyAnimator` and `EnemyController` are enemy-type agnostic. Future enemies reuse both; each supplies its own Animator Controller that honours the `IsMoving` bool contract.
- `PlayerAnimationEventReceiver` is a thin bridge only — no logic, just delegation to `PlayerController`.
- Movement lock lives in `PlayerMovement`, keeping `PlayerController` as the coordinator and `PlayerMovement` as the single authority over velocity.
