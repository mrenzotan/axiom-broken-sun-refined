# DEV-41: Save/Load System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist player progression and world snapshot to human-readable JSON under `Application.persistentDataPath` using `System.IO`, with autosave on Platformer ↔ Battle transitions and at world save points; load on Continue restores `GameManager` state before gameplay; corrupt/missing files fail gracefully (logged warning, no crash).

**Architecture:** `SaveData` lives in `Axiom.Data` as pure `[Serializable]` DTOs (no `UnityEngine.Object` references). `SaveService` is plain C# in `Axiom.Core` beside `GameManager`, owns path + `File` I/O + `JsonUtility` round-trip. `GameManager` remains the **only** cross-scene state owner (`CLAUDE.md`); it exposes `CaptureWorldState` / `BuildSaveData` / `ApplySaveData` / `PersistToDisk()` so `Axiom.Platformer` and `Axiom.Battle` never reference `SaveService` directly. Spell IDs, item IDs, and XP/level fields align with Phase 5 data tickets (`DEV-39` inventory, `DEV-40` XP/level) — stub defaults are acceptable until those systems land, but the **save file shape** must match the Jira acceptance list now.

**Tech Stack:** Unity 6 LTS, URP 2D, C# 9, NUnit via Unity Test Framework (Edit Mode in `Assets/Tests/Editor/Core/`, Play Mode only where `GameManager` lifecycle requires it), `JsonUtility` + `System.IO.File`, UVCS for primary check-ins (`docs/VERSION_CONTROL.md`); optional `git commit` mirror uses `feat(DEV-41): …`.

---

## Project context (read before coding)

| Source | What applies to this ticket |
|--------|-----------------------------|
| `docs/GAME_PLAN.md` §Phase 5 | Save/load via `System.IO` JSON; save on scene transition and at save points; progression persists between sessions; label `phase-5-data`. |
| `docs/GAME_PLAN.md` §6 Key Architectural Decisions | `GameManager` DontDestroyOnLoad as state owner; **Save Format = JSON via System.IO**. |
| `docs/PROJECT_OVERVIEW.md` | Phase 5 bundles save/load with XP/leveling and ScriptableObject-driven data — coordinate field ownership with `DEV-39` / `DEV-40` so one source of truth. |
| `docs/VERSION_CONTROL.md` | **UVCS** is the collaboration source of truth (include scenes/prefabs); Git tracks scripts/docs only — scene wiring steps must be checked in via UVCS. |
| `docs/GAME_DESIGN_DOCUMENT.md` | Progression spans platformer exploration + voice-driven combat; saves must preserve enough state that returning from battle matches Phase 4 “world state restore” expectations. |
| `docs/LORE_AND_MECHANICS.md` | No narrative change required; persistence is a systems concern. |
| `CLAUDE.md` | MonoBehaviours = lifecycle only; **no static singleton except `GameManager`**; UI scripts under `Battle/UI/` or `Platformer/UI/` — **no** `Assets/Scripts/UI/`; ScriptableObject-driven IDs for spells/items. |
| `.claude/skills/unity-developer/SKILL.md` | Prefer UTF (Edit/Play Mode) for regression tests; validate I/O and error paths without relying on manual play alone. |
| `.claude/skills/game-development/2d-games/SKILL.md` | Save points are world checkpoints (clear collision/trigger layering, readable player-facing placement). |

---

## Current state (repository)

**Already implemented:**

- `GameManager` (`Assets/Scripts/Core/GameManager.cs`) — `DontDestroyOnLoad`, singleton guard, owns `PlayerState`, `PendingBattle` for battle handoff.
- `PlayerState` (`Assets/Scripts/Core/PlayerState.cs`) — HP/MP, combat stats, `ActiveSceneName`, `InventoryItemIds` (`List<string>` placeholder per file comment). **No** level, XP, unlocked spells, or world position yet — add or bridge as part of this ticket so `SaveData` can round-trip the Jira payload.
- Platformer → Battle — `ExplorationEnemyCombatTrigger` sets `PendingBattle` then `SceneManager.LoadScene("Battle")`.
- Battle → Platformer — `BattleController` loads `"Platformer"` on `BattleState.Fled` only (no other `LoadScene` call sites in `Assets/Scripts` yet).

**Missing (scope of DEV-41):**

- `SaveData` + `SaveService` + disk path + corrupt/missing handling.
- `GameManager` APIs to map runtime state ↔ `SaveData`, snapshot player world position before battle transition, restore after load.
- Autosave calls immediately **before** `SceneManager.LoadScene` on both transition scripts.
- `SavePointTrigger` (or equivalent) in platformer for designated save points.
- Continue / load entry hook — **coordinate with `DEV-42`** (main menu not in repo yet); implement a small `Axiom.Core` API that the menu can call without inventing forbidden `Assets/Scripts/UI/` paths.

**Dependencies / ordering:**

- `DEV-42` — Continue disabled when no save; calls load before scene load.
- `DEV-39` / `DEV-40` — when landed, progression fields must feed the same `SaveData` keys (avoid parallel duplicate state).

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Data/SaveData.cs` | Serializable DTO: level, XP, HP/MP, spell IDs, inventory entries, world position, scene name |
| Create | `Assets/Scripts/Core/SaveService.cs` | `HasSave`, `Save`, `TryLoad`, optional `DeleteSave`; `System.IO.File`; `Application.persistentDataPath` |
| Modify | `Assets/Scripts/Core/GameManager.cs` | Map to/from `SaveData`, `PersistToDisk()`, world snapshot hooks |
| Modify | `Assets/Scripts/Core/PlayerState.cs` | Fields/mutators for any persisted stats not yet modeled (level, XP, spells, last world pose) — keep plain C# |
| Modify | `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Before `LoadScene("Battle")`, snapshot player position + call `GameManager.PersistToDisk()` |
| Modify | `Assets/Scripts/Battle/BattleController.cs` | Before `LoadScene("Platformer")`, call `GameManager.PersistToDisk()` |
| Create | `Assets/Scripts/Platformer/SavePointTrigger.cs` | Interact/trigger → persist (2D trigger/collider friendly per 2D skill) |
| Create | TBD by `DEV-42` under `Platformer/UI/` or `Core/` | Continue/New Game wiring — must call `SaveService`/`GameManager` load API |
| Create | `Assets/Tests/Editor/Core/SaveServiceTests.cs` | Edit Mode tests: write/read/corrupt/missing |
| Modify | `Assets/Tests/Editor/Core/GameManagerPendingBattleTests.cs` or new sibling | Tests for save mapping / `PersistToDisk` where practical in Edit Mode |

No new `.asmdef` files — use existing `Axiom.Data`, `Axiom.Core`, `Axiom.Platformer`, `Axiom.Battle`, `CoreTests`.

---

## Task 1: `SaveData` DTO (Axiom.Data)

**Files:**

- Create: `Assets/Scripts/Data/SaveData.cs`
- Create: `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`

### Step 1.1 — Add the DTO file

Create `Assets/Scripts/Data/SaveData.cs` in namespace `Axiom.Data`:

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
    }
}
```

> **Note:** `JsonUtility` does not serialize `List<>` at the root cleanly; arrays are used intentionally. Conversion from `List<string>` / inventory lists happens in `GameManager` mapping.

### Step 1.2 — Add Edit Mode round-trip test

Create `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`:

```csharp
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class SaveDataSerializationTests
    {
        [Test]
        public void SaveData_JsonUtility_RoundTrip_PreservesFields()
        {
            var original = new SaveData
            {
                playerLevel = 4,
                playerXp = 1200,
                currentHp = 37,
                currentMp = 12,
                maxHp = 100,
                maxMp = 50,
                unlockedSpellIds = new[] { "spell_a", "spell_b" },
                inventory = new[]
                {
                    new InventorySaveEntry { itemId = "potion_hp", quantity = 3 }
                },
                worldPositionX = 12.5f,
                worldPositionY = -3.25f,
                activeSceneName = "Platformer"
            };

            string json = JsonUtility.ToJson(original, prettyPrint: true);
            var copy = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(original.playerLevel, copy.playerLevel);
            Assert.AreEqual(original.playerXp, copy.playerXp);
            Assert.AreEqual(original.currentHp, copy.currentHp);
            Assert.AreEqual(original.currentMp, copy.currentMp);
            Assert.AreEqual(original.maxHp, copy.maxHp);
            Assert.AreEqual(original.maxMp, copy.maxMp);
            Assert.AreEqual(original.unlockedSpellIds.Length, copy.unlockedSpellIds.Length);
            Assert.AreEqual("spell_a", copy.unlockedSpellIds[0]);
            Assert.AreEqual(1, copy.inventory.Length);
            Assert.AreEqual("potion_hp", copy.inventory[0].itemId);
            Assert.AreEqual(3, copy.inventory[0].quantity);
            Assert.AreEqual(original.worldPositionX, copy.worldPositionX);
            Assert.AreEqual(original.worldPositionY, copy.worldPositionY);
            Assert.AreEqual(original.activeSceneName, copy.activeSceneName);
        }
    }
}
```

### Step 1.3 — Run tests

> **Unity Editor task (user):** Window → General → Test Runner → **Edit Mode** → run `SaveData_JsonUtility_RoundTrip_PreservesFields`.
> **Expected:** PASS. JSON string is human-readable (pretty-printed).

### Step 1.4 — Check in (UVCS + optional Git)

> **Unity Editor task (user):** UVCS → Pending Changes → stage `SaveData.cs`, `SaveDataSerializationTests.cs`, and any `.meta` files Unity generated.
>
> Check in message: `feat(DEV-41): add SaveData DTO and JSON round-trip test`

---

## Task 2: `SaveService` (disk + corrupt handling)

**Files:**

- Create: `Assets/Scripts/Core/SaveService.cs`
- Create: `Assets/Tests/Editor/Core/SaveServiceTests.cs`

### Step 2.1 — Implement `SaveService`

Create `Assets/Scripts/Core/SaveService.cs`. Use a **directory override** parameter so Edit Mode tests write under `Temp` / a disposable folder without touching the real persistent path:

```csharp
using System;
using System.IO;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    public sealed class SaveService
    {
        public const string DefaultFileName = "savegame.json";

        private readonly string _directory;

        public SaveService(string directoryOverride = null)
        {
            _directory = string.IsNullOrEmpty(directoryOverride)
                ? Application.persistentDataPath
                : directoryOverride;
        }

        private string FullPath => Path.Combine(_directory, DefaultFileName);

        public bool HasSave() => File.Exists(FullPath);

        public void Save(SaveData data)
        {
            Directory.CreateDirectory(_directory);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(FullPath, json);
        }

        public bool TryLoad(out SaveData data)
        {
            data = null;
            if (!HasSave()) return false;

            try
            {
                string json = File.ReadAllText(FullPath);
                var parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null) return false;
                data = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Load failed — treating as no save. {ex.Message}");
                return false;
            }
        }

        public void DeleteSave()
        {
            if (HasSave())
                File.Delete(FullPath);
        }
    }
}
```

### Step 2.2 — Edit Mode tests for `SaveService`

Create `Assets/Tests/Editor/Core/SaveServiceTests.cs` — use `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))` as the service directory each test:

- `HasSave_False_WhenNoFile`
- `Save_Then_TryLoad_RestoresPayload` (reuse field assertions from Task 1)
- `TryLoad_ReturnsFalse_OnCorruptJson` (write `{ not valid json` to file)
- `TryLoad_DoesNotThrow_OnCorruptJson`

### Step 2.3 — Run tests

> **Unity Editor task (user):** Test Runner → Edit Mode → run all tests in `SaveServiceTests`.
> **Expected:** All PASS; corrupt case logs a **warning**, returns false, no exception.

### Step 2.4 — Check in

> **Unity Editor task (user):** UVCS check in: `feat(DEV-41): add SaveService with safe TryLoad fallback`

---

## Task 3: Extend `PlayerState` + `GameManager` mapping

**Files:**

- Modify: `Assets/Scripts/Core/PlayerState.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify or create tests under `Assets/Tests/Editor/Core/`

### Step 3.1 — Extend `PlayerState` for save-relevant fields

Add **plain C#** fields with clear invariants, for example:

- `int Level`, `int Xp`
- `List<string> UnlockedSpellIds` (or `HashSet` + serialization conversion in `GameManager` — pick one; lists are easier for Unity serialization if you ever inspect state in Editor)

Add methods such as `ApplyProgression(int level, int xp)`, `SetWorldPosition(float x, float y)`, and spell/inventory mutators **without** introducing new singletons. Keep validation consistent with existing constructor style.

> **Coordination:** If `DEV-40` lands first, merge their naming; if DEV-41 lands first, use minimal stubs (`Level = 1`, `Xp = 0`, empty spell list) so gameplay unchanged until progression exists.

### Step 3.2 — `GameManager` responsibilities

On `GameManager`:

- Hold a `SaveService` instance (created in `Awake`, or lazy — single place).
- `SaveData BuildSaveData()` — copy from `PlayerState` + any other cross-scene fields you own later.
- `void ApplySaveData(SaveData data)` — replace or update `PlayerState` from disk; then refresh any cached references systems already hold (document follow-up if some systems cache stats only in `Start()`).
- `void PersistToDisk()` — `Save(BuildSaveData())`.
- `bool TryLoadFromDiskIntoGame()` — `TryLoad` + `ApplySaveData` on success; on failure log warning and return false (caller: main menu / bootstrap).

### Step 3.3 — Edit Mode tests

Extend `GameManagerPendingBattleTests` or add `GameManagerSaveDataTests.cs`:

- After creating `GameManager`, call `ApplySaveData` with a fabricated `SaveData`, assert `PlayerState` reflects HP/MP/level/position.
- `PersistToDisk` with directory override: inject a `SaveService` **test-only** path — simplest pattern: `internal` setter on `GameManager` for save directory used only from tests in same assembly, **or** pass `SaveService` via `InitializeForTests(SaveService s)` method guarded by `#if UNITY_INCLUDE_TESTS`. Pick the least invasive pattern consistent with the codebase.

### Step 3.4 — Run tests

> **Unity Editor task (user):** Edit Mode → run new `GameManager` save mapping tests.
> **Expected:** PASS.

### Step 3.5 — Check in

> **UVCS:** `feat(DEV-41): map PlayerState to SaveData on GameManager`

---

## Task 4: Autosave on scene transitions

**Files:**

- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`

### Step 4.1 — Platformer → Battle

In `TriggerBattle`, **after** `SetPendingBattle` and **before** `SceneManager.LoadScene("Battle")`:

1. Resolve player transform (e.g. `other.transform` from trigger path, or `FindFirstObjectByType` for advantaged path — document the chosen rule).
2. `GameManager.Instance` non-null: call something like `CaptureWorldSnapshot(Vector2 position)` then `PersistToDisk()`.

### Step 4.2 — Battle → Platformer

In `BattleController` where `BattleState.Fled` loads `"Platformer"`, call `GameManager.Instance?.PersistToDisk()` **before** `LoadScene`.

> **Future:** When victory or other exits load `Platformer`, apply the same ordering (save first).

### Step 4.3 — Play Mode smoke (manual)

> **Unity Editor task (user):** Play Mode — enter battle via trigger, flee, return. Confirm `savegame.json` appears under persistent data path and updates timestamps.
> **Expected:** No console errors; file content matches `SaveData` schema.

### Step 4.4 — Check in

> **UVCS** (scripts + any scene/prefab if you added debug objects): `feat(DEV-41): autosave before Platformer battle transitions`

---

## Task 5: World save points (platformer)

**Files:**

- Create: `Assets/Scripts/Platformer/SavePointTrigger.cs`

### Step 5.1 — Behavior

- Use `Collider2D` **Is Trigger** on a dedicated layer; on `OnTriggerEnter2D` with `Player` tag (match `ExplorationEnemyCombatTrigger` conventions).
- Optional: require button interact for clearer player intent (2D clarity — avoid accidental saves every frame).
- Call `GameManager.Instance.CaptureWorldSnapshot(...)` + `PersistToDisk()`.

### Step 5.2 — Manual Editor setup

> **Unity Editor task (user):** In `Assets/Scenes/Platformer.unity`, place save-point prefabs/objects at designated checkpoints; assign collider size, layer, and any VFX/audio references consistent with existing platformer feel.

### Step 5.3 — Check in

> **UVCS** (script + scene/prefab): `feat(DEV-41): add platformer save point triggers`

---

## Task 6: Continue / load API for `DEV-42`

**Files:**

- Modify: `Assets/Scripts/Core/GameManager.cs` (public API surface only)
- Modify: menu script **when `DEV-42` adds it** (expected under `Platformer/UI/` or `Core/` — not `Assets/Scripts/UI/`)

### Step 6.1 — Expose stable calls for UI

- `bool HasSaveFile()` → delegates to `SaveService.HasSave()`
- `bool TryContinueGame()` → `TryLoadFromDiskIntoGame()` then `SceneManager.LoadScene` to the correct start scene (likely `Platformer` — confirm in `DEV-42`)

### Step 6.2 — Acceptance: corrupt file

- If `TryLoad` fails, log warning, **do not** throw; main menu should treat as no valid save (Continue disabled — `DEV-42`).

### Step 6.3 — Check in

> **UVCS:** `feat(DEV-41): expose load/continue hooks for main menu integration`

---

## Final state (checklist against Jira DEV-41)

- [ ] `SaveService` serializes/deserializes `SaveData` with: level, XP, current HP/MP, unlocked spells, inventory, world position (and max HP/MP as needed for consistency).
- [ ] Autosave on Platformer ↔ Battle transitions (both directions currently in codebase).
- [ ] Save points in world call the same persistence path.
- [ ] File path uses `Application.persistentDataPath` + `System.IO.File`; JSON is human-readable.
- [ ] Missing/corrupt save: warning logged, no crash; Continue behavior owned with `DEV-42`.
- [ ] `docs/VERSION_CONTROL.md`: scenes/prefabs checked in through **UVCS**; scripts/docs through UVCS + Git mirror as usual.

---

## Self-review

**Spec coverage (DEV-41 + Phase 5 alignment):**

- JSON + `System.IO` + persistent path — Tasks 1–2 ✓
- Payload fields — Task 1 + 3 ✓
- Scene transition saves — Task 4 ✓
- Save points — Task 5 ✓
- Continue/load — Task 6 + `DEV-42` ✓
- Corrupt/missing handling — Task 2 + 6 ✓

**Placeholder scan:** No TBD file paths for existing code; menu path explicitly TBD by `DEV-42` (accurate for repo).

**Type consistency:** `SaveData` uses `InventorySaveEntry` + arrays throughout Task 1 test and Task 2 service — consistent.

**Assembly rules:** `SaveData` in `Axiom.Data`; I/O in `Axiom.Core`; triggers in `Axiom.Platformer` / `Axiom.Battle` call `GameManager` only — avoids circular dependencies and matches `CLAUDE.md` singleton rule.

---

**Plan complete and saved to** `docs/superpowers/plans/2026-04-14-dev-41-save-load-system.md`.

**Execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks.  
2. **Inline execution** — `executing-plans` with batch checkpoints.

Which approach do you want?
