# DEV-65 — Persist ATK/DEF/SPD Across Save/Load Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the bug where level-up-grown `Attack`, `Defense`, and `Speed` on `PlayerState` are not written to disk, causing them to reset to `CharacterData` base values on reload.

**Architecture:** Extend the `SaveData` DTO with three new integer fields. Introduce a dedicated `PlayerState.ApplyStats(int, int, int)` method (separate from `ApplyVitals`) so each apply path keeps a single responsibility. `GameManager.BuildSaveData` projects the stats out; `GameManager.ApplySaveData` writes them back, falling back to the already-seeded `PlayerState` base stats when the field is zero — the JsonUtility default for absent fields in legacy saves.

**Tech Stack:** C# 9, Unity 6 LTS, `JsonUtility` for serialization, NUnit (Unity Test Framework) in Edit Mode under `CoreTests`.

---

## Context & Non-Goals

- **In scope:** `SaveData` schema extension, `PlayerState` new apply method, `GameManager` wiring, round-trip and legacy-fallback tests.
- **Out of scope:** `ProgressionService` growth formulas, `CharacterData` base values, save file versioning bumps (the legacy fallback is handled purely by JsonUtility defaults + a zero-check).
- **Legacy fallback decision (documented per AC):** When loading a pre-DEV-65 save, `data.attack`, `data.defense`, `data.speed` deserialize as `0`. A value of `0` is treated as "field absent" and the call falls through to the stats already on `PlayerState` (which were seeded from `CharacterData` in `EnsurePlayerState`). This mirrors the existing `maxHp > 0 ? data.maxHp : PlayerState.MaxHp` pattern at `GameManager.cs:268-269`.

## File Map

| File | Change |
|---|---|
| `Assets/Scripts/Data/SaveData.cs` | Add `attack`, `defense`, `speed` int fields. |
| `Assets/Scripts/Core/PlayerState.cs` | Add `ApplyStats(int attack, int defense, int speed)` method. |
| `Assets/Scripts/Core/GameManager.cs` | `BuildSaveData` populates new fields; `ApplySaveData` writes them back with zero-fallback. |
| `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs` | Add tests: round-trip stats; legacy JSON without stats deserializes as zero. |
| `Assets/Tests/Editor/Core/PlayerStateTests.cs` | Add tests for `ApplyStats` (happy path, negatives throw). |
| `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | Add tests: `BuildSaveData` includes stats; `ApplySaveData` restores stats; legacy save (zero stats) preserves base stats; persist→load round-trip preserves grown stats. |

No Unity Editor tasks. No new asmdefs. No scene changes. No prefab changes.

---

## Task 1: Extend `SaveData` Schema

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Test: `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs` (inside the existing `SaveDataSerializationTests` class):

```csharp
[Test]
public void SaveData_JsonUtility_RoundTrip_PreservesAttackDefenseSpeed()
{
    var original = new SaveData
    {
        attack  = 18,
        defense = 11,
        speed   = 14
    };

    string json = JsonUtility.ToJson(original, prettyPrint: true);
    SaveData copy = JsonUtility.FromJson<SaveData>(json);

    Assert.AreEqual(18, copy.attack);
    Assert.AreEqual(11, copy.defense);
    Assert.AreEqual(14, copy.speed);
}

[Test]
public void SaveData_DefaultAttackDefenseSpeed_AreZero()
{
    var data = new SaveData();
    Assert.AreEqual(0, data.attack);
    Assert.AreEqual(0, data.defense);
    Assert.AreEqual(0, data.speed);
}

[Test]
public void SaveData_JsonUtility_BackwardCompat_MissingAttackDefenseSpeed_DeserializesAsZero()
{
    // Legacy JSON written before DEV-65 — has no attack/defense/speed fields.
    string legacyJson = "{\"saveVersion\":1,\"playerLevel\":3,\"playerXp\":0,\"currentHp\":50," +
                        "\"currentMp\":20,\"maxHp\":100,\"maxMp\":50," +
                        "\"unlockedSpellIds\":[],\"inventory\":[]," +
                        "\"worldPositionX\":0.0,\"worldPositionY\":0.0," +
                        "\"activeSceneName\":\"Platformer\",\"activatedCheckpointIds\":[]," +
                        "\"defeatedEnemyIds\":[],\"damagedEnemyHp\":[]}";

    SaveData copy = JsonUtility.FromJson<SaveData>(legacyJson);

    Assert.AreEqual(0, copy.attack);
    Assert.AreEqual(0, copy.defense);
    Assert.AreEqual(0, copy.speed);
    Assert.AreEqual(3, copy.playerLevel); // sanity: legacy fields still parse
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Editor → Window → General → Test Runner → EditMode tab → filter `SaveDataSerializationTests` → Run.

Expected: FAIL for all three new tests with compiler error `'SaveData' does not contain a definition for 'attack'`.

- [ ] **Step 3: Add the three new fields to `SaveData`**

In `Assets/Scripts/Data/SaveData.cs`, add the three fields immediately after `maxMp` (so they sit with the other numeric stat-like fields). Replace the block from `maxHp` / `maxMp` through the blank line before `unlockedSpellIds`:

```csharp
        public int playerLevel;
        public int playerXp;
        public int currentHp;
        public int currentMp;
        public int maxHp;
        public int maxMp;

        public int attack;
        public int defense;
        public int speed;

        public string[] unlockedSpellIds = Array.Empty<string>();
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Editor → Test Runner → EditMode → filter `SaveDataSerializationTests` → Run.
Expected: PASS — all three new tests plus existing tests green.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-65): add attack/defense/speed fields to SaveData`
  - `Assets/Scripts/Data/SaveData.cs`
  - `Assets/Scripts/Data/SaveData.cs.meta`
  - `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`
  - `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs.meta`

---

## Task 2: Add `ApplyStats` to `PlayerState`

**Files:**
- Modify: `Assets/Scripts/Core/PlayerState.cs`
- Test: `Assets/Tests/Editor/Core/PlayerStateTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Core/PlayerStateTests.cs` (inside the existing `PlayerStateTests` class):

```csharp
// ── ApplyStats ───────────────────────────────────────────────────────

[Test]
public void ApplyStats_OverwritesAttackDefenseSpeed()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);

    state.ApplyStats(attack: 22, defense: 13, speed: 17);

    Assert.AreEqual(22, state.Attack);
    Assert.AreEqual(13, state.Defense);
    Assert.AreEqual(17, state.Speed);
}

[Test]
public void ApplyStats_AllowsZeroValues()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);

    state.ApplyStats(attack: 0, defense: 0, speed: 0);

    Assert.AreEqual(0, state.Attack);
    Assert.AreEqual(0, state.Defense);
    Assert.AreEqual(0, state.Speed);
}

[Test]
public void ApplyStats_ThrowsOnNegativeAttack()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    Assert.Throws<ArgumentOutOfRangeException>(
        () => state.ApplyStats(attack: -1, defense: 5, speed: 8));
}

[Test]
public void ApplyStats_ThrowsOnNegativeDefense()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    Assert.Throws<ArgumentOutOfRangeException>(
        () => state.ApplyStats(attack: 10, defense: -1, speed: 8));
}

[Test]
public void ApplyStats_ThrowsOnNegativeSpeed()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    Assert.Throws<ArgumentOutOfRangeException>(
        () => state.ApplyStats(attack: 10, defense: 5, speed: -1));
}

[Test]
public void ApplyStats_DoesNotAlterHpMpOrProgression()
{
    var state = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
    state.ApplyProgression(level: 4, xp: 600);
    state.ApplyVitals(maxHp: 120, maxMp: 60, currentHp: 90, currentMp: 40);

    state.ApplyStats(attack: 99, defense: 99, speed: 99);

    Assert.AreEqual(120, state.MaxHp);
    Assert.AreEqual(60,  state.MaxMp);
    Assert.AreEqual(90,  state.CurrentHp);
    Assert.AreEqual(40,  state.CurrentMp);
    Assert.AreEqual(4,   state.Level);
    Assert.AreEqual(600, state.Xp);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → EditMode → filter `PlayerStateTests` → Run.
Expected: FAIL with compiler error `'PlayerState' does not contain a definition for 'ApplyStats'`.

- [ ] **Step 3: Add `ApplyStats` to `PlayerState`**

In `Assets/Scripts/Core/PlayerState.cs`, add the method immediately after `ApplyProgression` (line 82):

```csharp
        /// <summary>
        /// Overwrites base combat stats with persisted values. Used by
        /// <c>GameManager.ApplySaveData</c> to restore level-up stat growth on load.
        /// Deltas are not applied — this is an absolute write.
        /// All values must be non-negative.
        /// </summary>
        public void ApplyStats(int attack, int defense, int speed)
        {
            if (attack  < 0) throw new ArgumentOutOfRangeException(nameof(attack),  "attack cannot be negative.");
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense), "defense cannot be negative.");
            if (speed   < 0) throw new ArgumentOutOfRangeException(nameof(speed),   "speed cannot be negative.");

            Attack  = attack;
            Defense = defense;
            Speed   = speed;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Test Runner → EditMode → filter `PlayerStateTests` → Run.
Expected: PASS — all new `ApplyStats_*` tests plus existing tests green.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-65): add PlayerState.ApplyStats for persisted stat restore`
  - `Assets/Scripts/Core/PlayerState.cs`
  - `Assets/Scripts/Core/PlayerState.cs.meta`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs.meta`

---

## Task 3: Wire `GameManager.BuildSaveData` and `ApplySaveData`

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Test: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` (inside the existing class, after the checkpoint tests):

```csharp
// ── ATK/DEF/SPD persistence (DEV-65) ─────────────────────────────────

[Test]
public void BuildSaveData_IncludesGrownAttackDefenseSpeed()
{
    // PlayerState is seeded with CD base stats (atk=10, def=5, spd=8).
    _gameManager.PlayerState.ApplyStats(attack: 25, defense: 14, speed: 19);

    SaveData data = _gameManager.BuildSaveData();

    Assert.AreEqual(25, data.attack);
    Assert.AreEqual(14, data.defense);
    Assert.AreEqual(19, data.speed);
}

[Test]
public void ApplySaveData_RestoresAttackDefenseSpeed()
{
    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        attack  = 30,
        defense = 18,
        speed   = 22
    };

    _gameManager.ApplySaveData(saveData);

    Assert.AreEqual(30, _gameManager.PlayerState.Attack);
    Assert.AreEqual(18, _gameManager.PlayerState.Defense);
    Assert.AreEqual(22, _gameManager.PlayerState.Speed);
}

[Test]
public void ApplySaveData_LegacySaveWithZeroStats_KeepsCharacterDataBaseStats()
{
    // A pre-DEV-65 save has attack/defense/speed == 0 (JsonUtility default).
    // Expected behavior: fall back to PlayerState base values from CharacterData.
    // Test CharacterData: atk=10, def=5, spd=8 (see CreateTestCharacterData).
    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        attack  = 0,
        defense = 0,
        speed   = 0
    };

    _gameManager.ApplySaveData(saveData);

    Assert.AreEqual(10, _gameManager.PlayerState.Attack);
    Assert.AreEqual(5,  _gameManager.PlayerState.Defense);
    Assert.AreEqual(8,  _gameManager.PlayerState.Speed);
}

[Test]
public void PersistAndLoad_RoundTrip_RestoresGrownStats()
{
    SaveService tempSaveService = CreateTempSaveService();
    _gameManager.SetSaveServiceForTests(tempSaveService);

    // Simulate level-up stat growth.
    _gameManager.PlayerState.ApplyStats(attack: 42, defense: 27, speed: 33);
    _gameManager.PersistToDisk();

    // Reset to base stats to prove the load (not the in-memory state) restores them.
    _gameManager.PlayerState.ApplyStats(attack: 10, defense: 5, speed: 8);

    bool loaded = _gameManager.TryLoadFromDiskIntoGame();

    Assert.IsTrue(loaded);
    Assert.AreEqual(42, _gameManager.PlayerState.Attack);
    Assert.AreEqual(27, _gameManager.PlayerState.Defense);
    Assert.AreEqual(33, _gameManager.PlayerState.Speed);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → EditMode → filter `GameManagerSaveDataTests` → Run.
Expected: FAIL — `BuildSaveData_IncludesGrownAttackDefenseSpeed` expects `25` but gets `0` (default struct field); `ApplySaveData_RestoresAttackDefenseSpeed` expects `30` but gets `10` (no restore happens); round-trip fails for the same reason; legacy test currently passes by accident (PlayerState isn't touched) — acceptable, it documents the expected behavior.

- [ ] **Step 3: Update `BuildSaveData` in `GameManager.cs`**

In `Assets/Scripts/Core/GameManager.cs`, modify the object initializer inside `BuildSaveData` (starts at line 242). Add the three new fields after `maxMp`:

```csharp
            return new SaveData
            {
                playerLevel = PlayerState.Level,
                playerXp = PlayerState.Xp,
                currentHp = PlayerState.CurrentHp,
                currentMp = PlayerState.CurrentMp,
                maxHp = PlayerState.MaxHp,
                maxMp = PlayerState.MaxMp,
                attack  = PlayerState.Attack,
                defense = PlayerState.Defense,
                speed   = PlayerState.Speed,
                unlockedSpellIds = CopyStringList(PlayerState.UnlockedSpellIds),
                inventory = PlayerState.Inventory.ToSaveEntries(),
                worldPositionX = PlayerState.WorldPositionX,
                worldPositionY = PlayerState.WorldPositionY,
                activeSceneName = sceneName ?? string.Empty,
                activatedCheckpointIds = CopyReadOnlyStringList(PlayerState.ActivatedCheckpointIds),
                defeatedEnemyIds = CopyHashSet(_defeatedEnemyIds),
                damagedEnemyHp = BuildEnemyHpEntries(_damagedEnemyHp)
            };
```

- [ ] **Step 4: Update `ApplySaveData` in `GameManager.cs`**

In the same file, modify `ApplySaveData` (starts at line 261). Add the stats apply immediately after `ApplyVitals` (line 271), using the same zero-fallback pattern as `maxHp`/`maxMp`:

```csharp
        public void ApplySaveData(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsurePlayerState();

            int targetMaxHp = data.maxHp > 0 ? data.maxHp : PlayerState.MaxHp;
            int targetMaxMp = data.maxMp >= 0 ? data.maxMp : PlayerState.MaxMp;

            PlayerState.ApplyVitals(targetMaxHp, targetMaxMp, data.currentHp, data.currentMp);

            // Legacy saves (pre-DEV-65) have attack/defense/speed == 0. Treat zero as
            // "field absent" and keep the base stats PlayerState was seeded with from
            // CharacterData. Non-zero values reflect real stored (and possibly grown) stats.
            int targetAttack  = data.attack  > 0 ? data.attack  : PlayerState.Attack;
            int targetDefense = data.defense > 0 ? data.defense : PlayerState.Defense;
            int targetSpeed   = data.speed   > 0 ? data.speed   : PlayerState.Speed;
            PlayerState.ApplyStats(targetAttack, targetDefense, targetSpeed);

            PlayerState.ApplyProgression(data.playerLevel, data.playerXp);
            PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());
            EnsureSpellUnlockService();
            _spellUnlockService?.RestoreFromIds(data.unlockedSpellIds ?? Array.Empty<string>());
            _spellUnlockService?.NotifyPlayerLevel(PlayerState.Level);
            PlayerState.Inventory.LoadFromSaveEntries(data.inventory);
            PlayerState.SetWorldPosition(data.worldPositionX, data.worldPositionY);
            PlayerState.SetActiveScene(data.activeSceneName ?? string.Empty);
            PlayerState.SetActivatedCheckpointIds(data.activatedCheckpointIds ?? Array.Empty<string>());
            RestoreDefeatedEnemies(data.defeatedEnemyIds);
            RestoreDamagedEnemyHp(data.damagedEnemyHp);
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Test Runner → EditMode → filter `GameManagerSaveDataTests` → Run.
Expected: PASS — all four new DEV-65 tests plus every existing test green.

- [ ] **Step 6: Run the full Core test suite (regression check)**

Test Runner → EditMode → right-click the `CoreTests` assembly node → Run All.
Expected: PASS — including `PlayerStateTests`, `SaveServiceTests`, `SaveDataSerializationTests`, and `GameManagerSaveDataTests`.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-65): persist ATK/DEF/SPD through save-load with legacy fallback`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Scripts/Core/GameManager.cs.meta`
  - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`

---

## Acceptance Criteria Verification

Before marking the Jira ticket Done, confirm each AC against the implemented behavior:

| AC | Verified by |
|---|---|
| Save after level-ups and reload restores grown ATK/DEF/SPD (not base). | `PersistAndLoad_RoundTrip_RestoresGrownStats` (Task 3). |
| `SaveData` schema includes `attack`, `defense`, `speed`. | `SaveData.cs` diff + `SaveData_JsonUtility_RoundTrip_PreservesAttackDefenseSpeed` (Task 1). |
| Legacy saves without these fields load without throwing; fallback to base stats. | `SaveData_JsonUtility_BackwardCompat_MissingAttackDefenseSpeed_DeserializesAsZero` (Task 1) + `ApplySaveData_LegacySaveWithZeroStats_KeepsCharacterDataBaseStats` (Task 3). Documented in code comment in `ApplySaveData`. |
| `PlayerState` has an apply path for persisted stat values. | `ApplyStats_OverwritesAttackDefenseSpeed` (Task 2). |
| EditMode tests cover (1) round-trip preservation, (2) legacy save without stat fields does not regress. | Tasks 1 & 3. |
| No change to `ProgressionService` growth formulas or `CharacterData`. | Grep-diff confirms neither file is modified. |
