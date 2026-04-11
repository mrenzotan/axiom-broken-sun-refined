# DEV-31: Persistent GameManager Singleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a `GameManager` MonoBehaviour singleton that persists across scenes via `DontDestroyOnLoad` and serves as the single source of truth for cross-scene player state (HP/MP, stats, scene context, inventory placeholder).

**Architecture:** `PlayerState` is a plain C# class that holds all player data and mutation logic — fully unit-testable without Unity. `GameManager` is a MonoBehaviour that performs only Unity lifecycle work (`Awake`, `OnDestroy`): it enforces the singleton guard, calls `DontDestroyOnLoad`, and owns the `PlayerState` instance. Other systems access state through `GameManager.Instance.PlayerState` at initialization time, storing a local reference — they must never add a `static GameManager` field of their own.

**Tech Stack:** Unity 6 LTS, C# 9, Unity Test Framework (Edit Mode NUnit + Play Mode UnityTest), UVCS

---

## File Map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Core/Axiom.Core.asmdef` | Assembly definition for the Core module |
| `Assets/Scripts/Core/PlayerState.cs` | Plain C# class — all cross-scene player data and mutation logic |
| `Assets/Scripts/Core/GameManager.cs` | MonoBehaviour — DontDestroyOnLoad singleton, owns `PlayerState` instance |
| `Assets/Tests/Editor/Core/CoreTests.asmdef` | Edit Mode test assembly for Core |
| `Assets/Tests/Editor/Core/PlayerStateTests.cs` | Edit Mode NUnit tests for `PlayerState` |
| `Assets/Tests/PlayMode/Core/CorePlayModeTests.asmdef` | Play Mode test assembly for Core |
| `Assets/Tests/PlayMode/Core/GameManagerTests.cs` | Play Mode UnityTest for `GameManager` singleton lifecycle |

---

### Task 1: Create Core Assembly Definitions

**Files:**
- Create: `Assets/Scripts/Core/Axiom.Core.asmdef`
- Create: `Assets/Tests/Editor/Core/CoreTests.asmdef`
- Create: `Assets/Tests/PlayMode/Core/CorePlayModeTests.asmdef`

- [ ] **Step 1: Create the `Assets/Scripts/Core/` folder**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Scripts/` → Create → Folder → name it `Core`.

- [ ] **Step 2: Write `Axiom.Core.asmdef`**

Write this file to `Assets/Scripts/Core/Axiom.Core.asmdef`:

```json
{
    "name": "Axiom.Core",
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

- [ ] **Step 3: Create the `Assets/Tests/Editor/Core/` folder**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Tests/Editor/` → Create → Folder → name it `Core`.

- [ ] **Step 4: Write `CoreTests.asmdef`**

Write this file to `Assets/Tests/Editor/Core/CoreTests.asmdef`:

```json
{
    "name": "CoreTests",
    "references": [
        "Axiom.Core"
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

- [ ] **Step 5: Create `Assets/Tests/PlayMode/` and `Assets/Tests/PlayMode/Core/` folders**

> **Unity Editor task (user):** In the Project window, right-click `Assets/Tests/` → Create → Folder → name it `PlayMode`. Then right-click the new `PlayMode/` → Create → Folder → name it `Core`.

- [ ] **Step 6: Write `CorePlayModeTests.asmdef`**

Write this file to `Assets/Tests/PlayMode/Core/CorePlayModeTests.asmdef`:

```json
{
    "name": "CorePlayModeTests",
    "references": [
        "Axiom.Core"
    ],
    "testReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
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

- [ ] **Step 7: Verify asmdefs compile cleanly**

> **Unity Editor task (user):** Switch to the Unity Editor and wait for the status bar to finish compiling. Click `Assets/Scripts/Core/Axiom.Core.asmdef` in the Project window — the Inspector must show "Assembly Definition" with name `Axiom.Core` and zero Console errors.

- [ ] **Step 8: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `chore(DEV-31): add Axiom.Core and test assembly definitions`

  - `Assets/Scripts/Core/` *(folder .meta)*
  - `Assets/Scripts/Core/Axiom.Core.asmdef`
  - `Assets/Scripts/Core/Axiom.Core.asmdef.meta`
  - `Assets/Tests/Editor/Core/` *(folder .meta)*
  - `Assets/Tests/Editor/Core/CoreTests.asmdef`
  - `Assets/Tests/Editor/Core/CoreTests.asmdef.meta`
  - `Assets/Tests/PlayMode/` *(folder .meta — new folder)*
  - `Assets/Tests/PlayMode/Core/` *(folder .meta)*
  - `Assets/Tests/PlayMode/Core/CorePlayModeTests.asmdef`
  - `Assets/Tests/PlayMode/Core/CorePlayModeTests.asmdef.meta`

---

### Task 2: Implement `PlayerState` with Edit Mode Tests (TDD)

**Files:**
- Create: `Assets/Tests/Editor/Core/PlayerStateTests.cs`
- Create: `Assets/Scripts/Core/PlayerState.cs`

- [ ] **Step 1: Write the failing tests**

Write this file to `Assets/Tests/Editor/Core/PlayerStateTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    public class PlayerStateTests
    {
        // ── Constructor ──────────────────────────────────────────────────────

        [Test]
        public void Constructor_SetsCurrentHpEqualToMaxHp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(100, state.CurrentHp);
        }

        [Test]
        public void Constructor_SetsCurrentMpEqualToMaxMp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(50, state.CurrentMp);
        }

        [Test]
        public void Constructor_SetsStats()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(10, state.Attack);
            Assert.AreEqual(5,  state.Defense);
            Assert.AreEqual(8,  state.Speed);
        }

        [Test]
        public void Constructor_ActiveSceneNameIsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(string.Empty, state.ActiveSceneName);
        }

        [Test]
        public void Constructor_InventoryIsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            Assert.AreEqual(0, state.InventoryItemIds.Count);
        }

        [Test]
        public void Constructor_ThrowsOnZeroMaxHp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 0, maxMp: 50, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeMaxHp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: -1, maxMp: 50, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeMaxMp()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: -1, attack: 10, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeAttack()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: -1, defense: 5, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeDefense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: -1, speed: 8));
        }

        [Test]
        public void Constructor_ThrowsOnNegativeSpeed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: -1));
        }

        // ── SetCurrentHp ─────────────────────────────────────────────────────

        [Test]
        public void SetCurrentHp_SetsExactValueWithinBounds()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(42);
            Assert.AreEqual(42, state.CurrentHp);
        }

        [Test]
        public void SetCurrentHp_ClampsToMaxHp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(150);
            Assert.AreEqual(100, state.CurrentHp);
        }

        [Test]
        public void SetCurrentHp_ClampsToZero()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentHp(-10);
            Assert.AreEqual(0, state.CurrentHp);
        }

        // ── SetCurrentMp ─────────────────────────────────────────────────────

        [Test]
        public void SetCurrentMp_SetsExactValueWithinBounds()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(25);
            Assert.AreEqual(25, state.CurrentMp);
        }

        [Test]
        public void SetCurrentMp_ClampsToMaxMp()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(999);
            Assert.AreEqual(50, state.CurrentMp);
        }

        [Test]
        public void SetCurrentMp_ClampsToZero()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetCurrentMp(-1);
            Assert.AreEqual(0, state.CurrentMp);
        }

        // ── SetActiveScene ────────────────────────────────────────────────────

        [Test]
        public void SetActiveScene_SetsName()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene("Platformer");
            Assert.AreEqual("Platformer", state.ActiveSceneName);
        }

        [Test]
        public void SetActiveScene_NullTreatedAsEmpty()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene(null);
            Assert.AreEqual(string.Empty, state.ActiveSceneName);
        }

        [Test]
        public void SetActiveScene_CanBeOverwritten()
        {
            var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            state.SetActiveScene("Platformer");
            state.SetActiveScene("World_01");
            Assert.AreEqual("World_01", state.ActiveSceneName);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

> **Unity Editor task (user):** Window → General → Test Runner → EditMode tab → Run All (or filter to `CoreTests`). Expected: all `PlayerStateTests` fail with a compile error — "The type or namespace name 'PlayerState' could not be found."

- [ ] **Step 3: Implement `PlayerState`**

Write this file to `Assets/Scripts/Core/PlayerState.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Axiom.Core
{
    public sealed class PlayerState
    {
        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }
        public int MaxMp { get; private set; }
        public int CurrentMp { get; private set; }
        public int Attack { get; private set; }
        public int Defense { get; private set; }
        public int Speed { get; private set; }
        public string ActiveSceneName { get; private set; }

        // Phase 5 (Data Layer) will replace List<string> with proper ItemData references.
        public List<string> InventoryItemIds { get; }

        public PlayerState(int maxHp, int maxMp, int attack, int defense, int speed)
        {
            if (maxHp <= 0)  throw new ArgumentOutOfRangeException(nameof(maxHp),  "maxHp must be greater than zero.");
            if (maxMp < 0)   throw new ArgumentOutOfRangeException(nameof(maxMp),  "maxMp cannot be negative.");
            if (attack < 0)  throw new ArgumentOutOfRangeException(nameof(attack),  "attack cannot be negative.");
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense), "defense cannot be negative.");
            if (speed < 0)   throw new ArgumentOutOfRangeException(nameof(speed),   "speed cannot be negative.");

            MaxHp = maxHp;
            CurrentHp = maxHp;
            MaxMp = maxMp;
            CurrentMp = maxMp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            ActiveSceneName = string.Empty;
            InventoryItemIds = new List<string>();
        }

        public void SetCurrentHp(int value) =>
            CurrentHp = Math.Clamp(value, 0, MaxHp);

        public void SetCurrentMp(int value) =>
            CurrentMp = Math.Clamp(value, 0, MaxMp);

        public void SetActiveScene(string sceneName) =>
            ActiveSceneName = sceneName ?? string.Empty;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

> **Unity Editor task (user):** Test Runner → EditMode → Run All. Expected: all `PlayerStateTests` pass (green checkmarks). Zero failures.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-31): implement PlayerState for cross-scene player data`

  - `Assets/Tests/Editor/Core/PlayerStateTests.cs`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs.meta`
  - `Assets/Scripts/Core/PlayerState.cs`
  - `Assets/Scripts/Core/PlayerState.cs.meta`

---

### Task 3: Implement `GameManager` with Play Mode Tests (TDD)

`GameManager`'s singleton guard and `DontDestroyOnLoad` depend on `Awake()` being called by the Unity runtime, which requires Play Mode. These tests use `[UnityTest]` and `yield return null` to advance one frame after `AddComponent` so Unity has called `Awake`.

**Files:**
- Create: `Assets/Tests/PlayMode/Core/GameManagerTests.cs`
- Create: `Assets/Scripts/Core/GameManager.cs`

- [ ] **Step 1: Write the failing Play Mode tests**

Write this file to `Assets/Tests/PlayMode/Core/GameManagerTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Axiom.Core;

namespace CoreTests.PlayMode
{
    public class GameManagerTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Awake_SetsSingletonInstance()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null; // wait one frame for Awake

            Assert.AreEqual(gm, GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator Awake_InitializesPlayerState()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            yield return null;

            Assert.IsNotNull(GameManager.Instance.PlayerState);
            Assert.Greater(GameManager.Instance.PlayerState.MaxHp, 0);
            Assert.Greater(GameManager.Instance.PlayerState.MaxMp, 0);
        }

        [UnityTest]
        public IEnumerator Awake_DestroysDuplicateInstance_KeepsFirst()
        {
            var go1 = new GameObject("GameManager1");
            var first = go1.AddComponent<GameManager>();
            yield return null;

            var go2 = new GameObject("GameManager2");
            go2.AddComponent<GameManager>();
            yield return null;

            // First instance must still be the singleton
            Assert.AreEqual(first, GameManager.Instance);
            // First GameObject must not have been destroyed
            Assert.IsTrue(go1 != null);
        }

        [UnityTest]
        public IEnumerator OnDestroy_ClearsInstance()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            yield return null;

            Object.Destroy(go);
            yield return null;

            Assert.IsNull(GameManager.Instance);
        }
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

> **Unity Editor task (user):** Test Runner → PlayMode tab → Run All (or filter to `CorePlayModeTests`). Expected: all `GameManagerTests` fail — "The type or namespace name 'GameManager' could not be found."

- [ ] **Step 3: Implement `GameManager`**

Write this file to `Assets/Scripts/Core/GameManager.cs`:

```csharp
using UnityEngine;

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

- [ ] **Step 4: Run tests — verify they pass**

> **Unity Editor task (user):** Test Runner → PlayMode tab → Run All. Expected: all four `GameManagerTests` pass. Zero failures.

- [ ] **Step 5: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-31): implement GameManager persistent singleton`

  - `Assets/Tests/PlayMode/Core/GameManagerTests.cs`
  - `Assets/Tests/PlayMode/Core/GameManagerTests.cs.meta`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Scripts/Core/GameManager.cs.meta`

---

### Task 4: Place `GameManager` Prefab in Scenes

The singleton guard allows the prefab to live in both scenes safely: if Battle is loaded additively after Platformer, the second `GameManager` is destroyed immediately in `Awake`. If a developer plays the Battle scene directly from the Editor without first passing through Platformer, the GameManager in Battle provides the singleton.

- [ ] **Step 1: Create the `Assets/Prefabs/` folder (if it doesn't exist)**

> **Unity Editor task (user):** In the Project window, check if `Assets/Prefabs/` already exists. If not, right-click `Assets/` → Create → Folder → name it `Prefabs`.

- [ ] **Step 2: Create the GameManager prefab**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Platformer.unity`.
> 2. In the Hierarchy, right-click → Create Empty → rename the new GameObject to `GameManager`.
> 3. With `GameManager` selected, in the Inspector → Add Component → type `GameManager` → select `Axiom.Core.GameManager`.
> 4. Drag the `GameManager` GameObject from the Hierarchy into `Assets/Prefabs/` to create `Assets/Prefabs/GameManager.prefab`.
> 5. Delete the original `GameManager` GameObject from the Hierarchy (you will re-add it as a prefab instance next).

- [ ] **Step 3: Add the prefab instance to the Platformer scene**

> **Unity Editor task (user):**
> 1. While `Assets/Scenes/Platformer.unity` is open, drag `Assets/Prefabs/GameManager.prefab` from the Project window into the Hierarchy.
> 2. Save the scene (Ctrl+S).

- [ ] **Step 4: Add the prefab instance to the Battle scene**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Drag `Assets/Prefabs/GameManager.prefab` into the Hierarchy.
> 3. Save the scene (Ctrl+S).

- [ ] **Step 5: Smoke test in Play Mode**

> **Unity Editor task (user):**
> 1. Open `Platformer.unity`, enter Play Mode.
> 2. Confirm the Console shows no errors and only one `GameManager` exists in the `DontDestroyOnLoad` scene (visible in the Hierarchy while in Play Mode).

- [ ] **Step 6: Check in via UVCS**

  Unity Version Control → Pending Changes → stage the files below → Check in with message: `chore(DEV-31): add GameManager prefab to Platformer and Battle scenes`

  - `Assets/Prefabs/` *(folder .meta — if new)*
  - `Assets/Prefabs/GameManager.prefab`
  - `Assets/Prefabs/GameManager.prefab.meta`
  - `Assets/Scenes/Platformer.unity`
  - `Assets/Scenes/Battle.unity`
