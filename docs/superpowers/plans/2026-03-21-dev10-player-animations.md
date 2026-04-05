# DEV-10 Player Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire idle, run, jump, and fall sprite animations into a working Animator Controller and drive them from a plain C# `PlayerAnimator` service.

**Architecture:** `PlayerAnimator` (plain C#) reads state from `PlayerMovement` and sets Animator parameters each frame. `PlayerController` (MonoBehaviour) owns both and calls `PlayerAnimator.Tick()` from `Update()`. The Animator Controller has **four blend tree states** total:

| State | Blend param | Motions |
|---|---|---|
| `Grounded_R` | `VelocityX` (0 → 1) | `playerIdleRight`, `playerMoveRight` |
| `Grounded_L` | `VelocityX` (-1 → 0) | `playerMoveLeft`, `playerIdleLeft` |
| `JumpFall_R` | `VelocityY` (-1 → 1) | `playerFallRight`, `playerJumpRight` |
| `JumpFall_L` | `VelocityY` (-1 → 1) | `playerFallLeft`, `playerJumpLeft` |

> **Why JumpFall blend trees instead of 4 separate states?** Consolidating jump + fall into one blend tree per facing direction removes the need for jump→fall apex transitions. `VelocityY` drives the blend smoothly: positive = jump clip, negative = fall clip. Mid-air facing changes are handled by `JumpFall_R ↔ JumpFall_L` transitions on `IsFacingRight`. Result: 8 transitions instead of 14, and no hard frame cut at the apex.

**Tech Stack:** Unity 6 LTS, URP 2D, Unity Animator, New Input System, C#

**Division of labour:**
- 🖱 **Unity Editor steps** — done by you in the Unity Editor
- 💻 **Script steps** — Claude writes the code directly

---

## Files

| Action | Path | Responsibility |
|---|---|---|
| ~~Modify~~ ✅ | `Assets/Animations/Player/PlayerAnimator.controller` | All states, blend trees, transitions |
| ~~Create~~ ✅ | `Assets/Scripts/Platformer/PlayerAnimator.cs` | Plain C# — sets Animator params each frame |
| ~~Modify~~ ✅ | `Assets/Scripts/Platformer/PlayerController.cs` | Owns PlayerAnimator, calls Tick in Update |

---

## Animation Clip Name Reference

| Animation | Actual clip name |
|---|---|
| Idle (right) | `playerIdleRight` |
| Idle (left) | `playerIdleLeft` |
| Run right | `playerMoveRight` |
| Run left | `playerMoveLeft` |
| Jump right | `playerJumpRight` |
| Jump left | `playerJumpLeft` |
| Fall right | `playerFallRight` |
| Fall left | `playerFallLeft` |

---

## Task 1: Set up the Animator Controller ✅

Parameters added, old states cleaned up.

| Parameter | Type |
|---|---|
| `IsGrounded` | Bool |
| `IsFacingRight` | Bool |
| `VelocityX` | Float |
| `VelocityY` | Float |

---

## Task 2: Set up the two Grounded Blend Trees ✅

### Grounded_R (default state — orange)

- 1D blend on **VelocityX**
- Threshold `0` → `playerIdleRight` (Loop ON)
- Threshold `1` → `playerMoveRight` (Loop ON)
- Write Defaults: OFF

### Grounded_L

- 1D blend on **VelocityX**
- Threshold `-1` → `playerMoveLeft` (Loop ON)
- Threshold `0` → `playerIdleLeft` (Loop ON)
- Write Defaults: OFF

---

## Task 3: Set up the two JumpFall Blend Trees ✅

Replaces the originally planned 4 separate air states (Jump_R, Jump_L, Fall_R, Fall_L). Each blend tree covers both jump and fall for one facing direction, driven by `VelocityY`.

### JumpFall_R

- 1D blend on **VelocityY**
- Threshold `-1` → `playerFallRight` (Loop OFF)
- Threshold `1` → `playerJumpRight` (Loop OFF)
- Write Defaults: OFF

### JumpFall_L

- 1D blend on **VelocityY**
- Threshold `-1` → `playerFallLeft` (Loop OFF)
- Threshold `1` → `playerJumpLeft` (Loop OFF)
- Write Defaults: OFF

---

## Task 4: Add all transitions ✅

Eight transitions total. All: **Has Exit Time OFF**, **Transition Duration 0**, **Transition Offset 0**.

### Grounded facing switches

| From | To | Condition |
|---|---|---|
| `Grounded_R` | `Grounded_L` | `IsFacingRight` = false |
| `Grounded_L` | `Grounded_R` | `IsFacingRight` = true |

### Grounded ↔ Air

| From | To | Condition |
|---|---|---|
| `Grounded_R` | `JumpFall_R` | `IsGrounded` = false |
| `JumpFall_R` | `Grounded_R` | `IsGrounded` = true |
| `Grounded_L` | `JumpFall_L` | `IsGrounded` = false |
| `JumpFall_L` | `Grounded_L` | `IsGrounded` = true |

### Air facing switches (mid-air direction change)

| From | To | Condition |
|---|---|---|
| `JumpFall_R` | `JumpFall_L` | `IsFacingRight` = false |
| `JumpFall_L` | `JumpFall_R` | `IsFacingRight` = true |

---

## Task 5: Add placeholder states (Phase 2/3) ✅

Unreachable states — clips registered in the controller for future phases. No transitions.

| State name | Motion clip |
|---|---|
| `playerHurtRight` | `playerHurtRight` |
| `playerHurtLeft` | `playerHurtLeft` |
| `playerDeath` | `playerDeath` |
| `playerAttackRight` | `playerAttackRight` |
| `playerAttackLeft` | `playerAttackLeft` |
| `playerCastRight` | `playerCastRight` |
| `playerCastLeft` | `playerCastLeft` |

---

## Task 6: Write PlayerAnimator.cs ✅

Plain C# class — no MonoBehaviour. Pushes four Animator parameters each frame.

**Key behaviour:**
- `_facingRight` tracks last intentional input direction — holds at idle so facing is preserved when stopped
- `normalizedX` explicitly handles `VelocityX = 0` (Mathf.Sign(0f) returns 1 in Unity — would wrongly drive Run_R at idle)
- `VelocityY` passed through raw — `JumpFall` blend trees use it directly to blend between jump and fall clips
- `Debug.Assert` on `animator` null — stripped from release builds, catches missing Animator component during dev

**File:** `Assets/Scripts/Platformer/PlayerAnimator.cs` ✅

---

## Task 7: Wire PlayerAnimator into PlayerController ✅

- `_animator = GetComponentInChildren<Animator>(true)` — includes inactive children
- Falls back to `FindAnyObjectByType<Animator>()` with a LogWarning if not found on hierarchy
- `_playerAnimator = new PlayerAnimator(_animator, _movement)` constructed after `_movement`
- `_playerAnimator.Tick(_moveInput)` called at end of `Update()`

**File:** `Assets/Scripts/Platformer/PlayerController.cs` ✅

---

## Task 8: Verify in Play Mode ✅

| Action | Expected |
|---|---|
| Stand still (last moved right) | `playerIdleRight` plays |
| Stand still (last moved left) | `playerIdleLeft` plays |
| Walk right | `playerMoveRight` plays |
| Walk left | `playerMoveLeft` plays |
| Stop after walking right | Snaps to `playerIdleRight` |
| Stop after walking left | Snaps to `playerIdleLeft` |
| Jump (facing right) | `playerJumpRight` → blends into `playerFallRight` at apex |
| Jump (facing left) | `playerJumpLeft` → blends into `playerFallLeft` at apex |
| Change direction mid-air | `JumpFall_R ↔ JumpFall_L` switches immediately |
| Walk off ledge (facing right) | `playerFallRight` plays immediately (VelocityY < 0) |
| Land facing right | Returns to `Grounded_R` |
| Land facing left | Returns to `Grounded_L` |
