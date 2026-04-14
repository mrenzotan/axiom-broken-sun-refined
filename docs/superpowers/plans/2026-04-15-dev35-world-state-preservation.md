# DEV-35: Freeze and Restore Platformer World State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the Platformer world state (player position, enemy positions, interactable states) before loading the Battle scene and restore it exactly when returning, so the world feels continuous and uninterrupted across multiple battle-and-return cycles.

**Architecture:** A plain C# `WorldSnapshot` (stored on `GameManager`) captures all world entity positions via `ExplorationEnemyCombatTrigger` before the battle transition. On return to the Platformer scene, a new `PlatformerWorldRestoreController` MonoBehaviour subscribes to `GameManager.OnSceneReady`, reads the snapshot, teleports entities to their saved positions, then clears the snapshot. `BattleController.HandleStateChanged` is extended to transition back to Platformer on `BattleState.Victory` (placeholder — DEV-37 adds XP/loot flow on top of this).

**Tech Stack:** Unity 6 LTS · URP 2D · C# · Unity Test Framework (Edit Mode)

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `Assets/Scripts/Core/EnemyWorldState.cs` | **Create** | Immutable position snapshot for one enemy |
| `Assets/Scripts/Core/WorldSnapshot.cs` | **Create** | Snapshot of all world entity positions keyed by stable ID |
| `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs` | **Create** | MonoBehaviour — restores world state when Platformer scene is ready |
| `Assets/Tests/Editor/Core/WorldSnapshotTests.cs` | **Create** | Unit tests for `WorldSnapshot` and `EnemyWorldState` |
| `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs` | **Create** | Unit tests for `GameManager` snapshot storage |
| `Assets/Scripts/Core/GameManager.cs` | **Modify** | Add `CurrentWorldSnapshot`, `SetWorldSnapshot()`, `ClearWorldSnapshot()` |
| `Assets/Scripts/Platformer/EnemyController.cs` | **Modify** | Add `_enemyId` field, `EnemyId` property, `RestoreWorldPosition()` |
| `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | **Modify** | Build `WorldSnapshot` from scene enemies and push to `GameManager` before battle |
| `Assets/Scripts/Battle/BattleController.cs` | **Modify** | Add `BattleState.Victory` → Platformer transition in `HandleStateChanged` |

**No new `.asmdef` files needed.** All new Core C# goes in `Axiom.Core.asmdef`; Platformer C# in `Axiom.Platformer.asmdef` (already references `Axiom.Core`); tests in `Assets/Tests/Editor/Core/` covered by existing `CoreTests.asmdef`.

---

## Context: What Already Exists

Before touching any code, understand the current state:

- `GameManager.CaptureWorldSnapshot(Vector2)` — **already exists** and saves player position to `PlayerState.WorldPositionX/Y` + `PlayerState.ActiveSceneName`.
- `ExplorationEnemyCombatTrigger.TriggerBattle()` — **already calls** `CaptureWorldSnapshot`, `SetPendingBattle`, `PersistToDisk`, and `BeginTransition("Battle", WhiteFlash)`.
- `BattleController.HandleStateChanged(BattleState.Fled)` — **already handles** Fled → Platformer with BlackFade transition.
- `BattleState.Victory` — **not yet handled** in `HandleStateChanged`; player is stuck after victory.
- `EnemyController` — **no stable ID field**, no restore method; positions reset on scene reload.
- `PlayerController.InitializeFromTransition()` — enables input/movement but **does not restore position**.

---

## Task 1: EnemyWorldState and WorldSnapshot — data classes with unit tests

**Files:**
- Create: `Assets/Scripts/Core/EnemyWorldState.cs`
- Create: `Assets/Scripts/Core/WorldSnapshot.cs`
- Create: `Assets/Tests/Editor/Core/WorldSnapshotTests.cs`

- [ ] **Write the failing tests in `WorldSnapshotTests.cs`**

```csharp
// Assets/Tests/Editor/Core/WorldSnapshotTests.cs
using NUnit.Framework;
using Axiom.Core;

namespace Axiom.Tests.Editor.Core
{
    public class WorldSnapshotTests
    {
        // ── EnemyWorldState ──────────────────────────────────────────────────

        [Test]
        public void EnemyWorldState_StoresPositionCorrectly()
        {
            var state = new EnemyWorldState(3f, -7.5f);

            Assert.AreEqual(3f,    state.PositionX, 0.001f);
            Assert.AreEqual(-7.5f, state.PositionY, 0.001f);
        }

        // ── WorldSnapshot — enemy capture ────────────────────────────────────

        [Test]
        public void CaptureEnemy_ThenTryGet_ReturnsCorrectPosition()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 5f, 2f);

            bool found = snapshot.TryGetEnemy("enemy_01", out EnemyWorldState state);

            Assert.IsTrue(found);
            Assert.AreEqual(5f, state.PositionX, 0.001f);
            Assert.AreEqual(2f, state.PositionY, 0.001f);
        }

        [Test]
        public void TryGetEnemy_ReturnsFalse_ForUnknownId()
        {
            var snapshot = new WorldSnapshot();

            bool found = snapshot.TryGetEnemy("ghost_id", out EnemyWorldState state);

            Assert.IsFalse(found);
            Assert.IsNull(state);
        }

        [Test]
        public void CaptureEnemy_IgnoresNullId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy(null, 1f, 2f);

            bool found = snapshot.TryGetEnemy(null, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void CaptureEnemy_IgnoresWhitespaceId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("   ", 1f, 2f);

            bool found = snapshot.TryGetEnemy("   ", out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void CaptureEnemy_OverwritesPreviousEntryForSameId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 1f, 2f);
            snapshot.CaptureEnemy("enemy_01", 9f, -4f);

            snapshot.TryGetEnemy("enemy_01", out EnemyWorldState state);

            Assert.AreEqual(9f,  state.PositionX, 0.001f);
            Assert.AreEqual(-4f, state.PositionY, 0.001f);
        }

        [Test]
        public void CaptureEnemy_MultipleEnemies_AreStoredIndependently()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureEnemy("enemy_01", 1f, 0f);
            snapshot.CaptureEnemy("enemy_02", 5f, 3f);

            snapshot.TryGetEnemy("enemy_01", out EnemyWorldState s1);
            snapshot.TryGetEnemy("enemy_02", out EnemyWorldState s2);

            Assert.AreEqual(1f, s1.PositionX, 0.001f);
            Assert.AreEqual(5f, s2.PositionX, 0.001f);
        }

        // ── WorldSnapshot — interactable capture ─────────────────────────────

        [Test]
        public void CaptureInteractable_ThenTryGet_ReturnsCorrectState()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("door_01", isActive: true);

            bool found = snapshot.TryGetInteractable("door_01", out bool isActive);

            Assert.IsTrue(found);
            Assert.IsTrue(isActive);
        }

        [Test]
        public void CaptureInteractable_FalseState_RoundTrips()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("chest_02", isActive: false);

            snapshot.TryGetInteractable("chest_02", out bool isActive);

            Assert.IsFalse(isActive);
        }

        [Test]
        public void TryGetInteractable_ReturnsFalse_ForNullId()
        {
            var snapshot = new WorldSnapshot();
            snapshot.CaptureInteractable("door_01", true);

            bool found = snapshot.TryGetInteractable(null, out bool isActive);

            Assert.IsFalse(found);
            Assert.IsFalse(isActive);
        }

        [Test]
        public void TryGetInteractable_ReturnsFalse_ForUnknownId()
        {
            var snapshot = new WorldSnapshot();

            bool found = snapshot.TryGetInteractable("nonexistent", out bool _);

            Assert.IsFalse(found);
        }
    }
}
```

- [ ] **Run tests to verify they all fail**

  Unity Test Runner: Window → General → Test Runner → EditMode tab → `WorldSnapshotTests` → Run Selected
  Expected: all tests fail with "type or namespace not found" compile errors.

- [ ] **Create `Assets/Scripts/Core/EnemyWorldState.cs`**

```csharp
// Assets/Scripts/Core/EnemyWorldState.cs
namespace Axiom.Core
{
    /// <summary>
    /// Immutable snapshot of one enemy's world position, captured before a battle begins.
    /// </summary>
    public sealed class EnemyWorldState
    {
        public float PositionX { get; }
        public float PositionY { get; }

        public EnemyWorldState(float positionX, float positionY)
        {
            PositionX = positionX;
            PositionY = positionY;
        }
    }
}
```

- [ ] **Create `Assets/Scripts/Core/WorldSnapshot.cs`**

```csharp
// Assets/Scripts/Core/WorldSnapshot.cs
using System;
using System.Collections.Generic;

namespace Axiom.Core
{
    /// <summary>
    /// Stores the Platformer world state captured immediately before a battle begins.
    /// Held by <see cref="GameManager"/> and consumed by PlatformerWorldRestoreController on return.
    /// </summary>
    public sealed class WorldSnapshot
    {
        private readonly Dictionary<string, EnemyWorldState> _enemies =
            new Dictionary<string, EnemyWorldState>(StringComparer.Ordinal);

        private readonly Dictionary<string, bool> _interactables =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Records an enemy's position by stable ID.
        /// Silently ignores null/whitespace IDs.
        /// If the same ID is captured twice the second call overwrites the first.
        /// </summary>
        public void CaptureEnemy(string enemyId, float positionX, float positionY)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) return;
            _enemies[enemyId] = new EnemyWorldState(positionX, positionY);
        }

        /// <returns>
        /// <c>true</c> and populates <paramref name="state"/> if the ID is found;
        /// <c>false</c> and <c>null</c> state for unknown or invalid IDs.
        /// </returns>
        public bool TryGetEnemy(string enemyId, out EnemyWorldState state)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                state = null;
                return false;
            }
            return _enemies.TryGetValue(enemyId, out state);
        }

        /// <summary>
        /// Records an interactable object's active state by stable ID.
        /// Silently ignores null/whitespace IDs.
        /// If the same ID is captured twice the second call overwrites the first.
        /// </summary>
        public void CaptureInteractable(string objectId, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return;
            _interactables[objectId] = isActive;
        }

        /// <returns>
        /// <c>true</c> and populates <paramref name="isActive"/> if the ID is found;
        /// <c>false</c> and <c>false</c> for unknown or invalid IDs.
        /// </returns>
        public bool TryGetInteractable(string objectId, out bool isActive)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                isActive = default;
                return false;
            }
            return _interactables.TryGetValue(objectId, out isActive);
        }
    }
}
```

- [ ] **Run tests to verify they all pass**

  Unity Test Runner: Window → General → Test Runner → EditMode tab → `WorldSnapshotTests` → Run Selected
  Expected: all 12 tests pass.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): add EnemyWorldState and WorldSnapshot data classes`
  - `Assets/Scripts/Core/EnemyWorldState.cs`
  - `Assets/Scripts/Core/EnemyWorldState.cs.meta`
  - `Assets/Scripts/Core/WorldSnapshot.cs`
  - `Assets/Scripts/Core/WorldSnapshot.cs.meta`
  - `Assets/Tests/Editor/Core/WorldSnapshotTests.cs`
  - `Assets/Tests/Editor/Core/WorldSnapshotTests.cs.meta`

---

## Task 2: GameManager — add WorldSnapshot storage with unit tests

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Create: `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs`

- [ ] **Write the failing tests in `GameManagerWorldSnapshotTests.cs`**

```csharp
// Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerWorldSnapshotTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            // Destroy any stale Instance from an interrupted previous run so the
            // singleton guard in Awake never fires unexpectedly.
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>(); // triggers Awake → sets Instance
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go); // triggers OnDestroy → clears Instance
        }

        [Test]
        public void CurrentWorldSnapshot_IsNullByDefault()
        {
            Assert.IsNull(_gm.CurrentWorldSnapshot);
        }

        [Test]
        public void SetWorldSnapshot_StoresSnapshot()
        {
            var snapshot = new WorldSnapshot();

            _gm.SetWorldSnapshot(snapshot);

            Assert.AreSame(snapshot, _gm.CurrentWorldSnapshot);
        }

        [Test]
        public void SetWorldSnapshot_ReplacesExistingSnapshot()
        {
            var first  = new WorldSnapshot();
            var second = new WorldSnapshot();
            _gm.SetWorldSnapshot(first);

            _gm.SetWorldSnapshot(second);

            Assert.AreSame(second, _gm.CurrentWorldSnapshot);
        }

        [Test]
        public void ClearWorldSnapshot_SetsPropertyToNull()
        {
            _gm.SetWorldSnapshot(new WorldSnapshot());

            _gm.ClearWorldSnapshot();

            Assert.IsNull(_gm.CurrentWorldSnapshot);
        }

        [Test]
        public void ClearWorldSnapshot_IsNoOp_WhenAlreadyNull()
        {
            Assert.DoesNotThrow(() => _gm.ClearWorldSnapshot());
        }
    }
}
```

- [ ] **Run tests to verify they all fail**

  Unity Test Runner: EditMode → `GameManagerWorldSnapshotTests` → Run Selected
  Expected: fail with "GameManager does not contain a definition for 'CurrentWorldSnapshot'".

- [ ] **Add the three members to `GameManager.cs`**

  Open `Assets/Scripts/Core/GameManager.cs`. After the `PendingBattle` property (around line 50), add:

```csharp
/// <summary>
/// Snapshot of the Platformer world state captured immediately before a battle.
/// Non-null only between the battle transition and the first Platformer scene restore.
/// Set by <see cref="SetWorldSnapshot"/>; cleared by PlatformerWorldRestoreController
/// after restoration completes.
/// </summary>
public WorldSnapshot CurrentWorldSnapshot { get; private set; }

/// <summary>Sets the world snapshot. Replaces any existing snapshot.</summary>
public void SetWorldSnapshot(WorldSnapshot snapshot) => CurrentWorldSnapshot = snapshot;

/// <summary>Clears the world snapshot. Safe to call when already null.</summary>
public void ClearWorldSnapshot() => CurrentWorldSnapshot = null;
```

- [ ] **Run tests to verify they all pass**

  Unity Test Runner: EditMode → `GameManagerWorldSnapshotTests` → Run Selected
  Expected: all 5 tests pass.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): add world snapshot storage to GameManager`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs.meta`

---

## Task 3: EnemyController — add stable ID field and restore method

**Files:**
- Modify: `Assets/Scripts/Platformer/EnemyController.cs`

`EnemyController` is in the global namespace (no namespace declaration) — do not add one.

- [ ] **Add `_enemyId` field, `EnemyId` property, and `RestoreWorldPosition()` to `EnemyController`**

  Open `Assets/Scripts/Platformer/EnemyController.cs`. In the `[Header("Patrol")]` block, add the ID field directly above the existing patrol fields. Also add the property and method at the bottom of the class before the closing brace.

  Add inside the class, at the top of the serialized fields:

```csharp
[Header("World State")]
[SerializeField]
[Tooltip("Stable unique ID used to match this enemy to its captured world position. " +
         "Must be non-empty and unique within the Platformer scene. Assign in Inspector.")]
private string _enemyId = string.Empty;

/// <summary>Stable unique ID for this enemy. Assigned in the Inspector.</summary>
public string EnemyId => _enemyId;
```

  Add at the bottom of the class body (before the final `}`):

```csharp
/// <summary>
/// Teleports the enemy to the given world position and zeroes velocity.
/// Called by PlatformerWorldRestoreController when restoring world state after a battle.
/// </summary>
public void RestoreWorldPosition(float positionX, float positionY)
{
    transform.position = new Vector3(positionX, positionY, transform.position.z);
    if (_rb != null)
        _rb.linearVelocity = Vector2.zero;
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): add enemy ID and world position restore to EnemyController`
  - `Assets/Scripts/Platformer/EnemyController.cs`

---

## Task 4: ExplorationEnemyCombatTrigger — capture world snapshot before battle

**Files:**
- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`

`ExplorationEnemyCombatTrigger` is in `namespace Axiom.Platformer`. `EnemyController` is in the global namespace but the same `Axiom.Platformer` assembly — accessible without a `using` statement.

- [ ] **Add snapshot capture to `TriggerBattle()`**

  Open `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`. In `TriggerBattle()`, after the null-guard on `SceneTransitionController` and before the `SetPendingBattle` call, add:

```csharp
// Build world snapshot: capture every enemy's current position by stable ID.
// Enemies without an _enemyId assigned in the Inspector are silently skipped.
var snapshot = new WorldSnapshot();
var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(
    UnityEngine.FindObjectsSortMode.None);
foreach (EnemyController enemy in enemies)
    snapshot.CaptureEnemy(enemy.EnemyId, enemy.transform.position.x, enemy.transform.position.y);

GameManager.Instance.SetWorldSnapshot(snapshot);
```

  The `TriggerBattle()` method should look like this after the edit (full method shown for clarity):

```csharp
private void TriggerBattle(CombatStartState startState, Collider2D playerCollider = null)
{
    _triggered = true;

    if (GameManager.Instance == null)
    {
        Debug.LogWarning(
            "[ExplorationEnemyCombatTrigger] GameManager not found — battle cannot start and trigger is now consumed. " +
            "Add the GameManager prefab to the Platformer scene.",
            this);
        return;
    }

    if (GameManager.Instance.SceneTransition == null)
    {
        Debug.LogWarning(
            "[ExplorationEnemyCombatTrigger] SceneTransitionController not found on GameManager " +
            "— battle cannot start. Check the GameManager prefab has a SceneTransitionController child.",
            this);
        return;
    }

    // Build world snapshot: capture every enemy's current position by stable ID.
    // Enemies without an _enemyId assigned in the Inspector are silently skipped.
    var snapshot = new WorldSnapshot();
    var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(
        UnityEngine.FindObjectsSortMode.None);
    foreach (EnemyController enemy in enemies)
        snapshot.CaptureEnemy(enemy.EnemyId, enemy.transform.position.x, enemy.transform.position.y);

    GameManager.Instance.SetWorldSnapshot(snapshot);

    GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData));

    Vector2 playerWorldPosition = ResolvePlayerWorldPosition(playerCollider);

    GameManager.Instance.CaptureWorldSnapshot(playerWorldPosition);

    GameManager.Instance.PersistToDisk();

    GameManager.Instance.SceneTransition.BeginTransition("Battle", TransitionStyle.WhiteFlash);
}
```

  Note: `using Axiom.Core;` is already at the top of this file — `WorldSnapshot` is in `Axiom.Core` and is accessible. `EnemyController` is in the same assembly (global namespace) and requires no `using`.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): capture world snapshot before battle transition`
  - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`

---

## Task 5: PlatformerWorldRestoreController — restore world state on scene ready

**Files:**
- Create: `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`

This MonoBehaviour should be added to the Platformer scene (Task 8). It is the single point responsible for:
1. Teleporting the player to their pre-battle position.
2. Restoring all enemy positions from the snapshot.
3. Clearing the snapshot after restoration.

`PlayerController` and `EnemyController` are both in the global namespace but in the same `Axiom.Platformer` assembly — accessible without a `using` statement.

- [ ] **Create `PlatformerWorldRestoreController.cs`**

```csharp
// Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs
using UnityEngine;
using Axiom.Core;

namespace Axiom.Platformer
{
    /// <summary>
    /// MonoBehaviour — restores Platformer world state after returning from a battle.
    ///
    /// Add one instance of this to the Platformer scene root.
    ///
    /// Restoration only occurs when GameManager holds a non-null CurrentWorldSnapshot
    /// (i.e. the player just returned from a battle). After restoration the snapshot
    /// is cleared so subsequent scene loads are not affected.
    ///
    /// Script Execution Order: set to -10 (Edit → Project Settings → Script Execution Order)
    /// so this runs before PlayerController's Start, ensuring position is teleported
    /// before input is enabled.
    /// </summary>
    public class PlatformerWorldRestoreController : MonoBehaviour
    {
        private void Start()
        {
            if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
                GameManager.Instance.OnSceneReady += InitializeFromTransition;
            else
                InitializeFromTransition();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= InitializeFromTransition;
        }

        private void InitializeFromTransition()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= InitializeFromTransition;

            RestoreWorldState();
        }

        private void RestoreWorldState()
        {
            if (GameManager.Instance?.CurrentWorldSnapshot == null) return;

            WorldSnapshot snapshot  = GameManager.Instance.CurrentWorldSnapshot;
            PlayerState playerState = GameManager.Instance.PlayerState;

            // ── 1. Restore player position ──────────────────────────────────
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = new Vector3(
                    playerState.WorldPositionX,
                    playerState.WorldPositionY,
                    player.transform.position.z);
            }
            else
            {
                Debug.LogWarning(
                    "[PlatformerWorldRestoreController] PlayerController not found in scene — " +
                    "player position will not be restored.",
                    this);
            }

            // ── 2. Restore enemy positions ───────────────────────────────────
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (EnemyController enemy in enemies)
            {
                if (snapshot.TryGetEnemy(enemy.EnemyId, out EnemyWorldState state))
                    enemy.RestoreWorldPosition(state.PositionX, state.PositionY);
            }

            // ── 3. Clear snapshot — restoration complete ────────────────────
            GameManager.Instance.ClearWorldSnapshot();
        }
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): add PlatformerWorldRestoreController`
  - `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`
  - `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs.meta`

---

## Task 6: BattleController — Victory returns to Platformer

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

`BattleState.Victory` currently has no handling in `HandleStateChanged`. The player is stuck on the battle outcome screen. This task adds the return transition as a **placeholder** — DEV-37 will replace this with an XP/loot screen before the fade.

- [ ] **Add Victory → Platformer transition to `HandleStateChanged()`**

  Open `Assets/Scripts/Battle/BattleController.cs`. Find `HandleStateChanged` (around line 494). Add the `Victory` branch directly after the `Fled` block:

```csharp
else if (state == BattleState.Victory)
{
    // Placeholder — DEV-37 will insert XP/loot screen before this transition.
    GameManager.Instance?.PersistToDisk();
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
}
```

  The full `HandleStateChanged` method after the edit:

```csharp
private void HandleStateChanged(BattleState state)
{
    Debug.Log($"[Battle] → {state}");
    OnBattleStateChanged?.Invoke(state);

    if (state == BattleState.PlayerTurn)
        ProcessPlayerTurnStart();
    else if (state == BattleState.EnemyTurn)
        ProcessEnemyTurnStart();
    else if (state == BattleState.Fled)
    {
        GameManager.Instance?.PersistToDisk();
        if (GameManager.Instance?.SceneTransition != null)
            GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
        else
            SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
    }
    else if (state == BattleState.Victory)
    {
        // Placeholder — DEV-37 will insert XP/loot screen before this transition.
        GameManager.Instance?.PersistToDisk();
        if (GameManager.Instance?.SceneTransition != null)
            GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
        else
            SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
    }
}
```

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-35): return to Platformer on BattleState.Victory`
  - `Assets/Scripts/Battle/BattleController.cs`

---

## Task 7: Unity Editor — assign enemy IDs and wire PlatformerWorldRestoreController

> **Unity Editor task (user):** All steps in this task are performed in the Unity Editor, not in code.

- [ ] **Set Script Execution Order for `PlatformerWorldRestoreController`**

  Edit → Project Settings → Script Execution Order → click **+** → select `PlatformerWorldRestoreController` → set value to **-10** → Apply.

  This ensures `PlatformerWorldRestoreController.Start()` runs before `PlayerController.Start()` so the player is teleported before input/movement is enabled.

- [ ] **Add `PlatformerWorldRestoreController` to the Platformer scene**

  Open `Assets/Scenes/Platformer.unity` → select the root scene GameObject (or the GameManager child — anywhere permanent) → Add Component → `PlatformerWorldRestoreController`. Save the scene.

- [ ] **Assign unique enemy IDs in the Inspector**

  For each enemy GameObject in the Platformer scene:
  1. Select the enemy root GameObject.
  2. In the `EnemyController` component, find the **World State / Enemy Id** field.
  3. Enter a unique, stable string ID (e.g. `patrol_enemy_01`, `patrol_enemy_02`).

  Rules for IDs:
  - Must be **unique** within the Platformer scene (duplicates silently prevent position restore for one of them).
  - Must be **stable** — do not rename after assigning, as the ID is what links the captured state back to the restored enemy.
  - Use a consistent format: `<type>_<index>` (e.g. `patrol_enemy_01`).

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-35): assign enemy IDs and add PlatformerWorldRestoreController to Platformer scene`
  - `Assets/Scenes/Platformer.unity`
  - `Assets/Scenes/Platformer.unity.meta` (if modified)
  - `ProjectSettings/ProjectSettings.asset` (Script Execution Order is stored here)

---

## Task 8: Manual end-to-end verification

No code changes. Verify all four Acceptance Criteria from the Jira ticket.

> **Unity Editor task (user):** Play the game from the Platformer scene with the GameManager prefab present.

- [ ] **AC 1 — Player position is saved and restored**

  1. Enter Play Mode with the Platformer scene.
  2. Walk the player to a position away from the spawn point (e.g. right side of the level).
  3. Engage an enemy (Surprised or Advantaged).
  4. Complete the battle (either Flee or defeat the enemy).
  5. Verify the player teleports back to the position they were at before the battle, not the spawn point.

- [ ] **AC 2 — Enemy positions are preserved**

  1. Chase a patrolling enemy so it moves to a non-default position.
  2. Engage a *different* enemy to start a battle (so the first enemy keeps patrolling and reaches a new position).
  3. Complete the battle and return.
  4. Verify the patrolling enemy is at the position it had *when the battle started*, not its spawn position.

- [ ] **AC 3 — No corruption across multiple cycles**

  1. Engage three battles in succession (flee each one).
  2. After each return, verify player position and enemy positions are correctly restored.
  3. Verify no `NullReferenceException` appears in the console.

- [ ] **AC 4 — Interactable state (foundation verified)**

  No interactable objects exist in the scene yet — this AC will be verified when interactable objects are added in a future phase. Confirm that `WorldSnapshot.CaptureInteractable` / `TryGetInteractable` are tested by unit tests (Task 1) as the infrastructure is in place.

---

## Deferred Verification — Features Not Testable Yet

The following behaviors are implemented as infrastructure but cannot be end-to-end verified within DEV-35's scope. They depend on systems that land in later tickets; track them here so they aren't forgotten.

| Item | Blocked on | Notes |
|------|------------|-------|
| `WorldSnapshot.CaptureInteractable` / `TryGetInteractable` round-trip in a real scene | Interactable objects (chests, doors, switches) — future world-content ticket | Unit tests cover the data layer (Task 1). No live capture/restore call site exists until interactables exist. |
| `GameManager.ClearDefeatedEnemies()` on new-game / load-game | DEV-5x save/load flow | Method exists and is correct, but has no call site yet. Without it, `_defeatedEnemyIds` persists across sessions within a single process — acceptable for now since DEV-35 doesn't wire save/load of defeated enemies. |
| Persistence of `_defeatedEnemyIds` across app restart | DEV-5x save system extension | Currently in-memory only. If the player quits between Victory and Platformer scene load, the defeated enemy will re-spawn and the loop bug returns. Document as known limitation until save format is extended. |
| `BattleState.Victory` XP / loot screen before Platformer transition | DEV-37 | Current Victory handler is a placeholder that transitions straight to Platformer. DEV-37 will insert the reward UI between `MarkEnemyDefeated` and `BeginTransition`. |
| `PlatformerWorldRestoreController` restore ordering in the first rendered frame | Manual play-mode verification only | Relies on SceneTransitionController's opaque overlay covering the scene until after Start() runs. No automated coverage — must be eyeballed in Play Mode for Bug 1 (spawn-point flicker). |

---

## Self-Review

### Spec coverage

| AC | Covered by |
|----|------------|
| Player world position saved before battle | `ExplorationEnemyCombatTrigger.TriggerBattle()` already calls `CaptureWorldSnapshot`; this was pre-existing. |
| Player world position restored after battle | Task 5 — `PlatformerWorldRestoreController.RestoreWorldState()` teleports player. |
| Enemy positions preserved across battle | Task 3 (ID + restore method) + Task 4 (capture) + Task 5 (restore). |
| Interactable states preserved | `WorldSnapshot.CaptureInteractable/TryGetInteractable` added in Task 1 as infrastructure; no live hookup until interactables exist. |
| No corruption across multiple cycles | Snapshot is cleared after each restore (Task 5 last line); next battle sets a fresh snapshot. Verified in AC 3. |

### Type/method consistency check

| Symbol defined in | Used in | Match? |
|---|---|---|
| `EnemyWorldState(float, float)` — Task 1 | `WorldSnapshot.CaptureEnemy` — Task 1 | ✓ |
| `WorldSnapshot.TryGetEnemy` — Task 1 | `PlatformerWorldRestoreController` — Task 5 | ✓ |
| `GameManager.SetWorldSnapshot(WorldSnapshot)` — Task 2 | `ExplorationEnemyCombatTrigger` — Task 4 | ✓ |
| `GameManager.ClearWorldSnapshot()` — Task 2 | `PlatformerWorldRestoreController` — Task 5 | ✓ |
| `GameManager.CurrentWorldSnapshot` — Task 2 | `PlatformerWorldRestoreController` — Task 5 | ✓ |
| `EnemyController.EnemyId` — Task 3 | `ExplorationEnemyCombatTrigger` — Task 4 | ✓ |
| `EnemyController.RestoreWorldPosition(float, float)` — Task 3 | `PlatformerWorldRestoreController` — Task 5 | ✓ |
| `BattleState.Victory` | `BattleController.HandleStateChanged` — Task 6 | ✓ (type is pre-existing) |

### UVCS staged file audit

Every created file has its `.meta` listed. The Platformer scene is staged in Task 7. No `.asmdef` files are created or modified — none to stage.

### Unity Editor task isolation

All Unity Editor steps are in dedicated `> **Unity Editor task (user):**` sections within Task 7. No code and Editor steps are mixed.
