# DEV-82 Puzzle Persistence + Success Cue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the two highest-value open DEV-82 acceptance criteria — (1) a melted Ice Wall stays melted across a Battle round-trip within the same level run, and (2) a clear visual + audio cue fires on a successful environmental spell cast.

**Architecture:** Persistence reuses the existing `GameManager` per-scene session-flag pattern (mirrors `IsEnemyDefeated`/`MarkEnemyDefeated`) plus the existing `PlatformerWorldRestoreController.Start()` re-apply hook — **not** the transient `WorldSnapshot` (which is cleared after one restore). Save-file persistence is explicitly out of DEV-82 scope, so the in-memory set (survives scene loads via `DontDestroyOnLoad`) is sufficient. The success cue is per-prefab VFX (a `ParticleSystem`) plus a local `AudioSource` routed through the existing `AudioManager.RouteSourceThroughSfxBus` so the SFX volume slider applies — no new central audio method.

**Tech Stack:** Unity 6.0.4 LTS, C#, URP 2D, Unity Test Framework (Edit Mode / NUnit), UVCS for check-ins.

---

## Confirmed design decisions (call out before executing)

1. **Persistence applies to one-way puzzles only.** `MeltableObstacleController` (Ice Wall) persists. `FreezablePlatformController` (water platform) does **NOT** — its timed revert to water is intentional. This plan touches the freeze platform only for the success cue (Task 5), never for persistence.
2. **Player death re-forms solved puzzles in the respawn scene.** Mirroring how `RespawnAtLastCheckpoint` already clears that scene's defeated enemies and damaged HP, it also clears that scene's solved-puzzle set — so dying re-forms the Ice Wall (and any future non-temporary obstacle) in the scene you respawn into, forcing a re-solve. Puzzles solved in *other* already-cleared scenes stay solved (identical scoping to enemies — see the `ClearDefeatedEnemiesInScene` comment at `GameManager.cs:566–567`). A full reset still happens only on `StartNewGame()`. Intentionally-temporary puzzles (the freeze platform) are unaffected — they carry no persistence.
3. **Keying is per-scene** (matches the dominant `_defeatedEnemiesByScene` precedent and inherits the existing `SetActiveScene` resync at `PlatformerWorldRestoreController.cs:36`). Puzzle IDs need only be unique *within a scene*. The re-apply loop therefore must run *after* that resync (Task 3).

---

## File Structure

| File | Change | Responsibility |
|------|--------|----------------|
| `Assets/Scripts/Core/GameManager.cs` | Modify | Add per-scene solved-puzzle session set + Mark/Is/Clear API; clear-all on New Game; clear-scene on death/respawn |
| `Assets/Tests/Editor/Core/GameManagerPuzzleTests.cs` | Create | Edit Mode tests for the solved-puzzle API (mirrors `GameManagerPickupTests`) |
| `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs` | Modify | Assert `StartNewGame` clears solved puzzles |
| `Assets/Scripts/Platformer/MeltableObstacleController.cs` | Modify | Add stable `PuzzleId`; mark solved on melt; `ApplySolvedImmediate()` for restore; success cue |
| `Assets/Scripts/Platformer/FreezablePlatformController.cs` | Modify | Success cue only (no persistence) |
| `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs` | Modify | Re-apply solved puzzles on scene load after the active-scene resync |

No new `.asmdef` files are needed — every modified script lives in an existing assembly (`Axiom.Core`, `Platformer`) and the test file belongs to the existing `Assets/Tests/Editor/Core/CoreTests.asmdef`.

---

## Task 1: GameManager solved-puzzle session set (per-scene) + Edit Mode tests

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Test: `Assets/Tests/Editor/Core/GameManagerPuzzleTests.cs` (create)
- Modify: `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/GameManagerPuzzleTests.cs`. This mirrors `GameManagerPickupTests` exactly (same SetUp/TearDown harness) but exercises the new per-scene API:

```csharp
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerPuzzleTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                Object.DestroyImmediate(_gameManagerObject);
        }

        [Test]
        public void IsPuzzleSolved_ReturnsFalse_ByDefault()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void IsPuzzleSolved_ReturnsFalse_ForNullOrEmpty()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            Assert.IsFalse(_gameManager.IsPuzzleSolved(null));
            Assert.IsFalse(_gameManager.IsPuzzleSolved(string.Empty));
        }

        [Test]
        public void MarkPuzzleSolved_ThenIsPuzzleSolved_ReturnsTrue_InSameScene()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");
            Assert.IsTrue(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void MarkPuzzleSolved_IsScopedPerScene()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");

            _gameManager.PlayerState.SetActiveScene("Level_2-1");
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"),
                "Same puzzle ID in a different scene must not read as solved.");
        }

        [Test]
        public void MarkPuzzleSolved_NullOrEmpty_IsIgnored()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved(null);
            _gameManager.MarkPuzzleSolved(string.Empty);
            Assert.IsFalse(_gameManager.IsPuzzleSolved(null));
            Assert.IsFalse(_gameManager.IsPuzzleSolved(string.Empty));
        }

        [Test]
        public void ClearSolvedPuzzles_RemovesEverything()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");
            _gameManager.ClearSolvedPuzzles();
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }

        [Test]
        public void ClearSolvedPuzzlesInScene_LeavesOtherScenesIntact()
        {
            _gameManager.MarkPuzzleSolvedInScene("Level_1-1", "ice_wall_a");
            _gameManager.MarkPuzzleSolvedInScene("Level_1-2", "ice_wall_b");

            _gameManager.ClearSolvedPuzzlesInScene("Level_1-1");

            Assert.IsFalse(_gameManager.IsPuzzleSolvedInScene("Level_1-1", "ice_wall_a"),
                "Respawn-scene puzzles must re-form.");
            Assert.IsTrue(_gameManager.IsPuzzleSolvedInScene("Level_1-2", "ice_wall_b"),
                "Puzzles in other already-cleared scenes stay solved.");
        }

        private CharacterData CreateTestCharacterData(
            int maxHp = 100, int maxMp = 50, int atk = 10, int def = 5, int spd = 8)
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK = atk;
            cd.baseDEF = def;
            cd.baseSPD = spd;
            return cd;
        }
    }
}
```

> **Note:** This `CreateTestCharacterData(...)` is copied verbatim from `GameManagerPickupTests.cs` (parameterized with defaults; sets `characterName` + the five base stats). `SetUp` calls it with no args, which uses the defaults. If that source helper has drifted since this plan was written, re-copy it exactly rather than hand-editing the fields here.

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Editor → Window → General → Test Runner → EditMode → run `GameManagerPuzzleTests`.
Expected: FAIL — compile error, `IsPuzzleSolved` / `MarkPuzzleSolved` / `ClearSolvedPuzzles` do not exist on `GameManager`.

- [ ] **Step 3: Add the solved-puzzle field and API to GameManager**

In `Assets/Scripts/Core/GameManager.cs`, directly below the `_collectedPickupIds` field declaration (currently around line 229–230):

```csharp
        // Puzzles solved this playthrough, bucketed by the originating level scene
        // (PlayerState.ActiveSceneName), mirroring _defeatedEnemiesByScene. Persists for
        // the whole session via DontDestroyOnLoad so a one-way puzzle (e.g. a melted Ice
        // Wall) stays solved across a Battle round-trip. Save-file persistence is out of
        // DEV-82 scope — this set is intentionally NOT written to SaveData yet.
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _solvedPuzzlesByScene =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.Ordinal);
```

Then, directly above `public bool IsPickupCollected(...)` (around line 344), add the API. Note the guard ordering: the `enemyId`/`puzzleId` null check comes first because an invalid ID never needs the scene bucket.

```csharp
        // ── Solved puzzles (per-scene, session only) ────────────────────────

        /// <summary>
        /// True when the given puzzle ID has been solved in the player's current
        /// originating scene (<see cref="PlayerState.ActiveSceneName"/>).
        /// </summary>
        public bool IsPuzzleSolved(string puzzleId) =>
            IsPuzzleSolvedInScene(GetActiveSceneBucket(), puzzleId);

        public bool IsPuzzleSolvedInScene(string sceneName, string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return false;
            string key = sceneName ?? string.Empty;
            return _solvedPuzzlesByScene.TryGetValue(key, out System.Collections.Generic.HashSet<string> set)
                && set.Contains(puzzleId);
        }

        /// <summary>
        /// Records the puzzle as solved under the player's current originating scene.
        /// Null/empty IDs are silently ignored.
        /// </summary>
        public void MarkPuzzleSolved(string puzzleId) =>
            MarkPuzzleSolvedInScene(GetActiveSceneBucket(), puzzleId);

        public void MarkPuzzleSolvedInScene(string sceneName, string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return;
            string key = sceneName ?? string.Empty;
            if (!_solvedPuzzlesByScene.TryGetValue(key, out System.Collections.Generic.HashSet<string> set))
            {
                set = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                _solvedPuzzlesByScene[key] = set;
            }
            set.Add(puzzleId);
        }

        /// <summary>Clears every scene's solved-puzzle set. Used by StartNewGame.</summary>
        public void ClearSolvedPuzzles() => _solvedPuzzlesByScene.Clear();

        /// <summary>
        /// Clears only the named scene's solved-puzzle set. Used by RespawnAtLastCheckpoint
        /// so dying re-forms that scene's one-way obstacles (e.g. a melted Ice Wall) while
        /// puzzles solved in other already-cleared scenes stay solved.
        /// </summary>
        public void ClearSolvedPuzzlesInScene(string sceneName) =>
            _solvedPuzzlesByScene.Remove(sceneName ?? string.Empty);
```

> The existing file already has `using System;` and `using System.Collections.Generic;` at the top — you may drop the fully-qualified `System.` / `System.Collections.Generic.` prefixes above to match house style. They are written long-form here only to be unambiguous about the types.

- [ ] **Step 4: Run the tests to verify they pass**

Run `GameManagerPuzzleTests` in the Test Runner.
Expected: PASS (all 6 tests).

- [ ] **Step 5: Add the failing New Game test**

In `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`, add a test that mirrors the existing `StartNewGame_ClearsDefeatedEnemies` test already in that file (that is the closest in-file analog — the pickup-clear test, `StartNewGame_ClearsCollectedPickups`, lives in `GameManagerPickupTests.cs`, not here). It must seed the player CharacterData so `StartNewGame` does not early-return:

```csharp
        [Test]
        public void StartNewGame_ClearsSolvedPuzzles()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            _gameManager.MarkPuzzleSolved("ice_wall_a");

            _gameManager.StartNewGame();

            _gameManager.PlayerState.SetActiveScene("Level_1-2");
            Assert.IsFalse(_gameManager.IsPuzzleSolved("ice_wall_a"));
        }
```

> If `StartNewGame` in this test harness loads a scene or needs `_playerCharacterData`, copy whatever setup the neighbouring `StartNewGame_ClearsDefeatedEnemies` test already does (its `SetUp` seeds CharacterData the same way). Do not invent new harness wiring.

- [ ] **Step 6: Run the new test to verify it fails**

Expected: FAIL — `StartNewGame` does not yet clear the solved-puzzle set.

- [ ] **Step 7: Clear solved puzzles in StartNewGame**

In `GameManager.cs`, inside `StartNewGame()`, alongside the existing clear calls (currently `ClearPendingBattle(); ClearWorldSnapshot(); ClearDefeatedEnemies(); ClearAllDamagedEnemyHp(); ClearCollectedPickups();` around lines 622–626), add:

```csharp
            ClearSolvedPuzzles();
```

- [ ] **Step 8: Run the New Game test to verify it passes**

Expected: PASS.

- [ ] **Step 9: Clear the respawn scene's solved puzzles on death**

In `GameManager.cs`, inside `RespawnAtLastCheckpoint`, alongside the existing per-scene clears (currently `ClearDefeatedEnemiesInScene(sceneToLoad); ClearAllDamagedEnemyHpInScene(sceneToLoad);` around lines 568–569), add:

```csharp
            ClearSolvedPuzzlesInScene(sceneToLoad);
```

This re-forms the respawn scene's one-way puzzles (e.g. a melted Ice Wall) on death, while puzzles solved in other already-cleared scenes stay solved — matching the existing enemy/HP scoping. `ClearSolvedPuzzlesInScene` is already covered by the `ClearSolvedPuzzlesInScene_LeavesOtherScenesIntact` Edit Mode test from Step 1; the full death→respawn flow is verified in Play Mode (Task 3, Step 6).

- [ ] **Step 10: Re-run the full GameManager Edit Mode suite to verify nothing regressed**

Run `GameManagerPuzzleTests`, `GameManagerNewGameTests`, and `GameManagerSaveDataTests` in the Test Runner.
Expected: PASS (no regressions from the `RespawnAtLastCheckpoint` edit).

- [ ] **Step 11: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-82): add per-scene solved-puzzle session set to GameManager`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Tests/Editor/Core/GameManagerPuzzleTests.cs`
- `Assets/Tests/Editor/Core/GameManagerPuzzleTests.cs.meta`
- `Assets/Tests/Editor/Core/GameManagerNewGameTests.cs`

---

## Task 2: Give MeltableObstacleController a stable PuzzleId, mark solved, expose instant-apply

**Files:**
- Modify: `Assets/Scripts/Platformer/MeltableObstacleController.cs`

This is a MonoBehaviour. The persistence *logic* lives in `GameManager` (Task 1); here we only add a serialized ID, a one-line mark on success, and a no-animation apply for restore — mirroring how `ItemPickup` references `GameManager.MarkPickupCollected`.

- [ ] **Step 1: Add the serialized PuzzleId field and public getter**

In `MeltableObstacleController.cs`, add to the serialized field block (below `_meltSpells` / `_fadeDuration`):

```csharp
        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the solved (melted) state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;
```

- [ ] **Step 2: Mark the puzzle solved at the moment of a successful melt**

In `TryMelt`, after `_isMelted = true;` and before/after `StartCoroutine(MeltCoroutine());`, record the solve. Guard on a non-empty ID and a live `GameManager` so Edit Mode / isolated scenes are unaffected (mirrors `ItemPickup`'s null guards):

```csharp
        public bool TryMelt(string spellId)
        {
            if (!CanMeltWith(spellId)) return false;

            _isMelted = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            StartCoroutine(MeltCoroutine());
            return true;
        }
```

Add `using Axiom.Core;` to the top of the file if it is not already present (it imports `GameManager`).

- [ ] **Step 3: Add ApplySolvedImmediate for restore (no animation, no cue)**

Add a public method that drops the obstacle straight to its terminal melted state — the same end-state `MeltCoroutine` reaches, minus the flash/fade and minus any success cue. The restore controller (Task 3) calls this:

```csharp
        /// <summary>
        /// Forces the terminal melted state with no animation and no success cue.
        /// Called on scene load by PlatformerWorldRestoreController when this puzzle
        /// was already solved earlier in the session.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            if (_isMelted && _solidCollider == null && (_tilemap == null || !_tilemap.gameObject.activeSelf))
                return; // already in terminal state

            _isMelted = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_tilemap != null)
                _tilemap.gameObject.SetActive(false);
        }
```

- [ ] **Step 4: Verify it compiles**

Return to the Unity Editor and let it recompile. Watch the Console.
Expected: no compile errors; no new warnings from this file.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → stage → Check in with message: `feat(DEV-82): persist melted Ice Wall via GameManager puzzle id`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs`

---

## Task 3: Re-apply solved puzzles on scene load

**Files:**
- Modify: `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`

- [ ] **Step 1: Call a new re-apply step from Start, after the active-scene resync**

In `PlatformerWorldRestoreController.Start()`, the order matters: the per-scene key is only correct *after* `SetActiveScene` runs (line 36). Add the re-apply call immediately after the existing `DestroyDefeatedEnemies(); DestroyCollectedPickups();` block and before the snapshot restore:

```csharp
        private void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.PlayerState.SetActiveScene(SceneManager.GetActiveScene().name);

            DestroyDefeatedEnemies();
            DestroyCollectedPickups();
            ReapplySolvedPuzzles();

            if (GameManager.Instance.CurrentWorldSnapshot != null)
                RestoreWorldState();
        }
```

- [ ] **Step 2: Implement ReapplySolvedPuzzles**

Add this method alongside `DestroyDefeatedEnemies` / `DestroyCollectedPickups` (same `FindObjectsByType(...Exclude)` shape — the controller GO stays active because `MeltableObstacleController` deactivates its separate `_tilemap` GO, not itself):

```csharp
        private void ReapplySolvedPuzzles()
        {
            MeltableObstacleController[] obstacles =
                FindObjectsByType<MeltableObstacleController>(FindObjectsInactive.Exclude);
            foreach (MeltableObstacleController obstacle in obstacles)
            {
                if (!string.IsNullOrWhiteSpace(obstacle.PuzzleId)
                    && GameManager.Instance.IsPuzzleSolved(obstacle.PuzzleId))
                {
                    obstacle.ApplySolvedImmediate();
                }
            }
        }
```

- [ ] **Step 3: Verify it compiles**

Recompile in the Editor; watch the Console.
Expected: no compile errors.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage → Check in with message: `feat(DEV-82): restore solved Ice Wall puzzles after battle round-trip`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`

- [ ] **Step 5: Play-mode verification of the persistence fix (manual)**

> **Unity Editor task (user):** In `Level_1-2`, select the Ice Wall obstacle GameObject and set its `MeltableObstacleController` → **Puzzle Id** to a scene-unique value (e.g. `ice_wall_main`). Save the scene.

> **Unity Editor task (user):** Confirm the scene root has a `PlatformerWorldRestoreController` and that **Script Execution Order** for it is still `-10` (Edit → Project Settings → Script Execution Order).

> **Unity Editor task (user):** Enter Play Mode in `Level_1-2`. Melt the Ice Wall with the Melt spell. Walk into the enemy trigger to start a Battle. Win (or flee) so you return to `Level_1-2`. **Expected:** the Ice Wall is still gone (no respawn, no melt animation replay), and its collider does not block the path. Repeat the Battle round-trip a second time to confirm it stays melted (proves it is not the one-shot snapshot).

- [ ] **Step 6: Play-mode verification of death re-forming the puzzle (manual)**

> **Unity Editor task (user):** In `Level_1-2`, touch a save point, then melt the Ice Wall. Die (e.g. fall in a pit / lose a battle) so you respawn at that checkpoint in `Level_1-2`. **Expected:** the Ice Wall is back (solid, blocking, re-formed) and must be melted again. Then confirm scoping: solve a puzzle in `Level_1-2`, walk to a *different* level scene, die there, and verify the `Level_1-2` puzzle is **still solved** when you return — only the respawn scene's puzzles re-form.

---

## Task 4: Success cue — VFX + SFX on a successful cast (MeltableObstacle)

**Files:**
- Modify: `Assets/Scripts/Platformer/MeltableObstacleController.cs`

The cue fires **only** on a real solve (inside the `TryMelt` success path), so a mismatched spell stays silently ignored. The restore path (`ApplySolvedImmediate`) deliberately does not play it.

- [ ] **Step 1: Add serialized cue fields + local AudioSource wiring**

Add to the serialized field block in `MeltableObstacleController.cs`:

```csharp
        [Header("Success cue")]
        [SerializeField]
        [Tooltip("Optional particle burst played once when this obstacle is successfully melted.")]
        private ParticleSystem _successVfx;

        [SerializeField]
        [Tooltip("Optional one-shot played when this obstacle is successfully melted. Routed through the SFX mixer bus.")]
        private AudioClip _successSfx;

        [SerializeField]
        [Tooltip("AudioSource on this prefab used to play the success SFX. Auto-routed through the SFX bus on Start.")]
        private AudioSource _audioSource;
```

Add a `Start` to route the local source through the existing SFX bus (this is the project's established pattern — `AudioManager.RouteSourceThroughSfxBus`, used for gameplay/battle sources so the SFX slider applies). A MonoBehaviour lifecycle method is the correct home for this wiring:

```csharp
        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }
```

- [ ] **Step 2: Play the cue in the TryMelt success path**

Extend `TryMelt` (from Task 2) to fire the cue once, after the solve is recorded:

```csharp
        public bool TryMelt(string spellId)
        {
            if (!CanMeltWith(spellId)) return false;

            _isMelted = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            PlaySuccessCue();
            StartCoroutine(MeltCoroutine());
            return true;
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }
```

- [ ] **Step 3: Verify it compiles**

Recompile; watch the Console. Expected: no errors.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage → Check in with message: `feat(DEV-82): add success VFX/SFX cue to meltable obstacle`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs`

---

## Task 5: Success cue for FreezablePlatform (cue only — no persistence)

**Files:**
- Modify: `Assets/Scripts/Platformer/FreezablePlatformController.cs`

The freeze platform is intentionally temporary, so it gets the cue for parity but **no** `PuzzleId` and **no** `MarkPuzzleSolved`.

- [ ] **Step 1: Add the same serialized cue fields + Start routing**

In `FreezablePlatformController.cs`, add `using Axiom.Core;` if missing, then add to the serialized field block:

```csharp
        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;
```

`FreezablePlatformController` **already has** a `Start()` — the animated-water-platform feature (branch `feat-DEV-82-level-1-add-animated-water-platforms`, merged into `dev` in cs:877 *after* this plan was written) added `private void Start() { StartWaterLoop(); }`. Do **not** add a second `Start()` (it will not compile). Fold the SFX-bus routing into the existing one, keeping `StartWaterLoop()` first:

```csharp
        private void Start()
        {
            StartWaterLoop();

            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }
```

- [ ] **Step 2: Fire the cue in the TryFreeze success path**

Extend `TryFreeze`:

```csharp
        public bool TryFreeze(string spellId)
        {
            if (!CanFreezeWith(spellId)) return false;

            _isFrozen = true;
            PlaySuccessCue();
            StartCoroutine(FreezeCoroutine());
            return true;
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }
```

- [ ] **Step 3: Verify it compiles**

Recompile; watch the Console. Expected: no errors.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage → Check in with message: `feat(DEV-82): add success VFX/SFX cue to freezable platform`
- `Assets/Scripts/Platformer/FreezablePlatformController.cs`

---

## Task 6: Author the cue assets and wire prefabs (Unity Editor)

**Files:** scenes/prefabs only — no scripts.

- [ ] **Step 1: Provide cue assets**

> **Unity Editor task (user):** Import or create a short success SFX clip (e.g. a soft chime / steam hiss) and, optionally, a `ParticleSystem` prefab/child for the melt burst and the freeze sparkle. Keep them small.

- [ ] **Step 2: Wire the Ice Wall obstacle**

> **Unity Editor task (user):** On the `Level_1-2` Ice Wall GameObject's `MeltableObstacleController`: add a child `AudioSource` (Play On Awake = off) and assign it to **Audio Source**; assign **Success Sfx**; optionally add a child `ParticleSystem` (Play On Awake = off, Looping = off) and assign **Success Vfx**. Confirm **Puzzle Id** is set (from Task 3 Step 5).

- [ ] **Step 3: Wire the Water Platform**

> **Unity Editor task (user):** On the `Level_1-3` (and any other) Water Platform's `FreezablePlatformController`: assign **Audio Source**, **Success Sfx**, and optionally **Success Vfx**. Do NOT set any persistence field — there is none for this component.

- [ ] **Step 4: Play-mode verification of the cue (manual)**

> **Unity Editor task (user):** Enter Play Mode. Cast Melt on the Ice Wall → expect the burst + SFX once, at the cast moment. Cast Freeze on the Water Platform → expect the sparkle + SFX. Cast a *non-matching* spell at each → expect **silence and no VFX** (silent-ignore preserved). Adjust the SFX volume slider in the pause/settings menu and re-cast → expect the cue volume to follow it (confirms SFX-bus routing).

- [ ] **Step 5: Regression check — Battle voice flow intact**

> **Unity Editor task (user):** Load `Battle`, push-to-talk, and cast a spell as before. Expect no change to battle behavior (the platformer cue path does not touch `Battle`).

- [ ] **Step 6: Check in scene/prefab/asset changes via UVCS**

> **Note:** These are binary/scene assets — they go through UVCS only, never git.

Unity Version Control → Pending Changes → stage the changed scenes, prefabs, the new audio clip, and any particle assets (with their `.meta` files) → Check in with message: `feat(DEV-82): wire success cue assets and Ice Wall puzzle id`

---

## Self-Review (completed by plan author)

- **Spec coverage:** DEV-82 open AC → tasks. "Scene-session persistence" → Tasks 1–3 + 3.5 manual verify. "Visual + audio success cue" → Tasks 4–6. "Silent ignore on mismatch" → preserved (cue only in success path; `CanMeltWith`/`CanFreezeWith` unchanged). "Battle voice flow intact" → Task 6 Step 5 regression check. Generic `EnvironmentalPuzzleTarget` and authoring more level puzzles are explicitly **deferred** (tracked under DEV-94) and out of this plan's scope.
- **Guard-clause ordering:** `IsPuzzleSolvedInScene`/`MarkPuzzleSolvedInScene` check the `puzzleId` null/empty *before* touching the scene bucket — an invalid ID never needs the bucket. The `TryMelt` mark/cue calls sit *after* `CanMeltWith` returns true, so mismatches never mark or cue.
- **Test coverage:** new GameManager API has Edit Mode tests for default-false, null/empty (both `Is` and `Mark`), happy path, per-scene isolation, clear-all, per-scene clear (`ClearSolvedPuzzlesInScene_LeavesOtherScenesIntact`, mirroring the enemy equivalent in `GameManagerSaveDataTests`), and New-Game clear. The `RespawnAtLastCheckpoint` wiring is a one-line call to that tested method; the full death→respawn flow and its cross-scene scoping are covered by the manual Play-Mode steps (Task 3, Steps 5–6), consistent with the project's Edit-Mode-for-logic / manual-for-scene convention.
- **Type/signature consistency:** `PuzzleId` (getter) and `_puzzleId` (field), `MarkPuzzleSolved`/`IsPuzzleSolved`/`ClearSolvedPuzzles`, `ApplySolvedImmediate`, `ReapplySolvedPuzzles`, `PlaySuccessCue` are spelled identically across every task that references them.
- **UVCS .meta audit:** the only *new* file is `GameManagerPuzzleTests.cs` → its `.cs.meta` is listed in Task 1 Step 9. All other script changes are modifications to existing files (their `.meta` already tracked). No new folders, no new `.asmdef`. Editor-authored assets (Task 6) are staged with their `.meta` in Step 6.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-09-dev82-puzzle-persistence-and-success-cue.md`. Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints for review.

Note: Tasks 1–5 are code (Claude-executable); Task 6 and the Play-Mode verification steps are Unity Editor work you perform. Which approach?
