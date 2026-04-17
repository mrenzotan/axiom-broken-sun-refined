# DEV-64: Remove Hardcoded Player Vitals — Single Source of Truth from CharacterData SO Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `CharacterData` the single source of truth for the player's Level‑1 base stats. Remove the three competing override sources (`BattleController._playerStats` Inspector defaults, `GameManager` hardcoded `new PlayerState(100, 50, 10, 5, 8)`, direct `_playerData` read in `BattleController`) so the player enters battle with the values defined on `CD_Player_Kaelen` (currently `baseMaxHP=30, baseMaxMP=30, baseATK=12, baseDEF=6, baseSPD=8`).

**Architecture:**
- `GameManager` gains a `[SerializeField] CharacterData _playerCharacterData` plus a test-only injector. `EnsurePlayerState()` and `StartNewGame()` read stats from this SO; no numeric literals remain for player stats.
- `EnsurePlayerState()` is removed from `Awake()` and stays lazy (already accessed via the property getter), so tests can inject CharacterData after `AddComponent` and before the first `PlayerState` access.
- `BattleController._playerStats` becomes a plain private field (no `[SerializeField]`, no inline initializer). `Initialize()` logs an error and returns when `_playerData` is missing. When `GameManager.Instance` is present the stats are sourced from `PlayerState` (which itself was seeded from CharacterData); when it is absent (standalone Battle scene testing) the stats come directly from `_playerData`. The old triple-override chain collapses to a single branch.

**Tech Stack:** Unity 6 LTS (6000.0.x), C# 9, NUnit (Edit Mode), Unity Test Framework (Play Mode), UVCS (Unity Version Control) for check-ins.

**Jira:** [DEV-64](https://axiombrokensunrefined.atlassian.net/browse/DEV-64)

---

## File Map

| File | Change |
| --- | --- |
| `Assets/Scripts/Core/GameManager.cs` | Add `_playerCharacterData` serialized field + test-only setter; remove `EnsurePlayerState()` call from `Awake()`; rewrite `EnsurePlayerState()` and `StartNewGame()` to seed from CharacterData |
| `Assets/Scripts/Battle/BattleController.cs` | Remove `[SerializeField]` + inline initializer on `_playerStats`; add `Debug.LogError` for null `_playerData`; simplify Initialize() stat-source branches |
| `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs` | Add a test-CharacterData helper; inject it in `SetUp`; add new tests for CharacterData-driven seeding |
| `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | Inject test CharacterData in `SetUp` |
| `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs` | Inject test CharacterData in `SetUp` |
| `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs` | Inject test CharacterData in `SetUp` |
| `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs` | Inject test CharacterData in `SetUp` (fixture instantiates `GameManager` in `SetUp`; confirmed present) |
| `Assets/Tests/PlayMode/Core/GameManagerTests.cs` | Update tests to inject test CharacterData before first `PlayerState` access |
| `Assets/Prefabs/Core/GameManager.prefab` | **Unity Editor task (user):** Assign `CD_Player_Kaelen` to `GameManager._playerCharacterData` |

---

## Task 1: GameManager — Add `_playerCharacterData` field and test-only injector

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

**Context:** We need a Unity-serializable field for the production path (prefab assignment) and a test-only setter so Edit Mode / Play Mode tests can inject a runtime `CharacterData` without loading an asset. This is the same pattern already used for `SetSaveServiceForTests`.

- [ ] **Step 1: Add the serialized field above `_spellCatalog`**

In `Assets/Scripts/Core/GameManager.cs`, immediately after the existing `DefaultContinueScene` constant and `Instance` property (around line 27), add:

```csharp
        [SerializeField]
        [Tooltip("CharacterData for the player. Seeds PlayerState base stats on new game and lazy initialization. Assign CD_Player_Kaelen on the GameManager prefab.")]
        private Axiom.Data.CharacterData _playerCharacterData;
```

(Keep `_spellCatalog` where it is — do not reorder existing fields.)

- [ ] **Step 2: Add the test-only setter**

Find the existing `SetSaveServiceForTests` block (around line 340):

```csharp
#if UNITY_INCLUDE_TESTS
        public void SetSaveServiceForTests(SaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }
#endif
```

Replace it with:

```csharp
#if UNITY_INCLUDE_TESTS
        public void SetSaveServiceForTests(SaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        /// <summary>
        /// Test-only injector for the player CharacterData. Must be called after
        /// <c>AddComponent&lt;GameManager&gt;()</c> and before the first <see cref="PlayerState"/>
        /// access so the lazy <see cref="EnsurePlayerState"/> path can read base stats.
        /// </summary>
        public void SetPlayerCharacterDataForTests(Axiom.Data.CharacterData data)
        {
            _playerCharacterData = data
                ?? throw new ArgumentNullException(nameof(data));
        }
#endif
```

- [ ] **Step 3: Verify compilation**

In Unity, wait for the domain reload to finish. Expected: Console shows no compile errors. (No tests touch this field yet — that comes in Task 2.)

- [ ] **Step 4: No UVCS check-in yet.** This task is bundled with Task 2 for a single feat check-in.

---

## Task 2: GameManager — Remove `EnsurePlayerState()` from Awake and rewrite it to seed from CharacterData

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs` (lines 347–360 `Awake`, lines 371–375 `EnsurePlayerState`, lines 289–309 `StartNewGame`)
- Test: `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs` (new tests)

**Context:** With `_playerCharacterData` in place, `EnsurePlayerState()` and `StartNewGame()` must read stats from it. `EnsurePlayerState()` also needs to leave `_playerState` null and log an error when `_playerCharacterData` is missing, so the AC is met ("no hardcoded player stat values remain"). Removing the eager `EnsurePlayerState()` call from `Awake` is mandatory because tests inject `_playerCharacterData` *after* `AddComponent` (which triggers `Awake`); if `Awake` creates state first, it either reads null or reverts to hardcoded values — both violate the AC.

- [ ] **Step 1: Write failing tests for CharacterData-driven seeding**

At the top of `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`, add a `using Axiom.Data;` import if not present (it's not — confirmed). Inside the class, above `[Test] StartNewGame_ResetsPlayerLevelToOne`, add a helper and four new tests:

```csharp
        private CharacterData CreateTestCharacterData(
            int maxHp = 100,
            int maxMp = 50,
            int atk = 10,
            int def = 5,
            int spd = 8,
            string name = "TestPlayer")
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = name;
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK   = atk;
            cd.baseDEF   = def;
            cd.baseSPD   = spd;
            return cd;
        }

        [Test]
        public void EnsurePlayerState_SeedsMaxHpFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(maxHp: 77);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            Assert.AreEqual(77, _gameManager.PlayerState.MaxHp);
        }

        [Test]
        public void EnsurePlayerState_SeedsAllBaseStatsFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(
                maxHp: 42, maxMp: 13, atk: 7, def: 3, spd: 11);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            PlayerState ps = _gameManager.PlayerState;
            Assert.AreEqual(42, ps.MaxHp);
            Assert.AreEqual(13, ps.MaxMp);
            Assert.AreEqual(7,  ps.Attack);
            Assert.AreEqual(3,  ps.Defense);
            Assert.AreEqual(11, ps.Speed);
        }

        [Test]
        public void EnsurePlayerState_LogsError_AndReturnsNullState_WhenCharacterDataMissing()
        {
            // No SetPlayerCharacterDataForTests() call — _playerCharacterData stays null.
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            PlayerState state = _gameManager.PlayerState;

            Assert.IsNull(state);
        }

        [Test]
        public void StartNewGame_SeedsMaxHpFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(maxHp: 30);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            _gameManager.StartNewGame();

            Assert.AreEqual(30, _gameManager.PlayerState.MaxHp);
        }

        [Test]
        public void StartNewGame_LogsError_WhenCharacterDataMissing()
        {
            // Discard the SetUp-injected GameManager and build a fresh one with no CD.
            UnityEngine.Object.DestroyImmediate(_gameManagerObject);
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            _gameManager.StartNewGame();

            Assert.IsNull(_gameManager.PlayerState);
        }
```

Also add `using UnityEngine.TestTools;` at the top of the file if not present (it isn't — confirmed by reading the file).

- [ ] **Step 2: Inject test CharacterData in the existing SetUp so existing tests keep working**

Change the existing `SetUp()` method body in `GameManagerNewGameTests.cs` from:

```csharp
        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);

            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
        }
```

to:

```csharp
        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);

            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }
```

This preserves the previous effective defaults (`maxHp=100, maxMp=50, attack=10, defense=5, speed=8, name="TestPlayer"`) so all existing assertions in this file continue to pass — in particular `StartNewGame_ReturnsMaxHpOf100` and `StartNewGame_ResetsCurrentMpToMaxMp`.

The one exception is `EnsurePlayerState_LogsError_AndReturnsNullState_WhenCharacterDataMissing`, which deliberately needs `_playerCharacterData` to be null. Override SetUp locally in the test body by nulling the serialized field via reflection-free means is not possible; instead, immediately after SetUp runs we need a way to clear the data. Simplest fix: add an optional overload at the top of the test class so this single test can create a *fresh* GameManager with no CD injected. Replace the newly-written `EnsurePlayerState_LogsError_AndReturnsNullState_WhenCharacterDataMissing` with:

```csharp
        [Test]
        public void EnsurePlayerState_LogsError_AndReturnsNullState_WhenCharacterDataMissing()
        {
            // Discard the SetUp-injected GameManager and build a fresh one with no CD.
            UnityEngine.Object.DestroyImmediate(_gameManagerObject);
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            PlayerState state = _gameManager.PlayerState;

            Assert.IsNull(state);
        }
```

- [ ] **Step 3: Run the new tests to confirm they FAIL**

Open Unity → `Window → General → Test Runner → EditMode` → run only the five new tests.

Expected: all five fail. Reasons (any of):
- Compile error on `CharacterData cd = ...` because the `using Axiom.Data;` directive is missing (add it if you skipped Step 1's note).
- `EnsurePlayerState_SeedsMaxHpFromCharacterData`: fails with `AreEqual(77, 100)` because `EnsurePlayerState` still returns the hardcoded `new PlayerState(100, ...)`.
- `EnsurePlayerState_SeedsAllBaseStatsFromCharacterData`: fails similarly on `Attack` / `Defense` / `Speed`.
- `EnsurePlayerState_LogsError_...`: fails because current code silently creates a hardcoded PlayerState instead of logging.
- `StartNewGame_SeedsMaxHpFromCharacterData`: fails similarly.
- `StartNewGame_LogsError_WhenCharacterDataMissing`: fails because current code silently creates `new PlayerState(100, 50, ...)` instead of logging and skipping.

- [ ] **Step 4: Remove the eager `EnsurePlayerState()` call from Awake**

In `Assets/Scripts/Core/GameManager.cs`, change the `Awake` method (currently lines 347–360):

```csharp
private void Awake()
{
    if (Instance != null)
    {
        Destroy(gameObject);
        return;
    }

    Instance = this;
    if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
    EnsurePlayerState();
    EnsureSaveService();
}
```

to:

```csharp
private void Awake()
{
    if (Instance != null)
    {
        Destroy(gameObject);
        return;
    }

    Instance = this;
    if (Application.isPlaying)
        DontDestroyOnLoad(gameObject);
    EnsureSaveService();
}
```

(Drop only the `EnsurePlayerState()` line. Keep `EnsureSaveService()` — it has no CharacterData dependency.)

- [ ] **Step 5: Rewrite `EnsurePlayerState()` to seed from CharacterData**

Replace the existing `EnsurePlayerState` (currently lines 371–375):

```csharp
private void EnsurePlayerState()
{
    if (_playerState == null)
        _playerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
}
```

with:

```csharp
private void EnsurePlayerState()
{
    if (_playerState != null) return;
    if (_playerCharacterData == null)
    {
        Debug.LogError(
            "[GameManager] _playerCharacterData is not assigned. Assign CD_Player_Kaelen on the GameManager prefab.",
            this);
        return;
    }

    _playerState = new PlayerState(
        maxHp:   _playerCharacterData.baseMaxHP,
        maxMp:   _playerCharacterData.baseMaxMP,
        attack:  _playerCharacterData.baseATK,
        defense: _playerCharacterData.baseDEF,
        speed:   _playerCharacterData.baseSPD);
}
```

- [ ] **Step 6: Rewrite `StartNewGame()` to seed from CharacterData**

Replace the body of `StartNewGame` (currently lines 289–309). The only line that changes is the `PlayerState = new PlayerState(...)` literal on line 291. Change:

```csharp
public void StartNewGame()
{
    PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    ClearPendingBattle();
    ...
}
```

to:

```csharp
public void StartNewGame()
{
    if (_playerCharacterData == null)
    {
        Debug.LogError(
            "[GameManager] _playerCharacterData is not assigned. Cannot start a new game without base stats.",
            this);
        return;
    }

    PlayerState = new PlayerState(
        maxHp:   _playerCharacterData.baseMaxHP,
        maxMp:   _playerCharacterData.baseMaxMP,
        attack:  _playerCharacterData.baseATK,
        defense: _playerCharacterData.baseDEF,
        speed:   _playerCharacterData.baseSPD);
    ClearPendingBattle();
    ClearWorldSnapshot();
    ClearDefeatedEnemies();
    ClearAllDamagedEnemyHp();

    EnsureSaveService();
    _saveService.DeleteSave();

    EnsureSpellUnlockService();
    if (_spellUnlockService != null)
    {
        // Reset the service by restoring from an empty list, then grant level-1 starters.
        _spellUnlockService.RestoreFromIds(Array.Empty<string>());
        _spellUnlockService.NotifyPlayerLevel(PlayerState.Level);
    }

    LoadScene("Platformer");
}
```

(Preserve every existing side-effect line — only the opening `new PlayerState(...)` literal and the new null guard change.)

- [ ] **Step 7: Run the five new tests to confirm they PASS**

In the EditMode Test Runner, re-run the five tests. Expected: all five pass.

- [ ] **Step 8: Run the entire `GameManagerNewGameTests` class to confirm no regressions**

Expected: all existing tests (`StartNewGame_ResetsPlayerLevelToOne`, `..._ResetsXpToZero`, `..._ResetsCurrentHpToMaxHp`, `..._ResetsCurrentMpToMaxMp`, `..._ClearsUnlockedSpells`, `..._ClearsInventory`, `..._ReturnsMaxHpOf100`, `..._ClearsDefeatedEnemies`, `..._ClearsWorldSnapshot`, `..._DeletesExistingSaveFile`, `..._ClearsDamagedEnemyHp`) remain green because `SetUp` now injects a test CharacterData with `maxHp=100, maxMp=50`.

- [ ] **Step 9: Do not check in yet.** Remaining test files (SaveData, PendingBattle, WorldSnapshot, Transition, PlayMode) will break on `NullReferenceException` inside `EnsurePlayerState` — Task 3 fixes them.

---

## Task 3: Update the rest of the GameManager tests to inject a test CharacterData

**Files:**
- Modify: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs`
- Modify: `Assets/Tests/PlayMode/Core/GameManagerTests.cs`

**Context:** Every test fixture that instantiates a fresh `GameManager` now needs to inject a test CharacterData before its tests access `PlayerState` (or anything that calls `EnsurePlayerState` transitively — `ApplySaveData`, `BuildSaveData`, `TryActivateCheckpointRegen`, `CaptureWorldSnapshot`). Use a shared `CreateTestCharacterData` helper inlined in each fixture (these tests already duplicate their own SetUp boilerplate — matching that pattern).

- [ ] **Step 1: `GameManagerSaveDataTests.cs` — add helper and update SetUp**

Open `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`. Note the namespace is `CoreTests` (not `Axiom.Tests.Editor.Core`) — do not "fix" this; leave it alone.

Add near the bottom of the class, just above the existing `CreateTempSaveService` helper (around line 514):

```csharp
        private CharacterData CreateTestCharacterData(
            int maxHp = 100,
            int maxMp = 50,
            int atk = 10,
            int def = 5,
            int spd = 8,
            string name = "TestPlayer")
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = name;
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK   = atk;
            cd.baseDEF   = def;
            cd.baseSPD   = spd;
            return cd;
        }
```

Change the existing `SetUp`:

```csharp
        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
        }
```

to:

```csharp
        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }
```

Confirm `using Axiom.Data;` is present at the top of the file — it already is (line 5).

- [ ] **Step 2: `GameManagerPendingBattleTests.cs` — add helper and update SetUp**

Open `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`. Confirm `using Axiom.Data;` is already present (it is — line 4).

At the bottom of the class, add the same helper:

```csharp
        private CharacterData CreateTestCharacterData()
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = 100;
            cd.baseMaxMP = 50;
            cd.baseATK   = 10;
            cd.baseDEF   = 5;
            cd.baseSPD   = 8;
            return cd;
        }
```

Change `SetUp` from:

```csharp
        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
        }
```

to:

```csharp
        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }
```

- [ ] **Step 3: `GameManagerWorldSnapshotTests.cs` — covered together with Step 4**

This fixture uses the same `_go` / `_gm` pattern as `GameManagerTransitionTests`. The change is identical and is described in Step 4 (a single code block covers both).

- [ ] **Step 4: `GameManagerTransitionTests.cs` — add helper and update SetUp**

Open `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs`. The fixture fields are `_go` and `_gm` (not `_gameManagerObject` / `_gameManager`). `SetUp` already uses `AddComponent<GameManager>()` — append the injector call as the new last line. Add `using Axiom.Data;` to the usings at the top.

Add the helper inside the class (pattern identical to the one in `GameManagerPendingBattleTests`):

```csharp
        private CharacterData CreateTestCharacterData()
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = 100;
            cd.baseMaxMP = 50;
            cd.baseATK   = 10;
            cd.baseDEF   = 5;
            cd.baseSPD   = 8;
            return cd;
        }
```

Change `SetUp` from:

```csharp
        [SetUp]
        public void SetUp()
        {
            // Destroy any stale Instance from an interrupted previous run so the
            // singleton guard in Awake never fires unexpectedly.
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
        }
```

to:

```csharp
        [SetUp]
        public void SetUp()
        {
            // Destroy any stale Instance from an interrupted previous run so the
            // singleton guard in Awake never fires unexpectedly.
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }
```

Apply the same change verbatim to `GameManagerWorldSnapshotTests.cs` (same field names `_go` / `_gm`, same `SetUp` shape). Add `using Axiom.Data;` to that file too.

- [ ] **Step 5: `GameManagerTests.cs` (PlayMode) — update tests to inject CharacterData before PlayerState access**

Open `Assets/Tests/PlayMode/Core/GameManagerTests.cs`. Replace the entire file body (keep the namespace and usings, but replace the test methods) with:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Axiom.Core;
using Axiom.Data;

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

        private static CharacterData CreateTestCharacterData()
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = 100;
            cd.baseMaxMP = 50;
            cd.baseATK   = 10;
            cd.baseDEF   = 5;
            cd.baseSPD   = 8;
            return cd;
        }

        [UnityTest]
        public IEnumerator Awake_SetsSingletonInstance()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            Assert.AreEqual(gm, GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator PlayerState_IsLazilyInitialized_FromCharacterData()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
            yield return null;

            Assert.IsNotNull(gm.PlayerState);
            Assert.Greater(gm.PlayerState.MaxHp, 0);
            Assert.Greater(gm.PlayerState.MaxMp, 0);
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

            Assert.AreEqual(first, GameManager.Instance);
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

Notes on this rewrite:
- The original `Awake_InitializesPlayerState` test is renamed to `PlayerState_IsLazilyInitialized_FromCharacterData` because `PlayerState` is no longer created in `Awake` — it's created lazily on first property access.
- The other three tests (`Awake_SetsSingletonInstance`, `Awake_DestroysDuplicateInstance_KeepsFirst`, `OnDestroy_ClearsInstance`) do not touch `PlayerState` and therefore do not need CharacterData injection.

- [ ] **Step 6: Run the Edit Mode test suite**

Unity → `Window → General → Test Runner → EditMode → Run All`. Expected: every `GameManager*Tests` class passes. No red tests. The four new tests from Task 2 remain green.

- [ ] **Step 7: Run the Play Mode test suite**

Unity → `Window → General → Test Runner → PlayMode → Run All`. Expected: `CoreTests.PlayMode.GameManagerTests` passes all four tests including the renamed one.

- [ ] **Step 8: Check in via UVCS:**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-64): seed player base stats from CharacterData in GameManager`

- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/Core/GameManager.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`
- `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs`
- `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs`
- `Assets/Tests/Editor/Core/GameManagerWorldSnapshotTests.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs` *(only if modified in Step 4)*
- `Assets/Tests/Editor/Core/GameManagerTransitionTests.cs.meta` *(only if modified in Step 4)*
- `Assets/Tests/PlayMode/Core/GameManagerTests.cs`
- `Assets/Tests/PlayMode/Core/GameManagerTests.cs.meta`

---

## Task 4: Unity Editor — Assign CD_Player_Kaelen to GameManager prefab

**Files:**
- Modify (scene/prefab): `Assets/Prefabs/Core/GameManager.prefab`

**Context:** Until the production prefab has a CharacterData assigned, every Play Mode session will hit the new `Debug.LogError` and run with a null `PlayerState`. The asset `Assets/Data/Characters/CD_Player_Kaelen.asset` already exists and has `baseMaxHP=30, baseMaxMP=30, baseATK=12, baseDEF=6, baseSPD=8`.

- [ ] **Step 1:**

> **Unity Editor task (user):** In the Project window, open `Assets/Prefabs/Core/GameManager.prefab`. In the Inspector, find the `GameManager` component's new `Player Character Data` slot. Drag `Assets/Data/Characters/CD_Player_Kaelen.asset` into the slot. Click *Overrides → Apply to Base Prefab* if editing an instance. Save the project (*File → Save Project* or Ctrl/Cmd+S).

- [ ] **Step 2: Verify in Play Mode**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity`, press Play, and trigger a battle by colliding with an enemy. In the Console, confirm **no** `[GameManager] _playerCharacterData is not assigned` error appears. In the Battle scene, confirm the HP bar displays 30/30 (matching CD_Player_Kaelen) rather than 100/50.

- [ ] **Step 3: Check in via UVCS:**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-64): assign CD_Player_Kaelen on GameManager prefab`

- `Assets/Prefabs/Core/GameManager.prefab`

*(No `.meta` changes expected for prefab-field edits; if UVCS shows a `.prefab.meta` change, include it too.)*

---

## Task 5: BattleController — Remove hardcoded player stats and simplify the Initialize() override chain

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs` (field declaration at lines 30–33, `Initialize` method at lines 233–333)

**Context:** With GameManager now providing correct base stats through `PlayerState`, the `BattleController._playerStats` Inspector defaults are dead weight — they mask misconfiguration. The AC requires: (a) strip `[SerializeField]` + inline initializer from `_playerStats`, (b) log an error when `_playerData` is null, (c) reduce the triple-override chain (Inspector defaults → CharacterData → PlayerState) to a single source path. When `GameManager.Instance` is available, `PlayerState` is authoritative (it is already seeded from CharacterData via Task 2). When it is not (standalone Battle-scene testing), `_playerData` is authoritative.

No new Edit Mode tests are added for `BattleController` because it is a MonoBehaviour and the existing code paths have no test coverage at this level. Verification is done by running the existing test suite (to prove no regressions) plus a Play Mode smoke test.

- [ ] **Step 1: Remove `[SerializeField]` and the inline initializer from `_playerStats`**

In `Assets/Scripts/Battle/BattleController.cs`, replace lines 30–33:

```csharp
        [SerializeField]
        [Tooltip("Player stats. Populated from _playerData at Initialize(); fallback Inspector values used when _playerData is unassigned (standalone testing only).")]
        private CharacterStats _playerStats = new CharacterStats
            { Name = "Kael", MaxHP = 100, MaxMP = 30, ATK = 12, DEF = 6, SPD = 8 };
```

with:

```csharp
        private CharacterStats _playerStats;
```

- [ ] **Step 2: Simplify `Initialize()`'s player-stat source chain**

Find the player-stat population block inside `Initialize` (currently lines 257–282):

```csharp
            if (_playerData != null)
            {
                _playerStats.Name  = _playerData.characterName;
                _playerStats.MaxHP = _playerData.baseMaxHP;
                _playerStats.MaxMP = _playerData.baseMaxMP;
                _playerStats.ATK   = _playerData.baseATK;
                _playerStats.DEF   = _playerData.baseDEF;
                _playerStats.SPD   = _playerData.baseSPD;
            }

            if (_enemyData != null)
            {
                _enemyStats.Name  = _enemyData.enemyName;
                _enemyStats.MaxHP = _enemyData.maxHP;
                _enemyStats.MaxMP = _enemyData.maxMP;
                _enemyStats.ATK   = _enemyData.atk;
                _enemyStats.DEF   = _enemyData.def;
                _enemyStats.SPD   = _enemyData.spd;
            }

            if (GameManager.Instance != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                _playerStats.MaxHP = ps.MaxHp;
                _playerStats.MaxMP = ps.MaxMp;
            }
```

Replace only the `_playerData`-sourced block and the trailing `GameManager.Instance` override (the enemy block stays unchanged). Result:

```csharp
            if (_playerData == null)
            {
                Debug.LogError(
                    "[Battle] _playerData is null. Assign CD_Player_Kaelen on the BattleController in the Battle scene.",
                    this);
                return;
            }

            _playerStats = new CharacterStats { Name = _playerData.characterName };

            if (GameManager.Instance != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                if (ps == null)
                {
                    Debug.LogError(
                        "[Battle] GameManager.PlayerState is null — check that CharacterData is assigned on the GameManager prefab.",
                        this);
                    return;
                }
                _playerStats.MaxHP = ps.MaxHp;
                _playerStats.MaxMP = ps.MaxMp;
                _playerStats.ATK   = ps.Attack;
                _playerStats.DEF   = ps.Defense;
                _playerStats.SPD   = ps.Speed;
            }
            else
            {
                // Standalone Battle scene testing (no GameManager in scene).
                _playerStats.MaxHP = _playerData.baseMaxHP;
                _playerStats.MaxMP = _playerData.baseMaxMP;
                _playerStats.ATK   = _playerData.baseATK;
                _playerStats.DEF   = _playerData.baseDEF;
                _playerStats.SPD   = _playerData.baseSPD;
            }

            if (_enemyData != null)
            {
                _enemyStats.Name  = _enemyData.enemyName;
                _enemyStats.MaxHP = _enemyData.maxHP;
                _enemyStats.MaxMP = _enemyData.maxMP;
                _enemyStats.ATK   = _enemyData.atk;
                _enemyStats.DEF   = _enemyData.def;
                _enemyStats.SPD   = _enemyData.spd;
            }
```

Note the reorder: the early-return null guard for `_playerData` comes *before* any usage of `_playerData`, and the legacy "GameManager overrides after CharacterData" logic has collapsed into a single `if/else` — one source, single authority, per the AC.

- [ ] **Step 3: Verify compilation**

In Unity, wait for the domain reload. Expected: no compile errors. If a red error appears on `_playerStats.MaxHP = ...` lines, re-check Step 1 — the field must still be `private CharacterStats _playerStats;` (same name, same type) so Inspector references on the Battle scene continue to match.

- [ ] **Step 4: Run the full Edit Mode test suite**

Unity → Test Runner → EditMode → Run All. Expected: all GameManager tests from Task 3 still pass. `BattleTests.asmdef` tests (CharacterStats, SpellEffectResolver, SpellResultMatcher, etc.) unchanged and passing. Data, Voice, Platformer, Core, UI, SceneSetup, LevelBuilder suites unchanged.

- [ ] **Step 5: Run the Play Mode smoke test**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Battle.unity`. In the Hierarchy, select the `BattleController` GameObject.
> 2. Confirm the `Player Data` field holds `CD_Player_Kaelen`. Confirm the `Player Stats` fold-out no longer appears in the Inspector (the `[SerializeField]` was removed).
> 3. Press Play on the Battle scene directly (standalone path — no GameManager). Verify the HP bar reads 30/30 (from CD_Player_Kaelen), not 100/30.
> 4. Stop Play. Open `Assets/Scenes/Platformer.unity`. Play from there and trigger a battle by colliding with an enemy. Verify the HP bar reads 30/30 (from GameManager.PlayerState, seeded from CD_Player_Kaelen).
> 5. In the Inspector, temporarily clear the `Player Data` field on BattleController. Re-run the Battle scene. Confirm the Console shows the exact error `[Battle] _playerData is null. Assign CD_Player_Kaelen on the BattleController in the Battle scene.` Re-assign the field before moving on.

- [ ] **Step 6: Check in via UVCS:**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-64): single-source player stats in BattleController from PlayerState`

- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/BattleController.cs.meta`
- `Assets/Scenes/Battle.unity` *(only if the Inspector re-serialized `_playerStats` removal — check Pending Changes; include if listed)*

---

## Task 6: Final verification

**Files:** None (verification only).

**Context:** Confirm every AC bullet is green end-to-end before closing the ticket.

- [ ] **Step 1: AC sweep**

Walk the Jira ACs one by one and check each against the code:
1. `GameManager` accepts a `CharacterData` serialized field and uses it in `EnsurePlayerState()` and `StartNewGame()` → `GameManager.cs` has `[SerializeField] private CharacterData _playerCharacterData;` and both methods read from it. ✓
2. No hardcoded player stat values remain in `GameManager.cs` → `grep` for numeric literals against `new PlayerState(` — should find zero matches. ✓
3. `[SerializeField]` and inline initializer removed from `_playerStats` in `BattleController.cs` → field is `private CharacterStats _playerStats;`. ✓
4. `Debug.LogError` in `BattleController.Initialize()` if `_playerData` is null → new guard at the top of the stat block. ✓
5. Triple-override simplified to single authority → `Initialize()` has one `if/else` for the stat source, not three sequential writes. ✓
6. Existing editor tests pass → confirmed in Task 3 Step 6 and Task 5 Step 4.

- [ ] **Step 2: Transition Jira ticket to Done**

> **Unity Editor / Jira task (user):** Move DEV-64 to `Done` in the board. Paste the UVCS change-set IDs into the ticket comment for traceability.

---

## Self-Review Notes

- **Spec coverage:** All 6 acceptance criteria map to at least one task (ACs 1–2 → Tasks 1–2; AC 6 → Task 3; AC 3 → Task 5 Step 1; AC 4 → Task 5 Step 2; AC 5 → Task 5 Step 2; prefab wiring → Task 4).
- **Placeholder scan:** No "TBD" / "similar to" / "add error handling" placeholders. Every step either changes named code or specifies an exact Unity Editor action.
- **Type consistency:** `CharacterData` is referenced as `Axiom.Data.CharacterData` in `GameManager.cs` (matching the existing `Axiom.Data.EnemyData` usage pattern on `BattleController`) and as `CharacterData` in test files that already `using Axiom.Data;`. `PlayerState` properties `Attack`, `Defense`, `Speed` (confirmed from `Assets/Scripts/Core/PlayerState.cs:12–14`) match the `CharacterStats.ATK/DEF/SPD` fields. Method name `SetPlayerCharacterDataForTests` used consistently in GameManager definition and all test call sites.
