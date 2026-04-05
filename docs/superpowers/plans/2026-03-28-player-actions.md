# DEV-13: Player Actions — Attack, Spell (Placeholder), Item (Placeholder), Flee

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the four player actions (Attack, Spell placeholder, Item placeholder, Flee) that fire from the Battle UI during `PlayerTurn`, wiring into the existing `BattleManager` state machine.

**Architecture:** `CharacterStats` (plain C#, serializable) holds per-character stats and mutable HP/MP; `PlayerActionHandler` (plain C#) executes each action against those stats; `BattleController` (MonoBehaviour) wires UI buttons to the handler and forwards results to `BattleManager`. A new `Fled` terminal state is added to `BattleState` and `BattleManager` to handle the Flee action cleanly.

**Tech Stack:** Unity 6 LTS · URP 2D · C# · NUnit (Unity Test Framework Edit Mode) · Unity Version Control (UVCS)

---

## File Map

| File | Status | Responsibility |
|------|--------|---------------|
| `Assets/Scripts/Battle/CharacterStats.cs` | **Create** | Serializable plain C# stats container (MaxHP, MaxMP, ATK, DEF, SPD, CurrentHP, CurrentMP); `TakeDamage()`, `Initialize()`, `IsDefeated` |
| `Assets/Scripts/Battle/BattleState.cs` | **Modify** | Add `Fled` terminal state |
| `Assets/Scripts/Battle/BattleManager.cs` | **Modify** | Add `OnPlayerFled()` method → transitions to `Fled`; guard: no-op outside `PlayerTurn` |
| `Assets/Scripts/Battle/PlayerActionHandler.cs` | **Create** | Plain C# class: `ExecuteAttack()` (ATK vs DEF formula, returns `bool` defeated), `ExecuteSpell()` (returns placeholder string), `ExecuteItem()` (returns placeholder string), `ExecuteFlee()` (intentionally empty) |
| `Assets/Scripts/Battle/BattleController.cs` | **Modify** | Remove all `Simulate*` methods; add `[SerializeField] CharacterStats` fields; add `PlayerAttack()`, `PlayerSpell()`, `PlayerItem()`, `PlayerFlee()` public methods; handle `Fled` state → `SceneManager.LoadScene("SampleScene")` as Phase 4 placeholder |
| `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | **Create** | Edit Mode tests: initialize, TakeDamage, IsDefeated |
| `Assets/Tests/Editor/Battle/BattleManagerTests.cs` | **Modify** | Add tests for `OnPlayerFled()` transitions |
| `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs` | **Create** | Edit Mode tests: damage formula, min-1-damage, defeated flag, placeholder returns |

**No new `.asmdef` files needed.** All new scripts fall under the existing `Axiom.Battle` asmdef; all new tests fall under the existing `BattleTests` asmdef.

---

## Task 1: `CharacterStats` — Stats Container

**Files:**
- Create: `Assets/Scripts/Battle/CharacterStats.cs`
- Create: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;

public class CharacterStatsTests
{
    private static CharacterStats MakeStats(int maxHp = 100, int maxMp = 30,
                                            int atk = 10, int def = 5, int spd = 8)
        => new CharacterStats { MaxHP = maxHp, MaxMP = maxMp, ATK = atk, DEF = def, SPD = spd };

    // ---- Initialize ----

    [Test]
    public void Initialize_SetsCurrentHPToMaxHP()
    {
        var stats = MakeStats(maxHp: 80);
        stats.Initialize();
        Assert.AreEqual(80, stats.CurrentHP);
    }

    [Test]
    public void Initialize_SetsCurrentMPToMaxMP()
    {
        var stats = MakeStats(maxMp: 40);
        stats.Initialize();
        Assert.AreEqual(40, stats.CurrentMP);
    }

    // ---- TakeDamage ----

    [Test]
    public void TakeDamage_ReducesCurrentHP_ByAmount()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(30);
        Assert.AreEqual(70, stats.CurrentHP);
    }

    [Test]
    public void TakeDamage_ClampsToZero_WhenOverkill()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(9999);
        Assert.AreEqual(0, stats.CurrentHP);
    }

    [Test]
    public void TakeDamage_ZeroDamage_LeavesHPUnchanged()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(0);
        Assert.AreEqual(100, stats.CurrentHP);
    }

    // ---- IsDefeated ----

    [Test]
    public void IsDefeated_ReturnsFalse_WhenHPAboveZero()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        Assert.IsFalse(stats.IsDefeated);
    }

    [Test]
    public void IsDefeated_ReturnsTrue_WhenHPIsZero()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(100);
        Assert.IsTrue(stats.IsDefeated);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Open Unity Editor → Window → General → Test Runner → Edit Mode tab → select `CharacterStatsTests` → Run Selected.

Expected: all 7 tests fail with "The type or namespace name 'CharacterStats' could not be found."

- [ ] **Step 3: Implement `CharacterStats`**

Create `Assets/Scripts/Battle/CharacterStats.cs`:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Serializable plain C# class holding a character's base stats and runtime HP/MP.
    /// No MonoBehaviour — attach as a SerializeField on BattleController to set values in the Inspector.
    /// Call Initialize() before battle begins to reset CurrentHP/CurrentMP to their maximums.
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public int MaxHP;
        public int MaxMP;
        public int ATK;
        public int DEF;
        public int SPD;

        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public bool IsDefeated => CurrentHP <= 0;

        /// <summary>Resets CurrentHP and CurrentMP to their maximum values. Call once per battle start.</summary>
        public void Initialize()
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Reduces CurrentHP by <paramref name="amount"/>, clamped to zero.</summary>
        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

Unity Editor → Test Runner → Edit Mode → select `CharacterStatsTests` → Run Selected.

Expected: all 7 tests pass (green).

- [ ] **Step 5: Check in via Unity Version Control**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/CharacterStats.cs`
- `Assets/Scripts/Battle/CharacterStats.cs.meta`
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs.meta`

Check in with message: `feat: add CharacterStats plain C# class with TakeDamage and IsDefeated`

---

## Task 2: Add `Fled` Terminal State to `BattleState` and `BattleManager`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleState.cs`
- Modify: `Assets/Scripts/Battle/BattleManager.cs`
- Modify: `Assets/Tests/Editor/Battle/BattleManagerTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these two tests to the bottom of `Assets/Tests/Editor/Battle/BattleManagerTests.cs` (inside the class, before the closing `}`):

```csharp
    // ---- Flee ----

    [Test]
    public void OnPlayerFled_DuringPlayerTurn_TransitionsToFled()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged); // PlayerTurn
        manager.OnPlayerFled();
        Assert.AreEqual(BattleState.Fled, manager.CurrentState);
    }

    [Test]
    public void OnPlayerFled_DuringEnemyTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised); // EnemyTurn
        manager.OnPlayerFled();
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }
```

- [ ] **Step 2: Run tests to confirm they fail**

Unity Editor → Test Runner → Edit Mode → select the two new `OnPlayerFled` tests → Run Selected.

Expected: both fail — `Fled` does not exist in `BattleState`, `OnPlayerFled` does not exist on `BattleManager`.

- [ ] **Step 3: Add `Fled` to `BattleState`**

Replace the entire content of `Assets/Scripts/Battle/BattleState.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// All discrete states the battle can be in at any moment.
    /// Victory, Defeat, and Fled are terminal states — no further transitions occur.
    /// </summary>
    public enum BattleState
    {
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat,
        Fled
    }
}
```

- [ ] **Step 4: Add `OnPlayerFled()` to `BattleManager`**

In `Assets/Scripts/Battle/BattleManager.cs`, add the following method after `OnPlayerActionComplete` (before `OnEnemyActionComplete`):

```csharp
        /// <summary>
        /// Call when the player chooses to flee.
        /// Transitions to Fled. No-op if called outside PlayerTurn.
        /// </summary>
        public void OnPlayerFled()
        {
            if (CurrentState != BattleState.PlayerTurn) return;
            TransitionTo(BattleState.Fled);
        }
```

- [ ] **Step 5: Run tests to confirm they pass**

Unity Editor → Test Runner → Edit Mode → Run All (to confirm both new tests pass and no existing tests regressed).

Expected: all 12 tests in `BattleManagerTests` pass (green).

- [ ] **Step 6: Check in via Unity Version Control**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleState.cs`
- `Assets/Scripts/Battle/BattleManager.cs`
- `Assets/Tests/Editor/Battle/BattleManagerTests.cs`

Check in with message: `feat: add Fled terminal state and OnPlayerFled to BattleManager`

---

## Task 3: `PlayerActionHandler` — Player Action Logic

**Files:**
- Create: `Assets/Scripts/Battle/PlayerActionHandler.cs`
- Create: `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;

public class PlayerActionHandlerTests
{
    // Helper: creates initialized stats with given values.
    private static CharacterStats MakeStats(int maxHp, int atk = 0, int def = 0)
    {
        var s = new CharacterStats { MaxHP = maxHp, MaxMP = 0, ATK = atk, DEF = def, SPD = 0 };
        s.Initialize();
        return s;
    }

    // ---- Attack: damage formula ----

    [Test]
    public void ExecuteAttack_DealsDamage_UsingATKMinusDEF()
    {
        // player ATK=15, enemy DEF=5 → 10 damage; 50 - 10 = 40 HP remaining
        var player = MakeStats(maxHp: 100, atk: 15, def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy);

        handler.ExecuteAttack();

        Assert.AreEqual(40, enemy.CurrentHP);
    }

    [Test]
    public void ExecuteAttack_DealsMinimumOneDamage_WhenATKLessOrEqualDEF()
    {
        // player ATK=3 <= enemy DEF=10 → clamped to 1 damage; 50 - 1 = 49 HP remaining
        var player = MakeStats(maxHp: 100, atk: 3,  def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 10);
        var handler = new PlayerActionHandler(player, enemy);

        handler.ExecuteAttack();

        Assert.AreEqual(49, enemy.CurrentHP);
    }

    // ---- Attack: return value ----

    [Test]
    public void ExecuteAttack_ReturnsTrue_WhenEnemyDefeated()
    {
        // 1-shot kill: ATK=100, DEF=0, HP=10
        var player = MakeStats(maxHp: 100, atk: 100, def: 0);
        var enemy  = MakeStats(maxHp: 10,  atk: 0,   def: 0);
        var handler = new PlayerActionHandler(player, enemy);

        bool defeated = handler.ExecuteAttack();

        Assert.IsTrue(defeated);
    }

    [Test]
    public void ExecuteAttack_ReturnsFalse_WhenEnemyAlive()
    {
        // Low ATK vs high HP enemy — survives
        var player = MakeStats(maxHp: 100, atk: 10, def: 0);
        var enemy  = MakeStats(maxHp: 100, atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy);

        bool defeated = handler.ExecuteAttack();

        Assert.IsFalse(defeated);
    }

    // ---- Attack: does not affect player HP ----

    [Test]
    public void ExecuteAttack_DoesNotChangePlayerHP()
    {
        var player = MakeStats(maxHp: 100, atk: 10, def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy);

        handler.ExecuteAttack();

        Assert.AreEqual(100, player.CurrentHP);
    }

    // ---- Spell placeholder ----

    [Test]
    public void ExecuteSpell_ReturnsPlaceholderMessage()
    {
        var handler = new PlayerActionHandler(MakeStats(100), MakeStats(50));
        Assert.AreEqual("No spells yet.", handler.ExecuteSpell());
    }

    // ---- Item placeholder ----

    [Test]
    public void ExecuteItem_ReturnsPlaceholderMessage()
    {
        var handler = new PlayerActionHandler(MakeStats(100), MakeStats(50));
        Assert.AreEqual("No items.", handler.ExecuteItem());
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Unity Editor → Test Runner → Edit Mode → select `PlayerActionHandlerTests` → Run Selected.

Expected: all 7 tests fail with "The type or namespace name 'PlayerActionHandler' could not be found."

- [ ] **Step 3: Implement `PlayerActionHandler`**

Create `Assets/Scripts/Battle/PlayerActionHandler.cs`:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes the four player actions during their turn.
    /// Reads stats from CharacterStats instances injected at construction time.
    /// No MonoBehaviour — BattleController creates and holds this.
    /// </summary>
    public class PlayerActionHandler
    {
        private readonly CharacterStats _playerStats;
        private readonly CharacterStats _enemyStats;

        public PlayerActionHandler(CharacterStats playerStats, CharacterStats enemyStats)
        {
            _playerStats = playerStats;
            _enemyStats  = enemyStats;
        }

        /// <summary>
        /// Deals damage to the enemy using the formula: damage = max(1, playerATK - enemyDEF).
        /// Returns true if the enemy is defeated (CurrentHP == 0).
        /// </summary>
        public bool ExecuteAttack()
        {
            int damage = Math.Max(1, _playerStats.ATK - _enemyStats.DEF);
            _enemyStats.TakeDamage(damage);
            return _enemyStats.IsDefeated;
        }

        /// <summary>
        /// Placeholder for Phase 3 voice spell system.
        /// Returns a message for the Battle UI to display.
        /// </summary>
        public string ExecuteSpell() => "No spells yet.";

        /// <summary>
        /// Placeholder for Phase 5 inventory system.
        /// Returns a message for the Battle UI to display.
        /// </summary>
        public string ExecuteItem() => "No items.";

        /// <summary>
        /// No combat logic for Flee — BattleManager transitions to Fled state.
        /// Intentionally empty. BattleController calls BattleManager.OnPlayerFled() directly.
        /// </summary>
        public void ExecuteFlee() { }
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

Unity Editor → Test Runner → Edit Mode → Run All.

Expected: all `PlayerActionHandlerTests` pass (7 green) and all other Battle tests remain green.

- [ ] **Step 5: Check in via Unity Version Control**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/PlayerActionHandler.cs`
- `Assets/Scripts/Battle/PlayerActionHandler.cs.meta`
- `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs`
- `Assets/Tests/Editor/Battle/PlayerActionHandlerTests.cs.meta`

Check in with message: `feat: add PlayerActionHandler with Attack formula and placeholder Spell/Item`

---

## Task 4: Wire `BattleController` to Real Player Actions

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

> **Note:** `BattleController` is a MonoBehaviour — its wiring is tested manually (Play Mode) and via the existing Edit Mode state machine tests on `BattleManager`. No new automated tests are written for the controller itself.

- [ ] **Step 1: Rewrite `BattleController`**

Replace the entire content of `Assets/Scripts/Battle/BattleController.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour wrapper for BattleManager and PlayerActionHandler.
    /// Handles Unity lifecycle only (Start, OnDestroy).
    /// Exposes four public methods for UI Buttons: PlayerAttack, PlayerSpell, PlayerItem, PlayerFlee.
    ///
    /// In Phase 4, Initialize() will be called by GameManager on scene load
    /// with the CombatStartState determined by the overworld engagement.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Used for standalone Battle scene testing only. Phase 4 will override via GameManager.")]
        private CombatStartState _startState = CombatStartState.Advantaged;

        [SerializeField]
        [Tooltip("Player stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _playerStats = new CharacterStats
            { MaxHP = 100, MaxMP = 30, ATK = 12, DEF = 6, SPD = 8 };

        [SerializeField]
        [Tooltip("Enemy stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _enemyStats = new CharacterStats
            { MaxHP = 60, MaxMP = 0, ATK = 8, DEF = 4, SPD = 5 };

        private BattleManager _battleManager;
        private PlayerActionHandler _actionHandler;

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
            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            _playerStats.Initialize();
            _enemyStats.Initialize();

            _actionHandler = new PlayerActionHandler(_playerStats, _enemyStats);
            _battleManager = new BattleManager();
            _battleManager.OnStateChanged += HandleStateChanged;
            _battleManager.StartBattle(startState);
        }

        // ---- Player action methods — wire these to UI Buttons in the Battle scene ----

        /// <summary>Executes the Attack action. No-op outside PlayerTurn.</summary>
        public void PlayerAttack()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            bool enemyDefeated = _actionHandler.ExecuteAttack();
            _battleManager.OnPlayerActionComplete(enemyDefeated);
        }

        /// <summary>Executes the Spell placeholder action. No-op outside PlayerTurn.</summary>
        public void PlayerSpell()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            string message = _actionHandler.ExecuteSpell();
            Debug.Log($"[Battle] Spell: {message}");
            _battleManager.OnPlayerActionComplete(enemyDefeated: false);
        }

        /// <summary>Executes the Item placeholder action. No-op outside PlayerTurn.</summary>
        public void PlayerItem()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            string message = _actionHandler.ExecuteItem();
            Debug.Log($"[Battle] Item: {message}");
            _battleManager.OnPlayerActionComplete(enemyDefeated: false);
        }

        /// <summary>Executes Flee. Transitions BattleManager to Fled. No-op outside PlayerTurn.</summary>
        public void PlayerFlee()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            _battleManager.OnPlayerFled();
        }

        private void HandleStateChanged(BattleState state)
        {
            Debug.Log($"[Battle] → {state}");

            if (state == BattleState.Fled)
            {
                // Phase 4: GameManager will handle the scene transition.
                // Placeholder: return to Platformer scene.
                SceneManager.LoadScene("Platformer");
            }
        }

        private void OnDestroy()
        {
            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;
        }
    }
}
```

- [ ] **Step 2: Verify the project compiles**

Unity Editor → check the Console window for any compile errors after Unity reimports the file.

Expected: Console shows no errors. All scripts compile cleanly.

- [ ] **Step 3: Check in via Unity Version Control**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleController.cs`

Check in with message: `feat: wire BattleController to real player actions — Attack, Spell placeholder, Item placeholder, Flee`

- [ ] **Step 4: Run all Edit Mode tests to confirm no regressions**

Unity Editor → Test Runner → Edit Mode → Run All.

Expected: all tests pass (green). Confirm `BattleManagerTests`, `CharacterStatsTests`, and `PlayerActionHandlerTests` all green.

---

## Task 5: Unity Editor Wiring — Connect UI Buttons to `BattleController`

> **Unity Editor task (user):** All steps in this task are performed manually in the Unity Editor, not in code.

- [ ] **Step 1: Open the Battle scene**

File → Open Scene → `Assets/Scenes/Battle.unity` (or the scene currently used for Phase 2 battle testing).

- [ ] **Step 2: Confirm `BattleController` has the new fields**

Select the GameObject that holds `BattleController` in the Hierarchy. In the Inspector, confirm you can see:
- `Start State` (dropdown: Advantaged / Surprised)
- `Player Stats` (foldout with MaxHP, MaxMP, ATK, DEF, SPD)
- `Enemy Stats` (foldout with MaxHP, MaxMP, ATK, DEF, SPD)

Set test values, for example:
- Player: MaxHP=100, MaxMP=30, ATK=12, DEF=6, SPD=8
- Enemy: MaxHP=60, MaxMP=0, ATK=8, DEF=4, SPD=5

- [ ] **Step 3: Wire the four action buttons**

For each Button in the Battle UI Canvas that was previously wired to a `Simulate*` method:

1. Select the Button in the Hierarchy
2. In the Inspector → `On Click ()` section → click the `+` to add an entry (or reconfigure the existing entry)
3. Drag the `BattleController` GameObject into the object slot
4. Set the function dropdown to the correct method:

| Button label | Function to select |
|---|---|
| Attack | `BattleController.PlayerAttack` |
| Spell  | `BattleController.PlayerSpell`  |
| Item   | `BattleController.PlayerItem`   |
| Flee   | `BattleController.PlayerFlee`   |

- [ ] **Step 4: Play Mode smoke test**

Press Play. Verify:

| Action | Expected Console output | Expected state |
|--------|------------------------|----------------|
| Click **Attack** on PlayerTurn | `[Battle] → EnemyTurn` (enemy survives) or `[Battle] → Victory` (enemy defeated) | Turn passes or Victory |
| Click **Spell** on PlayerTurn | `[Battle] Spell: No spells yet.` then `[Battle] → EnemyTurn` | Turn passes |
| Click **Item** on PlayerTurn | `[Battle] Item: No items.` then `[Battle] → EnemyTurn` | Turn passes |
| Click **Flee** on PlayerTurn | `[Battle] → Fled` then scene reloads | Returns to Platformer scene |
| Click any action button on EnemyTurn | No log, no state change | No-op |

- [ ] **Step 5: Save the scene and check in**

File → Save (Ctrl+S / Cmd+S) to save the Battle scene.

Unity Version Control → Pending Changes → stage:
- `Assets/Scenes/Battle.unity`

Check in with message: `chore: wire Battle scene UI buttons to BattleController player actions`

---

## Acceptance Criteria Verification

| AC item | Covered by |
|---------|-----------|
| Attack deals damage using player ATK vs enemy DEF formula | `PlayerActionHandler.ExecuteAttack()`, `PlayerActionHandlerTests` |
| Spell shows placeholder "No spells yet" | `PlayerActionHandler.ExecuteSpell()`, `PlayerActionHandlerTests` |
| Item shows placeholder "No items" | `PlayerActionHandler.ExecuteItem()`, `PlayerActionHandlerTests` |
| Flee exits battle and returns to platformer (or placeholder) | `BattleManager.OnPlayerFled()` + `SceneManager.LoadScene` in `BattleController` |
| Each action selectable from Battle UI action menu | Task 5 editor wiring |
| After action resolves, turn passes to enemy (or Victory/Defeat) | `BattleManager.OnPlayerActionComplete()` — existing logic unchanged |
| No hardcoded damage values — stats come from `CharacterStats` | `CharacterStats` injected into `PlayerActionHandler` via constructor |
