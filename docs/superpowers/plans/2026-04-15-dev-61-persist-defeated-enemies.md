# DEV-61: Persist Defeated Enemy IDs Across Sessions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing JSON save/load system so `GameManager._defeatedEnemyIds` persists to disk and restores on Continue, keeping defeated enemies removed across app restarts.

**Architecture:** Mirror the existing `activatedCheckpointIds` pattern exactly — a new `string[] defeatedEnemyIds` field on `SaveData`, read by `BuildSaveData`, written by `ApplySaveData` via a new `RestoreDefeatedEnemies(IEnumerable<string>)` method on `GameManager` (companion to the existing `ClearDefeatedEnemies()`). JSON backward compatibility is free — `JsonUtility` deserializes missing array fields as the declared default (empty array).

**Tech Stack:** Unity 6 LTS, C# 9, `JsonUtility`, NUnit (Unity Test Framework — Edit Mode). Assemblies: `Axiom.Core` (runtime), `Axiom.Data` (DTO), `CoreTests` (Edit Mode tests under `Assets/Tests/Editor/Core/`).

**Jira:** DEV-61 — <https://axiombrokensunrefined.atlassian.net/browse/DEV-61>

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Assets/Scripts/Data/SaveData.cs` | Modify | Add `defeatedEnemyIds` field (JSON payload) |
| `Assets/Scripts/Core/GameManager.cs` | Modify | Expose `DefeatedEnemyIds` read-only, add `RestoreDefeatedEnemies`, wire into `BuildSaveData` / `ApplySaveData` |
| `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs` | Modify | JSON round-trip + backward-compat tests for the new field |
| `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | Modify | GameManager build/apply tests for defeated enemies |

No new files, no new folders, no asmdef changes. All work lands inside existing `Axiom.Core` / `Axiom.Data` / `CoreTests` assemblies.

---

## Task 1: Extend `SaveData` DTO with `defeatedEnemyIds`

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`

- [ ] **Step 1: Write a failing JSON round-trip test**

Append this test method to `SaveDataSerializationTests` class in `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs` (just before the closing `}` of the class):

```csharp
[Test]
public void SaveData_JsonUtility_RoundTrip_PreservesDefeatedEnemyIds()
{
    var original = new SaveData
    {
        defeatedEnemyIds = new[] { "enemy_slime_01", "enemy_bat_02" }
    };

    string json = JsonUtility.ToJson(original, prettyPrint: true);
    SaveData copy = JsonUtility.FromJson<SaveData>(json);

    Assert.AreEqual(2, copy.defeatedEnemyIds.Length);
    Assert.AreEqual("enemy_slime_01", copy.defeatedEnemyIds[0]);
    Assert.AreEqual("enemy_bat_02", copy.defeatedEnemyIds[1]);
}

[Test]
public void SaveData_DefaultDefeatedEnemyIds_IsEmpty()
{
    var data = new SaveData();
    Assert.AreEqual(0, data.defeatedEnemyIds.Length);
}

[Test]
public void SaveData_JsonUtility_BackwardCompat_MissingDefeatedEnemyIds_DeserializesAsEmpty()
{
    // Legacy JSON written before DEV-61 — has no defeatedEnemyIds field.
    string legacyJson = "{\"saveVersion\":1,\"playerLevel\":3,\"playerXp\":0,\"currentHp\":50," +
                        "\"currentMp\":20,\"maxHp\":100,\"maxMp\":50," +
                        "\"unlockedSpellIds\":[],\"inventory\":[]," +
                        "\"worldPositionX\":0.0,\"worldPositionY\":0.0," +
                        "\"activeSceneName\":\"Platformer\",\"activatedCheckpointIds\":[]}";

    SaveData copy = JsonUtility.FromJson<SaveData>(legacyJson);

    Assert.IsNotNull(copy.defeatedEnemyIds);
    Assert.AreEqual(0, copy.defeatedEnemyIds.Length);
    Assert.AreEqual(3, copy.playerLevel);
}
```

- [ ] **Step 2: Run the tests — confirm compile failure**

> **Unity Editor task (user):** Open Unity → `Window → General → Test Runner` → Edit Mode tab → select the three new tests under `SaveDataSerializationTests`. Expected: **compile error** — `SaveData` does not contain a definition for `defeatedEnemyIds`.

- [ ] **Step 3: Add the `defeatedEnemyIds` field to `SaveData`**

Edit `Assets/Scripts/Data/SaveData.cs`. After the existing `activatedCheckpointIds` field:

```csharp
public string[] activatedCheckpointIds = Array.Empty<string>();

public string[] defeatedEnemyIds = Array.Empty<string>();
```

Full updated class body:

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
    }
}
```

- [ ] **Step 4: Re-run the three new `SaveDataSerializationTests` — confirm all pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → re-run the `SaveDataSerializationTests` class. Expected: the three new tests PASS (round-trip, default-is-empty, backward-compat). All previously-passing tests in the class still pass.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-61): add defeatedEnemyIds field to SaveData`
- `Assets/Scripts/Data/SaveData.cs`
- `Assets/Scripts/Data/SaveData.cs.meta`
- `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`
- `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs.meta`

---

## Task 2: Expose defeated IDs + add `RestoreDefeatedEnemies` on `GameManager`

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

- [ ] **Step 1: Write failing tests for the new accessor and restore method**

Append these tests to `GameManagerSaveDataTests` class in `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` (just before the private `CreateTempSaveService` helper):

```csharp
[Test]
public void DefeatedEnemyIds_IsEmpty_ByDefault()
{
    Assert.IsNotNull(_gameManager.DefeatedEnemyIds);
    using var enumerator = _gameManager.DefeatedEnemyIds.GetEnumerator();
    Assert.IsFalse(enumerator.MoveNext());
}

[Test]
public void DefeatedEnemyIds_ReflectsMarkEnemyDefeated()
{
    _gameManager.MarkEnemyDefeated("enemy_a");
    _gameManager.MarkEnemyDefeated("enemy_b");

    var ids = new List<string>(_gameManager.DefeatedEnemyIds);
    CollectionAssert.AreEquivalent(new[] { "enemy_a", "enemy_b" }, ids);
}

[Test]
public void RestoreDefeatedEnemies_ReplacesExistingSet()
{
    _gameManager.MarkEnemyDefeated("stale_enemy");

    _gameManager.RestoreDefeatedEnemies(new[] { "enemy_x", "enemy_y" });

    Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_x"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_y"));
}

[Test]
public void RestoreDefeatedEnemies_WithNull_ClearsSet()
{
    _gameManager.MarkEnemyDefeated("enemy_a");

    _gameManager.RestoreDefeatedEnemies(null);

    Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_a"));
    using var enumerator = _gameManager.DefeatedEnemyIds.GetEnumerator();
    Assert.IsFalse(enumerator.MoveNext());
}

[Test]
public void RestoreDefeatedEnemies_SkipsNullAndEmptyIds()
{
    _gameManager.RestoreDefeatedEnemies(new[] { "enemy_a", null, string.Empty, "enemy_b" });

    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_a"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_b"));
    var ids = new List<string>(_gameManager.DefeatedEnemyIds);
    Assert.AreEqual(2, ids.Count);
}
```

- [ ] **Step 2: Run the tests — confirm compile failure**

> **Unity Editor task (user):** Test Runner → Edit Mode → run the `GameManagerSaveDataTests` class. Expected: **compile error** — `GameManager` does not contain `DefeatedEnemyIds` or `RestoreDefeatedEnemies`.

- [ ] **Step 3: Add `DefeatedEnemyIds` accessor and `RestoreDefeatedEnemies` method**

Edit `Assets/Scripts/Core/GameManager.cs`. Find the `ClearDefeatedEnemies` line (around line 107):

```csharp
        public void ClearDefeatedEnemies() => _defeatedEnemyIds.Clear();
```

Replace with:

```csharp
        public void ClearDefeatedEnemies() => _defeatedEnemyIds.Clear();

        /// <summary>
        /// Read-only view of the defeated-enemy set. Used by BuildSaveData to
        /// project the set into the SaveData DTO. Do not cast and mutate — use
        /// MarkEnemyDefeated / ClearDefeatedEnemies / RestoreDefeatedEnemies.
        /// </summary>
        public IEnumerable<string> DefeatedEnemyIds => _defeatedEnemyIds;

        /// <summary>
        /// Replaces the defeated-enemy set with the provided IDs. Null or whitespace
        /// IDs in the input are skipped. A null input clears the set.
        /// Called on Continue after ApplySaveData to restore cross-session state.
        /// </summary>
        public void RestoreDefeatedEnemies(IEnumerable<string> enemyIds)
        {
            _defeatedEnemyIds.Clear();
            if (enemyIds == null)
                return;

            foreach (string id in enemyIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _defeatedEnemyIds.Add(id);
            }
        }
```

- [ ] **Step 4: Re-run the five new `GameManagerSaveDataTests` — confirm all pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → re-run the `GameManagerSaveDataTests` class. Expected: all five new tests PASS; all previously-passing tests in this class still pass.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-61): expose DefeatedEnemyIds and add RestoreDefeatedEnemies`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/Core/GameManager.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`

---

## Task 3: Wire defeated IDs into `BuildSaveData` and `ApplySaveData`

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

- [ ] **Step 1: Write failing tests for save/load integration**

Append to `GameManagerSaveDataTests` class (just before the private `CreateTempSaveService` helper):

```csharp
[Test]
public void BuildSaveData_IncludesDefeatedEnemyIds()
{
    _gameManager.MarkEnemyDefeated("enemy_slime_01");
    _gameManager.MarkEnemyDefeated("enemy_bat_02");

    SaveData data = _gameManager.BuildSaveData();

    Assert.IsNotNull(data.defeatedEnemyIds);
    Assert.AreEqual(2, data.defeatedEnemyIds.Length);
    CollectionAssert.AreEquivalent(
        new[] { "enemy_slime_01", "enemy_bat_02" },
        data.defeatedEnemyIds);
}

[Test]
public void BuildSaveData_DefeatedEnemyIds_IsEmptyArray_WhenNoneDefeated()
{
    SaveData data = _gameManager.BuildSaveData();

    Assert.IsNotNull(data.defeatedEnemyIds);
    Assert.AreEqual(0, data.defeatedEnemyIds.Length);
}

[Test]
public void ApplySaveData_RestoresDefeatedEnemyIds()
{
    _gameManager.MarkEnemyDefeated("stale_enemy");

    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        defeatedEnemyIds = new[] { "enemy_a", "enemy_b" }
    };

    _gameManager.ApplySaveData(saveData);

    Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_a"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_b"));
}

[Test]
public void ApplySaveData_NullDefeatedEnemyIds_ClearsSet()
{
    _gameManager.MarkEnemyDefeated("stale_enemy");

    var saveData = new SaveData
    {
        maxHp = 100,
        maxMp = 50,
        defeatedEnemyIds = null
    };

    _gameManager.ApplySaveData(saveData);

    Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
}

[Test]
public void PersistAndLoad_RoundTrip_RestoresDefeatedEnemyIds()
{
    SaveService tempSaveService = CreateTempSaveService();
    _gameManager.SetSaveServiceForTests(tempSaveService);

    _gameManager.MarkEnemyDefeated("enemy_slime_01");
    _gameManager.MarkEnemyDefeated("enemy_bat_02");
    _gameManager.PersistToDisk();

    _gameManager.ClearDefeatedEnemies();
    Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_slime_01"));

    bool loaded = _gameManager.TryLoadFromDiskIntoGame();

    Assert.IsTrue(loaded);
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_slime_01"));
    Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_bat_02"));
}
```

- [ ] **Step 2: Run the tests — confirm the four new `BuildSaveData` / `ApplySaveData` / round-trip tests fail**

> **Unity Editor task (user):** Test Runner → Edit Mode → run the new tests in `GameManagerSaveDataTests`. Expected: FAIL — `BuildSaveData` does not populate `defeatedEnemyIds`; `ApplySaveData` does not consume it.

- [ ] **Step 3: Populate `defeatedEnemyIds` in `BuildSaveData`**

Edit `Assets/Scripts/Core/GameManager.cs`. In the `BuildSaveData` method, add the new field after `activatedCheckpointIds`:

```csharp
            return new SaveData
            {
                playerLevel = PlayerState.Level,
                playerXp = PlayerState.Xp,
                currentHp = PlayerState.CurrentHp,
                currentMp = PlayerState.CurrentMp,
                maxHp = PlayerState.MaxHp,
                maxMp = PlayerState.MaxMp,
                unlockedSpellIds = CopyStringList(PlayerState.UnlockedSpellIds),
                inventory = BuildInventoryEntries(PlayerState.InventoryItemIds),
                worldPositionX = PlayerState.WorldPositionX,
                worldPositionY = PlayerState.WorldPositionY,
                activeSceneName = sceneName ?? string.Empty,
                activatedCheckpointIds = CopyReadOnlyStringList(PlayerState.ActivatedCheckpointIds),
                defeatedEnemyIds = CopyHashSet(_defeatedEnemyIds)
            };
```

Then add this private helper next to the existing `CopyStringList` / `CopyReadOnlyStringList` helpers at the bottom of the class:

```csharp
        private static string[] CopyHashSet(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            values.CopyTo(copy);
            return copy;
        }
```

- [ ] **Step 4: Consume `defeatedEnemyIds` in `ApplySaveData`**

In the same file, find `ApplySaveData` and add a `RestoreDefeatedEnemies` call at the end:

```csharp
        public void ApplySaveData(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsurePlayerState();

            int targetMaxHp = data.maxHp > 0 ? data.maxHp : PlayerState.MaxHp;
            int targetMaxMp = data.maxMp >= 0 ? data.maxMp : PlayerState.MaxMp;

            PlayerState.ApplyVitals(targetMaxHp, targetMaxMp, data.currentHp, data.currentMp);
            PlayerState.ApplyProgression(data.playerLevel, data.playerXp);
            PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());
            PlayerState.SetInventoryItemIds(ExpandInventory(data.inventory));
            PlayerState.SetWorldPosition(data.worldPositionX, data.worldPositionY);
            PlayerState.SetActiveScene(data.activeSceneName ?? string.Empty);
            PlayerState.SetActivatedCheckpointIds(data.activatedCheckpointIds ?? Array.Empty<string>());
            RestoreDefeatedEnemies(data.defeatedEnemyIds);
        }
```

(`RestoreDefeatedEnemies` already handles `null` by clearing the set, so no extra null-coalesce is needed here — matches the Task 2 contract.)

- [ ] **Step 5: Re-run all five new integration tests — confirm all pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → run the `GameManagerSaveDataTests` class. Expected: all five integration tests PASS (`BuildSaveData_IncludesDefeatedEnemyIds`, `BuildSaveData_DefeatedEnemyIds_IsEmptyArray_WhenNoneDefeated`, `ApplySaveData_RestoresDefeatedEnemyIds`, `ApplySaveData_NullDefeatedEnemyIds_ClearsSet`, `PersistAndLoad_RoundTrip_RestoresDefeatedEnemyIds`). No prior tests regressed.

- [ ] **Step 6: Run the full Edit Mode suite to catch regressions**

> **Unity Editor task (user):** Test Runner → Edit Mode → `Run All`. Expected: all tests pass; no previously-green test turned red.

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-61): persist defeated enemy ids across sessions`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/Core/GameManager.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
- `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`

---

## Task 4: Manual verification in Play Mode

No code changes in this task — verify acceptance criteria end-to-end in the running game.

- [ ] **Step 1: Pre-clean — delete any existing save file**

> **Unity Editor task (user):** Delete the save file so the run starts fresh. macOS: `~/Library/Application Support/<Company>/<Product>/savegame.json`. Windows: `%userprofile%\AppData\LocalLow\<Company>\<Product>\savegame.json`. (`Application.persistentDataPath` — see the dev note in the DEV-61 ticket.)

- [ ] **Step 2: Verify same-session behavior still works (regression guard)**

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/Platformer.unity` → Press Play.
> 2. Trigger a battle with a test enemy → win the battle.
> 3. On return, confirm the enemy is gone from the world (existing DEV-35 behavior).
> 4. Stay in Play Mode → verify nothing crashes or logs warnings.

- [ ] **Step 3: Verify cross-session persistence (the DEV-61 win condition)**

> **Unity Editor task (user):**
> 1. Still in Play Mode from Step 2, trigger any save-emitting action (the existing DEV-41 save on scene transition — e.g. enter/leave a checkpoint or battle). Alternatively stop Play Mode cleanly if the project saves on exit — whichever save trigger DEV-41 uses.
> 2. Stop Play Mode.
> 3. Open the `savegame.json` file at `Application.persistentDataPath`. Confirm a `defeatedEnemyIds` array is present with the defeated enemy's ID string (human-readable JSON).
> 4. Press Play again → on main menu click `Continue` (or equivalent DEV-42 entry point).
> 5. Verify the previously-defeated enemy does NOT reappear in the Platformer scene.

- [ ] **Step 4: Verify backward compatibility with pre-DEV-61 saves**

> **Unity Editor task (user):**
> 1. Stop Play Mode.
> 2. Open `savegame.json` in a text editor and DELETE just the `defeatedEnemyIds` line (simulate a legacy save from before this ticket). Save the file.
> 3. Press Play → Continue. Expected: loads cleanly, no exceptions in Console, no warning spam. All previously-defeated enemies reappear (since the legacy file has no record of them — correct fallback behavior).
> 4. Console must be clean — no `NullReferenceException`, no `[SaveService] Load failed` warnings.

- [ ] **Step 5: No check-in for this task**

This task is verification only. No files changed; nothing to check in.

---

## Acceptance Criteria Coverage (from DEV-61)

| Criterion | Where satisfied |
|---|---|
| `SaveData` DTO extended with a `DefeatedEnemyIds` collection serialized to JSON | Task 1 (field + JSON round-trip test) |
| On save, `SaveService` captures the current contents of `_defeatedEnemyIds` | Task 3 (`BuildSaveData` + `CopyHashSet` + round-trip test) |
| On load, saved defeated IDs are restored into `_defeatedEnemyIds` | Task 3 (`ApplySaveData` calls `RestoreDefeatedEnemies`) |
| `RestoreDefeatedEnemies(IEnumerable<string>)` exists as companion to `ClearDefeatedEnemies()` | Task 2 |
| Backward compatibility: legacy save files load cleanly with empty set, no crash, no warning spam | Task 1 Step 1 (`BackwardCompat_MissingDefeatedEnemyIds_DeserializesAsEmpty`) + Task 4 Step 4 (manual verify) |
| After load, re-entering Platformer skips/removes previously defeated enemies | Task 3 (`PersistAndLoad_RoundTrip` unit test) + Task 4 Step 3 (manual verify) |
| Save file remains human-readable JSON | Uses existing `JsonUtility.ToJson(prettyPrint: true)` in `SaveService.Save` — no change needed |
| `ClearDefeatedEnemies()` wired to New Game flow in DEV-42 | **Out of scope** — ticket explicitly notes this belongs to DEV-42 |
