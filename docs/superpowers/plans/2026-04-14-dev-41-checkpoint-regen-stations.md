# DEV-41 Addendum: Checkpoint Regen Stations - Implementation Plan (Improved)

**Goal:** Upgrade platformer save checkpoints into one-time regen stations (per checkpoint per save) that fully restore HP/MP and show floating `+HP` and `+MP` numbers above the player.

**Primary constraints from project architecture:**
- `GameManager` remains the only singleton owner of cross-scene save/runtime state.
- `SavePointTrigger` stays thin (Unity lifecycle + dependency wiring only).
- Persistence remains JSON via existing `SaveService` / `SaveData` flow.
- UI scripts stay in scene-owned folders (`Assets/Scripts/Platformer/` is valid).

**Acceptance alignment (DEV-41):**
- Save/load must stay resilient to missing/corrupt save data (no crash).
- New checkpoint payload must survive save/load round trips.
- Save file remains human-readable JSON under `Application.persistentDataPath`.

---

## Context Baseline (Verified)

- Runtime asmdef style in this repo: `Axiom.<Module>` (example: `Axiom.Platformer`).
- Existing changes are in existing assemblies (`Axiom.Core`, `Axiom.Data`, `Axiom.Platformer`), so no new asmdef is required for this addendum.
- Edit Mode tests are already in `Assets/Tests/Editor/Core/` under `CoreTests`.
- Existing scenes: `Platformer`, `Battle`, `SampleScene`.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Assets/Scripts/Data/SaveData.cs` | Persist `activatedCheckpointIds` |
| Modify | `Assets/Scripts/Core/PlayerState.cs` | Track activated checkpoint IDs and expose helper APIs |
| Modify | `Assets/Scripts/Core/GameManager.cs` | Save/load mapping + one-time regen orchestration |
| Modify | `Assets/Scripts/Platformer/SavePointTrigger.cs` | Trigger integration and floating-number dispatch |
| Create | `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs` | World-space pooled floating-number spawner |
| Create | `Assets/Scripts/Platformer/PlatformerFloatingNumberInstance.cs` | Floating-number instance animation + recycle |
| Modify | `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs` | SaveData JSON round-trip for checkpoint IDs |
| Modify | `Assets/Tests/Editor/Core/PlayerStateTests.cs` | Checkpoint ID API behavior and invariants |
| Modify | `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs` | Save/load mapping and regen workflow |

---

## Task 1: Persist Activated Checkpoint IDs in SaveData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`

- [ ] **Step 1.1 - Add SaveData payload field**
  - Add:
    - `public string[] activatedCheckpointIds = Array.Empty<string>();`
  - Keep field name stable (disk schema contract).

- [ ] **Step 1.2 - Extend SaveData round-trip test**
  - Populate `activatedCheckpointIds` with at least two values (for example: `cp_platformer_01`, `cp_platformer_02`).
  - Assert post-deserialization length and element order.
  - Add empty-array coverage to confirm default behavior remains valid.

- [ ] **Step 1.3 - Edit Mode verification**
  - Unity Test Runner -> Edit Mode -> run `SaveDataSerializationTests`.
  - Expected: PASS.

- [ ] **Check in via UVCS:**
  Unity Version Control -> Pending Changes -> stage the files listed below -> Check in with message: `feat(DEV-41): persist activated checkpoint ids in save data`
  - `Assets/Scripts/Data/SaveData.cs`
  - `Assets/Scripts/Data/SaveData.cs.meta`
  - `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs`
  - `Assets/Tests/Editor/Core/SaveDataSerializationTests.cs.meta`

---

## Task 2: Add PlayerState and GameManager Checkpoint APIs

**Files:**
- Modify: `Assets/Scripts/Core/PlayerState.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Tests/Editor/Core/PlayerStateTests.cs`
- Modify: `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`

- [ ] **Step 2.1 - Add PlayerState checkpoint collection and contracts**
  - Add internal storage for activated checkpoint IDs (no duplicates).
  - Add APIs:
    - `bool HasActivatedCheckpoint(string checkpointId)`
    - `bool MarkCheckpointActivated(string checkpointId)`
    - `void SetActivatedCheckpointIds(IEnumerable<string> checkpointIds)`
    - read-only exposure for save mapping (for example `IReadOnlyList<string>`).
  - Invalid IDs (`null`/empty/whitespace) are ignored.

- [ ] **Step 2.2 - Guard ordering requirement (must preserve early-exit correctness)**
  - For all new checkpoint methods:
    - Validate cheap early exits first (invalid ID, already activated).
    - Only after that do work that mutates state or triggers persistence.
  - Avoid guard order that throws/branches on values not needed by early-exit paths.

- [ ] **Step 2.3 - Map checkpoint IDs in GameManager save/load**
  - `BuildSaveData()` copies activated IDs into `SaveData.activatedCheckpointIds`.
  - `ApplySaveData()` restores IDs into `PlayerState` with null-safe fallback (`Array.Empty<string>()`).

- [ ] **Step 2.4 - Add one-time regen orchestration in GameManager**
  - Add API:
    - `bool TryActivateCheckpointRegen(string checkpointId, out int healedHp, out int healedMp)`
  - Required behavior:
    - Returns `false` for invalid ID or already-activated checkpoint.
    - Computes heal amounts as deltas to max (`MaxHp - CurrentHp`, `MaxMp - CurrentMp`), clamped at `>= 0`.
    - Applies full restore to current HP/MP.
    - Marks checkpoint activated exactly once.
    - Persists to disk after successful first activation.

- [ ] **Step 2.5 - Extend Edit Mode tests (branch-complete)**
  - `PlayerStateTests`:
    - add/contains behavior
    - duplicate add returns false / collection unchanged
    - null/empty/whitespace IDs ignored
    - replace/reset via `SetActivatedCheckpointIds(...)` works
  - `GameManagerSaveDataTests`:
    - `BuildSaveData()` includes activated IDs
    - `ApplySaveData()` restores activated IDs
    - first activation returns true and expected heal deltas
    - second activation on same ID returns false and zero heals
    - invalid checkpoint ID returns false and zero heals

- [ ] **Step 2.6 - Edit Mode verification**
  - Unity Test Runner -> Edit Mode -> run `PlayerStateTests` and `GameManagerSaveDataTests`.
  - Expected: PASS.

- [ ] **Check in via UVCS:**
  Unity Version Control -> Pending Changes -> stage the files listed below -> Check in with message: `feat(DEV-41): add one-time checkpoint regen state mapping`
  - `Assets/Scripts/Core/PlayerState.cs`
  - `Assets/Scripts/Core/PlayerState.cs.meta`
  - `Assets/Scripts/Core/GameManager.cs`
  - `Assets/Scripts/Core/GameManager.cs.meta`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs`
  - `Assets/Tests/Editor/Core/PlayerStateTests.cs.meta`
  - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs`
  - `Assets/Tests/Editor/Core/GameManagerSaveDataTests.cs.meta`

---

## Task 3: Keep SavePointTrigger Thin and Integrate One-Time Regen

**Files:**
- Modify: `Assets/Scripts/Platformer/SavePointTrigger.cs`

- [ ] **Step 3.1 - Add serialized checkpoint identity and dependencies**
  - Add `[SerializeField] private string checkpointId = string.Empty;`
  - Add `[SerializeField] private PlatformerFloatingNumberSpawner floatingNumberSpawner;`

- [ ] **Step 3.2 - Integrate GameManager checkpoint API**
  - In `OnTriggerEnter2D`:
    - Keep existing player-tag gate first.
    - Handle missing `GameManager.Instance` with warning and return.
    - Capture world snapshot before attempting regen.
    - Call `TryActivateCheckpointRegen(...)`.
  - Keep this MonoBehaviour as lifecycle/wiring only; no persistence/business logic duplication.

- [ ] **Step 3.3 - Failure path behavior**
  - Empty `checkpointId`: warn and skip regen path.
  - Already activated: no numbers shown, no duplicate activation.
  - Missing spawner: regen still succeeds; warning logged once per trigger event.

- [ ] **Check in via UVCS:**
  Unity Version Control -> Pending Changes -> stage the files listed below -> Check in with message: `feat(DEV-41): wire save point trigger to one-time regen flow`
  - `Assets/Scripts/Platformer/SavePointTrigger.cs`
  - `Assets/Scripts/Platformer/SavePointTrigger.cs.meta`

---

## Task 4: Implement Platformer World-Space Floating Numbers

**Files:**
- Create: `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`
- Create: `Assets/Scripts/Platformer/PlatformerFloatingNumberInstance.cs`
- Modify: `Assets/Scripts/Platformer/SavePointTrigger.cs`

- [ ] **Step 4.1 - Add pooled world-space components**
  - Implement pooled spawn/reuse flow (avoid repeated instantiate/destroy).
  - Numbers use platformer world space (not battle canvas-slot logic).
  - Visual policy:
    - HP heal: green `+<amount>`
    - MP heal: cyan/blue `+<amount>`

- [ ] **Step 4.2 - Emit numbers only on successful first activation**
  - After `TryActivateCheckpointRegen(...) == true`:
    - Spawn HP number only if `healedHp > 0`
    - Spawn MP number only if `healedMp > 0`
  - Use slight vertical/time offset between HP and MP numbers for readability.

- [ ] **Step 4.3 - Guard rails**
  - If spawner is missing, gameplay outcome is unaffected.
  - If heal amount is zero, skip that number (avoid noisy `+0`).

- [ ] **Check in via UVCS:**
  Unity Version Control -> Pending Changes -> stage the files listed below -> Check in with message: `feat(DEV-41): add platformer floating heal feedback`
  - `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs`
  - `Assets/Scripts/Platformer/PlatformerFloatingNumberSpawner.cs.meta`
  - `Assets/Scripts/Platformer/PlatformerFloatingNumberInstance.cs`
  - `Assets/Scripts/Platformer/PlatformerFloatingNumberInstance.cs.meta`
  - `Assets/Scripts/Platformer/SavePointTrigger.cs`
  - `Assets/Scripts/Platformer/SavePointTrigger.cs.meta`

---

## Task 5: Unity Editor Wiring and Manual QA

**Files:**
- `Assets/Scenes/Platformer.unity`
- Checkpoint prefabs/objects used in that scene

> **Unity Editor task (user):** Add one `PlatformerFloatingNumberSpawner` GameObject to `Platformer` scene, assign the floating text prefab/material, and tune motion/fade values.

> **Unity Editor task (user):** For each checkpoint object/prefab, assign a unique `checkpointId` and wire `floatingNumberSpawner`.

> **Unity Editor task (user):** Ensure each checkpoint collider remains `isTrigger = true` and player tag is `Player`.

- [ ] **Step 5.1 - Play Mode behavior pass**
  - Start below max HP/MP.
  - First touch on checkpoint A -> full restore + floating numbers.
  - Second touch on checkpoint A -> no second restore.
  - First touch on checkpoint B -> restore occurs once there.

- [ ] **Step 5.2 - Persistence behavior pass**
  - Save and stop play session.
  - Reload same save.
  - Previously activated checkpoint does not restore again.
  - Never-activated checkpoint still restores once.

- [ ] **Step 5.3 - Corruption/missing-save safety regression**
  - Confirm existing DEV-41 fallback behavior still applies (no crash, warning path).

- [ ] **Check in via UVCS:**
  Unity Version Control -> Pending Changes -> stage the files listed below -> Check in with message: `chore(DEV-41): wire platformer checkpoint regen stations in scene`
  - `Assets/Scenes/Platformer.unity`
  - `Assets/Scenes/Platformer.unity.meta`
  - `<each modified checkpoint prefab>.prefab`
  - `<each modified checkpoint prefab>.prefab.meta`

---

## Final State Checklist

- [ ] `SaveData` persists `activatedCheckpointIds` and round-trips correctly via `JsonUtility`.
- [ ] `PlayerState` exposes deterministic checkpoint APIs (invalid IDs ignored, duplicates blocked).
- [ ] `GameManager` save/load maps checkpoint IDs both directions.
- [ ] Checkpoint regen runs exactly once per checkpoint per save file.
- [ ] HP/MP floating numbers appear only on successful first activation and non-zero heals.
- [ ] Missing references/invalid IDs fail gracefully with warnings only.
- [ ] Edit Mode tests pass for `SaveData`, `PlayerState`, and `GameManager`.
- [ ] Platformer scene behavior validated manually in Play Mode.

---

## Post-Plan Review (Applied)

- **Guard ordering:** Added explicit requirement to place early exits before mutation/persistence work.
- **Test coverage:** Added null/empty/duplicate/already-activated/invalid-ID branches and first-vs-second activation tests.
- **Method signature consistency:** Added concrete method signatures for `PlayerState` and `GameManager` to avoid implementation/test drift.
- **Unity editor isolation:** All editor actions are in explicit `Unity Editor task (user)` callouts and separated from code tasks.
- **UVCS audit:** Every task now has a UVCS check-in step; `.meta` pairings are included for all `.cs` files and scene/prefab assets.

---

**Plan updated at:** `docs/superpowers/plans/2026-04-14-dev-41-checkpoint-regen-stations.md`
