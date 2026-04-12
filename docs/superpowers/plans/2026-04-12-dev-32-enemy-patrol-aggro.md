# DEV-32: Enemy Patrol with Aggro Detection and Chase Behavior — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a three-state patrol/aggro/return enemy AI for the Platformer scene — enemies roam between waypoints, detect the player via a configurable aggro radius, chase them, and return to patrol when the player leaves range.

**Architecture:** `EnemyPatrolBehavior` is a plain C# class that owns the state machine (`Patrol` → `Aggro` → `Returning` → `Patrol`) and returns a desired X velocity each `Tick()` — no Unity API calls, fully Edit Mode testable. `EnemyController` is a MonoBehaviour wrapper that calls `Physics2D.OverlapCircle` for detection, applies the velocity to `Rigidbody2D`, and sets sprite facing via `Transform.localScale.x` on the Visual child. Patrol waypoints are `Transform[]` in the Inspector, snapshotted to `Vector2[]` at Awake for the behavior class.

**Tech Stack:** Unity 6 LTS, C#, Unity Test Framework (Edit Mode NUnit), UVCS

---

## File Map

| File                                                         | Responsibility                                                                              |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------- |
| `Assets/Scripts/Platformer/EnemyPatrolBehavior.cs`           | Plain C# state machine — patrol/aggro/return logic, returns desired X velocity per `Tick()` |
| `Assets/Scripts/Platformer/EnemyController.cs`               | MonoBehaviour — `Physics2D` detection, `Rigidbody2D` velocity, sprite flipping              |
| `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`      | Edit Mode test assembly for Platformer scripts                                              |
| `Assets/Tests/Editor/Platformer/EnemyPatrolBehaviorTests.cs` | Edit Mode NUnit tests for `EnemyPatrolBehavior`                                             |

---

### Task 1: Create Platformer test assembly definition

**Files:**

- Create: `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`

- [ ] **Step 1: Create the `Assets/Tests/Editor/Platformer/` folder**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Tests/Editor/` → Create → Folder → name it `Platformer`.

- [ ] **Step 2: Write `PlatformerTests.asmdef`**

Write this file to `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`:

```json
{
  "name": "PlatformerTests",
  "references": ["Axiom.Platformer"],
  "testReferences": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 3: Verify asmdef compiles cleanly**

> **Unity Editor task (user):** Switch to the Unity Editor and wait for the status bar to finish compiling. Click `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef` in the Project window — the Inspector must show "Assembly Definition" with name `PlatformerTests` and zero Console errors.

- [ ] **Step 4: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `chore(DEV-32): add PlatformerTests edit mode assembly definition`
  - `Assets/Tests/Editor/Platformer/` _(folder .meta)_
  - `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`
  - `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef.meta`

---

### Task 2: Implement `EnemyPatrolBehavior` with Edit Mode tests (TDD)

**Files:**

- Create: `Assets/Tests/Editor/Platformer/EnemyPatrolBehaviorTests.cs`
- Create: `Assets/Scripts/Platformer/EnemyPatrolBehavior.cs`

**State machine overview:**

| From        | To          | Trigger                                                    |
| ----------- | ----------- | ---------------------------------------------------------- |
| `Patrol`    | `Aggro`     | `playerDetected == true`                                   |
| `Aggro`     | `Returning` | `playerDetected == false` AND deaggro grace period expires |
| `Returning` | `Patrol`    | enemy reaches current waypoint (within threshold)          |
| `Returning` | `Aggro`     | `playerDetected == true` (re-aggro mid-return)             |

`Tick()` returns desired X velocity each frame. `FacingDirectionX` is `1f` (right) or `-1f` (left), updated whenever direction changes.

- [ ] **Step 1: Write the failing tests**

Write this file to `Assets/Tests/Editor/Platformer/EnemyPatrolBehaviorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class EnemyPatrolBehaviorTests
    {
        private static Vector2[] TwoWaypoints() =>
            new[] { new Vector2(-3f, 0f), new Vector2(3f, 0f) };

        // ── Initial state ─────────────────────────────────────────────────────

        [Test]
        public void InitialState_IsPatrol()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }

        // ── Patrol → Aggro ────────────────────────────────────────────────────

        [Test]
        public void Patrol_PlayerDetected_TransitionsToAggro()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, playerDetected: true, playerPosition: new Vector2(2f, 0f), deltaTime: 0.016f);
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        // ── Aggro grace period ────────────────────────────────────────────────

        [Test]
        public void Aggro_PlayerNotDetected_StaysAggroDuringGracePeriod()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.5f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.1f);           // within grace period
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        [Test]
        public void Aggro_PlayerNotDetected_TransitionsToReturningAfterGracePeriod()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.3f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.4f);           // grace expired (0.4 > 0.3)
            Assert.AreEqual(EnemyPatrolBehavior.State.Returning, behavior.CurrentState);
        }

        // ── Returning → Patrol ────────────────────────────────────────────────

        [Test]
        public void Returning_WaypointReached_TransitionsToPatrol()
        {
            // waypoints[0] = (-3, 0), waypoints[1] = (3, 0)
            // Enemy starts at (0, 0). Index = 0, so target is (-3, 0).
            // After aggro/returning, position -2.8 is within threshold 0.5 of (-3, 0).
            var waypoints = new[] { new Vector2(-3f, 0f), new Vector2(3f, 0f) };
            var behavior = new EnemyPatrolBehavior(waypoints, patrolSpeed: 3f, chaseSpeed: 5f,
                waypointThreshold: 0.5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);          // → Aggro
            behavior.Tick(new Vector2(0.5f, 0f), false, Vector2.zero, 0.2f);          // grace expired → Returning
            behavior.Tick(new Vector2(-2.8f, 0f), false, Vector2.zero, 0.016f);       // reach waypoints[0] → Patrol
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }

        // ── Re-aggro during Returning ─────────────────────────────────────────

        [Test]
        public void Returning_PlayerDetected_TransitionsBackToAggro()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);   // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.2f);            // → Returning
            behavior.Tick(Vector2.zero, true, new Vector2(-2f, 0f), 0.016f);  // player back → Aggro
            Assert.AreEqual(EnemyPatrolBehavior.State.Aggro, behavior.CurrentState);
        }

        // ── Patrol velocity / facing ──────────────────────────────────────────

        [Test]
        public void Patrol_NoWaypoints_ReturnsZeroVelocity()
        {
            var behavior = new EnemyPatrolBehavior(new Vector2[0], patrolSpeed: 3f, chaseSpeed: 5f);
            float vel = behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(0f, vel);
        }

        [Test]
        public void Patrol_WaypointToRight_FacingDirectionIsPositive()
        {
            var behavior = new EnemyPatrolBehavior(new[] { new Vector2(5f, 0f) }, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Patrol_WaypointToLeft_FacingDirectionIsNegative()
        {
            var behavior = new EnemyPatrolBehavior(new[] { new Vector2(-5f, 0f) }, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);
            Assert.AreEqual(-1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Patrol_WaypointReached_AdvancesToNextWaypoint()
        {
            // waypoints[0] at (0.1, 0) — within default threshold 0.2 of spawn (0, 0)
            // After advancing, waypoints[1] at (5, 0) — should produce positive velocity
            var waypoints = new[] { new Vector2(0.1f, 0f), new Vector2(5f, 0f) };
            var behavior = new EnemyPatrolBehavior(waypoints, patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f); // reach waypoints[0], index advances to 1
            float vel = behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f); // now heading to waypoints[1]
            Assert.Greater(vel, 0f);
        }

        // ── Aggro velocity / facing ───────────────────────────────────────────

        [Test]
        public void Aggro_ReturnsChaseSpeedTowardPlayer()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            float vel = behavior.Tick(Vector2.zero, true, new Vector2(3f, 0f), 0.016f);
            Assert.AreEqual(5f, vel);
        }

        [Test]
        public void Aggro_PlayerToRight_FacingDirectionIsPositive()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(3f, 0f), 0.016f);
            Assert.AreEqual(1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Aggro_PlayerToLeft_FacingDirectionIsNegative()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(-3f, 0f), 0.016f);
            Assert.AreEqual(-1f, behavior.FacingDirectionX);
        }

        [Test]
        public void Aggro_PlayerAtSamePosition_ReturnsZeroVelocity()
        {
            var behavior = new EnemyPatrolBehavior(TwoWaypoints(), patrolSpeed: 3f, chaseSpeed: 5f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f); // enter Aggro from a distance
            // Player now at exact same X as enemy — dx == 0, within waypointThreshold
            float vel = behavior.Tick(Vector2.zero, true, Vector2.zero, 0.016f);
            Assert.AreEqual(0f, vel);
        }

        // ── Returning edge cases ──────────────────────────────────────────────

        [Test]
        public void Returning_NoWaypoints_TransitionsToPatrol()
        {
            // Even with no waypoints, Patrol can still transition to Aggro (player detected),
            // then to Returning (grace expired). TickReturning with no waypoints must go straight back to Patrol.
            var behavior = new EnemyPatrolBehavior(new Vector2[0], patrolSpeed: 3f, chaseSpeed: 5f, deaggroGracePeriod: 0.1f);
            behavior.Tick(Vector2.zero, true, new Vector2(2f, 0f), 0.016f);  // → Aggro
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.2f);           // grace expired → Returning
            behavior.Tick(Vector2.zero, false, Vector2.zero, 0.016f);         // no waypoints → Patrol
            Assert.AreEqual(EnemyPatrolBehavior.State.Patrol, behavior.CurrentState);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → Run All (or filter to `PlatformerTests`). Expected: all 13 `EnemyPatrolBehaviorTests` fail with a compile error — "The type or namespace name 'EnemyPatrolBehavior' could not be found."

- [ ] **Step 3: Implement `EnemyPatrolBehavior`**

Write this file to `Assets/Scripts/Platformer/EnemyPatrolBehavior.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Plain C# class — all enemy patrol/aggro/return state machine logic lives here.
/// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
///
/// Tick() returns desired X velocity. The caller (EnemyController) applies it to Rigidbody2D
/// and sets Transform.localScale.x based on FacingDirectionX.
/// </summary>
public class EnemyPatrolBehavior
{
    public enum State { Patrol, Aggro, Returning }

    public State CurrentState { get; private set; }

    /// <summary>1f when facing right, -1f when facing left.
    /// Set visualTransform.localScale.x to this value in EnemyController.</summary>
    public float FacingDirectionX { get; private set; }

    private readonly Vector2[] _waypoints;
    private readonly float _patrolSpeed;
    private readonly float _chaseSpeed;
    private readonly float _waypointThreshold;
    private readonly float _deaggroGracePeriod;

    private int _currentWaypointIndex;
    private float _deaggroTimer;

    public EnemyPatrolBehavior(
        Vector2[] waypoints,
        float patrolSpeed,
        float chaseSpeed,
        float waypointThreshold = 0.2f,
        float deaggroGracePeriod = 0.5f)
    {
        _waypoints = waypoints ?? new Vector2[0];
        _patrolSpeed = patrolSpeed;
        _chaseSpeed = chaseSpeed;
        _waypointThreshold = waypointThreshold;
        _deaggroGracePeriod = deaggroGracePeriod;
        CurrentState = State.Patrol;
        FacingDirectionX = 1f;
    }

    /// <summary>
    /// Call from EnemyController.FixedUpdate(). Returns desired X velocity.
    /// Caller must apply: rb.linearVelocity = new Vector2(result, rb.linearVelocity.y)
    /// </summary>
    public float Tick(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        switch (CurrentState)
        {
            case State.Patrol:    return TickPatrol(currentPosition, playerDetected, playerPosition, deltaTime);
            case State.Aggro:     return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
            case State.Returning: return TickReturning(currentPosition, playerDetected, playerPosition, deltaTime);
            default:              return 0f;
        }
    }

    private float TickPatrol(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (playerDetected)
        {
            CurrentState = State.Aggro;
            _deaggroTimer = _deaggroGracePeriod;
            return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
        }

        if (_waypoints.Length == 0)
            return 0f;

        Vector2 target = _waypoints[_currentWaypointIndex];
        float dx = target.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;
            return 0f;
        }

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _patrolSpeed;
    }

    private float TickAggro(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (!playerDetected)
        {
            _deaggroTimer -= deltaTime;
            if (_deaggroTimer <= 0f)
            {
                CurrentState = State.Returning;
                return 0f;
            }
            return FacingDirectionX * _chaseSpeed;
        }

        _deaggroTimer = _deaggroGracePeriod;
        float dx = playerPosition.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
            return 0f;

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _chaseSpeed;
    }

    private float TickReturning(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (playerDetected)
        {
            CurrentState = State.Aggro;
            _deaggroTimer = _deaggroGracePeriod;
            return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
        }

        if (_waypoints.Length == 0)
        {
            CurrentState = State.Patrol;
            return 0f;
        }

        Vector2 target = _waypoints[_currentWaypointIndex];
        float dx = target.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
        {
            CurrentState = State.Patrol;
            return 0f;
        }

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _patrolSpeed;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

> **Unity Editor task (user):** Test Runner → EditMode → Run All. Expected: all 13 `EnemyPatrolBehaviorTests` pass (green checkmarks). Zero failures.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-32): implement EnemyPatrolBehavior state machine with edit mode tests`
  - `Assets/Tests/Editor/Platformer/EnemyPatrolBehaviorTests.cs`
  - `Assets/Tests/Editor/Platformer/EnemyPatrolBehaviorTests.cs.meta`
  - `Assets/Scripts/Platformer/EnemyPatrolBehavior.cs`
  - `Assets/Scripts/Platformer/EnemyPatrolBehavior.cs.meta`

---

### Task 3: Implement `EnemyController` MonoBehaviour

**Files:**

- Create: `Assets/Scripts/Platformer/EnemyController.cs`

The MonoBehaviour does three things and nothing else:

1. Calls `Physics2D.OverlapCircle` to detect the player
2. Passes the result to `EnemyPatrolBehavior.Tick()` and applies the returned X velocity to `Rigidbody2D`
3. Sets `visualTransform.localScale.x` from `FacingDirectionX` for sprite flipping

- [ ] **Step 1: Write `EnemyController.cs`**

Write this file to `Assets/Scripts/Platformer/EnemyController.cs`:

```csharp
using UnityEngine;

/// <summary>
/// MonoBehaviour — Unity lifecycle and physics bridge only.
/// All patrol/aggro/return logic is delegated to EnemyPatrolBehavior.
///
/// Required prefab structure:
///   Enemy (root — Rigidbody2D, Collider2D, EnemyController)
///   └── Visual (child — SpriteRenderer, Animator)
///          EnemyController sets Visual's Transform.localScale.x for sprite flipping.
///          Never use SpriteRenderer.FlipX — see GAME_PLAN.md §6 Sprite Flipping.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed      = 3f;
    [SerializeField] private float chaseSpeed       = 5f;
    [SerializeField] private float waypointThreshold = 0.2f;

    [Header("Aggro")]
    [SerializeField] private float aggroRadius        = 5f;
    [SerializeField] private float deaggroGracePeriod = 0.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    private Rigidbody2D _rb;
    private EnemyPatrolBehavior _behavior;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        var waypoints = new Vector2[patrolPoints != null ? patrolPoints.Length : 0];
        for (int i = 0; i < waypoints.Length; i++)
            waypoints[i] = patrolPoints[i].position;

        _behavior = new EnemyPatrolBehavior(
            waypoints,
            patrolSpeed,
            chaseSpeed,
            waypointThreshold,
            deaggroGracePeriod);
    }

    private void FixedUpdate()
    {
        Collider2D hit  = Physics2D.OverlapCircle(transform.position, aggroRadius, playerLayer);
        bool detected   = hit != null;
        Vector2 playerPos = detected ? (Vector2)hit.transform.position : Vector2.zero;

        float xVel = _behavior.Tick((Vector2)transform.position, detected, playerPos, Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(xVel, _rb.linearVelocity.y);

        if (visualTransform != null)
            visualTransform.localScale = new Vector3(_behavior.FacingDirectionX, 1f, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
    }
}
```

- [ ] **Step 2: Verify it compiles cleanly**

> **Unity Editor task (user):** Switch to the Unity Editor. Wait for the status bar to finish compiling. Confirm zero Console errors.

- [ ] **Step 3: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-32): add EnemyController MonoBehaviour wrapper`
  - `Assets/Scripts/Platformer/EnemyController.cs`
  - `Assets/Scripts/Platformer/EnemyController.cs.meta`

---

### Task 4: Set up enemy prefab and patrol points in the Platformer scene

- [ ] **Step 1: Create the `Assets/Prefabs/Enemies/` folder**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Prefabs/` → Create → Folder → name it `Enemies`.

- [ ] **Step 2: Create the enemy root GameObject**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Platformer.unity`.
> 2. In the Hierarchy, right-click → Create Empty → rename it `Enemy`.
> 3. With `Enemy` selected, in the Inspector → Add Component → `Rigidbody2D`.
>    - Set **Gravity Scale** to `1`.
>    - Under **Constraints**, check **Freeze Rotation Z** to prevent tipping.
> 4. Add Component → `Capsule Collider 2D` (or `Box Collider 2D`) — resize to fit the enemy sprite.
> 5. Add Component → search `EnemyController` → select it.

- [ ] **Step 3: Add the Visual child**

> **Unity Editor task (user):**
>
> 1. Right-click the `Enemy` GameObject in the Hierarchy → Create Empty → rename it `Visual`.
> 2. With `Visual` selected, Add Component → `Sprite Renderer` → assign the enemy sprite in the **Sprite** field.
> 3. Leave `Visual`'s Transform at default scale (1, 1, 1) — `EnemyController` updates `localScale.x` at runtime.

- [ ] **Step 4: Assign `visualTransform` in `EnemyController`**

> **Unity Editor task (user):**
>
> 1. Select the `Enemy` root GameObject.
> 2. In the Inspector, find `EnemyController` → **Visual Transform** field.
> 3. Drag the `Visual` child from the Hierarchy into the **Visual Transform** slot.

- [ ] **Step 5: Create patrol point transforms**

> **Unity Editor task (user):**
>
> 1. Right-click the Hierarchy root (not inside `Enemy`) → Create Empty → rename it `PatrolPoint_A`.
> 2. Repeat for `PatrolPoint_B`.
> 3. Position both points on the tilemap where the enemy should turn around — match their Y to the enemy's feet height. Use the Move tool in Scene View.
> 4. Select `Enemy` → in `EnemyController`, set **Patrol Points** array **Size** to `2`.
> 5. Drag `PatrolPoint_A` into Element 0, `PatrolPoint_B` into Element 1.

- [ ] **Step 6: Assign the Player layer**

> **Unity Editor task (user):**
>
> 1. If a `Player` layer doesn't exist: Edit → Project Settings → Tags and Layers → add `Player` under User Layers.
> 2. Select the `Player` GameObject in the Hierarchy → set its **Layer** dropdown to `Player`.
> 3. Select `Enemy` → `EnemyController` → **Player Layer** field → tick the `Player` layer.

- [ ] **Step 7: Create the enemy prefab**

> **Unity Editor task (user):**
>
> 1. Drag the `Enemy` GameObject from the Hierarchy into `Assets/Prefabs/Enemies/` — this creates `Assets/Prefabs/Enemies/Enemy.prefab`.
> 2. Delete the `Enemy` GameObject from the Hierarchy.
> 3. Drag `Assets/Prefabs/Enemies/Enemy.prefab` from the Project window back into the Hierarchy as a prefab instance.

- [ ] **Step 8: Smoke test in Play Mode**

> **Unity Editor task (user):**
>
> 1. Enter Play Mode.
> 2. Verify the enemy walks between `PatrolPoint_A` and `PatrolPoint_B`.
> 3. Walk the player into the yellow aggro sphere (visible in Scene View) — the enemy must turn and chase.
> 4. Move the player out of range — after ~0.5 s the enemy must stop chasing and walk back toward its patrol waypoint.
> 5. While the enemy is returning, walk the player back in — confirm it re-aggros immediately.
> 6. Confirm the sprite flips to face the direction of travel at all times.
> 7. Confirm zero Console errors throughout.

- [ ] **Step 9: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `chore(DEV-32): add Enemy prefab and patrol setup to Platformer scene`
  - `Assets/Prefabs/Enemies/` _(folder .meta)_
  - `Assets/Prefabs/Enemies/Enemy.prefab`
  - `Assets/Prefabs/Enemies/Enemy.prefab.meta`
  - `Assets/Scenes/Platformer.unity`

---

### Task 5: Platform-scoped aggro via ledge detection

**Files:**

- Modify: `Assets/Scripts/Platformer/EnemyController.cs`

**Approach:** Add a downward raycast just ahead of the enemy's feet in the facing direction. If no ground is detected, suppress `playerDetected = false` before passing to `Tick()`. `EnemyPatrolBehavior` is unchanged — the suppression happens entirely in `EnemyController.FixedUpdate()`.

No new unit tests are needed: the ledge check uses `Physics2D.Raycast` (Unity API, not testable in Edit Mode), and `EnemyPatrolBehavior` has no new logic. Verify via Play Mode smoke test.

- [ ] **Step 1: Add ledge detection fields and method to `EnemyController`**

Add a `[Header("Ledge Detection")]` block to the serialized fields:

```csharp
[Header("Ledge Detection")]
[SerializeField] private float ledgeCheckOffsetX = 0.4f;  // horizontal distance ahead of feet to cast from
[SerializeField] private float ledgeCheckDepth   = 0.6f;  // how far down to cast
[SerializeField] private LayerMask groundLayer;
```

Replace `FixedUpdate()` with:

```csharp
private void FixedUpdate()
{
    Collider2D hit = Physics2D.OverlapCircle(transform.position, aggroRadius, playerLayer);
    bool detected = hit != null;
    Vector2 playerPos = detected ? (Vector2)hit.transform.position : Vector2.zero;

    if (detected && IsLedgeAhead())
        detected = false;

    float xVel = _behavior.Tick((Vector2)transform.position, detected, playerPos, Time.fixedDeltaTime);
    _rb.linearVelocity = new Vector2(xVel, _rb.linearVelocity.y);

    if (visualTransform != null)
        visualTransform.localScale = new Vector3(_behavior.FacingDirectionX, 1f, 1f);
}

private bool IsLedgeAhead()
{
    float ahead = _behavior.FacingDirectionX * ledgeCheckOffsetX;
    Vector2 origin = (Vector2)transform.position + new Vector2(ahead, 0f);
    return !Physics2D.Raycast(origin, Vector2.down, ledgeCheckDepth, groundLayer);
}
```

Replace `OnDrawGizmosSelected()` with (adds ledge ray visualization):

```csharp
private void OnDrawGizmosSelected()
{
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, aggroRadius);

    Gizmos.color = Color.red;
    float ahead = Application.isPlaying && _behavior != null
        ? _behavior.FacingDirectionX * ledgeCheckOffsetX
        : ledgeCheckOffsetX;
    Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(ahead, 0f);
    Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector2.down * ledgeCheckDepth);
}
```

- [ ] **Step 2: Verify it compiles cleanly**

> **Unity Editor task (user):** Switch to Unity Editor → wait for compile → confirm zero Console errors.

- [ ] **Step 3: Assign Ground layer in Inspector**

> **Unity Editor task (user):**
>
> 1. If a `Ground` layer doesn't exist: Edit → Project Settings → Tags and Layers → add `Ground`.
> 2. Select all ground/platform GameObjects in the Hierarchy → set their **Layer** to `Ground`.
> 3. Select `Enemy` → `EnemyController` → **Ground Layer** field → tick the `Ground` layer.
> 4. Tune **Ledge Check Offset X** to roughly match the enemy's half-width (default 0.4). Tune **Ledge Check Depth** so the red gizmo ray visibly reaches below the platform surface (default 0.6).

- [ ] **Step 4: Smoke test in Play Mode**

> **Unity Editor task (user):**
>
> 1. Enter Play Mode.
> 2. Walk the player to the opposite side of a ledge from the enemy — the enemy must stop at the ledge edge and de-aggro rather than fall off.
> 3. Walk the player onto the same platform — the enemy must resume chasing normally.
> 4. Confirm patrol and return behavior are unaffected (ledge check only fires when `playerDetected` is true).
> 5. Confirm zero Console errors.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-32): add ledge-detection platform-scoped aggro to EnemyController`
  - `Assets/Scripts/Platformer/EnemyController.cs`

---

## Reference: Hierarchy Organization for Multiple Enemies

Patrol points are **fixed world-space anchors** — they must never be children of the enemy GameObject (they would move with it, breaking the patrol route). Keep them in a sibling group:

```
Scene Hierarchy
├── Enemies/
│   ├── Enemy_A
│   ├── Enemy_B
│   └── Enemy_C
└── PatrolRoutes/
    ├── Route_Enemy_A/
    │   ├── Point_0
    │   └── Point_1
    ├── Route_Enemy_B/
    │   ├── Point_0
    │   └── Point_1
    └── Route_Enemy_C/
        ├── Point_0
        └── Point_1
```

**Rules:**
- `Enemies/` and `PatrolRoutes/` are empty GameObjects used only as Hierarchy labels — no components.
- Each `Route_Enemy_X/` folder groups the points for one enemy instance. Name it to match the enemy it serves.
- Wire up manually: drag `Route_Enemy_X`'s points into the **Patrol Points** array on the corresponding `EnemyController` in the Inspector.
- Selecting a `Route_Enemy_X` folder in the Hierarchy selects all its points, making it easy to reposition the whole route as a group.
