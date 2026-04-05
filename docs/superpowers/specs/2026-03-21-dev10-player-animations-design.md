# DEV-10 Player Animations — Design Spec

## Scope

Phase 1 only: Idle, Run_R, Run_L, Jump_R, Jump_L, Fall_R, Fall_L wired and functional.
Phase 2/3 clips (Hurt, Death, Attack_L/R, Cast_L/R) created but disconnected — no transitions.

## Sprite Sheet

- `Assets/Art/Player/Player_SpriteSheet.png` — one animation per row, all rows sliced
- Direction: separate L/R clips (no flipX) — catalyst arm is visually asymmetric
- All `.anim` clips already created by the developer

## Animator Controller

**File:** `Assets/Animations/Player/PlayerAnimator.controller`

### Parameters

| Name | Type |
|---|---|
| IsGrounded | Bool |
| IsFacingRight | Bool |
| VelocityX | Float |
| VelocityY | Float |

### States

| State | Type | Motion |
|---|---|---|
| Grounded | Blend Tree (1D) | VelocityX: −1→Run_L, 0→Idle, +1→Run_R |
| Jump_R | State | Player_Jump_R clip |
| Jump_L | State | Player_Jump_L clip |
| Fall_R | State | Player_Fall_R clip |
| Fall_L | State | Player_Fall_L clip |
| Hurt | State (no transitions) | Player_Hurt clip |
| Death | State (no transitions) | Player_Death clip |
| Attack_R | State (no transitions) | Player_Attack_R clip |
| Attack_L | State (no transitions) | Player_Attack_L clip |
| Cast_R | State (no transitions) | Player_Cast_R clip |
| Cast_L | State (no transitions) | Player_Cast_L clip |

Write Defaults: OFF on all states.

### Transitions (all: Has Exit Time OFF, Duration 0, Offset 0)

| From | To | Conditions |
|---|---|---|
| Grounded | Jump_R | !IsGrounded, VelocityY >= 0, IsFacingRight |
| Grounded | Jump_L | !IsGrounded, VelocityY >= 0, !IsFacingRight |
| Grounded | Fall_R | !IsGrounded, VelocityY < 0, IsFacingRight |
| Grounded | Fall_L | !IsGrounded, VelocityY < 0, !IsFacingRight |
| Jump_R | Fall_R | VelocityY < 0 |
| Jump_L | Fall_L | VelocityY < 0 |
| Fall_R | Grounded | IsGrounded |
| Fall_L | Grounded | IsGrounded |

## PlayerAnimator.cs

Plain C# class (not MonoBehaviour). Injected into `PlayerController`.

- Constructor: `(Animator animator, PlayerMovement movement)`
- `Tick(float moveInput)` called from `PlayerController.Update()`
- Tracks `_facingRight` from last non-zero moveInput
- Sets all 4 parameters each frame using `Animator.StringToHash` cached IDs
- VelocityX normalized to −1 / 0 / +1 (Mathf.Sign(0) returns 1 in Unity — handled explicitly)

## PlayerController changes

- Add `private PlayerAnimator _playerAnimator`
- Construct it in `Awake()` after `_animator` is assigned
- Call `_playerAnimator.Tick(_moveInput)` at end of `Update()`
