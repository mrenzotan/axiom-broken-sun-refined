# Pushable Ice Block Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable 4x4-tile pushable ice block that falls with gravity, can cover spike triggers physically, and can be stood on to reach higher platforms.

**Architecture:** Keep the ice block as a normal Unity 2D prefab with a `SpriteRenderer`, `BoxCollider2D`, `Rigidbody2D`, and one thin MonoBehaviour wrapper. Put push decision math in a plain C# helper and keep player speed changes inside `PlayerMovement`; `PlayerController` only exposes a small request method that delegates to the movement object.

**Tech Stack:** Unity 6 LTS, C#, Unity 2D Physics, Unity Test Framework Edit Mode tests, UVCS

---

## Assumptions

- Proposed Jira ticket: `DEV-111`. If Jira assigns a different ticket number, update the filename and UVCS messages before implementation.
- Tilemap cell size is `1x1` world units.
- Pixel art uses 16 pixels per unit. The first ice block sprite is `64x64`, which makes the prefab `4x4` world units.
- The player is tagged `Player` and has `PlayerController` plus `Rigidbody2D`.
- Spike and pit damage remains trigger-based through `HazardTrigger`; the ice block does not disable hazards. It prevents damage only by physically keeping the player collider out of the trigger volume.
- The block resets on scene reload or player death because its position is not saved.
- No VFX, SFX, push animation, crushing damage, or save persistence is part of this feature.

## Current Repo Context

- `PlayerController.FixedUpdate()` currently delegates to `PlayerMovement.Move(_moveInput)`.
- `PlayerMovement.Move(...)` writes `Rigidbody2D.linearVelocity.x` directly.
- `HazardTrigger` only damages colliders tagged `Player`, so the ice block does not need hazard-specific code.
- `Assets/Scripts/Platformer/Platformer.asmdef` already owns player/platformer code.
- Edit Mode platformer tests live in `Assets/Tests/Editor/Platformer/` and use `PlatformerTests` for older movement-adjacent classes.
- Unity docs confirm Dynamic `Rigidbody2D` bodies should be moved through physics state such as velocity, not by writing `Transform` position.

## File Structure

- Modify: `Assets/Scripts/Platformer/PlayerMovement.cs`
  - Adds a one-fixed-step external movement multiplier used while pushing.
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`
  - Adds a public request method that delegates to `PlayerMovement`.
- Create: `Assets/Scripts/Platformer/PushableIceBlockMotion.cs`
  - Plain C# helper for side-contact and push-velocity decisions.
- Create: `Assets/Scripts/Platformer/PushableIceBlock.cs`
  - MonoBehaviour wrapper for collision detection, grounded check, and rigidbody velocity application.
- Create: `Assets/Tests/Editor/Platformer/PlayerMovementSpeedModifierTests.cs`
- Create: `Assets/Tests/Editor/Platformer/PushableIceBlockMotionTests.cs`
- Create: `Assets/Art/Sprites/Platformer/IceBlock64.png`
- Create: `Assets/PhysicsMaterials`
- Create: `Assets/PhysicsMaterials/IceBlock.physicsMaterial2D`
- Create: `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab`

---

### Task 1: Add One-Step Player Movement Slowdown Hook

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerMovement.cs`
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`
- Create: `Assets/Tests/Editor/Platformer/PlayerMovementSpeedModifierTests.cs`

- [ ] **Step 1: Write failing Edit Mode tests for the player movement speed modifier**

Create `Assets/Tests/Editor/Platformer/PlayerMovementSpeedModifierTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class PlayerMovementSpeedModifierTests
    {
        private GameObject _playerGo;
        private Rigidbody2D _rb;
        private Transform _groundCheck;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("Player");
            _rb = _playerGo.AddComponent<Rigidbody2D>();

            var groundCheckGo = new GameObject("GroundCheck");
            _groundCheck = groundCheckGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_groundCheck.gameObject);
            Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void Move_RequestedExternalMultiplier_ScalesHorizontalVelocity()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.Move(1f);

            Assert.AreEqual(5.6f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_MultipleRequestedMultipliers_UsesSmallestMultiplier()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.9f);
            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.Move(1f);

            Assert.AreEqual(5.6f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_AfterResetExternalMultiplier_RestoresFullSpeed()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.ResetExternalMoveSpeedMultiplier();
            movement.Move(1f);

            Assert.AreEqual(8f, _rb.linearVelocity.x, 0.001f);
        }

        [Test]
        public void Move_WhenMovementLocked_StopsEvenWithExternalMultiplier()
        {
            PlayerMovement movement = CreateMovement(moveSpeed: 8f);

            movement.RequestExternalMoveSpeedMultiplier(0.7f);
            movement.SetMovementLocked(true);
            movement.Move(1f);

            Assert.AreEqual(0f, _rb.linearVelocity.x, 0.001f);
        }

        private PlayerMovement CreateMovement(float moveSpeed)
        {
            return new PlayerMovement(
                _rb,
                _groundCheck,
                groundLayer: 0,
                oneWayLayer: 0,
                playerLayerIndex: _playerGo.layer,
                moveSpeed: moveSpeed,
                jumpForce: 16f,
                coyoteTime: 0.15f,
                jumpBufferTime: 0.15f,
                fallGravityMultiplier: 2.5f,
                groundCheckRadius: 0.1f,
                dropThroughDuration: 0.2f);
        }
    }
}
```

- [ ] **Step 2: Run the new tests and confirm they fail**

> **Unity Editor task (user):** Open Test Runner → Edit Mode → run `PlatformerTests.PlayerMovementSpeedModifierTests`. Expected: compile failure because `RequestExternalMoveSpeedMultiplier(...)` and `ResetExternalMoveSpeedMultiplier()` do not exist yet.

- [ ] **Step 3: Add movement multiplier state to `PlayerMovement`**

In `Assets/Scripts/Platformer/PlayerMovement.cs`, add this field beside the other movement state fields:

```csharp
private float _externalMoveSpeedMultiplier = 1f;
```

Add these public methods after `SetMovementLocked(...)`:

```csharp
/// <summary>
/// Requests a movement speed multiplier for the next Move() call.
/// Multiple requests use the smallest multiplier so heavier constraints win.
/// </summary>
public void RequestExternalMoveSpeedMultiplier(float multiplier)
{
    _externalMoveSpeedMultiplier = Mathf.Min(
        _externalMoveSpeedMultiplier,
        Mathf.Max(0f, multiplier));
}

/// <summary>
/// Restores normal movement speed after the current physics step has consumed
/// any external movement request.
/// </summary>
public void ResetExternalMoveSpeedMultiplier()
{
    _externalMoveSpeedMultiplier = 1f;
}
```

Replace `Move(float horizontalInput)` with:

```csharp
/// <summary>Apply horizontal velocity. Called from FixedUpdate.</summary>
public void Move(float horizontalInput)
{
    float velocity = _movementLocked ? 0f : horizontalInput * _moveSpeed * _externalMoveSpeedMultiplier;
    _rb.linearVelocity = new Vector2(velocity, _rb.linearVelocity.y);
}
```

- [ ] **Step 4: Expose a player-controller request method and reset after each physics move**

In `Assets/Scripts/Platformer/PlayerController.cs`, replace `FixedUpdate()` with:

```csharp
private void FixedUpdate()
{
    _movement.Move(_moveInput);
    _movement.ResetExternalMoveSpeedMultiplier();
}
```

Add this method near the other public player interaction methods:

```csharp
/// <summary>
/// Requests a temporary movement speed multiplier for the next physics move.
/// World objects use this to express pushing/slowdown without owning player input.
/// </summary>
public void RequestExternalMoveSpeedMultiplier(float multiplier)
{
    _movement?.RequestExternalMoveSpeedMultiplier(multiplier);
}
```

- [ ] **Step 5: Re-run the player movement tests**

> **Unity Editor task (user):** Re-run `PlatformerTests.PlayerMovementSpeedModifierTests`. Expected: all tests pass.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-111): add player push speed modifier`
- `Assets/Scripts/Platformer/PlayerMovement.cs`
- `Assets/Scripts/Platformer/PlayerController.cs`
- `Assets/Tests/Editor/Platformer/PlayerMovementSpeedModifierTests.cs`

---

### Task 2: Add Plain C# Push Decision Logic

**Files:**
- Create: `Assets/Scripts/Platformer/PushableIceBlockMotion.cs`
- Create: `Assets/Tests/Editor/Platformer/PushableIceBlockMotionTests.cs`

- [ ] **Step 1: Write failing Edit Mode tests for side-contact push rules**

Create `Assets/Tests/Editor/Platformer/PushableIceBlockMotionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class PushableIceBlockMotionTests
    {
        [Test]
        public void CanPush_GroundedSideContactAndPlayerMovingIntoBlock_ReturnsTrue()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.left,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsTrue(canPush);
        }

        [Test]
        public void CanPush_WhenBlockAirborne_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.left,
                blockGrounded: false,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void CanPush_TopContact_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: 5.6f,
                contactNormal: Vector2.up,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void CanPush_PlayerMovingAwayFromBlock_ReturnsFalse()
        {
            bool canPush = PushableIceBlockMotion.CanPush(
                playerX: 0f,
                blockX: 2f,
                playerVelocityX: -5.6f,
                contactNormal: Vector2.left,
                blockGrounded: true,
                sideNormalThreshold: 0.7f,
                minPushVelocity: 0.05f);

            Assert.IsFalse(canPush);
        }

        [Test]
        public void GetPushVelocityX_ClampsToMaxPushSpeed()
        {
            float velocity = PushableIceBlockMotion.GetPushVelocityX(
                playerVelocityX: 8f,
                maxPushSpeed: 5.6f);

            Assert.AreEqual(5.6f, velocity, 0.001f);
        }

        [Test]
        public void GetPushVelocityX_PreservesDirection()
        {
            float velocity = PushableIceBlockMotion.GetPushVelocityX(
                playerVelocityX: -8f,
                maxPushSpeed: 5.6f);

            Assert.AreEqual(-5.6f, velocity, 0.001f);
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm the helper is missing**

> **Unity Editor task (user):** Run `PlatformerTests.PushableIceBlockMotionTests`. Expected: compile failure because `PushableIceBlockMotion` does not exist.

- [ ] **Step 3: Add the plain C# motion helper**

Create `Assets/Scripts/Platformer/PushableIceBlockMotion.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Plain C# push decision helper for PushableIceBlock.
/// No MonoBehaviour lifecycle and no direct Rigidbody writes live here.
/// </summary>
public static class PushableIceBlockMotion
{
    public static bool CanPush(
        float playerX,
        float blockX,
        float playerVelocityX,
        Vector2 contactNormal,
        bool blockGrounded,
        float sideNormalThreshold,
        float minPushVelocity)
    {
        if (!blockGrounded)
            return false;

        if (Mathf.Abs(contactNormal.x) < sideNormalThreshold)
            return false;

        if (Mathf.Abs(contactNormal.x) <= Mathf.Abs(contactNormal.y))
            return false;

        if (Mathf.Abs(playerVelocityX) < minPushVelocity)
            return false;

        float directionFromPlayerToBlock = blockX - playerX;
        if (Mathf.Abs(directionFromPlayerToBlock) < 0.001f)
            return false;

        return Mathf.Sign(playerVelocityX) == Mathf.Sign(directionFromPlayerToBlock);
    }

    public static float GetPushVelocityX(float playerVelocityX, float maxPushSpeed)
    {
        float speed = Mathf.Min(Mathf.Abs(playerVelocityX), Mathf.Max(0f, maxPushSpeed));
        return Mathf.Sign(playerVelocityX) * speed;
    }
}
```

- [ ] **Step 4: Re-run the helper tests**

> **Unity Editor task (user):** Re-run `PlatformerTests.PushableIceBlockMotionTests`. Expected: all tests pass.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-111): add ice block push decision logic`
- `Assets/Scripts/Platformer/PushableIceBlockMotion.cs`
- `Assets/Tests/Editor/Platformer/PushableIceBlockMotionTests.cs`

---

### Task 3: Add the Pushable Ice Block MonoBehaviour Wrapper

**Files:**
- Create: `Assets/Scripts/Platformer/PushableIceBlock.cs`

- [ ] **Step 1: Create the MonoBehaviour wrapper**

Create `Assets/Scripts/Platformer/PushableIceBlock.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Unity wrapper for a reusable pushable ice block.
/// Handles collision callbacks and Rigidbody2D writes only; push decisions live in PushableIceBlockMotion.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PushableIceBlock : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Push")]
    [SerializeField, Range(0.1f, 1f)] private float playerPushSpeedMultiplier = 0.7f;
    [SerializeField, Min(0f)] private float maxPushSpeed = 5.6f;
    [SerializeField, Range(0.1f, 1f)] private float sideNormalThreshold = 0.7f;
    [SerializeField, Min(0f)] private float minPushVelocity = 0.05f;

    [Header("Stop Feel")]
    [SerializeField, Min(0f)] private float stopDeceleration = 18f;

    [Header("Ground Check")]
    [SerializeField, Min(0.001f)] private float groundCheckHeight = 0.08f;
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.08f;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private bool _hasPushRequest;
    private float _requestedVelocityX;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    private void Reset()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 1f;
        }

        Collider2D blockCollider = GetComponent<Collider2D>();
        if (blockCollider != null)
            blockCollider.isTrigger = false;
    }

    private void FixedUpdate()
    {
        float velocityX = _hasPushRequest
            ? _requestedVelocityX
            : Mathf.MoveTowards(_rb.linearVelocity.x, 0f, stopDeceleration * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector2(velocityX, _rb.linearVelocity.y);
        _hasPushRequest = false;
        _requestedVelocityX = 0f;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsInLayerMask(collision.gameObject.layer, playerLayer))
            return;

        if (!collision.collider.CompareTag("Player"))
            return;

        Rigidbody2D playerRb = collision.rigidbody;
        if (playerRb == null)
            return;

        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        bool blockGrounded = IsGrounded();
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (!PushableIceBlockMotion.CanPush(
                    playerX: playerRb.position.x,
                    blockX: _rb.position.x,
                    playerVelocityX: playerRb.linearVelocity.x,
                    contactNormal: contact.normal,
                    blockGrounded: blockGrounded,
                    sideNormalThreshold: sideNormalThreshold,
                    minPushVelocity: minPushVelocity))
            {
                continue;
            }

            player.RequestExternalMoveSpeedMultiplier(playerPushSpeedMultiplier);
            _requestedVelocityX = PushableIceBlockMotion.GetPushVelocityX(
                playerRb.linearVelocity.x,
                maxPushSpeed);
            _hasPushRequest = true;
            return;
        }
    }

    private bool IsGrounded()
    {
        Bounds bounds = _collider.bounds;
        var origin = new Vector2(bounds.center.x, bounds.min.y + groundCheckHeight * 0.5f);
        var size = new Vector2(bounds.size.x * 0.9f, groundCheckHeight);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
```

- [ ] **Step 2: Compile-check the wrapper**

> **Unity Editor task (user):** Let Unity recompile scripts. Expected: no compiler errors in `PushableIceBlock.cs`.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-111): add pushable ice block component`
- `Assets/Scripts/Platformer/PushableIceBlock.cs`

---

### Task 4: Create the Ice Block Sprite, Material, Layer, and Prefab

**Files:**
- Create: `Assets/Art/Sprites/Platformer/IceBlock64.png`
- Create: `Assets/PhysicsMaterials`
- Create: `Assets/PhysicsMaterials/IceBlock.physicsMaterial2D`
- Create: `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab`
- Modify: `ProjectSettings/TagManager.asset`
- Modify: `ProjectSettings/Physics2DSettings.asset`
- Modify: `Assets/Prefabs/Player/Player (Exploration).prefab`

- [ ] **Step 1: Import the 64x64 ice sprite**

> **Unity Editor task (user):** Create or import a `64x64` pixel ice block sprite at `Assets/Art/Sprites/Platformer/IceBlock64.png`. In the Texture Import Settings set:
> - Texture Type: `Sprite (2D and UI)`
> - Sprite Mode: `Single`
> - Pixels Per Unit: `16`
> - Filter Mode: `Point (no filter)`
> - Compression: `None`
> - Mesh Type: `Full Rect`
> Then click Apply.

- [ ] **Step 2: Create the PushableBlock layer**

> **Unity Editor task (user):** Open Project Settings → Tags and Layers. Add a layer named `PushableBlock`. Set the ice block prefab root to this layer after the prefab is created.

- [ ] **Step 3: Update collision matrix**

> **Unity Editor task (user):** Open Project Settings → Physics 2D. Configure `PushableBlock` collisions:
> - Collides with `Player`.
> - Collides with enemy layers used by exploration enemies so enemies treat it as a wall.
> - Collides with ground/tilemap layers so it lands on terrain.
> - Does not need to collide with hazard trigger layers unless the existing matrix already does so; hazards damage only the player tag.

- [ ] **Step 4: Create the ice physics material**

> **Unity Editor task (user):** Create the folder `Assets/PhysicsMaterials`, then create `Assets/PhysicsMaterials/IceBlock.physicsMaterial2D` with:
> - Friction: `0.15`
> - Bounciness: `0`

- [ ] **Step 5: Create the prefab root**

> **Unity Editor task (user):** Create a GameObject named `P_PushableIceBlock`, then add:
> - `SpriteRenderer` using `Assets/Art/Sprites/Platformer/IceBlock64.png`
> - `BoxCollider2D`, not trigger, size aligned to `4x4` world units
> - `Rigidbody2D`
> - `PushableIceBlock`
>
> Configure `Rigidbody2D`:
> - Body Type: `Dynamic`
> - Material: `IceBlock.physicsMaterial2D`
> - Gravity Scale: `1`
> - Mass: `8`
> - Linear Damping: `2`
> - Angular Damping: `0.05`
> - Collision Detection: `Continuous`
> - Constraints: `Freeze Rotation Z`
>
> Configure `PushableIceBlock`:
> - Player Layer: the layer containing the player object
> - Ground Layer: the layer containing the solid tilemap ground
> - Player Push Speed Multiplier: `0.7`
> - Max Push Speed: `5.6`
> - Side Normal Threshold: `0.7`
> - Min Push Velocity: `0.05`
> - Stop Deceleration: `18`
> - Ground Check Height: `0.08`
> - Ground Check Distance: `0.08`
>
> Save it as `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab`.

- [ ] **Step 6: Add PushableBlock to the player ground mask**

> **Unity Editor task (user):** Select the exploration player prefab or scene player using `PlayerController`. Add `PushableBlock` to `groundLayer` so the player can stand on the ice block and still report grounded.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-111): create pushable ice block prefab`
- `Assets/Art/Sprites/Platformer/IceBlock64.png`
- `Assets/PhysicsMaterials/IceBlock.physicsMaterial2D`
- `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab`
- `ProjectSettings/TagManager.asset`
- `ProjectSettings/Physics2DSettings.asset`
- `Assets/Prefabs/Player/Player (Exploration).prefab`

---

### Task 5: Build and Verify a Spike-Pit Test Layout

**Files:**
- Modify: `Assets/Scenes/Platformer.unity` or the active test platformer level scene

- [ ] **Step 1: Place the block in a safe test area**

> **Unity Editor task (user):** In the platformer scene, place one `P_PushableIceBlock` on ground beside the player. Leave enough flat space on both sides to push it.

- [ ] **Step 2: Place a spike-pit cover test**

> **Unity Editor task (user):** Create a spike pit where the ground tilemap still physically catches the block below the spike trigger. Place the spike `HazardTrigger` low enough that the player standing on the 4x4 block does not overlap the trigger.

- [ ] **Step 3: Verify baseline movement**

> **Unity Editor task (user):** Enter Play Mode and verify:
> - The block falls under gravity.
> - The ground tilemap stops the block.
> - The player can stand on the block.
> - The player can push the grounded block from the side.
> - The player slows while actively pushing.
> - The block moves at the same slowed pushing pace and then slides briefly before stopping.

- [ ] **Step 4: Verify forbidden interactions**

> **Unity Editor task (user):** In Play Mode verify:
> - Standing on top of the block does not push it.
> - Touching the block while not moving into it does not slow the player.
> - The player cannot meaningfully push the block while it is airborne.
> - An enemy colliding with the block is blocked by it and does not drive it across the level.
> - A falling block does not damage the player or enemies.

- [ ] **Step 5: Verify spike coverage**

> **Unity Editor task (user):** Push the block into the spike pit and stand on top of it. Expected: the player does not take spike damage because the block physically keeps the player collider out of the trigger. Then step off the block into exposed spikes. Expected: existing `HazardTrigger` damage still works.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-111): verify pushable ice block in platformer scene`
- `Assets/Scenes/Platformer.unity` or the exact level scene edited during verification

---

## Final Verification Pass

- [ ] Run Edit Mode tests:
  - `PlatformerTests.PlayerMovementSpeedModifierTests`
  - `PlatformerTests.PushableIceBlockMotionTests`
- [ ] Open `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab` and confirm there are no Missing Script components.
- [ ] Open the platformer scene and confirm the player `groundLayer` includes `PushableBlock`.
- [ ] Enter Play Mode and verify the complete acceptance checklist from Task 5.
- [ ] Confirm no new save data fields or hazard-specific disable code were added.
- [ ] Confirm the ice block has no VFX, SFX, crushing, persistence, or manager code.

## Acceptance Criteria

- Designers can duplicate `Assets/Prefabs/Platformer/P_PushableIceBlock.prefab` to place any number of 4x4 ice blocks in a level.
- The block is a `64x64` sprite at `16` PPU, occupying `4x4` Unity world units.
- The block falls with gravity and lands on the solid tilemap.
- The block is solid footing for the player.
- The block can physically cover spike triggers when the level geometry supports it.
- The block is solid to enemies but only the player can intentionally push it.
- The player pushes automatically by walking into the block from the side.
- The block only accepts push while grounded.
- The player moves at `70%` speed while actively pushing.
- The block moves at that same pushing pace and then slides briefly before stopping.
- The block is harmless when falling.
- The block resets through normal scene reload/death behavior.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-13-dev-111-pushable-ice-block.md`. Two execution options:

1. **Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - execute tasks in this session using executing-plans, batch execution with checkpoints.
