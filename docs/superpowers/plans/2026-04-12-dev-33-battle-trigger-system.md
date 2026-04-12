# DEV-33: Two-Path Battle Trigger System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement two distinct overworld combat engagement paths — Advantaged (player strikes enemy first) and Surprised (enemy catches player) — each producing the correct `CombatStartState` that is passed through `GameManager` to `BattleController` when the Battle scene loads.

**Architecture:** `CombatStartState` moves from `Axiom.Battle` to `Axiom.Data` to break a potential circular assembly dependency. `GameManager` (in `Axiom.Core`) gains a `PendingBattle` property holding a new `BattleEntry` data object (in `Axiom.Data`). `BattleController.Start()` consumes `PendingBattle` from `GameManager` when present, falling back to Inspector values for standalone Battle scene testing. Two MonoBehaviours in `Axiom.Platformer` — `OverworldEnemyCombatTrigger` (on the enemy) and `PlayerOverworldAttack` (on the player) — set `PendingBattle` and call `SceneManager.LoadScene("Battle")`.

**Tech Stack:** Unity 6 LTS, C#, Unity New Input System, Unity SceneManager, NUnit (Edit Mode tests via Unity Test Runner)

---

## Dependency Graph After This Plan

```
Axiom.Data          (foundational — no project references)
     ↑
Axiom.Core          (references Axiom.Data)
     ↑
Axiom.Battle        (references Axiom.Data, Axiom.Core)
Axiom.Platformer    (references Axiom.Data, Axiom.Core, Unity.InputSystem)
```

No circular dependencies.

---

## DEV-32 Context Note

DEV-32 (enemy patrol + chase) is **complete**. This plan integrates directly with its output:

- `OverworldEnemyCombatTrigger` is a standalone MonoBehaviour — attach it to the patrol enemy prefab DEV-32 produced.
- `Assets/Prefabs/Enemies/Enemy.prefab` exists. Use it as the base in Task 8 — do not create a new enemy from scratch.

### Collision architecture with DEV-32

DEV-32's enemy root already has a **physics Collider2D** (non-trigger) required for standing on the ground. DEV-33 needs a **separate, trigger-only Collider2D** on the enemy root for the Surprised path `OnTriggerEnter2D`. Do **not** change `Is Trigger` on the existing physics collider — that would break gravity and wall collisions. Add a second Collider2D component with `Is Trigger` enabled.

### Layer setup relationship with DEV-32

DEV-32 creates a **"Player" layer** (assigned to the player GameObject) used by `EnemyController.playerLayer` for aggro detection. DEV-33 needs a separate **"Enemy" layer** (assigned to enemy GameObjects) used by `PlayerOverworldAttack._enemyLayer` for the Advantaged attack scan. These are two distinct layers serving different purposes — both must exist.

### Namespace note

DEV-32's `EnemyPatrolBehavior` and `EnemyController` are in the **global C# namespace** (no `namespace` wrapper), while `OverworldEnemyCombatTrigger` is in `namespace Axiom.Platformer`. Both live in the `Axiom.Platformer` assembly — no compile issues arise from the namespace difference.

---

## File Map

| Action | Path | Responsibility |
|--------|------|---------------|
| **Move** | `Assets/Scripts/Battle/CombatStartState.cs` → `Assets/Scripts/Data/CombatStartState.cs` | Shared enum, now in `Axiom.Data` namespace |
| **Create** | `Assets/Scripts/Data/BattleEntry.cs` | Cross-scene data: StartState + EnemyData |
| **Create** | `Assets/Tests/Editor/Battle/BattleEntryTests.cs` | Edit Mode tests for BattleEntry |
| **Modify** | `Assets/Scripts/Battle/BattleManager.cs` | Add `using Axiom.Data;` for CombatStartState |
| **Modify** | `Assets/Scripts/Battle/BattleController.cs` | Add `using Axiom.Data;` + `using Axiom.Core;`; consume PendingBattle in Start() |
| **Modify** | `Assets/Scripts/Core/GameManager.cs` | Add PendingBattle API |
| **Create** | `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs` | Edit Mode tests for GameManager pending battle API |
| **Modify** | `Assets/Scripts/Core/Axiom.Core.asmdef` | Add `Axiom.Data` reference |
| **Modify** | `Assets/Scripts/Battle/Battle.asmdef` | Add `Axiom.Core` reference |
| **Modify** | `Assets/Scripts/Platformer/Platformer.asmdef` | Add `Axiom.Core`, `Axiom.Data` references |
| **Modify** | `Assets/Tests/Editor/Core/CoreTests.asmdef` | Add `Axiom.Data` reference |
| **Create** | `Assets/Scripts/Platformer/OverworldEnemyCombatTrigger.cs` | Enemy-side MonoBehaviour; both trigger paths |
| **Create** | `Assets/Scripts/Platformer/PlayerOverworldAttack.cs` | Player-side MonoBehaviour; attack input → Advantaged path |

---

## Task 1: Move CombatStartState to Axiom.Data and update usages

**Files:**
- Move: `Assets/Scripts/Battle/CombatStartState.cs` → `Assets/Scripts/Data/CombatStartState.cs`
- Modify: `Assets/Scripts/Battle/BattleManager.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`

`CombatStartState` must live in `Axiom.Data` so both `Axiom.Core` and `Axiom.Battle` can reference it without circular dependency. This is a namespace-only move — the enum values do not change.

- [ ] **Step 1: Create the new file in Data**

Create `Assets/Scripts/Data/CombatStartState.cs` with this exact content (namespace changed from `Axiom.Battle` to `Axiom.Data`):

```csharp
namespace Axiom.Data
{
    /// <summary>
    /// Passed to BattleManager.StartBattle() to determine who acts first.
    /// Advantaged = player struck first (player goes first).
    /// Surprised  = enemy struck first (enemy goes first).
    /// </summary>
    public enum CombatStartState
    {
        Advantaged,
        Surprised
    }
}
```

- [ ] **Step 2: Delete the old file**

Delete `Assets/Scripts/Battle/CombatStartState.cs` and its `.meta` file.

> **Unity Editor task (user):** In the Project window, delete `Assets/Scripts/Battle/CombatStartState.cs`. Unity will prompt to also delete the `.meta` — confirm yes. Then right-click `Assets/Scripts/Data/` → Reimport All to pick up the new file.

- [ ] **Step 3: Add `using Axiom.Data;` to BattleManager.cs**

`BattleManager.cs` uses `CombatStartState` without a `using` statement because it was previously in the same namespace. Now add it at the top of `Assets/Scripts/Battle/BattleManager.cs`:

```csharp
using System;
using Axiom.Data;

namespace Axiom.Battle
{
    // ... rest of file unchanged
```

- [ ] **Step 4: Add `using Axiom.Data;` to BattleController.cs**

`BattleController.cs` already has `using Axiom.Data;` at line 4. Verify this line is present. If it is, no change is needed — `CombatStartState` is now resolved through the existing import. If somehow it is missing, add it:

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Data;

namespace Axiom.Battle
{
    // ... rest of file unchanged
```

- [ ] **Step 5: Check for any other files using CombatStartState**

Search `Assets/Scripts/` for all files referencing `CombatStartState`. If any other file has it under `Axiom.Battle` namespace assumptions, add `using Axiom.Data;` to that file too.

> Run in Unity Test Runner to confirm no compile errors at this stage before moving on.

- [ ] **Step 6: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-33): move CombatStartState to Axiom.Data namespace`
  - `Assets/Scripts/Data/CombatStartState.cs`
  - `Assets/Scripts/Data/CombatStartState.cs.meta`
  - `Assets/Scripts/Battle/BattleManager.cs`
  - `Assets/Scripts/Battle/BattleController.cs` _(only if Step 4 required adding the `using` directive — if the line was already present, omit this file)_

---

## Task 2: Add BattleEntry data class (TDD)

**Files:**
- Create: `Assets/Scripts/Data/BattleEntry.cs`
- Create: `Assets/Tests/Editor/Battle/BattleEntryTests.cs`

`BattleEntry` is an immutable data holder that carries the two pieces of information the Battle scene needs at load time: which `CombatStartState` applies and which `EnemyData` ScriptableObject was engaged.

`EnemyData` is nullable — passing `null` is valid for standalone Battle scene testing where `BattleController` falls back to its Inspector-configured stats.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/BattleEntryTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Data;

namespace Axiom.Tests.Editor.Battle
{
    public class BattleEntryTests
    {
        [Test]
        public void Constructor_StoresAdvantagedStartState()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            Assert.AreEqual(CombatStartState.Advantaged, entry.StartState);
        }

        [Test]
        public void Constructor_StoresSurprisedStartState()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);

            Assert.AreEqual(CombatStartState.Surprised, entry.StartState);
        }

        [Test]
        public void Constructor_AllowsNullEnemyData()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            Assert.IsNull(entry.EnemyData);
        }

        [Test]
        public void Constructor_StoresEnemyData_WhenProvided()
        {
            // EnemyData is a ScriptableObject. CreateInstance is the correct way to
            // instantiate it outside the Unity Editor's asset pipeline.
            var data = UnityEngine.ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = "Test Enemy";

            var entry = new BattleEntry(CombatStartState.Surprised, data);

            Assert.AreSame(data, entry.EnemyData);

            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
```

- [ ] **Step 2: Run the tests — expect compile errors (BattleEntry does not exist yet)**

  Unity Editor → Window → General → Test Runner → Edit Mode → Run All

  Expected: `BattleEntry` not found compile errors. This confirms the tests are wired correctly.

- [ ] **Step 3: Implement BattleEntry**

Create `Assets/Scripts/Data/BattleEntry.cs`:

```csharp
namespace Axiom.Data
{
    /// <summary>
    /// Cross-scene battle context. Set by the overworld trigger before loading the Battle
    /// scene; consumed and cleared by BattleController.Start() on Battle scene load.
    ///
    /// EnemyData may be null — BattleController falls back to its Inspector-configured
    /// stats when null, preserving standalone Battle scene testing.
    /// </summary>
    public sealed class BattleEntry
    {
        public CombatStartState StartState { get; }
        public EnemyData EnemyData { get; }

        public BattleEntry(CombatStartState startState, EnemyData enemyData)
        {
            StartState = startState;
            EnemyData = enemyData;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all 4 to pass**

  Unity Editor → Test Runner → Edit Mode → Run All

  Expected: `BattleEntryTests` — 4 passed, 0 failed.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): add BattleEntry data class`
  - `Assets/Scripts/Data/BattleEntry.cs`
  - `Assets/Scripts/Data/BattleEntry.cs.meta`
  - `Assets/Tests/Editor/Battle/BattleEntryTests.cs`
  - `Assets/Tests/Editor/Battle/BattleEntryTests.cs.meta`

---

## Task 3: Update assembly definitions

**Files:**
- Modify: `Assets/Scripts/Core/Axiom.Core.asmdef`
- Modify: `Assets/Scripts/Battle/Battle.asmdef`
- Modify: `Assets/Scripts/Platformer/Platformer.asmdef`
- Modify: `Assets/Tests/Editor/Core/CoreTests.asmdef`

These asmdef changes establish the dependency graph described at the top of this plan. All four edits must be made together — partial application will cause compile errors.

- [ ] **Step 1: Update Axiom.Core.asmdef**

Replace the contents of `Assets/Scripts/Core/Axiom.Core.asmdef` with:

```json
{
    "name": "Axiom.Core",
    "references": [
        "Axiom.Data"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Update Battle.asmdef**

Replace the contents of `Assets/Scripts/Battle/Battle.asmdef` with:

```json
{
    "name": "Axiom.Battle",
    "references": [
        "Axiom.Data",
        "Axiom.Core",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Update Platformer.asmdef**

Replace the contents of `Assets/Scripts/Platformer/Platformer.asmdef` with:

```json
{
    "name": "Axiom.Platformer",
    "references": [
        "Axiom.Core",
        "Axiom.Data",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Update CoreTests.asmdef**

Replace the contents of `Assets/Tests/Editor/Core/CoreTests.asmdef` with:

```json
{
    "name": "CoreTests",
    "references": [
        "Axiom.Core",
        "Axiom.Data",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
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

- [ ] **Step 5: Verify no compile errors**

  Save all `.asmdef` files and wait for Unity to recompile. The Console should show zero errors. If errors appear, they will name the missing reference — check the dependency graph at the top of this plan.

  **DEV-32 note:** `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef` exists and only references `Axiom.Platformer` — no update needed. Assembly reference dependencies are not transitive in Unity (test assemblies only see what they directly reference), and `PlatformerTests` only uses `EnemyPatrolBehavior` and `Vector2`, both accessible via `Axiom.Platformer` without needing `Axiom.Core` or `Axiom.Data` directly.

- [ ] **Step 6: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-33): update assembly definitions for Core→Data, Battle→Core, Platformer→Core+Data dependency chain`
  - `Assets/Scripts/Core/Axiom.Core.asmdef`
  - `Assets/Scripts/Battle/Battle.asmdef`
  - `Assets/Scripts/Platformer/Platformer.asmdef`
  - `Assets/Tests/Editor/Core/CoreTests.asmdef`

---

## Task 4: Extend GameManager with PendingBattle API (TDD)

**Files:**
- Create: `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`

`GameManager` gains three simple methods: `SetPendingBattle(BattleEntry)`, `ClearPendingBattle()`, and a read-only `PendingBattle` property. `BattleController` will call `ClearPendingBattle()` after consuming the entry so the data is never stale.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerPendingBattleTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            // AddComponent triggers Awake, which sets GameManager.Instance.
            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate triggers OnDestroy, which clears GameManager.Instance.
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void PendingBattle_IsNullByDefault()
        {
            Assert.IsNull(_gm.PendingBattle);
        }

        [Test]
        public void SetPendingBattle_StoresEntry()
        {
            var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

            _gm.SetPendingBattle(entry);

            Assert.AreSame(entry, _gm.PendingBattle);
        }

        [Test]
        public void SetPendingBattle_ReplacesExistingEntry()
        {
            var first  = new BattleEntry(CombatStartState.Advantaged, enemyData: null);
            var second = new BattleEntry(CombatStartState.Surprised,  enemyData: null);

            _gm.SetPendingBattle(first);
            _gm.SetPendingBattle(second);

            Assert.AreSame(second, _gm.PendingBattle);
        }

        [Test]
        public void ClearPendingBattle_NullsProperty()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);
            _gm.SetPendingBattle(entry);

            _gm.ClearPendingBattle();

            Assert.IsNull(_gm.PendingBattle);
        }

        [Test]
        public void ClearPendingBattle_IsNoOp_WhenAlreadyNull()
        {
            // Should not throw.
            Assert.DoesNotThrow(() => _gm.ClearPendingBattle());
        }
    }
}
```

- [ ] **Step 2: Run the tests — expect failures (API does not exist yet)**

  Unity Editor → Test Runner → Edit Mode → Run All

  Expected: `GameManagerPendingBattleTests` — all 5 fail with `PendingBattle` / `SetPendingBattle` / `ClearPendingBattle` not found.

- [ ] **Step 3: Implement PendingBattle API in GameManager**

Replace the full contents of `Assets/Scripts/Core/GameManager.cs` with:

```csharp
using UnityEngine;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Persistent singleton that survives scene loads and owns the cross-scene PlayerState.
    ///
    /// Access pattern for other systems — store a local reference, never a new static field:
    ///
    ///   private PlayerState _playerState;
    ///
    ///   void Start()
    ///   {
    ///       _playerState = GameManager.Instance.PlayerState;
    ///   }
    ///
    /// Do NOT write: public static GameManager instance; in any other class.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerState PlayerState { get; private set; }

        /// <summary>
        /// Set by OverworldEnemyCombatTrigger before loading the Battle scene.
        /// Consumed and cleared by BattleController.Start() on Battle scene load.
        /// Null when no battle transition is pending (normal state).
        /// </summary>
        public BattleEntry PendingBattle { get; private set; }

        /// <summary>Sets the pending battle context before transitioning to the Battle scene.</summary>
        public void SetPendingBattle(BattleEntry entry) => PendingBattle = entry;

        /// <summary>
        /// Clears the pending battle context after BattleController has consumed it.
        /// Safe to call when PendingBattle is already null.
        /// </summary>
        public void ClearPendingBattle() => PendingBattle = null;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect all 5 to pass**

  Unity Editor → Test Runner → Edit Mode → Run All

  Expected: `GameManagerPendingBattleTests` — 5 passed, 0 failed. All other existing tests still pass.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): add PendingBattle API to GameManager`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs.meta`

---

## Task 5: Wire BattleController.Start() to consume PendingBattle

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

`BattleController.Start()` currently always uses the Inspector-serialized `_startState` and `_enemyData`. After this change, if `GameManager.Instance` has a `PendingBattle` set, those values override the Inspector fields — then `PendingBattle` is cleared so it cannot affect any subsequent battle load.

If `GameManager` is absent (standalone Battle scene testing with no `GameManager` prefab in the scene), `Instance` is null and the guard falls through to the existing Inspector-driven `Initialize()` call unchanged.

- [ ] **Step 1: Add `using Axiom.Core;` to BattleController.cs**

`BattleController.cs` already has `using Axiom.Data;` — it does not yet have `using Axiom.Core;`. Add it to the using block at the top of `Assets/Scripts/Battle/BattleController.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle
{
    // ... rest of file
```

- [ ] **Step 2: Modify BattleController.Start()**

Find the `Start()` method in `BattleController.cs` (currently at line 193):

```csharp
private void Start()
{
    Initialize(_startState);
}
```

Replace it with:

```csharp
private void Start()
{
    var pending = GameManager.Instance?.PendingBattle;
    if (pending != null)
    {
        _startState = pending.StartState;
        _enemyData  = pending.EnemyData;
        GameManager.Instance.ClearPendingBattle();
    }

    Initialize(_startState);
}
```

- [ ] **Step 3: Run all Edit Mode tests to confirm no regressions**

  Unity Editor → Test Runner → Edit Mode → Run All

  Expected: all existing tests still pass. The `BattleController` change has no Edit Mode tests of its own (it requires scene lifecycle to test fully), but it must not break existing test compilation.

- [ ] **Step 4: Verify standalone Battle scene still works**

  Open the Battle scene. Press Play in the Unity Editor. The battle should start with Inspector-configured values (GameManager is not present in the Battle scene). Confirm the turn order matches the `_startState` value set in the `BattleController` Inspector.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): wire BattleController.Start to consume GameManager.PendingBattle`
  - `Assets/Scripts/Battle/BattleController.cs`

---

## Task 6: Implement OverworldEnemyCombatTrigger

**Files:**
- Create: `Assets/Scripts/Platformer/OverworldEnemyCombatTrigger.cs`

This MonoBehaviour lives on an overworld enemy GameObject. It handles both combat engagement paths:

- **Surprised:** `OnTriggerEnter2D` fires when the enemy's trigger collider overlaps the Player tag. This is the "enemy caught the player" path.
- **Advantaged:** `TriggerAdvantagedBattle()` is called externally by `PlayerOverworldAttack` when the player attacks first.

A `_triggered` guard prevents double-trigger if both conditions overlap in the same frame.

If `GameManager.Instance` is null (no GameManager prefab in the Platformer scene), a warning is logged and the scene still loads — `BattleController` falls back to its Inspector values. This ensures DEV-33 degrades gracefully during development when GameManager is not yet wired up.

- [ ] **Step 1: Implement OverworldEnemyCombatTrigger**

Create `Assets/Scripts/Platformer/OverworldEnemyCombatTrigger.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to an overworld enemy. Handles both combat engagement paths:
    ///
    ///   Surprised  — enemy body trigger overlaps the Player tag → enemy acts first.
    ///   Advantaged — PlayerOverworldAttack calls TriggerAdvantagedBattle() → player acts first.
    ///
    /// Sets GameManager.PendingBattle then loads the Battle scene.
    /// Requires a Collider2D on this GameObject with Is Trigger enabled for the Surprised path.
    /// Requires the player GameObject to have the "Player" tag.
    /// </summary>
    public class OverworldEnemyCombatTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("EnemyData ScriptableObject for this enemy. Passed to BattleController at battle load.")]
        private EnemyData _enemyData;

        // Prevents double-trigger if Advantaged and Surprised fire in the same frame.
        private bool _triggered;

        /// <summary>
        /// Called by PlayerOverworldAttack when the player attacks this enemy first.
        /// Produces CombatStartState.Advantaged — player takes the first turn.
        /// No-op if a battle trigger is already in progress.
        /// </summary>
        public void TriggerAdvantagedBattle()
        {
            if (_triggered) return;
            TriggerBattle(CombatStartState.Advantaged);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;
            TriggerBattle(CombatStartState.Surprised);
        }

        private void TriggerBattle(CombatStartState startState)
        {
            _triggered = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData));
            }
            else
            {
                Debug.LogWarning(
                    "[OverworldEnemyCombatTrigger] GameManager not found — Battle scene will " +
                    "use BattleController Inspector fallback values.",
                    this);
            }

            SceneManager.LoadScene("Battle");
        }
    }
}
```

- [ ] **Step 2: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): add OverworldEnemyCombatTrigger MonoBehaviour`
  - `Assets/Scripts/Platformer/OverworldEnemyCombatTrigger.cs`
  - `Assets/Scripts/Platformer/OverworldEnemyCombatTrigger.cs.meta`

---

## Task 7: Implement PlayerOverworldAttack

**Files:**
- Create: `Assets/Scripts/Platformer/PlayerOverworldAttack.cs`

This MonoBehaviour lives on the Player. When the Attack input action is pressed, it does an overlap circle check for any nearby enemy with an `OverworldEnemyCombatTrigger`. If one is found, it calls `TriggerAdvantagedBattle()`.

The attack range is drawn as a Gizmo in the Scene view when the player is selected.

> **Prerequisite:** The Unity Input Actions asset must have an "Attack" action defined in the "Player" action map before this script will compile without errors (see Task 8, Step 1).

- [ ] **Step 1: Implement PlayerOverworldAttack**

Create `Assets/Scripts/Platformer/PlayerOverworldAttack.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to the Player. Reads the "Player/Attack" input action.
    /// When Attack is pressed and an OverworldEnemyCombatTrigger is within range,
    /// calls TriggerAdvantagedBattle() — the player acts first in the resulting battle.
    ///
    /// Requires:
    ///   - "Attack" action in the "Player" action map of the project's Input Actions asset.
    ///   - _enemyLayer set to the layer used by overworld enemy GameObjects.
    ///   - OverworldEnemyCombatTrigger component present on enemy GameObjects.
    /// </summary>
    public class PlayerOverworldAttack : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Radius around the player's position to search for attackable enemies.")]
        private float _attackRange = 1.5f;

        [SerializeField]
        [Tooltip("Layer mask for overworld enemy GameObjects. Set to your enemy layer in the Inspector.")]
        private LayerMask _enemyLayer;

        private InputSystem_Actions _actions;
        private InputAction _attackAction;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            _attackAction = _actions.Player.Attack;
        }

        private void OnEnable()  => _attackAction.Enable();
        private void OnDisable() => _attackAction.Disable();

        private void Update()
        {
            if (!_attackAction.WasPerformedThisFrame()) return;

            Collider2D hit = Physics2D.OverlapCircle(transform.position, _attackRange, _enemyLayer);
            if (hit == null) return;

            var trigger = hit.GetComponent<OverworldEnemyCombatTrigger>();
            trigger?.TriggerAdvantagedBattle();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}
```

- [ ] **Step 2: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): add PlayerOverworldAttack MonoBehaviour`
  - `Assets/Scripts/Platformer/PlayerOverworldAttack.cs`
  - `Assets/Scripts/Platformer/PlayerOverworldAttack.cs.meta`

---

## Task 8: Unity Editor Setup

> All steps in this task are **Unity Editor tasks performed by the user**. Claude writes no code here.

### Step 1: Add "Attack" action to Input Actions asset

> **Unity Editor task (user):** Open `Assets/InputSystem_Actions.inputactions` (double-click → Input Actions editor opens). In the Action Maps panel on the left, select the **Player** action map. In the Actions panel, click the `+` button to add a new action. Name it **Attack**. Set Action Type to **Button**. Under Bindings, click `+` → Add Binding. Assign the key you want for the overworld attack (e.g. `Z` on keyboard, or a controller button). Click **Save Asset** (top-right of the Input Actions editor). Unity will regenerate `InputSystem_Actions.cs`.

### Step 2: Add GameManager to the Platformer scene

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity`. In the Hierarchy, check if a **GameManager** GameObject exists (it should have the `GameManager` MonoBehaviour). If it does not exist:
> - Create → Create Empty → rename it `GameManager`
> - Add Component → `GameManager`
> - The `DontDestroyOnLoad` call in `Awake()` will keep it alive across the scene transition to Battle.

### Step 3: Configure a test enemy for the Surprised path

> **Unity Editor task (user):** Open `Assets/Prefabs/Enemies/Enemy.prefab` in Prefab Mode (double-click). Proceed with steps 1–4 below inside Prefab Mode, then click **Save** to apply to all instances.
> 1. Add Component → **OverworldEnemyCombatTrigger**
> 2. In the Inspector, assign an **EnemyData** ScriptableObject to the `_enemyData` field. If none exist yet, right-click `Assets/Data/Enemies/` → Create → Axiom → Data → Enemy Data, fill in stats (e.g. Name: "Void Wraith", MaxHP: 60, ATK: 8, DEF: 4, SPD: 5), and assign it.
> 3. **Add a second Collider2D** for the Surprised trigger — do **not** modify the existing physics collider (that one keeps `Is Trigger` unchecked so the enemy can stand on the ground). Click **Add Component → Box Collider 2D** (or Capsule Collider 2D), check **Is Trigger**, and size it to match the enemy's visible body. This second collider is the one that fires `OnTriggerEnter2D`.
> 4. Create an "Enemy" layer if it does not yet exist: Edit → Project Settings → Tags and Layers → add `Enemy` under User Layers. Set the enemy GameObject's **Layer** to `Enemy`. _(Note: DEV-32 creates a "Player" layer for the player — "Enemy" is a separate layer needed here for `PlayerOverworldAttack`'s overlap scan.)_

### Step 4: Configure the player for the Advantaged path

> **Unity Editor task (user):** In the Platformer scene Hierarchy, select the **Player** GameObject:
>
> 1. Add Component → **PlayerOverworldAttack**
> 2. Set `_attackRange` to `1.5` (or adjust to taste — the red Gizmo circle in the Scene view shows the reach).
> 3. Set `_enemyLayer` to the **"Enemy"** layer created in Step 3. _(This is distinct from the "Player" layer DEV-32 creates for `EnemyController.playerLayer`.)_
> 4. Confirm the Player GameObject has the tag **Player** (top of the Inspector → Tag dropdown → Player). _(DEV-32's `EnemyController` uses this tag for aggro detection — it is already set on the Player.)_

### Step 5: Verify the "Battle" scene is in Build Settings

> **Unity Editor task (user):** File → Build Settings. Confirm both **Platformer** and **Battle** scenes are listed and have their checkboxes ticked. If Battle is missing, click **Add Open Scenes** while the Battle scene is open, or drag `Assets/Scenes/Battle.unity` into the list.

### Step 6: Check in via UVCS

> **Unity Editor task (user):** Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-33): configure Platformer scene with GameManager, test enemy trigger, and player attack component`
> - `Assets/Scenes/Platformer.unity`
> - `Assets/InputSystem_Actions.inputactions`
> - `Assets/InputSystem_Actions.inputactions.meta` _(if new)_
> - Any new `.asset` files created for EnemyData

---

## Task 9: End-to-End Integration Verification

Verify both trigger paths work in the Unity Editor before closing the ticket.

- [ ] **Step 1: Test the Surprised path**

  - Open `Assets/Scenes/Platformer.unity` and enter **Play Mode**.
  - Walk the player character into the test enemy's trigger collider.
  - Expected: the Battle scene loads and the enemy takes the first turn (enemy attacks before the player can act — `CombatStartState.Surprised`).
  - Also verify: the correct enemy name and stats appear in the Battle HUD (confirming `EnemyData` was passed through `BattleEntry`).

- [ ] **Step 2: Test the Advantaged path**

  - Return to the Platformer scene in Play Mode (or re-enter Play Mode).
  - Position the player within `_attackRange` of the test enemy (the red Gizmo circle in Scene view shows the range).
  - Press the Attack key configured in Step 1 of Task 8.
  - Expected: the Battle scene loads and the player takes the first turn (player can act immediately — `CombatStartState.Advantaged`).

- [ ] **Step 3: Verify standalone Battle scene still works**

  - Open `Assets/Scenes/Battle.unity` directly and enter Play Mode (no Platformer → no GameManager → `PendingBattle` is null).
  - Expected: battle starts normally using the Inspector-configured `_startState` and `_enemyData` on `BattleController`. No NullReferenceException.

- [ ] **Step 4: Run all Edit Mode tests — confirm no regressions**

  Unity Editor → Test Runner → Edit Mode → Run All

  Expected: all tests pass.

- [ ] **Step 5: Final check in via UVCS**

  If any minor adjustments were made during verification (collider sizing, attack range tuning, etc.):

  Unity Version Control → Pending Changes → stage modified files → Check in with message: `chore(DEV-33): integration verification adjustments`

---

## Self-Review: Spec Coverage

| Acceptance Criterion | Covered by |
|---------------------|-----------|
| Advantaged path: player strikes enemy → `CombatStartState.Advantaged` → player first | Task 7 (`PlayerOverworldAttack`), Task 6 (`TriggerAdvantagedBattle`), Task 5 (`BattleController.Start`) |
| Surprised path: enemy contacts player → `CombatStartState.Surprised` → enemy first | Task 6 (`OnTriggerEnter2D`) |
| `CombatStartState` passed cleanly to `BattleManager` at battle load | Task 5 (BattleController consumes PendingBattle, calls `Initialize(_startState)` which calls `BattleManager.StartBattle`) |
| Both paths reliably and consistently triggered | `_triggered` guard in Task 6; no double-trigger |
| Correct enemy data passed to the Battle scene on engagement | `BattleEntry.EnemyData` → consumed in Task 5; `_enemyData` field on `BattleController` overridden before `Initialize()` |