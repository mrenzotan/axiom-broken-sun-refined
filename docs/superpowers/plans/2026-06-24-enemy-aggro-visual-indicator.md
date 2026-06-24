# Enemy Aggro Detection Visual Indicator (DEV-125) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. For Unity-specific execution rules (UVCS check-ins, Editor handoffs, Test Runner), pair with `executing-unity-game-dev-plans`.

**Goal:** Show a brief floating `!` above a platformer enemy the moment it newly detects the player (enters its aggro radius and begins chasing), then fades out — toggleable per enemy.

**Architecture:** Edge-detection logic lives in a new plain-C# `AggroAlertGate` (rising-edge detector), mirroring the existing `EnemyPatrolBehavior` plain-C#/MonoBehaviour split. `EnemyController` (MonoBehaviour) reuses the value it already computes for aggro (`detected`, after the ledge check), feeds it to the gate each `FixedUpdate`, and on a rising edge calls a new `SpawnAggroAlert(...)` method on the existing scene `PlatformerFloatingNumberSpawner`. No new pooling, no new prefab, no new world-space text system.

**Tech Stack:** Unity 6.0.4 LTS, C#, URP 2D, TextMeshPro (via the existing `PlatformerFloatingNumberInstance`), Unity Test Framework (Edit Mode / NUnit), UVCS for check-ins.

## Global Constraints

- **MonoBehaviour separation (non-negotiable):** MonoBehaviours handle Unity lifecycle/physics only. All logic lives in plain C# classes injected into them. The rising-edge logic therefore lives in `AggroAlertGate`, not inline in `EnemyController`.
- **No new singletons:** `EnemyController` gets the spawner via a `[SerializeField]` reference assigned in the Inspector — the exact pattern already used by `SavePointTrigger` and `PlatformerVoiceSpellController`. Do **not** use `FindObjectOfType`/static access.
- **Reuse the existing spawner:** Extend `PlatformerFloatingNumberSpawner` with one focused public method that delegates to its existing private `Spawn(...)`. Do **not** call the private method via reflection, duplicate the pool, or add a new prefab.
- **Assembly:** All runtime code stays in the `Axiom.Platformer` assembly (`Assets/Scripts/Platformer/`). Tests stay in `PlatformerTests` (`Assets/Tests/Editor/Platformer/`).
- **Namespace:** New plain-C# logic class follows the `EnemyController`/`EnemyPatrolBehavior`/`EnemyAnimator` neighbors, which are in the **global namespace** (no `namespace` declaration). The spawner is in `namespace Axiom.Platformer`, so `EnemyController` needs `using Axiom.Platformer;`.
- **Version control:** UVCS only. Never `git add` / `git commit`. Check-in message format: `<type>(DEV-125): <short description>`.
- **Commit hygiene:** No `Co-Authored-By` lines anywhere.

## Design Decisions (read before implementing)

1. **What counts as "newly detected"?** `EnemyController.FixedUpdate` computes `bool detected` from `Physics2D.OverlapCircle`, then clears it to `false` if there is a ledge ahead (the enemy won't chase off a cliff). We fire the indicator on the rising edge of this **post-ledge** `detected` value, because that is the frame the enemy actually starts chasing — which is what the player perceives as "detected." A player standing in range across a ledge gap produces no chase and no `!`, by design.
2. **Fire once, not every frame** (the core AC) is guaranteed by `AggroAlertGate`: it returns `true` only on a `false → true` transition and `false` while detection stays `true`. Re-entering the radius after leaving fires again.
3. **The gate ticks every frame regardless of the toggle.** `RegisterDetection` is always called so the previous-state tracking stays correct; the `_showAggroIndicator` bool only gates the *spawn*. This means toggling the indicator on while the player is already inside the radius will not retroactively fire — it fires on the next genuine re-detection. This is the desired, non-surprising behavior.
4. **The `!` is a fire-and-forget world-space instance** that rises and fades from a fixed spawn point above the enemy (identical to how `-N MP` / `Not enough MP` already behave). It does **not** parent to or follow the enemy. Reusing the existing spawner unchanged is explicitly what the AC asks for ("reuse or adapt floating number spawner if appropriate"); a follow-the-enemy variant is out of scope for this ticket. The cue is brief (~0.8s) so drift is negligible while it remains readable.

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Assets/Scripts/Platformer/AggroAlertGate.cs` | Plain C# rising-edge detector. One method, one bool of state. | **Create** |
| `Assets/Tests/Editor/Platformer/AggroAlertGateTests.cs` | Edit Mode tests for the gate's edge semantics. | **Create** |
| `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs` | Add `SpawnAggroAlert(Vector2)` reusing private `Spawn(...)`. | **Modify** |
| `Assets/Scripts/Platformer/EnemyController.cs` | Add toggle + spawner ref + height field; wire the gate into `FixedUpdate`. | **Modify** |

No `.asmdef` changes: every file lands in an existing assembly. No new folders.

---

### Task 1: `AggroAlertGate` rising-edge detector (plain C#, TDD)

**Files:**
- Create: `Assets/Scripts/Platformer/AggroAlertGate.cs`
- Test: `Assets/Tests/Editor/Platformer/AggroAlertGateTests.cs`

**Interfaces:**
- Produces: `public bool RegisterDetection(bool detected)` on a parameterless `public class AggroAlertGate` (global namespace). Returns `true` only on a `false → true` transition; updates internal previous-state each call.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Platformer/AggroAlertGateTests.cs`:

```csharp
using NUnit.Framework;

namespace PlatformerTests
{
    public class AggroAlertGateTests
    {
        [Test]
        public void RegisterDetection_NotDetected_ReturnsFalse()
        {
            var gate = new AggroAlertGate();
            Assert.IsFalse(gate.RegisterDetection(false));
        }

        [Test]
        public void RegisterDetection_RisingEdge_ReturnsTrue()
        {
            var gate = new AggroAlertGate();
            Assert.IsTrue(gate.RegisterDetection(true));
        }

        [Test]
        public void RegisterDetection_StaysDetected_FiresOnlyOnce()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);
            Assert.IsFalse(
                gate.RegisterDetection(true),
                "Indicator must fire once on detection, not every frame the player stays in range.");
        }

        [Test]
        public void RegisterDetection_FallingEdge_ReturnsFalse()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);
            Assert.IsFalse(gate.RegisterDetection(false));
        }

        [Test]
        public void RegisterDetection_LosesThenRegainsPlayer_FiresAgain()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);   // first detection
            gate.RegisterDetection(false);  // player leaves radius
            Assert.IsTrue(
                gate.RegisterDetection(true),
                "Re-entering the aggro radius is a new detection and must fire again.");
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile / fail**

Unity Editor → Window → General → Test Runner → EditMode → Run.
Expected: `AggroAlertGateTests` fails — `AggroAlertGate` does not exist yet (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `Assets/Scripts/Platformer/AggroAlertGate.cs`:

```csharp
/// <summary>
/// Plain C# rising-edge detector for enemy aggro. Returns true only on the call where the
/// player is newly detected (a false -> true transition), so the aggro "!" indicator fires
/// once per detection instead of every frame the player stays inside the aggro radius.
/// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
/// </summary>
public class AggroAlertGate
{
    private bool _wasDetected;

    /// <summary>
    /// Records the current detection state and reports whether this is a new detection.
    /// </summary>
    /// <param name="detected">
    /// True when the enemy currently detects the player (inside the aggro radius and able to chase).
    /// </param>
    /// <returns>True only on the rising edge: not detected on the previous call, detected now.</returns>
    public bool RegisterDetection(bool detected)
    {
        bool rising = detected && !_wasDetected;
        _wasDetected = detected;
        return rising;
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Unity Editor → Test Runner → EditMode → Run.
Expected: all 5 `AggroAlertGateTests` pass.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-125): add AggroAlertGate rising-edge detector`
- `Assets/Scripts/Platformer/AggroAlertGate.cs`
- `Assets/Scripts/Platformer/AggroAlertGate.cs.meta`
- `Assets/Tests/Editor/Platformer/AggroAlertGateTests.cs`
- `Assets/Tests/Editor/Platformer/AggroAlertGateTests.cs.meta`

---

### Task 2: `SpawnAggroAlert` on the floating-number spawner

**Files:**
- Modify: `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`

**Interfaces:**
- Consumes: existing private `void Spawn(Vector2 position, string text, Color color, float durationOverride = -1f)`.
- Produces: `public void SpawnAggroAlert(Vector2 worldPosition)` — spawns a brief yellow `!` at `worldPosition`. Guards on a null prefab exactly like the sibling spawn methods.

- [ ] **Step 1: Add the warning constants**

In `PlatformerFloatingNumberSpawner.cs`, alongside the existing `InsufficientManaColor` / `InsufficientManaDuration` constants (after line 23), add:

```csharp
        // Bright warning yellow for the aggro "!" — pops against most platformer backgrounds.
        private static readonly Color AggroAlertColor = new Color(1f, 0.85f, 0.2f);

        // A single "!" reads as a quick alert, so it lingers less than the default ~1s number.
        private const float AggroAlertDuration = 0.8f;
```

- [ ] **Step 2: Add the public spawn method**

After the existing `SpawnInsufficientMana(...)` method (after line 80, before the private `Spawn`), add:

```csharp
        /// <summary>
        /// Spawns a brief yellow "!" at worldPosition when an enemy newly detects the player.
        /// Reuses the shared pooled-instance path; pass a position already offset above the enemy.
        /// </summary>
        public void SpawnAggroAlert(Vector2 worldPosition)
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[PlatformerFloatingNumberSpawner] Prefab not assigned.", this);
                return;
            }

            Spawn(worldPosition, "!", AggroAlertColor, AggroAlertDuration);
        }
```

- [ ] **Step 3: Confirm the project compiles**

Unity Editor → wait for recompile → Console shows no errors.
Expected: clean compile. (This method delegates to the already-tested `Spawn` path; no separate unit test — pooling/positioning is verified in the Play Mode pass in Task 4, consistent with the rest of this MonoBehaviour spawner, which has no Edit Mode tests.)

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-125): add aggro "!" spawn method to floating-number spawner`
- `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`
- `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs.meta`

---

### Task 3: Wire the indicator into `EnemyController`

**Files:**
- Modify: `Assets/Scripts/Platformer/EnemyController.cs`

**Interfaces:**
- Consumes: `AggroAlertGate.RegisterDetection(bool)` (Task 1) and `PlatformerFloatingNumberSpawner.SpawnAggroAlert(Vector2)` (Task 2).
- Produces: three new serialized Inspector fields on `EnemyController` (`_showAggroIndicator`, `_floatingNumberSpawner`, `_aggroIndicatorHeight`) that the user wires in Task 4.

- [ ] **Step 1: Add the `Axiom.Platformer` using directive**

At the top of `EnemyController.cs`, the file currently has only `using UnityEngine;` (line 1). Add below it:

```csharp
using UnityEngine;
using Axiom.Platformer;
```

(`EnemyController` is in the global namespace; the spawner is in `Axiom.Platformer`, so this import is required.)

- [ ] **Step 2: Add the serialized fields**

In `EnemyController.cs`, after the existing `[Header("Visual")]` block (after line 43, `[SerializeField] private Animator _animator;`), add a new header block:

```csharp
    [Header("Aggro Indicator")]
    [SerializeField]
    [Tooltip("When enabled, a floating \"!\" appears above this enemy the moment it newly detects the player.")]
    private bool _showAggroIndicator = true;

    [SerializeField]
    [Tooltip("World-space floating-number spawner used to display the aggro \"!\". " +
             "Assign the scene's PlatformerFloatingNumberSpawner.")]
    private PlatformerFloatingNumberSpawner _floatingNumberSpawner;

    [SerializeField]
    [Tooltip("Height above the enemy origin at which the aggro \"!\" appears.")]
    private float _aggroIndicatorHeight = 1.2f;
```

- [ ] **Step 3: Add the gate field and construct it in `Awake`**

In the private fields block (after line 48, `private EnemyAnimator _enemyAnimator;`), add:

```csharp
    private AggroAlertGate _aggroGate;
```

In `Awake()`, after the `_behavior = new EnemyPatrolBehavior(...)` assignment (after line 65, before the `if (_animator != null)` block), add:

```csharp
        _aggroGate = new AggroAlertGate();
```

- [ ] **Step 4: Fire the indicator on the rising edge in `FixedUpdate`**

In `FixedUpdate()`, the final value of `detected` is settled immediately after the ledge check (current lines 77-78):

```csharp
        if (detected && IsLedgeAhead())
            detected = false;
```

Directly **after** those two lines (and before the `float xVel = _behavior.Tick(...)` line), insert:

```csharp
        if (_aggroGate.RegisterDetection(detected) && _showAggroIndicator && _floatingNumberSpawner != null)
        {
            Vector2 indicatorPosition = (Vector2)transform.position + Vector2.up * _aggroIndicatorHeight;
            _floatingNumberSpawner.SpawnAggroAlert(indicatorPosition);
        }
```

Note ordering: `RegisterDetection(detected)` is evaluated first (and unconditionally, thanks to `&&` short-circuit only applying after it runs) so the gate's previous-state tracking stays correct every frame even when the indicator is toggled off or the spawner is unassigned.

- [ ] **Step 5: Confirm the project compiles**

Unity Editor → wait for recompile → Console shows no errors.
Expected: clean compile; existing `EnemyPatrolBehaviorTests` still pass in the Test Runner (EditMode → Run) — this change does not touch `EnemyPatrolBehavior`.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-125): show aggro "!" when enemy newly detects player`
- `Assets/Scripts/Platformer/EnemyController.cs`
- `Assets/Scripts/Platformer/EnemyController.cs.meta`

---

### Task 4: Unity Editor wiring + Play Mode verification (user)

This task has no code. It connects the new serialized fields and verifies the feature in-scene against the acceptance criteria.

- [ ] **Step 1 — Unity Editor task (user): Confirm a `PlatformerFloatingNumberSpawner` exists in the scene**

Open `Assets/Scenes/Platformer.unity`. Confirm the persistent GameObject carrying `PlatformerFloatingNumberSpawner` is present (the same one `SavePointTrigger` / the voice spell controller already reference). If absent, add it per its existing setup (prefab + pool size).

- [ ] **Step 2 — Unity Editor task (user): Wire each enemy**

For every enemy prefab / scene instance with an `EnemyController`:
- Set **Show Aggro Indicator** = checked.
- Drag the scene's `PlatformerFloatingNumberSpawner` GameObject into the **Floating Number Spawner** field.
- Leave **Aggro Indicator Height** at `1.2` (raise it if a given enemy's sprite is tall so the `!` clears its head).

Prefer setting these on the enemy **prefab** so all instances inherit the wiring; assign the scene spawner reference on the instances if the spawner lives in the scene (prefabs cannot serialize scene-object references).

- [ ] **Step 3 — Unity Editor task (user): Play Mode — happy path**

Enter Play Mode. Walk the player into an enemy's aggro radius.
Expected: a single yellow `!` appears ~1.2 units above the enemy, rises, and fades out within ~0.8s, while the enemy begins chasing. (AC: floating `!` on detection, brief fade-out, positioned clearly above the enemy.)

- [ ] **Step 4 — Unity Editor task (user): Play Mode — no spam**

Stay inside the aggro radius after the first `!`.
Expected: no repeated `!` while you remain detected. Leave the radius and re-enter.
Expected: a new `!` appears on re-entry. (AC: only on new detection, not every frame; re-detection fires again.)

- [ ] **Step 5 — Unity Editor task (user): Play Mode — toggle off**

Stop Play Mode, uncheck **Show Aggro Indicator** on one enemy, re-enter Play Mode, and walk into that enemy's radius.
Expected: no `!` appears for that enemy; others still show it. (AC: toggle via boolean.)

- [ ] **Step 6 — Unity Editor task (user): Play Mode — varied radii + no interference**

Test enemies configured with different `aggroRadius` values, including one with a ledge between the player and the enemy.
Expected: the `!` fires at the correct distance per enemy; the ledge-guarded enemy shows no `!` (it does not chase off the ledge); enemy movement, the `ExplorationEnemyCombatTrigger` battle hand-off, and existing combat/heal/mana feedback all behave exactly as before. (AC: uses existing `aggroRadius` logic; does not interfere with movement, battle triggers, or combat feedback.)

- [ ] **Step 7 — Jira:** Move DEV-125 to Done once Steps 3-6 pass. (User or assistant via Atlassian MCP, per your workflow.)

---

## Acceptance Criteria → Task Map (self-review)

| Acceptance Criterion | Covered by |
|---|---|
| Floating `!` appears when player enters aggro radius | Task 2 (`SpawnAggroAlert`), Task 3 Step 4, Task 4 Step 3 |
| `!` briefly fades out | Reused `PlatformerFloatingNumberInstance` fade + `AggroAlertDuration` (Task 2); verified Task 4 Step 3 |
| Only on new detection, not every frame | Task 1 `AggroAlertGate` (tested), Task 4 Step 4 |
| Positioned clearly above enemy, readable in motion | `_aggroIndicatorHeight` (Task 3 Step 2/4), Task 4 Step 3 |
| Reuse/adapt existing floating-number spawner | Task 2 (delegates to private `Spawn`, no new pool/prefab) |
| Boolean enable/disable on `EnemyController` | `_showAggroIndicator` (Task 3 Step 2/4), Task 4 Step 5 |
| Uses existing `aggroRadius` detection logic | Task 3 Step 4 reuses the settled `detected` flag; no new physics query |
| No interference with movement / battle triggers / combat feedback | Task 3 (additive, no existing line changed), Task 4 Step 6 |

All eight acceptance criteria and all seven subtasks map to a task. No spec gaps.
