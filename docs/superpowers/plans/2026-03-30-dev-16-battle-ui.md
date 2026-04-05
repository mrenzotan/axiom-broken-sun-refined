# Battle UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Battle scene UI — health bars, 2×2 action menu, turn indicator, floating damage numbers, and status message log — wired to `BattleController` via events, using Unity UI Canvas + TextMeshPro.

**Architecture:** Five single-responsibility MonoBehaviours (`HealthBarUI`, `ActionMenuUI`, `TurnIndicatorUI`, `FloatingNumberSpawner`, `StatusMessageUI`) coordinated by a thin `BattleHUD` façade. `BattleHUD` subscribes to events on `BattleController` — UI never calls into battle logic. `StatusMessageQueue` is a plain C# class extracted from `StatusMessageUI` so its queuing logic can be tested in Edit Mode.

**Tech Stack:** Unity 6 LTS, URP 2D, Unity UI Canvas, TextMeshPro, `UnityEngine.Pool.ObjectPool<T>`, NUnit (Edit Mode tests)

**Design Spec:** `docs/superpowers/specs/2026-03-30-dev16-battle-ui-design.md`

---

## File Map

### New Files
| Path | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Battle/AttackResult.cs` | Plain C# struct | Carries damage, isCrit, targetDefeated out of action handlers |
| `Assets/Scripts/Battle/UI/StatusMessageQueue.cs` | Plain C# class | 2-line rolling message queue (testable) |
| `Assets/Scripts/Battle/UI/HealthBarUI.cs` | MonoBehaviour | Drives HP/MP bar fill + numeric text with lerp animation |
| `Assets/Scripts/Battle/UI/ActionMenuUI.cs` | MonoBehaviour | 2×2 button grid; enables/disables; exposes action callbacks |
| `Assets/Scripts/Battle/UI/TurnIndicatorUI.cs` | MonoBehaviour | Moves ▼ arrow above active `RectTransform`; bobs |
| `Assets/Scripts/Battle/UI/FloatingNumberInstance.cs` | MonoBehaviour | Single pooled floating TMP number; float-up + fade coroutine |
| `Assets/Scripts/Battle/UI/FloatingNumberSpawner.cs` | MonoBehaviour | `ObjectPool<FloatingNumberInstance>`; spawns on hit/heal |
| `Assets/Scripts/Battle/UI/StatusMessageUI.cs` | MonoBehaviour | Wraps `StatusMessageQueue`; updates TMP display |
| `Assets/Scripts/Battle/UI/BattleHUD.cs` | MonoBehaviour | Coordinator; subscribes to `BattleController` events; wires all components |

> **Note:** All Battle UI scripts live under `Assets/Scripts/Battle/UI/` with `namespace Axiom.Battle`. They are part of the `Axiom.Battle` asmdef — no separate `Axiom.UI` assembly. Platformer UI goes in `Assets/Scripts/Platformer/UI/`.
| `Assets/Tests/Editor/UI/UITests.asmdef` | Assembly definition | Edit Mode test assembly for UI |
| `Assets/Tests/Editor/UI/AttackResultTests.cs` | NUnit Edit Mode | Tests `AttackResult` struct + `PlayerActionHandler`/`EnemyActionHandler` changes |
| `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs` | NUnit Edit Mode | Tests `StatusMessageQueue` queuing logic |

### Modified Files
| Path | Change |
|---|---|
| `Assets/Scripts/Battle/CharacterStats.cs` | Add `public string Name` field |
| `Assets/Scripts/Battle/PlayerActionHandler.cs` | Return `AttackResult`; add crit via injected `Func<float>`; update constructor |
| `Assets/Scripts/Battle/EnemyActionHandler.cs` | Return `AttackResult`; update `ExecuteAttack()` |
| `Assets/Scripts/Battle/BattleController.cs` | Add UI events; wire `BattleHUD`; update to use `AttackResult` |
| `Assets/Tests/Editor/Battle/BattleTests.cs` (if exists) | Update any `ExecuteAttack()` call-sites that used the old `bool` return |

---

## Task 1: Add `CharacterStats.Name` and `AttackResult` struct

**Files:**
- Modify: `Assets/Scripts/Battle/CharacterStats.cs`
- Create: `Assets/Scripts/Battle/AttackResult.cs`
- Modify: `Assets/Tests/Editor/Battle/` (verify existing tests still compile)

- [ ] **Step 1: Read the existing CharacterStats file to confirm current state**

Open `Assets/Scripts/Battle/CharacterStats.cs`. Confirm it has `MaxHP`, `MaxMP`, `ATK`, `DEF`, `SPD` and no `Name` field.

- [ ] **Step 2: Add `Name` to `CharacterStats.cs`**

Add one field after the opening of the class body:

```csharp
using System;

namespace Axiom.Battle
{
    [Serializable]
    public class CharacterStats
    {
        public string Name = string.Empty;   // ← add this line
        public int MaxHP;
        public int MaxMP;
        public int ATK;
        public int DEF;
        public int SPD;

        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public bool IsDefeated => CurrentHP <= 0;

        public void Initialize()
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }

        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        public bool SpendMP(int amount)
        {
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        public void RestoreMP(int amount)
        {
            CurrentMP = Math.Min(MaxMP, CurrentMP + amount);
        }
    }
}
```

- [ ] **Step 3: Create `AttackResult.cs`**

Create `Assets/Scripts/Battle/AttackResult.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Return value of ExecuteAttack() on action handlers.
    /// Carries damage amount, crit flag, and defeat flag so BattleController
    /// can fire the correct UI events without querying stats again.
    /// </summary>
    public struct AttackResult
    {
        public int Damage;
        public bool IsCrit;
        public bool TargetDefeated;
    }
}
```

- [ ] **Step 4: Open Unity Editor and confirm no compile errors**

> **Unity Editor task (user):** Switch to the Unity Editor. Check the Console for compile errors. Fix any before proceeding.

- [ ] **Step 5: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/CharacterStats.cs`
- `Assets/Scripts/Battle/AttackResult.cs`
- `Assets/Scripts/Battle/AttackResult.cs.meta`

Check in with message: `feat: add CharacterStats.Name and AttackResult struct`

---

## Task 2: Update `PlayerActionHandler` and `EnemyActionHandler` to return `AttackResult`

**Files:**
- Modify: `Assets/Scripts/Battle/PlayerActionHandler.cs`
- Modify: `Assets/Scripts/Battle/EnemyActionHandler.cs`
- Create: `Assets/Tests/Editor/UI/UITests.asmdef`
- Create: `Assets/Tests/Editor/UI/AttackResultTests.cs`

- [ ] **Step 1: Create the `UITests` assembly definition**

Create `Assets/Tests/Editor/UI/UITests.asmdef`:

```json
{
    "name": "UITests",
    "references": [
        "Axiom.Battle"
    ],
    "testReferences": [
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

- [ ] **Step 2: Write failing tests for `AttackResult` and `PlayerActionHandler`**

Create `Assets/Tests/Editor/UI/AttackResultTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Tests.UI
{
    public class AttackResultTests
    {
        // ── PlayerActionHandler ──────────────────────────────────────────────

        [Test]
        public void ExecuteAttack_DealsDamageToEnemy()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 12, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0,  DEF = 4, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f); // 0.9 > 0.2 → no crit
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(8, result.Damage);           // max(1, 12 - 4) = 8
            Assert.IsFalse(result.IsCrit);
            Assert.IsFalse(result.TargetDefeated);
            Assert.AreEqual(52, enemy.CurrentHP);        // 60 - 8 = 52
        }

        [Test]
        public void ExecuteAttack_CritHit_WhenRandomBelowThreshold()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 10, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0,  DEF = 0, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.05f); // 0.05 < 0.2 → crit
            AttackResult result = handler.ExecuteAttack();

            Assert.IsTrue(result.IsCrit);
            Assert.AreEqual(15, result.Damage); // (int)(10 * 1.5f) = 15
        }

        [Test]
        public void ExecuteAttack_ReturnsTargetDefeated_WhenEnemyHPReachesZero()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 100, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 10,  MaxMP = 0, ATK = 0,   DEF = 0, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.IsTrue(result.TargetDefeated);
            Assert.AreEqual(0, enemy.CurrentHP);
        }

        [Test]
        public void ExecuteAttack_MinimumDamageIsOne()
        {
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 1, DEF = 0, SPD = 0 };
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 0, DEF = 100, SPD = 0 };
            player.Initialize();
            enemy.Initialize();

            var handler = new PlayerActionHandler(player, enemy, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(1, result.Damage); // max(1, 1 - 100) = 1
        }

        // ── EnemyActionHandler ───────────────────────────────────────────────

        [Test]
        public void EnemyExecuteAttack_DealsDamageToPlayer()
        {
            var enemy  = new CharacterStats { MaxHP = 60,  MaxMP = 0, ATK = 8, DEF = 0, SPD = 0 };
            var player = new CharacterStats { MaxHP = 100, MaxMP = 0, ATK = 0, DEF = 6, SPD = 0 };
            enemy.Initialize();
            player.Initialize();

            var handler = new EnemyActionHandler(enemy, player, () => 0.9f);
            AttackResult result = handler.ExecuteAttack();

            Assert.AreEqual(2, result.Damage);            // max(1, 8 - 6) = 2
            Assert.IsFalse(result.TargetDefeated);
            Assert.AreEqual(98, player.CurrentHP);
        }
    }
}
```

- [ ] **Step 3: Run tests — confirm they FAIL (classes not updated yet)**

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All. Confirm `AttackResultTests` fails with compile errors or wrong return type.

- [ ] **Step 4: Update `PlayerActionHandler.cs`**

Full replacement of `Assets/Scripts/Battle/PlayerActionHandler.cs`:

```csharp
using System;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes the four player actions during their turn.
    /// Reads stats from CharacterStats instances injected at construction time.
    /// No MonoBehaviour — BattleController creates and holds this.
    ///
    /// Pass a custom randomSource for deterministic testing; omit for production
    /// (defaults to UnityEngine.Random.value).
    /// </summary>
    public class PlayerActionHandler
    {
        private readonly CharacterStats _playerStats;
        private readonly CharacterStats _enemyStats;
        private readonly Func<float> _randomSource;

        private const float CritChance = 0.2f;
        private const float CritMultiplier = 1.5f;

        public PlayerActionHandler(
            CharacterStats playerStats,
            CharacterStats enemyStats,
            Func<float> randomSource = null)
        {
            _playerStats  = playerStats;
            _enemyStats   = enemyStats;
            _randomSource = randomSource ?? (() => UnityEngine.Random.value);
        }

        /// <summary>
        /// Deals damage to the enemy. Returns AttackResult with damage, crit flag,
        /// and whether the enemy was defeated.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            int baseDamage = Math.Max(1, _playerStats.ATK - _enemyStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _enemyStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _enemyStats.IsDefeated
            };
        }

        /// <summary>Placeholder for Phase 3 voice spell system.</summary>
        public string ExecuteSpell() => "No spells yet.";

        /// <summary>Placeholder for Phase 5 inventory system.</summary>
        public string ExecuteItem() => "No items.";

        /// <summary>
        /// No combat logic for Flee — BattleManager transitions to Fled state.
        /// Intentionally empty. BattleController calls BattleManager.OnPlayerFled() directly.
        /// </summary>
        public void ExecuteFlee() { }
    }
}
```

- [ ] **Step 5: Update `EnemyActionHandler.cs`**

Full replacement of `Assets/Scripts/Battle/EnemyActionHandler.cs`:

```csharp
using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes the enemy's attack action during EnemyTurn.
    /// No MonoBehaviour — BattleController creates and holds this.
    /// </summary>
    public class EnemyActionHandler
    {
        private readonly CharacterStats _enemyStats;
        private readonly CharacterStats _playerStats;
        private readonly Func<float> _randomSource;

        private const float CritChance = 0.1f;     // enemies crit less often
        private const float CritMultiplier = 1.5f;

        public EnemyActionHandler(
            CharacterStats enemyStats,
            CharacterStats playerStats,
            Func<float> randomSource = null)
        {
            _enemyStats   = enemyStats;
            _playerStats  = playerStats;
            _randomSource = randomSource ?? (() => UnityEngine.Random.value);
        }

        /// <summary>
        /// Deals damage to the player. Returns AttackResult with damage, crit flag,
        /// and whether the player was defeated.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            int baseDamage = Math.Max(1, _enemyStats.ATK - _playerStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _playerStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _playerStats.IsDefeated
            };
        }
    }
}
```

- [ ] **Step 6: Fix `BattleController` call-sites for the new return type**

In `Assets/Scripts/Battle/BattleController.cs`, update `PlayerAttack()` and `ExecuteEnemyTurn()` to capture the result (full `BattleController` update comes in Task 3, but compile errors must be fixed now):

In `PlayerAttack()`, replace:
```csharp
bool enemyDefeated = _actionHandler.ExecuteAttack();
_battleManager.OnPlayerActionComplete(enemyDefeated);
```
with:
```csharp
AttackResult result = _actionHandler.ExecuteAttack();
_battleManager.OnPlayerActionComplete(result.TargetDefeated);
```

In `ExecuteEnemyTurn()`, replace:
```csharp
bool playerDefeated = _enemyActionHandler.ExecuteAttack();
_battleManager.OnEnemyActionComplete(playerDefeated);
```
with:
```csharp
AttackResult result = _enemyActionHandler.ExecuteAttack();
_battleManager.OnEnemyActionComplete(result.TargetDefeated);
```

- [ ] **Step 7: Run tests — confirm they PASS**

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All. Confirm all `AttackResultTests` pass.

- [ ] **Step 8: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/AttackResult.cs`
- `Assets/Scripts/Battle/PlayerActionHandler.cs`
- `Assets/Scripts/Battle/EnemyActionHandler.cs`
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Tests/Editor/UI/UITests.asmdef`
- `Assets/Tests/Editor/UI/UITests.asmdef.meta`
- `Assets/Tests/Editor/UI/AttackResultTests.cs`
- `Assets/Tests/Editor/UI/AttackResultTests.cs.meta`

Check in with message: `feat: PlayerActionHandler/EnemyActionHandler return AttackResult with crit`

---

## Task 3: Add UI events to `BattleController`

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 1: Full replacement of `BattleController.cs`**

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour wrapper for BattleManager, PlayerActionHandler, and EnemyActionHandler.
    /// Handles Unity lifecycle only (Start, OnDestroy).
    ///
    /// Fires UI events so BattleHUD can react without calling into battle logic:
    ///   OnBattleStateChanged — proxies BattleManager.OnStateChanged
    ///   OnDamageDealt        — fires after any attack lands (player or enemy)
    ///   OnCharacterDefeated  — fires when a character's HP reaches zero
    ///
    /// In Phase 4, Initialize() will be called by GameManager on scene load.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Used for standalone Battle scene testing only. Phase 4 will override via GameManager.")]
        private CombatStartState _startState = CombatStartState.Advantaged;

        [SerializeField]
        [Tooltip("Player stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _playerStats = new CharacterStats
            { Name = "Kael", MaxHP = 100, MaxMP = 30, ATK = 12, DEF = 6, SPD = 8 };

        [SerializeField]
        [Tooltip("Enemy stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _enemyStats = new CharacterStats
            { Name = "Void Wraith", MaxHP = 60, MaxMP = 0, ATK = 8, DEF = 4, SPD = 5 };

        [SerializeField]
        [Tooltip("Assign the BattleHUD MonoBehaviour from the Battle scene.")]
        private BattleHUD _battleHUD;

        // ── UI Events ────────────────────────────────────────────────────────
        /// <summary>Proxies BattleManager.OnStateChanged so BattleHUD can subscribe here.</summary>
        public event Action<BattleState> OnBattleStateChanged;

        /// <summary>
        /// Fires after every attack. Parameters: target CharacterStats, damage dealt, isCrit.
        /// </summary>
        public event Action<CharacterStats, int, bool> OnDamageDealt;

        /// <summary>Fires when a character's HP reaches zero.</summary>
        public event Action<CharacterStats> OnCharacterDefeated;

        // ── Private fields ───────────────────────────────────────────────────
        private BattleManager _battleManager;
        private PlayerActionHandler _actionHandler;
        private EnemyActionHandler _enemyActionHandler;

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

            _actionHandler      = new PlayerActionHandler(_playerStats, _enemyStats);
            _enemyActionHandler = new EnemyActionHandler(_enemyStats, _playerStats);
            _battleManager      = new BattleManager();
            _battleManager.OnStateChanged += HandleStateChanged;

            _battleHUD?.Setup(this, _playerStats, _enemyStats);
            _battleManager.StartBattle(startState);
        }

        // ── Player action methods — wired via ActionMenuUI.OnAttack etc. ─────

        /// <summary>Executes the Attack action. No-op outside PlayerTurn.</summary>
        public void PlayerAttack()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;

            AttackResult result = _actionHandler.ExecuteAttack();

            OnDamageDealt?.Invoke(_enemyStats, result.Damage, result.IsCrit);
            if (result.TargetDefeated)
                OnCharacterDefeated?.Invoke(_enemyStats);

            _battleManager.OnPlayerActionComplete(result.TargetDefeated);
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

        /// <summary>Executes Flee. No-op outside PlayerTurn.</summary>
        public void PlayerFlee()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            _battleManager.OnPlayerFled();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void HandleStateChanged(BattleState state)
        {
            Debug.Log($"[Battle] → {state}");
            OnBattleStateChanged?.Invoke(state);

            if (state == BattleState.EnemyTurn)
                ExecuteEnemyTurn();

            if (state == BattleState.Fled)
                SceneManager.LoadScene("Platformer");
        }

        private void ExecuteEnemyTurn()
        {
            AttackResult result = _enemyActionHandler.ExecuteAttack();

            OnDamageDealt?.Invoke(_playerStats, result.Damage, result.IsCrit);
            if (result.TargetDefeated)
                OnCharacterDefeated?.Invoke(_playerStats);

            _battleManager.OnEnemyActionComplete(result.TargetDefeated);
        }

        private void OnDestroy()
        {
            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;
        }
    }
}
```

> **Compile order note:** `BattleHUD` doesn't exist until Task 9. After saving this file, Unity will show a compile error. **Complete Tasks 4–9 before switching to the Editor to verify.** Alternatively, after Task 3, temporarily stub `BattleHUD` as an empty class (`public class BattleHUD : MonoBehaviour {}` in `Assets/Scripts/UI/BattleHUD.cs`) to keep the project compiling, then replace it fully in Task 9.

- [ ] **Step 2: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/Battle/BattleController.cs`

Check in with message: `feat: add UI events to BattleController (OnBattleStateChanged, OnDamageDealt, OnCharacterDefeated)`

---

## Task 4: `Axiom.UI` asmdef + `StatusMessageQueue` + `StatusMessageUI`

**Files:**
- Create: `Assets/Scripts/UI/Axiom.UI.asmdef`
- Create: `Assets/Scripts/UI/StatusMessageQueue.cs`
- Create: `Assets/Scripts/UI/StatusMessageUI.cs`
- Create: `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs`
- Modify: `Assets/Tests/Editor/UI/UITests.asmdef` — add `Axiom.UI` reference

- [ ] **Step 1: Create `Axiom.UI.asmdef`**

Create `Assets/Scripts/UI/Axiom.UI.asmdef`:

```json
{
    "name": "Axiom.UI",
    "references": [
        "Axiom.Battle"
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

- [ ] **Step 2: Update `UITests.asmdef` to reference `Axiom.UI`**

Replace `Assets/Tests/Editor/UI/UITests.asmdef`:

```json
{
    "name": "UITests",
    "references": [
        "Axiom.Battle",
        "Axiom.UI"
    ],
    "testReferences": [
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

- [ ] **Step 3: Write failing tests for `StatusMessageQueue`**

Create `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.UI;

namespace Axiom.Tests.UI
{
    public class StatusMessageQueueTests
    {
        [Test]
        public void Post_FirstMessage_DisplaysOnOneLine()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Enemy attacks!");
            Assert.AreEqual("Enemy attacks!", queue.GetDisplay());
        }

        [Test]
        public void Post_SecondMessage_DisplaysBothLines()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Enemy attacks!");
            queue.Post("Kael takes 8 damage.");
            Assert.AreEqual("Enemy attacks!\nKael takes 8 damage.", queue.GetDisplay());
        }

        [Test]
        public void Post_ThirdMessage_OldestLineDropped()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Line one.");
            queue.Post("Line two.");
            queue.Post("Line three.");
            Assert.AreEqual("Line two.\nLine three.", queue.GetDisplay());
        }

        [Test]
        public void GetDisplay_BeforeAnyPost_ReturnsEmpty()
        {
            var queue = new StatusMessageQueue();
            Assert.AreEqual(string.Empty, queue.GetDisplay());
        }
    }
}
```

- [ ] **Step 4: Run tests — confirm they FAIL**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Confirm `StatusMessageQueueTests` fail (`StatusMessageQueue` not found).

- [ ] **Step 5: Create `StatusMessageQueue.cs`**

Create `Assets/Scripts/UI/StatusMessageQueue.cs`:

```csharp
namespace Axiom.UI
{
    /// <summary>
    /// Plain C# rolling 2-line message buffer for battle narration.
    /// Extracted from StatusMessageUI so queue logic is testable without Unity.
    /// </summary>
    public class StatusMessageQueue
    {
        private string _line1 = string.Empty;
        private string _line2 = string.Empty;

        /// <summary>
        /// Adds a message. Pushes the current bottom line to the top,
        /// discarding the oldest top line.
        /// </summary>
        public void Post(string message)
        {
            _line1 = _line2;
            _line2 = message;
        }

        /// <summary>
        /// Returns the display string: one or two lines separated by a newline.
        /// Returns empty string if no messages have been posted.
        /// </summary>
        public string GetDisplay()
        {
            if (string.IsNullOrEmpty(_line1) && string.IsNullOrEmpty(_line2))
                return string.Empty;
            if (string.IsNullOrEmpty(_line1))
                return _line2;
            return $"{_line1}\n{_line2}";
        }
    }
}
```

- [ ] **Step 6: Run tests — confirm they PASS**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Confirm all `StatusMessageQueueTests` pass.

- [ ] **Step 7: Create `StatusMessageUI.cs`**

Create `Assets/Scripts/UI/StatusMessageUI.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace Axiom.UI
{
    /// <summary>
    /// MonoBehaviour wrapper for StatusMessageQueue.
    /// Attach to the MessageLog GameObject in the Battle Canvas.
    /// Call Post() to display battle narration lines.
    /// </summary>
    public class StatusMessageUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TMP text component that displays the message log.")]
        private TMP_Text _text;

        private readonly StatusMessageQueue _queue = new StatusMessageQueue();

        /// <summary>Posts a message to the 2-line rolling log.</summary>
        public void Post(string message)
        {
            _queue.Post(message);
            _text.text = _queue.GetDisplay();
        }
    }
}
```

- [ ] **Step 8: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors.

- [ ] **Step 9: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/Axiom.UI.asmdef` + `.meta`
- `Assets/Scripts/UI/StatusMessageQueue.cs` + `.meta`
- `Assets/Scripts/UI/StatusMessageUI.cs` + `.meta`
- `Assets/Tests/Editor/UI/UITests.asmdef`
- `Assets/Tests/Editor/UI/StatusMessageQueueTests.cs` + `.meta`

Check in with message: `feat: StatusMessageQueue (tested) and StatusMessageUI MonoBehaviour`

---

## Task 5: `HealthBarUI`

**Files:**
- Create: `Assets/Scripts/UI/HealthBarUI.cs`

- [ ] **Step 1: Create `HealthBarUI.cs`**

Create `Assets/Scripts/UI/HealthBarUI.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.UI
{
    /// <summary>
    /// Drives a gradient-fill HP bar and optional MP bar for one character slot
    /// (PartyMemberSlot or EnemyPanel).
    ///
    /// SetHP / SetMP record a target fill value; Update() lerps the Image fill
    /// smoothly toward it each frame.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image component used as the HP bar fill. Set Image Type to Filled.")]
        private Image _hpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' HP. Optional.")]
        private TMP_Text _hpText;

        [SerializeField]
        [Tooltip("Image component used as the MP bar fill. Null for enemy slots (no MP bar).")]
        private Image _mpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' MP. Null for enemy slots.")]
        private TMP_Text _mpText;

        [SerializeField]
        [Tooltip("Speed at which the bar fill lerps toward its target value (units per second).")]
        private float _lerpSpeed = 5f;

        private float _targetHPFill;
        private float _targetMPFill;

        private void Update()
        {
            if (_hpBarImage != null)
                _hpBarImage.fillAmount = Mathf.Lerp(
                    _hpBarImage.fillAmount, _targetHPFill, Time.deltaTime * _lerpSpeed);

            if (_mpBarImage != null)
                _mpBarImage.fillAmount = Mathf.Lerp(
                    _mpBarImage.fillAmount, _targetMPFill, Time.deltaTime * _lerpSpeed);
        }

        /// <summary>Updates the HP bar fill target and numeric text label.</summary>
        public void SetHP(int current, int max)
        {
            _targetHPFill = max > 0 ? (float)current / max : 0f;
            if (_hpText != null)
                _hpText.text = $"{current} / {max}";
        }

        /// <summary>
        /// Updates the MP bar fill target and numeric text label.
        /// No-op if this slot has no MP bar (e.g. enemy panel).
        /// </summary>
        public void SetMP(int current, int max)
        {
            if (_mpBarImage == null) return;
            _targetMPFill = max > 0 ? (float)current / max : 0f;
            if (_mpText != null)
                _mpText.text = $"{current} / {max}";
        }
    }
}
```

- [ ] **Step 2: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors.

- [ ] **Step 3: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/HealthBarUI.cs` + `.meta`

Check in with message: `feat: HealthBarUI with lerp-animated fill bars`

---

## Task 6: `ActionMenuUI`

**Files:**
- Create: `Assets/Scripts/UI/ActionMenuUI.cs`

- [ ] **Step 1: Create `ActionMenuUI.cs`**

Create `Assets/Scripts/UI/ActionMenuUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.UI
{
    /// <summary>
    /// Manages the 2×2 battle action menu (Attack, Spell, Item, Flee).
    ///
    /// BattleHUD wires OnAttack/OnSpell/OnItem/OnFlee to BattleController methods.
    /// SetInteractable(false) is called on EnemyTurn, Victory, and Defeat.
    /// </summary>
    public class ActionMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _spellButton;
        [SerializeField] private Button _itemButton;
        [SerializeField] private Button _fleeButton;

        /// <summary>Wire these to BattleController.PlayerAttack / PlayerSpell / PlayerItem / PlayerFlee.</summary>
        public Action OnAttack;
        public Action OnSpell;
        public Action OnItem;
        public Action OnFlee;

        private void Start()
        {
            _attackButton.onClick.AddListener(() => OnAttack?.Invoke());
            _spellButton.onClick.AddListener(() => OnSpell?.Invoke());
            _itemButton.onClick.AddListener(() => OnItem?.Invoke());
            _fleeButton.onClick.AddListener(() => OnFlee?.Invoke());
        }

        /// <summary>
        /// Enables or disables all four buttons.
        /// Call with false during EnemyTurn, Victory, and Defeat states.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
            _spellButton.interactable  = interactable;
            _itemButton.interactable   = interactable;
            _fleeButton.interactable   = interactable;
        }

        private void OnDestroy()
        {
            _attackButton.onClick.RemoveAllListeners();
            _spellButton.onClick.RemoveAllListeners();
            _itemButton.onClick.RemoveAllListeners();
            _fleeButton.onClick.RemoveAllListeners();
        }
    }
}
```

- [ ] **Step 2: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors.

- [ ] **Step 3: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/ActionMenuUI.cs` + `.meta`

Check in with message: `feat: ActionMenuUI with interactable toggle and action callbacks`

---

## Task 7: `TurnIndicatorUI`

**Files:**
- Create: `Assets/Scripts/UI/TurnIndicatorUI.cs`

- [ ] **Step 1: Create `TurnIndicatorUI.cs`**

Create `Assets/Scripts/UI/TurnIndicatorUI.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace Axiom.UI
{
    /// <summary>
    /// Repositions a ▼ arrow RectTransform above the active character's slot.
    /// Runs a continuous bob coroutine while active.
    /// Designed to accept any RectTransform target — party-ready.
    /// </summary>
    public class TurnIndicatorUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The RectTransform of the ▼ arrow image.")]
        private RectTransform _arrowRect;

        [SerializeField]
        [Tooltip("Vertical offset in canvas units above the target slot.")]
        private float _yOffset = 60f;

        [SerializeField]
        [Tooltip("Height of the bob animation in canvas units.")]
        private float _bobHeight = 6f;

        [SerializeField]
        [Tooltip("Speed of the bob animation.")]
        private float _bobSpeed = 3f;

        private RectTransform _currentTarget;
        private Coroutine _bobCoroutine;

        /// <summary>
        /// Moves the arrow above the given target slot and (re)starts the bob.
        /// Pass null to hide the indicator.
        /// </summary>
        public void SetActiveTarget(RectTransform target)
        {
            _currentTarget = target;

            if (_bobCoroutine != null)
                StopCoroutine(_bobCoroutine);

            if (target == null)
            {
                _arrowRect.gameObject.SetActive(false);
                return;
            }

            _arrowRect.gameObject.SetActive(true);
            _arrowRect.position = target.position + Vector3.up * _yOffset;
            _bobCoroutine = StartCoroutine(Bob());
        }

        private IEnumerator Bob()
        {
            float elapsed = 0f;
            Vector3 basePosition = _arrowRect.position;

            while (true)
            {
                elapsed += Time.deltaTime;
                _arrowRect.position = basePosition + Vector3.up * (Mathf.Sin(elapsed * _bobSpeed) * _bobHeight);
                yield return null;
            }
        }
    }
}
```

- [ ] **Step 2: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors.

- [ ] **Step 3: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/TurnIndicatorUI.cs` + `.meta`

Check in with message: `feat: TurnIndicatorUI with bobbing arrow, party-ready`

---

## Task 8: `FloatingNumberInstance` + `FloatingNumberSpawner`

**Files:**
- Create: `Assets/Scripts/UI/FloatingNumberInstance.cs`
- Create: `Assets/Scripts/UI/FloatingNumberSpawner.cs`

- [ ] **Step 1: Create `FloatingNumberInstance.cs`**

Create `Assets/Scripts/UI/FloatingNumberInstance.cs`:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace Axiom.UI
{
    /// <summary>
    /// A single pooled floating damage/heal/crit number.
    /// FloatingNumberSpawner configures and releases it back to the pool.
    /// </summary>
    public class FloatingNumberInstance : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private float _floatDistance = 80f;
        [SerializeField] private float _duration = 0.9f;

        private IObjectPool<FloatingNumberInstance> _pool;
        private float _originalFontSize;

        private void Awake()
        {
            _originalFontSize = _text.fontSize;
        }

        public void Initialize(IObjectPool<FloatingNumberInstance> pool)
        {
            _pool = pool;
        }

        /// <summary>
        /// Sets the text, colour, scale, and starting position, then starts the animation.
        /// Called by FloatingNumberSpawner immediately after retrieving from pool.
        /// </summary>
        public void Play(string label, Color color, float scale, Vector3 canvasPosition)
        {
            _text.text = label;
            _text.color = color;
            _text.fontSize = _originalFontSize * scale;
            transform.position = canvasPosition;
            _canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos   = startPos + Vector3.up * _floatDistance;
            float elapsed    = 0f;

            while (elapsed < _duration)
            {
                float t = elapsed / _duration;
                transform.position  = Vector3.Lerp(startPos, endPos, t);
                _canvasGroup.alpha  = Mathf.Lerp(1f, 0f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _pool.Release(this);
        }
    }
}
```

- [ ] **Step 2: Create `FloatingNumberSpawner.cs`**

Create `Assets/Scripts/UI/FloatingNumberSpawner.cs`:

```csharp
using UnityEngine;
using UnityEngine.Pool;

namespace Axiom.UI
{
    /// <summary>
    /// Maintains an ObjectPool of FloatingNumberInstance prefabs.
    /// Call Spawn() to show a floating damage, heal, or crit number
    /// at a given RectTransform's canvas position.
    /// </summary>
    public class FloatingNumberSpawner : MonoBehaviour
    {
        public enum NumberType { Damage, Heal, Crit }

        [SerializeField]
        [Tooltip("Prefab with FloatingNumberInstance, TMP_Text, and CanvasGroup components.")]
        private FloatingNumberInstance _prefab;

        [SerializeField] private int _defaultPoolSize = 8;
        [SerializeField] private int _maxPoolSize = 20;

        private static readonly Color DamageColor = new Color(0.92f, 0.30f, 0.20f); // red
        private static readonly Color HealColor   = new Color(0.15f, 0.76f, 0.36f); // green
        private static readonly Color CritColor   = new Color(0.95f, 0.61f, 0.07f); // gold

        private IObjectPool<FloatingNumberInstance> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<FloatingNumberInstance>(
                createFunc: CreateInstance,
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: instance => instance.gameObject.SetActive(false),
                actionOnDestroy: instance => Destroy(instance.gameObject),
                collectionCheck: false,
                defaultCapacity: _defaultPoolSize,
                maxSize: _maxPoolSize);
        }

        /// <summary>
        /// Spawns a floating number above the given origin slot.
        /// </summary>
        public void Spawn(RectTransform origin, int amount, NumberType type)
        {
            Color color;
            float scale;
            string label;

            switch (type)
            {
                case NumberType.Heal:
                    color = HealColor;
                    scale = 1f;
                    label = $"+{amount}";
                    break;
                case NumberType.Crit:
                    color = CritColor;
                    scale = 1.4f;
                    label = $"{amount}!";
                    break;
                default: // Damage
                    color = DamageColor;
                    scale = 1f;
                    label = $"-{amount}";
                    break;
            }

            var instance = _pool.Get();
            instance.Play(label, color, scale, origin.position);
        }

        private FloatingNumberInstance CreateInstance()
        {
            var instance = Instantiate(_prefab, transform);
            instance.Initialize(_pool);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
```

- [ ] **Step 3: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors.

- [ ] **Step 4: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/FloatingNumberInstance.cs` + `.meta`
- `Assets/Scripts/UI/FloatingNumberSpawner.cs` + `.meta`

Check in with message: `feat: FloatingNumberSpawner with ObjectPool for damage/heal/crit numbers`

---

## Task 9: `BattleHUD` — coordinator

**Files:**
- Create: `Assets/Scripts/UI/BattleHUD.cs`

- [ ] **Step 1: Create `BattleHUD.cs`**

Create `Assets/Scripts/UI/BattleHUD.cs`:

```csharp
using System.Collections.Generic;
using Axiom.Battle;
using TMPro;
using UnityEngine;

namespace Axiom.UI
{
    /// <summary>
    /// Thin coordinator MonoBehaviour for the Battle scene UI.
    /// Subscribes to BattleController events and delegates to the five UI components.
    ///
    /// Called by BattleController.Initialize() via Setup().
    /// All Inspector references must be assigned in the Battle scene.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        [Header("Enemy Panel")]
        [SerializeField] private HealthBarUI _enemyHealthBar;
        [SerializeField] private TMP_Text    _enemyNameText;

        [Header("Party Panel (single slot for Phase 2)")]
        [SerializeField] private HealthBarUI _partyHealthBar;
        [SerializeField] private TMP_Text    _partyNameText;
        [SerializeField] private RectTransform _partySlotRect;

        [Header("Enemy Slot RectTransform (for floating numbers + arrow)")]
        [SerializeField] private RectTransform _enemySlotRect;

        [Header("UI Components")]
        [SerializeField] private ActionMenuUI         _actionMenuUI;
        [SerializeField] private TurnIndicatorUI      _turnIndicatorUI;
        [SerializeField] private FloatingNumberSpawner _floatingNumberSpawner;
        [SerializeField] private StatusMessageUI      _statusMessageUI;

        // ── Internal state ────────────────────────────────────────────────────
        private BattleController _battleController;
        private CharacterStats   _playerStats;
        private CharacterStats   _enemyStats;

        // Maps CharacterStats → their slot RectTransform for floating numbers.
        private readonly Dictionary<CharacterStats, RectTransform> _statToRect
            = new Dictionary<CharacterStats, RectTransform>();

        /// <summary>
        /// Called by BattleController.Initialize().
        /// Subscribes to events and initialises all UI to starting values.
        /// </summary>
        public void Setup(BattleController battleController, CharacterStats playerStats, CharacterStats enemyStats)
        {
            // Unsubscribe from previous controller if reinitialised mid-session
            if (_battleController != null)
                Unsubscribe();

            _battleController = battleController;
            _playerStats      = playerStats;
            _enemyStats       = enemyStats;

            _statToRect[playerStats] = _partySlotRect;
            _statToRect[enemyStats]  = _enemySlotRect;

            // Wire action menu callbacks to BattleController
            _actionMenuUI.OnAttack = _battleController.PlayerAttack;
            _actionMenuUI.OnSpell  = _battleController.PlayerSpell;
            _actionMenuUI.OnItem   = _battleController.PlayerItem;
            _actionMenuUI.OnFlee   = _battleController.PlayerFlee;

            // Subscribe to battle events
            _battleController.OnBattleStateChanged += HandleStateChanged;
            _battleController.OnDamageDealt        += HandleDamageDealt;
            _battleController.OnCharacterDefeated  += HandleCharacterDefeated;

            // Initialise display
            _enemyNameText.text  = enemyStats.Name;
            _enemyHealthBar.SetHP(enemyStats.CurrentHP, enemyStats.MaxHP);

            _partyNameText.text = playerStats.Name;
            _partyHealthBar.SetHP(playerStats.CurrentHP, playerStats.MaxHP);
            _partyHealthBar.SetMP(playerStats.CurrentMP, playerStats.MaxMP);
        }

        private void OnDestroy() => Unsubscribe();

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleStateChanged(BattleState state)
        {
            bool isPlayerTurn = state == BattleState.PlayerTurn;
            bool isActive     = state == BattleState.PlayerTurn || state == BattleState.EnemyTurn;

            _actionMenuUI.SetInteractable(isPlayerTurn);

            if (state == BattleState.PlayerTurn)
            {
                _turnIndicatorUI.SetActiveTarget(_partySlotRect);
                _statusMessageUI.Post("Your turn.");
            }
            else if (state == BattleState.EnemyTurn)
            {
                _turnIndicatorUI.SetActiveTarget(_enemySlotRect);
                _statusMessageUI.Post($"{_enemyStats.Name}'s turn.");
            }
            else if (state == BattleState.Victory)
            {
                _turnIndicatorUI.SetActiveTarget(null);
                _statusMessageUI.Post("Victory!");
            }
            else if (state == BattleState.Defeat)
            {
                _turnIndicatorUI.SetActiveTarget(null);
                _statusMessageUI.Post("Defeated...");
            }
            else if (state == BattleState.Fled)
            {
                _turnIndicatorUI.SetActiveTarget(null);
            }
        }

        private void HandleDamageDealt(CharacterStats target, int amount, bool isCrit)
        {
            // Update health bar
            if (target == _playerStats)
            {
                _partyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
                _partyHealthBar.SetMP(target.CurrentMP, target.MaxMP);
            }
            else if (target == _enemyStats)
            {
                _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
            }

            // Floating number
            if (_statToRect.TryGetValue(target, out RectTransform rect))
            {
                var numberType = isCrit
                    ? FloatingNumberSpawner.NumberType.Crit
                    : FloatingNumberSpawner.NumberType.Damage;
                _floatingNumberSpawner.Spawn(rect, amount, numberType);
            }

            // Status message
            bool isEnemyAttacking = target == _playerStats;
            string attacker = isEnemyAttacking ? _enemyStats.Name : _playerStats.Name;
            string defender = isEnemyAttacking ? _playerStats.Name : _enemyStats.Name;
            string critTag  = isCrit ? " Critical hit!" : string.Empty;
            _statusMessageUI.Post($"{attacker} attacks! {defender} takes {amount} damage.{critTag}");
        }

        private void HandleCharacterDefeated(CharacterStats character)
        {
            if (character == _playerStats)
            {
                _partyHealthBar.SetHP(0, character.MaxHP);
                _statusMessageUI.Post($"{character.Name} was defeated...");
            }
            else if (character == _enemyStats)
            {
                _enemyHealthBar.SetHP(0, character.MaxHP);
                _statusMessageUI.Post($"{character.Name} was defeated!");
            }
        }

        private void Unsubscribe()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged -= HandleStateChanged;
            _battleController.OnDamageDealt        -= HandleDamageDealt;
            _battleController.OnCharacterDefeated  -= HandleCharacterDefeated;
        }
    }
}
```

- [ ] **Step 2: Confirm no compile errors**

> **Unity Editor task (user):** Switch to Unity Editor. Confirm Console shows no errors. All scripts should compile cleanly now.

- [ ] **Step 3: Check in**

Unity Version Control → Pending Changes → stage:
- `Assets/Scripts/UI/BattleHUD.cs` + `.meta`

Check in with message: `feat: BattleHUD coordinator — subscribes to BattleController events, wires all UI components`

---

## Task 10: Unity Editor — Canvas Setup

> **All steps in this task are Unity Editor tasks performed by the user.**

- [ ] **Step 1: Create the `FloatingNumber` prefab**

1. In the Battle scene Hierarchy, create: GameObject → UI → Text - TextMeshPro. Name it `FloatingNumber`.
2. Add a `CanvasGroup` component to it.
3. Add the `FloatingNumberInstance` script component.
4. Set the TMP Text font size to `36`, alignment `Centre Middle`, colour white.
5. In the `FloatingNumberInstance` Inspector, assign the TMP Text to `_text` and the CanvasGroup to `_canvasGroup`.
6. Drag it to `Assets/Prefabs/UI/FloatingNumber.prefab` (create the `Prefabs/UI/` folder if needed).
7. Delete the scene instance.

- [ ] **Step 2: Build the Canvas hierarchy**

Open `Assets/Scenes/Battle.unity`. In the Hierarchy, create the following structure under the existing Canvas (or create a new Screen Space Overlay Canvas named `BattleHUD`):

```
Canvas (BattleHUD)
├── TurnIndicator              ← UI → Image, name it TurnIndicator. Set sprite to a ▼ arrow or use TMP "▼"
├── EnemyPanel                 ← Empty GameObject
│   ├── EnemyNameText          ← UI → Text - TextMeshPro
│   ├── EnemyHPBar             ← UI → Image. Set Image Type = Filled, Fill Method = Horizontal
│   └── EnemyHPText            ← UI → Text - TextMeshPro
├── PartyPanel                 ← Empty GameObject. Add Horizontal Layout Group component.
│   └── PartyMemberSlot        ← Empty GameObject
│       ├── MemberNameText     ← UI → Text - TextMeshPro
│       ├── HPBar              ← UI → Image (Filled, Horizontal)
│       ├── HPText             ← UI → Text - TextMeshPro
│       ├── MPBar              ← UI → Image (Filled, Horizontal)
│       └── MPText             ← UI → Text - TextMeshPro
├── ActionMenu                 ← Empty GameObject. Add Grid Layout Group (2 columns).
│   ├── AttackButton           ← UI → Button - TextMeshPro. Set child TMP label to "Attack"
│   ├── SpellButton            ← UI → Button - TextMeshPro. Label: "Spell"
│   ├── ItemButton             ← UI → Button - TextMeshPro. Label: "Item"
│   └── FleeButton             ← UI → Button - TextMeshPro. Label: "Flee"
├── MessageLog                 ← UI → Text - TextMeshPro. Set height for 2 lines.
└── FloatingNumberPool         ← Empty GameObject
```

- [ ] **Step 3: Add and configure MonoBehaviour components**

For each listed GameObject, add the MonoBehaviour and assign the serialized fields:

| GameObject | Component | Fields to assign |
|---|---|---|
| `EnemyPanel` | `HealthBarUI` | `_hpBarImage` → EnemyHPBar; `_hpText` → EnemyHPText; leave `_mpBarImage` and `_mpText` empty |
| `PartyMemberSlot` | `HealthBarUI` | `_hpBarImage` → HPBar; `_hpText` → HPText; `_mpBarImage` → MPBar; `_mpText` → MPText |
| `ActionMenu` | `ActionMenuUI` | `_attackButton`, `_spellButton`, `_itemButton`, `_fleeButton` → respective buttons |
| `TurnIndicator` | `TurnIndicatorUI` | `_arrowRect` → TurnIndicator's own RectTransform |
| `FloatingNumberPool` | `FloatingNumberSpawner` | `_prefab` → `FloatingNumber` prefab from Assets/Prefabs/UI/ |
| `MessageLog` | `StatusMessageUI` | `_text` → MessageLog TMP Text |
| `Canvas (BattleHUD)` | `BattleHUD` | All component references (see below) |

For `BattleHUD`, assign:
- `_enemyHealthBar` → EnemyPanel's `HealthBarUI`
- `_enemyNameText` → EnemyNameText TMP
- `_partyHealthBar` → PartyMemberSlot's `HealthBarUI`
- `_partyNameText` → MemberNameText TMP
- `_partySlotRect` → PartyMemberSlot RectTransform
- `_enemySlotRect` → EnemyPanel RectTransform
- `_actionMenuUI` → ActionMenu's `ActionMenuUI`
- `_turnIndicatorUI` → TurnIndicator's `TurnIndicatorUI`
- `_floatingNumberSpawner` → FloatingNumberPool's `FloatingNumberSpawner`
- `_statusMessageUI` → MessageLog's `StatusMessageUI`

- [ ] **Step 4: Wire `BattleController` → `BattleHUD`**

Select the `BattleController` GameObject. In the Inspector, assign the `BattleHUD` field to the Canvas's `BattleHUD` component.

> **Note:** Remove any existing direct button → BattleController OnClick wiring from the Inspector. `ActionMenuUI` now handles this via `BattleHUD.Setup()`.

- [ ] **Step 5: Set HP bar gradient colours**

For each HP bar Image:
- Set the Image Color to the gradient start colour:
  - Player HP: `#47C26F` (green)
  - Player MP: `#3A8FDB` (blue)
  - Enemy HP: `#E74C3C` (red)
- For a two-tone gradient, add a second Image as a sibling overlay with the lighter shade and set its fill as well. (Simple single-colour fill is acceptable for Phase 2.)

- [ ] **Step 6: Play Mode smoke test**

1. Press Play in the Unity Editor.
2. Confirm the battle starts with player name and HP/MP shown correctly.
3. Click **Attack** — confirm:
   - Enemy HP bar animates down.
   - A red floating number appears above the enemy slot.
   - The message log shows "Kael attacks! Void Wraith takes X damage."
   - Turn indicator arrow moves to enemy slot.
   - Action menu buttons become non-interactable during enemy turn.
   - Enemy attacks back, player HP bar updates.
   - Arrow returns to player slot, buttons re-enable.
4. Continue until Victory or Defeat — confirm:
   - Message log shows "Void Wraith was defeated!" or "Kael was defeated..."
   - Action menu stays greyed out.
   - Turn indicator hides.

- [ ] **Step 7: Save scene and check in**

File → Save (Ctrl+S) to save the Battle scene.

Unity Version Control → Pending Changes → stage:
- `Assets/Scenes/Battle.unity`
- `Assets/Prefabs/UI/FloatingNumber.prefab` + `.meta`
- Any new `.meta` files for folders created

Check in with message: `feat: Battle scene UI canvas hierarchy wired to BattleHUD — DEV-16 complete`
