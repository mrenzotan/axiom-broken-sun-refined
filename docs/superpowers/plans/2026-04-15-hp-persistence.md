# DEV-63: Persist Player and Enemy HP Across Battle Transitions Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure combat HP is truly persistent — player and enemy HP survives battle return transitions and full save/load cycles.

**Architecture:** Battle-end HP sync copies `CharacterStats` runtime values into `GameManager.PlayerState` (player) and a new `_damagedEnemyHp` dictionary (enemies) before any `PersistToDisk()` call. Battle-start HP init reads those values back instead of resetting to max. A new `EnemyHpSaveEntry[]` field in `SaveData` handles disk serialization for damaged enemies.

**Tech Stack:** Unity 6 LTS, C#, NUnit (Edit Mode tests), JsonUtility (save/load), UVCS (version control)

**Jira:** [DEV-63](https://axiombrokensunrefined.atlassian.net/browse/DEV-63)

---

## Task 1: CharacterStats — Accept Optional Starting HP/MP in Initialize

**Files:**
- Modify: `Assets/Scripts/Battle/CharacterStats.cs` (Initialize method, lines 98-109)
- Test: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`

**Context:** `CharacterStats.Initialize()` currently resets `CurrentHP = MaxHP` and `CurrentMP = MaxMP` unconditionally. We need an overload that accepts optional starting values so `BattleController` can feed in persistent HP/MP from `PlayerState` or `BattleEntry`.

**Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`:

```csharp
// ---- Initialize with optional startHp/startMp ----

[Test]
public void Initialize_WithStartHp_UsesProvidedValue()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize(startHp: 75);
    Assert.AreEqual(75, stats.CurrentHP);
}

[Test]
public void Initialize_WithStartHp_ClampsToMaxHP()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize(startHp: 999);
    Assert.AreEqual(100, stats.CurrentHP);
}

[Test]
public void Initialize_WithStartHp_ClampsToZero()
{
    var stats = MakeStats(maxHp: 100);
    stats.Initialize(startHp: -5);
    Assert.AreEqual(0, stats.CurrentHP);
}

[Test]
public void Initialize_WithStartMp_UsesProvidedValue()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize(startMp: 20);
    Assert.AreEqual(20, stats.CurrentMP);
}

[Test]
public void Initialize_WithStartMp_ClampsToMaxMP()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize(startMp: 999);
    Assert.AreEqual(30, stats.CurrentMP);
}

[Test]
public void Initialize_WithStartMp_ClampsToZero()
{
    var stats = MakeStats(maxMp: 30);
    stats.Initialize(startMp: -1);
    Assert.AreEqual(0, stats.CurrentMP);
}

[Test]
public void Initialize_WithStartHpAndInnateConditions_BothApply()
{
    var stats = MakeStats(maxHp: 100);
    var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid };
    stats.Initialize(innateConditions: innate, startHp: 60);
    Assert.AreEqual(60, stats.CurrentHP);
    Assert.IsTrue(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
}

[Test]
public void Initialize_WithNullStartHp_DefaultsToMaxHP()
{
    var stats = MakeStats(maxHp: 80);
    stats.Initialize(startHp: null);
    Assert.AreEqual(80, stats.CurrentHP);
}

[Test]
public void Initialize_WithNullStartMp_DefaultsToMaxMP()
{
    var stats = MakeStats(maxMp: 40);
    stats.Initialize(startMp: null);
    Assert.AreEqual(40, stats.CurrentMP);
}
```

**Step 2: Run tests in Unity Test Runner → expect 9 FAIL (compile error — parameter doesn't exist yet)**

**Step 3: Implement the change**

In `Assets/Scripts/Battle/CharacterStats.cs`, replace the `Initialize` method signature and first three lines:

Replace:
```csharp
public void Initialize(List<ChemicalCondition> innateConditions = null)
{
    CurrentHP = MaxHP;
    CurrentMP = MaxMP;
    ShieldHP  = 0;
```

With:
```csharp
public void Initialize(List<ChemicalCondition> innateConditions = null,
                        int? startHp = null, int? startMp = null)
{
    CurrentHP = startHp.HasValue ? Math.Clamp(startHp.Value, 0, MaxHP) : MaxHP;
    CurrentMP = startMp.HasValue ? Math.Clamp(startMp.Value, 0, MaxMP) : MaxMP;
    ShieldHP  = 0;
```

The rest of Initialize stays unchanged.

**Step 4: Run tests in Unity Test Runner → expect all CharacterStatsTests PASS (existing + new)**

**Step 5: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): add optional startHp/startMp to CharacterStats.Initialize`
> - `Assets/Scripts/Battle/CharacterStats.cs`
> - `Assets/Scripts/Battle/CharacterStats.cs.meta`
> - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
> - `Assets/Tests/Editor/Battle/CharacterStatsTests.cs.meta`

---

## Task 2: BattleEntry — Add EnemyCurrentHp Property

**Files:**
- Modify: `Assets/Scripts/Data/BattleEntry.cs`
- Test: `Assets/Tests/Editor/Battle/BattleEntryTests.cs`

**Context:** `BattleEntry` carries cross-scene battle context. We need to carry the enemy's current HP so a fled battle can resume with the correct enemy HP.

**Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Battle/BattleEntryTests.cs`:

```csharp
[Test]
public void Constructor_DefaultEnemyCurrentHp_IsNegativeOne()
{
    var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null);

    Assert.AreEqual(-1, entry.EnemyCurrentHp);
}

[Test]
public void Constructor_StoresEnemyCurrentHp_WhenProvided()
{
    var entry = new BattleEntry(CombatStartState.Advantaged, enemyData: null,
                                enemyId: "e1", enemyCurrentHp: 42);

    Assert.AreEqual(42, entry.EnemyCurrentHp);
}

[Test]
public void Constructor_StoresEnemyId_WithEnemyCurrentHp()
{
    var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null,
                                enemyId: "e2", enemyCurrentHp: 10);

    Assert.AreEqual("e2", entry.EnemyId);
    Assert.AreEqual(10, entry.EnemyCurrentHp);
}
```

**Step 2: Run tests → expect FAIL (compile error — parameter/property doesn't exist)**

**Step 3: Implement**

Replace `Assets/Scripts/Data/BattleEntry.cs` entirely:

```csharp
namespace Axiom.Data
{
    /// <summary>
    /// Cross-scene battle context. Set by the overworld trigger before loading the Battle
    /// scene; consumed and cleared by BattleController.Start() on Battle scene load.
    ///
    /// EnemyData may be null — BattleController falls back to its Inspector-configured
    /// stats when null, preserving standalone Battle scene testing.
    ///
    /// EnemyCurrentHp: -1 means "use max HP from EnemyData" (fresh encounter).
    /// A value >= 0 means "resume at this HP" (re-engaging a damaged enemy after flee).
    /// </summary>
    public sealed class BattleEntry
    {
        public CombatStartState StartState { get; }
        public EnemyData EnemyData { get; }
        public string EnemyId { get; }
        public int EnemyCurrentHp { get; }

        public BattleEntry(CombatStartState startState, EnemyData enemyData,
                           string enemyId = null, int enemyCurrentHp = -1)
        {
            StartState = startState;
            EnemyData = enemyData;
            EnemyId = enemyId;
            EnemyCurrentHp = enemyCurrentHp;
        }
    }
}
```

**Step 4: Run tests → expect all BattleEntryTests PASS**

**Step 5: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): add EnemyCurrentHp to BattleEntry`
> - `Assets/Scripts/Data/BattleEntry.cs`
> - `Assets/Scripts/Data/BattleEntry.cs.meta`
> - `Assets/Tests/Editor/Battle/BattleEntryTests.cs`
> - `Assets/Tests/Editor/Battle/BattleEntryTests.cs.meta`

---

## Task 3: SaveData — Add EnemyHpSaveEntry Struct and Field

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Test: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

**Context:** `SaveData` needs to serialize damaged enemy HP so a quit-and-continue after fleeing restores enemy HP correctly. `JsonUtility` will deserialize missing fields to their default values, so old saves load safely.

**Step 1: Implement**

In `Assets/Scripts/Data/SaveData.cs`, add the struct **before** the `SaveData` class, and add the field inside the class:

```csharp
using System;

namespace Axiom.Data
{
    [Serializable]
    public struct InventorySaveEntry
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public struct EnemyHpSaveEntry
    {
        public string enemyId;
        public int currentHp;
    }

    /// <summary>
    /// Disk-serializable snapshot only. No UnityEngine.Object references.
    /// Field names are stable for JSON on disk.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int saveVersion = 1;

        public int playerLevel;
        public int playerXp;
        public int currentHp;
        public int currentMp;
        public int maxHp;
        public int maxMp;

        public string[] unlockedSpellIds = Array.Empty<string>();
        public InventorySaveEntry[] inventory = Array.Empty<InventorySaveEntry>();

        public float worldPositionX;
        public float worldPositionY;
        public string activeSceneName = string.Empty;

        public string[] activatedCheckpointIds = Array.Empty<string>();

        public string[] defeatedEnemyIds = Array.Empty<string>();

        public EnemyHpSaveEntry[] damagedEnemyHp = Array.Empty<EnemyHpSaveEntry>();
    }
}
```

**Step 2: Verify compile succeeds in Unity**

**Step 3: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): add EnemyHpSaveEntry to SaveData`
> - `Assets/Scripts/Data/SaveData.cs`
> - `Assets/Scripts/Data/SaveData.cs.meta`

---

## Task 4: GameManager — Damaged Enemy HP Tracking + Save/Load

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Test: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

**Context:** `GameManager` needs a persistent dictionary mapping `enemyId → currentHp` for enemies the player fled from. This integrates with `BuildSaveData` / `ApplySaveData` for disk persistence.

**Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`:

```csharp
// ---- Damaged Enemy HP ----

[Test]
public void GetDamagedEnemyHp_ReturnsNegativeOne_WhenNotTracked()
{
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("unknown_enemy"));
}

[Test]
public void GetDamagedEnemyHp_ReturnsNegativeOne_ForNullId()
{
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(null));
}

[Test]
public void GetDamagedEnemyHp_ReturnsNegativeOne_ForEmptyId()
{
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(string.Empty));
}

[Test]
public void SetDamagedEnemyHp_ThenGet_ReturnsStoredValue()
{
    _gameManager.SetDamagedEnemyHp("enemy_slime", 35);
    Assert.AreEqual(35, _gameManager.GetDamagedEnemyHp("enemy_slime"));
}

[Test]
public void SetDamagedEnemyHp_OverwritesPreviousValue()
{
    _gameManager.SetDamagedEnemyHp("enemy_slime", 35);
    _gameManager.SetDamagedEnemyHp("enemy_slime", 10);
    Assert.AreEqual(10, _gameManager.GetDamagedEnemyHp("enemy_slime"));
}

[Test]
public void SetDamagedEnemyHp_NullId_IsNoOp()
{
    _gameManager.SetDamagedEnemyHp(null, 50);
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(null));
}

[Test]
public void ClearDamagedEnemyHp_RemovesEntry()
{
    _gameManager.SetDamagedEnemyHp("enemy_bat", 20);
    _gameManager.ClearDamagedEnemyHp("enemy_bat");
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_bat"));
}

[Test]
public void ClearDamagedEnemyHp_NullId_IsNoOp()
{
    _gameManager.SetDamagedEnemyHp("enemy_bat", 20);
    _gameManager.ClearDamagedEnemyHp(null);
    Assert.AreEqual(20, _gameManager.GetDamagedEnemyHp("enemy_bat"));
}

[Test]
public void ClearAllDamagedEnemyHp_RemovesAllEntries()
{
    _gameManager.SetDamagedEnemyHp("enemy_a", 10);
    _gameManager.SetDamagedEnemyHp("enemy_b", 20);
    _gameManager.ClearAllDamagedEnemyHp();
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_a"));
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_b"));
}

[Test]
public void BuildSaveData_IncludesDamagedEnemyHp()
{
    _gameManager.SetDamagedEnemyHp("enemy_slime_01", 25);
    _gameManager.SetDamagedEnemyHp("enemy_bat_02", 40);

    SaveData data = _gameManager.BuildSaveData();

    Assert.IsNotNull(data.damagedEnemyHp);
    Assert.AreEqual(2, data.damagedEnemyHp.Length);

    var lookup = new System.Collections.Generic.Dictionary<string, int>();
    foreach (var entry in data.damagedEnemyHp)
        lookup[entry.enemyId] = entry.currentHp;
    Assert.AreEqual(25, lookup["enemy_slime_01"]);
    Assert.AreEqual(40, lookup["enemy_bat_02"]);
}

[Test]
public void BuildSaveData_DamagedEnemyHp_IsEmptyArray_WhenNoneDamaged()
{
    SaveData data = _gameManager.BuildSaveData();

    Assert.IsNotNull(data.damagedEnemyHp);
    Assert.AreEqual(0, data.damagedEnemyHp.Length);
}

[Test]
public void ApplySaveData_RestoresDamagedEnemyHp()
{
    _gameManager.SetDamagedEnemyHp("stale_enemy", 99);

    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        damagedEnemyHp = new[]
        {
            new EnemyHpSaveEntry { enemyId = "enemy_a", currentHp = 30 },
            new EnemyHpSaveEntry { enemyId = "enemy_b", currentHp = 15 }
        }
    };

    _gameManager.ApplySaveData(saveData);

    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("stale_enemy"));
    Assert.AreEqual(30, _gameManager.GetDamagedEnemyHp("enemy_a"));
    Assert.AreEqual(15, _gameManager.GetDamagedEnemyHp("enemy_b"));
}

[Test]
public void ApplySaveData_NullDamagedEnemyHp_ClearsDictionary()
{
    _gameManager.SetDamagedEnemyHp("stale", 50);

    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        damagedEnemyHp = null
    };

    _gameManager.ApplySaveData(saveData);

    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("stale"));
}

[Test]
public void PersistAndLoad_RoundTrip_RestoresDamagedEnemyHp()
{
    SaveService tempSaveService = CreateTempSaveService();
    _gameManager.SetSaveServiceForTests(tempSaveService);

    _gameManager.SetDamagedEnemyHp("enemy_slime_01", 25);
    _gameManager.SetDamagedEnemyHp("enemy_bat_02", 40);
    _gameManager.PersistToDisk();

    _gameManager.ClearAllDamagedEnemyHp();
    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_slime_01"));

    bool loaded = _gameManager.TryLoadFromDiskIntoGame();

    Assert.IsTrue(loaded);
    Assert.AreEqual(25, _gameManager.GetDamagedEnemyHp("enemy_slime_01"));
    Assert.AreEqual(40, _gameManager.GetDamagedEnemyHp("enemy_bat_02"));
}

[Test]
public void ApplySaveData_MissingDamagedEnemyHp_DefaultsToEmpty()
{
    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        damagedEnemyHp = null
    };

    _gameManager.ApplySaveData(saveData);

    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("any_enemy"));
}

[Test]
public void StartNewGame_ClearsDamagedEnemyHp()
{
    _gameManager.SetDamagedEnemyHp("enemy_a", 30);

    _gameManager.StartNewGame();

    Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_a"));
}
```

**Step 2: Run tests → expect FAIL (compile errors — methods don't exist yet)**

**Step 3: Implement in GameManager.cs**

Add the following private field alongside `_defeatedEnemyIds`:

```csharp
private readonly Dictionary<string, int> _damagedEnemyHp =
    new Dictionary<string, int>(StringComparer.Ordinal);
```

Add these public methods after the defeated-enemy methods:

```csharp
/// <summary>
/// Returns the persisted current HP for a damaged enemy, or -1 if the enemy
/// has no damage override (meaning it should start at full HP).
/// </summary>
public int GetDamagedEnemyHp(string enemyId)
{
    if (string.IsNullOrEmpty(enemyId)) return -1;
    return _damagedEnemyHp.TryGetValue(enemyId, out int hp) ? hp : -1;
}

/// <summary>
/// Records a damaged enemy's current HP. Called by BattleController on Fled.
/// Null/empty IDs are silently ignored.
/// </summary>
public void SetDamagedEnemyHp(string enemyId, int currentHp)
{
    if (!string.IsNullOrEmpty(enemyId))
        _damagedEnemyHp[enemyId] = currentHp;
}

/// <summary>
/// Removes a single enemy's damage override. Called on Victory (enemy is dead,
/// no HP to persist). Null/empty IDs are silently ignored.
/// </summary>
public void ClearDamagedEnemyHp(string enemyId)
{
    if (!string.IsNullOrEmpty(enemyId))
        _damagedEnemyHp.Remove(enemyId);
}

/// <summary>Clears all damaged enemy HP overrides.</summary>
public void ClearAllDamagedEnemyHp() => _damagedEnemyHp.Clear();
```

In `BuildSaveData()`, add to the return object initializer (after `defeatedEnemyIds`):

```csharp
damagedEnemyHp = BuildEnemyHpEntries(_damagedEnemyHp)
```

Add the private helper method:

```csharp
private static EnemyHpSaveEntry[] BuildEnemyHpEntries(Dictionary<string, int> damagedHp)
{
    if (damagedHp == null || damagedHp.Count == 0)
        return Array.Empty<EnemyHpSaveEntry>();

    var entries = new EnemyHpSaveEntry[damagedHp.Count];
    int i = 0;
    foreach (var kvp in damagedHp)
    {
        entries[i++] = new EnemyHpSaveEntry { enemyId = kvp.Key, currentHp = kvp.Value };
    }
    return entries;
}
```

In `ApplySaveData()`, add after `RestoreDefeatedEnemies(...)`:

```csharp
RestoreDamagedEnemyHp(data.damagedEnemyHp);
```

Add the restore method:

```csharp
private void RestoreDamagedEnemyHp(EnemyHpSaveEntry[] entries)
{
    _damagedEnemyHp.Clear();
    if (entries == null) return;

    foreach (EnemyHpSaveEntry entry in entries)
    {
        if (!string.IsNullOrWhiteSpace(entry.enemyId) && entry.currentHp >= 0)
            _damagedEnemyHp[entry.enemyId] = entry.currentHp;
    }
}
```

In `StartNewGame()`, add after `ClearDefeatedEnemies()`:

```csharp
ClearAllDamagedEnemyHp();
```

Add `using Axiom.Data;` if not already present (it already is).

**Step 4: Run tests → expect all GameManagerSaveDataTests PASS**

**Step 5: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): add damaged enemy HP tracking and save/load to GameManager`
> - `Assets/Scripts/Core/GameManager.cs`
> - `Assets/Scripts/Core/GameManager.cs.meta`
> - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
> - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`

---

## Task 5: BattleController — Sync HP on Battle End

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs` (HandleStateChanged method, lines 500-528)

**Context:** Currently `HandleStateChanged` calls `PersistToDisk()` on Fled/Victory without copying battle HP back to `PlayerState`. The player's HP "snaps back" to whatever was in `PlayerState` before the battle.

**Step 1: Add the sync helper method**

In `Assets/Scripts/Battle/BattleController.cs`, add as a private method (e.g. after `HandleStateChanged`):

```csharp
/// <summary>
/// Copies the player's current battle HP/MP back into the persistent GameManager.PlayerState.
/// Called before PersistToDisk on every terminal battle state (Victory, Defeat, Fled).
/// No-op when GameManager is absent (standalone Battle scene testing).
/// </summary>
private void SyncBattleHpToPlayerState()
{
    if (GameManager.Instance == null) return;
    GameManager.Instance.PlayerState.SetCurrentHp(_playerStats.CurrentHP);
    GameManager.Instance.PlayerState.SetCurrentMp(_playerStats.CurrentMP);
}
```

**Step 2: Update HandleStateChanged — Fled path**

Replace:
```csharp
else if (state == BattleState.Fled)
{
    GameManager.Instance?.PersistToDisk();
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer");
}
```

With:
```csharp
else if (state == BattleState.Fled)
{
    SyncBattleHpToPlayerState();
    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
        GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);
    GameManager.Instance?.PersistToDisk();
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer");
}
```

**Step 3: Update HandleStateChanged — Victory path**

Replace:
```csharp
else if (state == BattleState.Victory)
{
    // Placeholder — DEV-37 will insert XP/loot screen before this transition.
    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
        GameManager.Instance.MarkEnemyDefeated(_battleEnemyId);
    GameManager.Instance?.PersistToDisk();
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer");
}
```

With:
```csharp
else if (state == BattleState.Victory)
{
    SyncBattleHpToPlayerState();
    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
    {
        GameManager.Instance.MarkEnemyDefeated(_battleEnemyId);
        GameManager.Instance.ClearDamagedEnemyHp(_battleEnemyId);
    }
    GameManager.Instance?.PersistToDisk();
    if (GameManager.Instance?.SceneTransition != null)
        GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
    else
        SceneManager.LoadScene("Platformer");
}
```

**Step 4: Add Defeat path**

After the Victory block, add a new `else if` for Defeat. Currently there is no Defeat branch in `HandleStateChanged` — only the `OnBattleStateChanged` event fires (which disables the action menu in BattleHUD).

```csharp
else if (state == BattleState.Defeat)
{
    SyncBattleHpToPlayerState();
    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
        GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);
}
```

> No `PersistToDisk()` and no scene load — Defeat is a game-over state. The in-memory sync satisfies the AC that "on battle end (Victory, Defeat, and Fled), the player's current battle HP is copied." If a future DEV ticket adds a game-over screen with "retry from last save," the on-disk state is already correct from the pre-battle save.

**Step 5: Verify compile succeeds in Unity**

**Step 6: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): sync battle HP to PlayerState on battle end`
> - `Assets/Scripts/Battle/BattleController.cs`
> - `Assets/Scripts/Battle/BattleController.cs.meta`

---

## Task 6: BattleController — Read Persistent HP on Initialize

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs` (Start method + Initialize method)

**Context:** `BattleController.Start()` consumes `BattleEntry` fields. `Initialize()` must now read persistent HP values from `PlayerState` (player) and `BattleEntry.EnemyCurrentHp` (enemy) instead of always resetting to max.

**Step 1: Add `_enemyStartHp` field**

At the top of `BattleController`, with the other private fields:

```csharp
private int _enemyStartHp = -1;
```

**Step 2: Store EnemyCurrentHp in Start()**

In the `Start()` method, inside the `if (pending != null)` block, add after `_battleEnemyId`:

```csharp
_enemyStartHp  = pending.EnemyCurrentHp;
```

So the block becomes:
```csharp
var pending = GameManager.Instance?.PendingBattle;
if (pending != null)
{
    _startState    = pending.StartState;
    _enemyData     = pending.EnemyData;
    _battleEnemyId = pending.EnemyId;
    _enemyStartHp  = pending.EnemyCurrentHp;
    GameManager.Instance.ClearPendingBattle();
}
```

**Step 3: Update Initialize() — sync player MaxHP/MaxMP before Initialize call**

In `Initialize()`, **after** the EnemyData stat override block and **before** `_playerStats.Initialize()`, add:

```csharp
if (GameManager.Instance != null)
{
    PlayerState ps = GameManager.Instance.PlayerState;
    _playerStats.MaxHP = ps.MaxHp;
    _playerStats.MaxMP = ps.MaxMp;
}
```

This requires adding `using Axiom.Core;` at the top of the file (check if already present — it is).

**Step 4: Update Initialize() — pass persistent HP to Initialize calls**

Replace:
```csharp
_playerStats.Initialize();
_enemyStats.Initialize(_enemyData != null ? _enemyData.innateConditions : null);
```

With:
```csharp
int? playerStartHp = GameManager.Instance != null
    ? GameManager.Instance.PlayerState.CurrentHp
    : (int?)null;
int? playerStartMp = GameManager.Instance != null
    ? GameManager.Instance.PlayerState.CurrentMp
    : (int?)null;

_playerStats.Initialize(startHp: playerStartHp, startMp: playerStartMp);

int? enemyStartHp = _enemyStartHp >= 0 ? _enemyStartHp : (int?)null;
_enemyStats.Initialize(
    _enemyData != null ? _enemyData.innateConditions : null,
    startHp: enemyStartHp);
```

> When `GameManager` is null (standalone Battle scene testing), `playerStartHp` and `playerStartMp` are null, so `Initialize` falls back to MaxHP/MaxMP — preserving standalone testing behavior.
>
> When `_enemyStartHp` is -1 (fresh encounter, not re-engaging a fled enemy), `enemyStartHp` is null, so the enemy starts at full HP.

**Step 5: Verify compile succeeds and run full test suite in Unity Test Runner**

**Step 6: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): read persistent HP on battle initialize`
> - `Assets/Scripts/Battle/BattleController.cs`
> - `Assets/Scripts/Battle/BattleController.cs.meta`

---

## Task 7: ExplorationEnemyCombatTrigger — Pass Enemy HP in BattleEntry

**Files:**
- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` (TriggerBattle method)

**Context:** When triggering a battle, the combat trigger must look up whether this enemy has a damaged HP override in `GameManager` and pass it through `BattleEntry` so `BattleController` can resume the enemy at the correct HP.

**Step 1: Implement**

In `ExplorationEnemyCombatTrigger.TriggerBattle()`, replace the two lines:

```csharp
string enemyId = GetComponent<EnemyController>()?.EnemyId;
GameManager.Instance.SetPendingBattle(new BattleEntry(startState, _enemyData, enemyId));
```

With:

```csharp
string enemyId = GetComponent<EnemyController>()?.EnemyId;
int enemyCurrentHp = GameManager.Instance.GetDamagedEnemyHp(enemyId);
GameManager.Instance.SetPendingBattle(
    new BattleEntry(startState, _enemyData, enemyId, enemyCurrentHp));
```

**Step 2: Verify compile succeeds in Unity**

**Step 3: Check in via UVCS**

> **Unity Editor task (user):**
> Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-63): pass enemy damaged HP through BattleEntry on combat trigger`
> - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
> - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs.meta`

---

## Task 8: Manual Play-Test Verification

> **Unity Editor task (user):** Run the following manual test scenarios in Play Mode.

### Scenario A: Player HP persists across battle Flee

1. Open Platformer scene. Enter Play Mode.
2. Walk into an enemy to trigger a Surprised battle.
3. In battle, use Attack to deal/receive damage. Note your HP.
4. Select Flee.
5. Back in Platformer — walk into the same (or another) enemy.
6. **Verify:** Player starts the second battle at the HP they had when they fled the first battle (not full HP).

### Scenario B: Enemy HP persists across battle Flee

1. In a battle, deal damage to the enemy but don't kill it.
2. Select Flee.
3. Re-engage the same enemy.
4. **Verify:** Enemy HP starts at the value it had when you fled (not full HP).

### Scenario C: Checkpoint still heals to full

1. Walk to a save point checkpoint.
2. **Verify:** HP is restored to max.
3. Enter a battle.
4. **Verify:** Player starts at max HP.

### Scenario D: Save/Load round-trip

1. Take damage in battle, flee.
2. Walk to a save point (or let the auto-save trigger).
3. Quit and restart the game. Select Continue.
4. Enter a battle.
5. **Verify:** Player HP matches what it was at the save point (post-damage, post-checkpoint-heal).

### Scenario E: Defeated enemy HP is cleared

1. Defeat an enemy in battle.
2. Return to platformer.
3. **Verify:** Enemy is removed from the world (existing behavior unchanged).

### Scenario F: Standalone Battle scene still works

1. Open Battle scene directly (no GameManager in scene).
2. Enter Play Mode.
3. **Verify:** Battle starts with Inspector default stats (100 HP, 30 MP for player). No errors.

---

## Summary of Modified Files

| File | Change |
|------|--------|
| `Assets/Scripts/Battle/CharacterStats.cs` | `Initialize()` accepts optional `startHp`, `startMp` |
| `Assets/Scripts/Data/BattleEntry.cs` | New `EnemyCurrentHp` property + constructor param |
| `Assets/Scripts/Data/SaveData.cs` | New `EnemyHpSaveEntry` struct + `damagedEnemyHp` field |
| `Assets/Scripts/Core/GameManager.cs` | Damaged enemy HP dictionary + `Get/Set/Clear` methods + `BuildSaveData`/`ApplySaveData` integration |
| `Assets/Scripts/Battle/BattleController.cs` | `SyncBattleHpToPlayerState()` on all terminal states + read persistent HP in `Initialize()` |
| `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Pass `enemyCurrentHp` in `BattleEntry` constructor |
| `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | 9 new tests for `startHp`/`startMp` |
| `Assets/Tests/Editor/Battle/BattleEntryTests.cs` | 3 new tests for `EnemyCurrentHp` |
| `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | 17 new tests for damaged enemy HP tracking + save/load + backward compat |

No new files created. No new asmdef files needed (all changes are within existing assemblies).
