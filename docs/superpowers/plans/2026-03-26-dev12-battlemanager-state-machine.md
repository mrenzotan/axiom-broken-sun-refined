# DEV-12: BattleManager — Turn-order State Machine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `BattleManager` as a pure C# state machine driving turn-based combat (PlayerTurn → EnemyTurn → Victory/Defeat), integrated into a Battle scene with placeholder actions.

**Architecture:** `BattleManager` is a plain C# class (no Unity API) with a deterministic state machine and `System.Action` events for decoupled state-change notifications. A thin `BattleController` MonoBehaviour wraps it for Unity lifecycle and routes events to Debug.Log placeholders. All state transitions are covered by Edit Mode unit tests before any Unity scene work.

**Tech Stack:** C# plain class, `System.Action` events, Unity Test Framework (Edit Mode / NUnit), Unity 6 LTS, URP 2D.

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Assets/Scripts/Battle/Battle.asmdef` | Create | Assembly definition `Axiom.Battle` for all Battle runtime scripts |
| `Assets/Scripts/Battle/CombatStartState.cs` | Create | Enum: `Advantaged`, `Surprised` — who goes first |
| `Assets/Scripts/Battle/BattleState.cs` | Create | Enum: `PlayerTurn`, `EnemyTurn`, `Victory`, `Defeat` |
| `Assets/Scripts/Battle/BattleManager.cs` | Create | Pure C# state machine — all transition logic, no UnityEngine calls |
| `Assets/Scripts/Battle/BattleController.cs` | Create | MonoBehaviour wrapper — lifecycle, event routing, placeholder actions |
| `Assets/Tests/Editor/Battle/BattleTests.asmdef` | Create | Test assembly definition referencing `Axiom.Battle` |
| `Assets/Tests/Editor/Battle/BattleManagerTests.cs` | Create | Edit Mode unit tests for all state transitions and events |

---

## Task 1: Create Battle Assembly Definition and Enum Files

**Files:**
- Create: `Assets/Scripts/Battle/Battle.asmdef`
- Create: `Assets/Scripts/Battle/CombatStartState.cs`
- Create: `Assets/Scripts/Battle/BattleState.cs`

- [ ] **Step 1.1: Create the Battle assembly definition**

Create `Assets/Scripts/Battle/Battle.asmdef`:

```json
{
    "name": "Axiom.Battle",
    "references": [],
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

- [ ] **Step 1.2: Create CombatStartState enum**

Create `Assets/Scripts/Battle/CombatStartState.cs`:

```csharp
namespace Axiom.Battle
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

- [ ] **Step 1.3: Create BattleState enum**

Create `Assets/Scripts/Battle/BattleState.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// All discrete states the battle can be in at any moment.
    /// </summary>
    public enum BattleState
    {
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }
}
```

- [ ] **Step 1.4: Verify Unity compiles cleanly**

In Unity Editor, check the Console window (Window → General → Console). Expect: **no errors**. If a red error appears, re-check the asmdef JSON for syntax mistakes (missing quotes, trailing commas).

---

## Task 2: Set Up Edit Mode Test Assembly

**Files:**
- Create: `Assets/Tests/Editor/Battle/BattleTests.asmdef`

- [ ] **Step 2.1: Create the test assembly definition**

Create `Assets/Tests/Editor/Battle/BattleTests.asmdef`:

```json
{
    "name": "BattleTests",
    "references": [
        "Axiom.Battle"
    ],
    "optionalUnityReferences": [
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

- [ ] **Step 2.2: Verify test assembly is recognized**

In Unity Editor: Window → General → Test Runner → switch to **EditMode** tab. Expect: `BattleTests` assembly appears in the list (may be empty of tests, but assembly itself should be visible once tests are added).

---

## Task 3: Write All Failing BattleManager Tests

**Files:**
- Create: `Assets/Tests/Editor/Battle/BattleManagerTests.cs`

- [ ] **Step 3.1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/BattleManagerTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;

public class BattleManagerTests
{
    // ---- CombatStartState routing ----

    [Test]
    public void StartBattle_Advantaged_SetsPlayerTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    [Test]
    public void StartBattle_Surprised_SetsEnemyTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    // ---- PlayerTurn transitions ----

    [Test]
    public void OnPlayerActionComplete_EnemyAlive_TransitionsToEnemyTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        manager.OnPlayerActionComplete(enemyDefeated: false);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    [Test]
    public void OnPlayerActionComplete_EnemyDefeated_TransitionsToVictory()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        manager.OnPlayerActionComplete(enemyDefeated: true);
        Assert.AreEqual(BattleState.Victory, manager.CurrentState);
    }

    // ---- EnemyTurn transitions ----

    [Test]
    public void OnEnemyActionComplete_PlayerAlive_TransitionsToPlayerTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        manager.OnEnemyActionComplete(playerDefeated: false);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    [Test]
    public void OnEnemyActionComplete_PlayerDefeated_TransitionsToDefeat()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        manager.OnEnemyActionComplete(playerDefeated: true);
        Assert.AreEqual(BattleState.Defeat, manager.CurrentState);
    }

    // ---- Guard clauses: wrong-state calls are no-ops ----

    [Test]
    public void OnPlayerAction_DuringEnemyTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised); // starts at EnemyTurn
        manager.OnPlayerActionComplete(enemyDefeated: false);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    [Test]
    public void OnEnemyAction_DuringPlayerTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged); // starts at PlayerTurn
        manager.OnEnemyActionComplete(playerDefeated: false);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    // ---- OnStateChanged event ----

    [Test]
    public void OnStateChanged_FiresWithNewState_OnStartBattle()
    {
        var manager = new BattleManager();
        BattleState? captured = null;
        manager.OnStateChanged += s => captured = s;

        manager.StartBattle(CombatStartState.Advantaged);

        Assert.AreEqual(BattleState.PlayerTurn, captured);
    }

    [Test]
    public void OnStateChanged_FiresWithNewState_OnTransition()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        BattleState? captured = null;
        manager.OnStateChanged += s => captured = s;

        manager.OnPlayerActionComplete(enemyDefeated: false);

        Assert.AreEqual(BattleState.EnemyTurn, captured);
    }
}
```

- [ ] **Step 3.2: Run tests — verify all 10 FAIL**

In Unity Editor: Window → General → Test Runner → **EditMode** tab → click **Run All**.

Expected: All 10 `BattleManagerTests` tests show red (fail) with an error like:
`The type or namespace name 'BattleManager' could not be found`

This confirms the tests are wired up correctly and the implementation doesn't exist yet.

---

## Task 4: Implement BattleManager

**Files:**
- Create: `Assets/Scripts/Battle/BattleManager.cs`

- [ ] **Step 4.1: Implement the minimal BattleManager**

Create `Assets/Scripts/Battle/BattleManager.cs`:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# state machine for turn-based combat.
    /// Contains zero UnityEngine calls — all Unity lifecycle is handled by BattleController.
    /// </summary>
    public class BattleManager
    {
        public BattleState CurrentState { get; private set; }

        /// <summary>Fires every time the state changes, passing the new state.</summary>
        public event Action<BattleState> OnStateChanged;

        /// <summary>
        /// Starts the battle. Advantaged gives the player the first turn;
        /// Surprised gives the enemy the first turn.
        /// </summary>
        public void StartBattle(CombatStartState startState)
        {
            var firstState = startState == CombatStartState.Advantaged
                ? BattleState.PlayerTurn
                : BattleState.EnemyTurn;
            TransitionTo(firstState);
        }

        /// <summary>
        /// Call when the player finishes their action.
        /// Pass enemyDefeated=true to end in Victory; false to continue to EnemyTurn.
        /// No-op if called outside PlayerTurn.
        /// </summary>
        public void OnPlayerActionComplete(bool enemyDefeated)
        {
            if (CurrentState != BattleState.PlayerTurn) return;
            TransitionTo(enemyDefeated ? BattleState.Victory : BattleState.EnemyTurn);
        }

        /// <summary>
        /// Call when the enemy finishes their action.
        /// Pass playerDefeated=true to end in Defeat; false to continue to PlayerTurn.
        /// No-op if called outside EnemyTurn.
        /// </summary>
        public void OnEnemyActionComplete(bool playerDefeated)
        {
            if (CurrentState != BattleState.EnemyTurn) return;
            TransitionTo(playerDefeated ? BattleState.Defeat : BattleState.PlayerTurn);
        }

        private void TransitionTo(BattleState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
```

- [ ] **Step 4.2: Run tests — verify all 10 PASS**

In Unity Editor: Window → General → Test Runner → **EditMode** tab → click **Run All**.

Expected: All 10 `BattleManagerTests` show green. Zero failures.

If any test fails, re-read it against the implementation — the test name tells you exactly which behavior is broken.

- [ ] **Step 4.3: Check in via UVCS**

In Unity Editor: Unity Version Control (top menu or bottom toolbar) → **Pending Changes** → stage:
- `Assets/Scripts/Battle/Battle.asmdef`
- `Assets/Scripts/Battle/Battle.asmdef.meta`
- `Assets/Scripts/Battle/CombatStartState.cs`
- `Assets/Scripts/Battle/CombatStartState.cs.meta`
- `Assets/Scripts/Battle/BattleState.cs`
- `Assets/Scripts/Battle/BattleState.cs.meta`
- `Assets/Scripts/Battle/BattleManager.cs`
- `Assets/Scripts/Battle/BattleManager.cs.meta`
- `Assets/Tests/Editor/Battle/BattleTests.asmdef`
- `Assets/Tests/Editor/Battle/BattleTests.asmdef.meta`
- `Assets/Tests/Editor/Battle/BattleManagerTests.cs`
- `Assets/Tests/Editor/Battle/BattleManagerTests.cs.meta`

Check-in comment: `feat: BattleManager state machine with 10 passing Edit Mode tests`

---

## Task 5: Implement BattleController (MonoBehaviour Wrapper)

**Files:**
- Create: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 5.1: Implement BattleController**

Create `Assets/Scripts/Battle/BattleController.cs`:

```csharp
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour wrapper for BattleManager.
    /// Handles Unity lifecycle (Start, OnDestroy) and provides placeholder
    /// public methods for wiring to UI buttons during end-to-end testing.
    ///
    /// In Phase 4, Initialize() will be called by GameManager on scene load
    /// with the CombatStartState determined by the overworld engagement.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Used for standalone Battle scene testing only. Phase 4 will override this via GameManager.")]
        private CombatStartState _startState = CombatStartState.Advantaged;

        private BattleManager _battleManager;

        private void Start()
        {
            Initialize(_startState);
        }

        /// <summary>
        /// Called by GameManager (Phase 4) to start the battle with the correct start state.
        /// Also called from Start() during isolated Battle scene testing.
        /// </summary>
        public void Initialize(CombatStartState startState)
        {
            _battleManager = new BattleManager();
            _battleManager.OnStateChanged += HandleStateChanged;
            _battleManager.StartBattle(startState);
        }

        // ---- Placeholder action methods — wire to UI Buttons in Battle scene ----
        // These will be replaced by real Player Actions (DEV-13) and Enemy AI (DEV-15).

        /// <summary>Simulates player completing their action without defeating the enemy.</summary>
        public void SimulatePlayerAction()
        {
            _battleManager.OnPlayerActionComplete(enemyDefeated: false);
        }

        /// <summary>Simulates enemy completing their action without defeating the player.</summary>
        public void SimulateEnemyAction()
        {
            _battleManager.OnEnemyActionComplete(playerDefeated: false);
        }

        /// <summary>Simulates player defeating the enemy → Victory.</summary>
        public void SimulatePlayerKillsEnemy()
        {
            _battleManager.OnPlayerActionComplete(enemyDefeated: true);
        }

        /// <summary>Simulates enemy defeating the player → Defeat.</summary>
        public void SimulateEnemyKillsPlayer()
        {
            _battleManager.OnEnemyActionComplete(playerDefeated: true);
        }

        private void HandleStateChanged(BattleState state)
        {
            Debug.Log($"[Battle] → {state}");
            // Phase 2 follow-up tickets will add: UI updates, animation triggers, etc.
        }

        private void OnDestroy()
        {
            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;
        }
    }
}
```

- [ ] **Step 5.2: Verify Unity compiles cleanly**

In Unity Editor Console: expect **no errors**. Common mistake: if `UnityEngine` is not resolving, confirm `noEngineReferences` is `false` in `Battle.asmdef`.

---

## Task 6: Battle Scene Setup (Unity Editor — User task)

> Claude writes code; the user sets up the scene in the Unity Editor.

- [ ] **Step 6.1: Create the Battle scene**

In Unity Editor: File → New Scene → select **Basic (URP)** template → save as `Assets/Scenes/Battle.unity`.

- [ ] **Step 6.2: Add BattleController to the scene**

1. In the Hierarchy: right-click → **Create Empty** → rename it `BattleController`.
2. In the Inspector, click **Add Component** → search `BattleController` → select it.
3. Confirm `Start State` field shows `Advantaged` (the default).

- [ ] **Step 6.3: Add four placeholder UI Buttons**

These buttons let you drive the state machine manually in Play Mode to verify all transitions.

1. Hierarchy → right-click → **UI → Button – TextMeshPro** → rename each:
   - `Btn_PlayerAction`
   - `Btn_EnemyAction`
   - `Btn_PlayerKillsEnemy`
   - `Btn_EnemyKillsPlayer`

2. In each Button's **Inspector → On Click ()**, click `+` → drag the `BattleController` GameObject into the slot → select the matching method:
   - `Btn_PlayerAction` → `BattleController.SimulatePlayerAction`
   - `Btn_EnemyAction` → `BattleController.SimulateEnemyAction`
   - `Btn_PlayerKillsEnemy` → `BattleController.SimulatePlayerKillsEnemy`
   - `Btn_EnemyKillsPlayer` → `BattleController.SimulateEnemyKillsPlayer`

3. Set each Button's label (TextMeshPro child) to match its method name for clarity.

- [ ] **Step 6.4: Add the Battle scene to Build Settings**

File → Build Settings → click **Add Open Scenes** to register `Battle.unity`. This is required for SceneManager to load it by name in Phase 4.

---

## Task 7: End-to-End Play Mode Validation

> Open Console (Window → General → Console) and enable **Clear on Play**.

- [ ] **Step 7.1: Validate Advantaged start — player goes first**

1. Ensure `BattleController` Inspector shows `Start State = Advantaged`.
2. Press **Play**.
3. Expected Console: `[Battle] → PlayerTurn`

- [ ] **Step 7.2: Validate PlayerTurn → EnemyTurn → PlayerTurn loop**

While in Play Mode:
1. Click `Btn_PlayerAction` → Expected: `[Battle] → EnemyTurn`
2. Click `Btn_EnemyAction` → Expected: `[Battle] → PlayerTurn`

- [ ] **Step 7.3: Validate Victory path**

Press **Play** (restart), then:
1. Click `Btn_PlayerKillsEnemy` → Expected: `[Battle] → Victory`
2. Clicking any other button after Victory produces no log (guard clauses hold).

- [ ] **Step 7.4: Validate Surprised start — enemy goes first**

1. Stop Play. In `BattleController` Inspector, set `Start State = Surprised`.
2. Press **Play**.
3. Expected Console: `[Battle] → EnemyTurn`

- [ ] **Step 7.5: Validate Defeat path**

While in Play Mode (Surprised start):
1. Click `Btn_EnemyKillsPlayer` → Expected: `[Battle] → Defeat`
2. Clicking any other button produces no log.

- [ ] **Step 7.6: Check in via UVCS**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleController.cs.meta`
- `Assets/Scenes/Battle.unity`
- `Assets/Scenes/Battle.unity.meta`

Check-in comment: `feat: BattleController wrapper + Battle scene with placeholder actions (DEV-12 complete)`

---

## Self-Review — Spec Coverage Checklist

| Acceptance Criterion | Covered By |
|---------------------|-----------|
| State machine covers `PlayerTurn`, `EnemyTurn`, `Victory`, `Defeat` | `BattleState.cs` + `BattleManager.cs` + 6 transition tests |
| Accepts `CombatStartState` (`Advantaged`, `Surprised`) | `CombatStartState.cs` + `BattleManager.StartBattle()` + 2 routing tests |
| `Advantaged` → player first; `Surprised` → enemy first | Tests 1 & 2 + Task 7 steps 7.1 & 7.4 |
| Exposes events/callbacks for state transitions | `OnStateChanged` event — 2 event tests + `BattleController.HandleStateChanged()` |
| No Unity API inside `BattleManager` | `BattleManager.cs` imports only `System` — no `UnityEngine` |
| All transitions deterministic and unit-testable | 10 Edit Mode tests, pure C# class, no scene dependency |
| Integrated into Battle scene with placeholder actions | Task 6 scene setup + Task 7 Play Mode validation |
