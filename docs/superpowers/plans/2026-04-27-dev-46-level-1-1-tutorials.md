# Level 1-1 Remaining Tutorials Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the five remaining tutorial moments in Level_1-1 (first-death prompt, first-spike-hit prompt, first-battle button-lock + Surprised reinforcement, movement-lock at `Tutorial_Advantaged`, and the spell-tutorial battle), with completion flags persisted across save/quit/reload.

**Architecture:** Approach A from the spec — composable per-tutorial controllers. Each conditional tutorial is its own small `MonoBehaviour`. Shared infrastructure: 4 persisted bool flags on `PlayerState`, an `OneShotTutorialFlag` enum on `TutorialPromptTrigger`, a `BattleTutorialMode` plumbed through `BattleEntry`, a plain-C# `BattleTutorialFlow` state machine driving the Battle-scene controller. CLAUDE.md rules upheld: `MonoBehaviour` for lifecycle only, plain C# for logic, no new singletons.

**Tech Stack:** Unity 6 LTS, C# (Mono), Unity Test Framework (Edit Mode), Unity Version Control.

**Spec:** [`docs/superpowers/specs/2026-04-27-dev-46-level-1-1-tutorials-design.md`](../specs/2026-04-27-dev-46-level-1-1-tutorials-design.md)

**Jira:** DEV-46 (Level 1: Snow Mountain — tutorial completion sub-feature)

---

## File map

### New scripts

| File | Purpose |
|---|---|
| `Assets/Scripts/Battle/BattleTutorialMode.cs` | Enum: `None`, `FirstBattle`, `SpellTutorial` |
| `Assets/Scripts/Battle/BattleTutorialAction.cs` | Struct DTO returned by the flow — prompt text + per-button interactability |
| `Assets/Scripts/Battle/BattleTutorialFlow.cs` | Plain C# state machine — pure logic |
| `Assets/Scripts/Battle/BattleTutorialController.cs` | MonoBehaviour wrapper — subscribes to `BattleController` events, drives UI |
| `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs` | MonoBehaviour panel UI — `Show(string)` / `Hide()` |
| `Assets/Scripts/Platformer/TutorialOneShotFlagResolver.cs` | Plain static helper — maps `OneShotTutorialFlag` enum to `PlayerState` bool reads |
| `Assets/Scripts/Platformer/FirstDeathPromptController.cs` | Listens for the post-respawn pending flag and shows a one-shot panel |
| `Assets/Scripts/Platformer/FirstSpikeHitPromptController.cs` | Subscribes to `HazardTrigger.OnPlayerFirstHitFrame` and shows a one-shot panel |

### Modified scripts

| File | Change |
|---|---|
| `Assets/Scripts/Core/PlayerState.cs` | Add 4 bool flags + `MarkXxx` methods + `RestoreTutorialFlags` |
| `Assets/Scripts/Data/SaveData.cs` | Add 4 bool fields |
| `Assets/Scripts/Core/GameManager.cs` | `NotifyDiedAndRespawning` + `ConsumeFirstDeathPromptPending`; `BuildSaveData` and `ApplySaveData` round-trip the new flags |
| `Assets/Scripts/Data/BattleEntry.cs` | Add `BattleTutorialMode TutorialMode { get; }` + optional last constructor parameter |
| `Assets/Scripts/Platformer/TutorialPromptTrigger.cs` | Add `OneShotTutorialFlag _oneShotFlag`, `bool _lockMovementWhileInside`, `PlayerController _playerController`; `Awake` self-disable; lock/unlock on enter/exit |
| `Assets/Scripts/Platformer/PlayerController.cs` | Add `public void SetTutorialMovementLocked(bool locked)` |
| `Assets/Scripts/Platformer/PlayerDeathHandler.cs` | Call `GameManager.Instance.NotifyDiedAndRespawning()` before `RespawnAtLastCheckpoint` |
| `Assets/Scripts/Platformer/HazardTrigger.cs` | Static C# event `OnPlayerFirstHitFrame`; fire in the spike first-hit branch only |
| `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Add `[SerializeField] BattleTutorialMode _tutorialMode`; pass to `BattleEntry` ctor |
| `Assets/Scripts/Battle/BattleController.cs` | Add `[SerializeField] BattleTutorialController _tutorialController`; call `_tutorialController?.Setup(pending.TutorialMode)` after capturing pending fields |
| `Assets/Scripts/Battle/UI/ActionMenuUI.cs` | Add `SetAttackInteractable`, `SetItemInteractable`, `SetFleeInteractable` |

### New tests

| File | Coverage |
|---|---|
| `Assets/Tests/Editor/Core/PlayerStateTutorialFlagsTests.cs` | 4 flags default false; `MarkXxx` set true; round-trip |
| `Assets/Tests/Editor/Core/SaveDataTutorialFlagsRoundTripTests.cs` | `BuildSaveData` + `ApplySaveData` preserve all 4 flags |
| `Assets/Tests/Editor/Core/GameManagerFirstDeathPendingTests.cs` | `NotifyDiedAndRespawning` no-ops once seen; consume returns once and clears |
| `Assets/Tests/Editor/Battle/BattleEntryTutorialModeTests.cs` | New ctor param defaults `None`; explicit value round-trips on the property |
| `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs` | Full state machine — both modes, all transitions |
| `Assets/Tests/Editor/Platformer/TutorialOneShotFlagResolverTests.cs` | Each enum maps to the correct `PlayerState` flag |

---

## Task 1: `PlayerState` — tutorial flags

**Files:**
- Modify: `Assets/Scripts/Core/PlayerState.cs`
- Create: `Assets/Tests/Editor/Core/PlayerStateTutorialFlagsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/PlayerStateTutorialFlagsTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Core;

namespace Axiom.Core.Tests
{
    public class PlayerStateTutorialFlagsTests
    {
        private static PlayerState NewState() =>
            new PlayerState(maxHp: 40, maxMp: 33, attack: 5, defense: 3, speed: 4);

        [Test]
        public void NewPlayerState_AllTutorialFlags_DefaultToFalse()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(ps.HasSeenFirstDeath);
            Assert.IsFalse(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkFirstDeathSeen_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsTrue(ps.HasSeenFirstDeath);
            Assert.IsFalse(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkFirstSpikeHitSeen_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstSpikeHitSeen();
            Assert.IsTrue(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasSeenFirstDeath);
        }

        [Test]
        public void MarkFirstBattleTutorialCompleted_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstBattleTutorialCompleted();
            Assert.IsTrue(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkSpellTutorialBattleCompleted_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkSpellTutorialBattleCompleted();
            Assert.IsTrue(ps.HasCompletedSpellTutorialBattle);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
        }

        [Test]
        public void RestoreTutorialFlags_AppliesAllFour()
        {
            PlayerState ps = NewState();
            ps.RestoreTutorialFlags(
                hasSeenFirstDeath: true,
                hasSeenFirstSpikeHit: true,
                hasCompletedFirstBattleTutorial: true,
                hasCompletedSpellTutorialBattle: true);
            Assert.IsTrue(ps.HasSeenFirstDeath);
            Assert.IsTrue(ps.HasSeenFirstSpikeHit);
            Assert.IsTrue(ps.HasCompletedFirstBattleTutorial);
            Assert.IsTrue(ps.HasCompletedSpellTutorialBattle);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

In Unity Editor → Window → General → Test Runner → EditMode tab → run the new file. Expected: 6 failures (compile errors — properties and methods don't exist yet).

- [ ] **Step 3: Add the flags and methods to `PlayerState`**

In `Assets/Scripts/Core/PlayerState.cs`, immediately after the `HasCheckpointProgression` line (around line 61), add:

```csharp
        /// <summary>
        /// Persisted across save/quit/reload. Set true the first time the player
        /// dies and respawns. Used by FirstDeathPromptController to fire the
        /// "you respawn at the last torch" prompt at most once per save.
        /// </summary>
        public bool HasSeenFirstDeath { get; private set; }

        /// <summary>Persisted. Set true the first time the player takes spike contact damage.</summary>
        public bool HasSeenFirstSpikeHit { get; private set; }

        /// <summary>Persisted. Set true on Victory of the first battle (IceSlime, Surprised).</summary>
        public bool HasCompletedFirstBattleTutorial { get; private set; }

        /// <summary>Persisted. Set true on Victory of the spell-tutorial battle (Meltspawn, Advantaged).</summary>
        public bool HasCompletedSpellTutorialBattle { get; private set; }

        public void MarkFirstDeathSeen()                  { HasSeenFirstDeath = true; }
        public void MarkFirstSpikeHitSeen()               { HasSeenFirstSpikeHit = true; }
        public void MarkFirstBattleTutorialCompleted()    { HasCompletedFirstBattleTutorial = true; }
        public void MarkSpellTutorialBattleCompleted()    { HasCompletedSpellTutorialBattle = true; }

        /// <summary>
        /// Bulk-applies persisted tutorial flags. Used by GameManager.ApplySaveData on load.
        /// </summary>
        public void RestoreTutorialFlags(
            bool hasSeenFirstDeath,
            bool hasSeenFirstSpikeHit,
            bool hasCompletedFirstBattleTutorial,
            bool hasCompletedSpellTutorialBattle)
        {
            HasSeenFirstDeath = hasSeenFirstDeath;
            HasSeenFirstSpikeHit = hasSeenFirstSpikeHit;
            HasCompletedFirstBattleTutorial = hasCompletedFirstBattleTutorial;
            HasCompletedSpellTutorialBattle = hasCompletedSpellTutorialBattle;
        }
```

- [ ] **Step 4: Re-run tests**

Re-run the EditMode `PlayerStateTutorialFlagsTests`. Expected: 6 PASS.

- [ ] **Step 5: Check in via UVCS:**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): add tutorial completion flags to PlayerState`
- `Assets/Scripts/Core/PlayerState.cs`
- `Assets/Tests/Editor/Core/PlayerStateTutorialFlagsTests.cs`
- `Assets/Tests/Editor/Core/PlayerStateTutorialFlagsTests.cs.meta`

---

## Task 2: `SaveData` — extend with 4 bool fields and round-trip through `GameManager`

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Scripts/Core/GameManager.cs` (`BuildSaveData` and `ApplySaveData`)
- Create: `Assets/Tests/Editor/Core/SaveDataTutorialFlagsRoundTripTests.cs`

- [ ] **Step 1: Write the failing round-trip test**

Create `Assets/Tests/Editor/Core/SaveDataTutorialFlagsRoundTripTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Core.Tests
{
    public class SaveDataTutorialFlagsRoundTripTests
    {
        private GameObject _go;
        private GameManager _gm;
        private CharacterData _characterData;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GM");
            _gm = _go.AddComponent<GameManager>();
            _characterData = ScriptableObject.CreateInstance<CharacterData>();
            _characterData.baseMaxHP = 40;
            _characterData.baseMaxMP = 33;
            _characterData.baseATK = 5;
            _characterData.baseDEF = 3;
            _characterData.baseSPD = 4;
            _gm.SetPlayerCharacterDataForTests(_characterData);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_characterData);
        }

        [Test]
        public void BuildSaveData_WritesAllFourFlagsTrue()
        {
            _gm.PlayerState.MarkFirstDeathSeen();
            _gm.PlayerState.MarkFirstSpikeHitSeen();
            _gm.PlayerState.MarkFirstBattleTutorialCompleted();
            _gm.PlayerState.MarkSpellTutorialBattleCompleted();

            SaveData data = _gm.BuildSaveData();

            Assert.IsTrue(data.hasSeenFirstDeath);
            Assert.IsTrue(data.hasSeenFirstSpikeHit);
            Assert.IsTrue(data.hasCompletedFirstBattleTutorial);
            Assert.IsTrue(data.hasCompletedSpellTutorialBattle);
        }

        [Test]
        public void ApplySaveData_RestoresAllFourFlags()
        {
            var data = new SaveData
            {
                maxHp = 40,
                maxMp = 33,
                currentHp = 40,
                currentMp = 33,
                hasSeenFirstDeath = true,
                hasSeenFirstSpikeHit = true,
                hasCompletedFirstBattleTutorial = true,
                hasCompletedSpellTutorialBattle = true,
            };

            _gm.ApplySaveData(data);

            Assert.IsTrue(_gm.PlayerState.HasSeenFirstDeath);
            Assert.IsTrue(_gm.PlayerState.HasSeenFirstSpikeHit);
            Assert.IsTrue(_gm.PlayerState.HasCompletedFirstBattleTutorial);
            Assert.IsTrue(_gm.PlayerState.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void ApplySaveData_LegacyDataMissingFlags_DefaultsToFalse()
        {
            // Legacy save (pre-tutorial-flags) deserializes with default-false bools.
            var data = new SaveData { maxHp = 40, maxMp = 33, currentHp = 40, currentMp = 33 };

            _gm.ApplySaveData(data);

            Assert.IsFalse(_gm.PlayerState.HasSeenFirstDeath);
            Assert.IsFalse(_gm.PlayerState.HasSeenFirstSpikeHit);
            Assert.IsFalse(_gm.PlayerState.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(_gm.PlayerState.HasCompletedSpellTutorialBattle);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → run the new file. Expected: 3 failures (compile errors — fields don't exist).

- [ ] **Step 3: Add the 4 fields to `SaveData`**

In `Assets/Scripts/Data/SaveData.cs`, after the `collectedPickupIds` field at the end of the class:

```csharp
        // Tutorial completion flags (DEV-46). Default false on legacy saves missing these keys.
        public bool hasSeenFirstDeath;
        public bool hasSeenFirstSpikeHit;
        public bool hasCompletedFirstBattleTutorial;
        public bool hasCompletedSpellTutorialBattle;
```

- [ ] **Step 4: Wire into `GameManager.BuildSaveData`**

In `Assets/Scripts/Core/GameManager.cs`, find `BuildSaveData()` and append the four flags inside the `new SaveData { ... }` initializer (after `collectedPickupIds`):

```csharp
                collectedPickupIds = CopyHashSet(_collectedPickupIds),
                hasSeenFirstDeath = PlayerState.HasSeenFirstDeath,
                hasSeenFirstSpikeHit = PlayerState.HasSeenFirstSpikeHit,
                hasCompletedFirstBattleTutorial = PlayerState.HasCompletedFirstBattleTutorial,
                hasCompletedSpellTutorialBattle = PlayerState.HasCompletedSpellTutorialBattle
```

(Replace the existing trailing comma after `collectedPickupIds` and add the new lines as shown.)

- [ ] **Step 5: Wire into `GameManager.ApplySaveData`**

In `ApplySaveData(SaveData data)`, immediately before the call to `RestoreCollectedPickups(data.collectedPickupIds);` near the end:

```csharp
            PlayerState.RestoreTutorialFlags(
                data.hasSeenFirstDeath,
                data.hasSeenFirstSpikeHit,
                data.hasCompletedFirstBattleTutorial,
                data.hasCompletedSpellTutorialBattle);
```

- [ ] **Step 6: Re-run tests**

Test Runner → re-run `SaveDataTutorialFlagsRoundTripTests`. Expected: 3 PASS. Also re-run all `Core` tests to ensure nothing else broke.

- [ ] **Step 7: Check in via UVCS:**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-46): persist tutorial completion flags through SaveData round-trip`
- `Assets/Scripts/Data/SaveData.cs`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Tests/Editor/Core/SaveDataTutorialFlagsRoundTripTests.cs`
- `Assets/Tests/Editor/Core/SaveDataTutorialFlagsRoundTripTests.cs.meta`

---

## Task 3: `GameManager` — first-death pending signal

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Create: `Assets/Tests/Editor/Core/GameManagerFirstDeathPendingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/Core/GameManagerFirstDeathPendingTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Core.Tests
{
    public class GameManagerFirstDeathPendingTests
    {
        private GameObject _go;
        private GameManager _gm;
        private CharacterData _characterData;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GM");
            _gm = _go.AddComponent<GameManager>();
            _characterData = ScriptableObject.CreateInstance<CharacterData>();
            _characterData.baseMaxHP = 40;
            _characterData.baseMaxMP = 33;
            _characterData.baseATK = 5;
            _characterData.baseDEF = 3;
            _characterData.baseSPD = 4;
            _gm.SetPlayerCharacterDataForTests(_characterData);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_characterData);
        }

        [Test]
        public void Consume_WithoutPriorNotify_ReturnsFalse()
        {
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending());
        }

        [Test]
        public void Notify_ThenConsume_ReturnsTrueOnce()
        {
            _gm.NotifyDiedAndRespawning();
            Assert.IsTrue(_gm.ConsumeFirstDeathPromptPending());
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending(), "Second consume must return false.");
        }

        [Test]
        public void Notify_AfterFlagAlreadySet_DoesNotMarkPending()
        {
            _gm.PlayerState.MarkFirstDeathSeen();
            _gm.NotifyDiedAndRespawning();
            Assert.IsFalse(_gm.ConsumeFirstDeathPromptPending(),
                "If HasSeenFirstDeath is already true, no prompt should be queued.");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → run the new file. Expected: 3 failures (compile errors — methods don't exist).

- [ ] **Step 3: Add the methods to `GameManager`**

In `Assets/Scripts/Core/GameManager.cs`, add a private field near the other transient fields (around the `_collectedPickupIds` block, line ~158) and two new public methods. Place them in the `// ── Defeated enemies ───────────...` region or just before. The simplest landing zone is right after `ClearPendingBattle()` (line 124):

```csharp
        // Transient (not persisted) — set when the player dies, consumed by the
        // post-respawn FirstDeathPromptController exactly once. No-ops if the player
        // has already seen the first-death prompt (HasSeenFirstDeath == true).
        private bool _firstDeathPromptPending;

        /// <summary>
        /// Called by PlayerDeathHandler immediately before RespawnAtLastCheckpoint.
        /// No-op when HasSeenFirstDeath is already true.
        /// </summary>
        public void NotifyDiedAndRespawning()
        {
            EnsurePlayerState();
            if (_playerState != null && !_playerState.HasSeenFirstDeath)
                _firstDeathPromptPending = true;
        }

        /// <summary>
        /// Called by FirstDeathPromptController in the post-respawn scene.
        /// Returns true at most once per pending death; clears the flag on read.
        /// </summary>
        public bool ConsumeFirstDeathPromptPending()
        {
            bool wasPending = _firstDeathPromptPending;
            _firstDeathPromptPending = false;
            return wasPending;
        }
```

- [ ] **Step 4: Re-run tests**

Test Runner → re-run `GameManagerFirstDeathPendingTests`. Expected: 3 PASS.

- [ ] **Step 5: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add first-death prompt pending signal to GameManager`
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Tests/Editor/Core/GameManagerFirstDeathPendingTests.cs`
- `Assets/Tests/Editor/Core/GameManagerFirstDeathPendingTests.cs.meta`

---

## Task 4: `PlayerDeathHandler` — fire the pending signal before respawn

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerDeathHandler.cs`

This is a one-line addition; verified manually since the rest of the death pipeline is already exercised by existing tests and play-mode flow.

- [ ] **Step 1: Edit `PlayerDeathHandler.cs`**

In `Assets/Scripts/Platformer/PlayerDeathHandler.cs`, in the `Update()` method, immediately before the line `if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint &&` (currently around line 47):

```csharp
            if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint)
                GameManager.Instance.NotifyDiedAndRespawning();

            if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint &&
                GameManager.Instance.RespawnAtLastCheckpoint(_transitionStyle))
            {
                return;
            }
```

(Adds the `NotifyDiedAndRespawning` call. Only fires for the respawn outcome, not the GameOver path.)

- [ ] **Step 2: Verify compile**

Save the file; let Unity recompile. Expected: no compile errors.

- [ ] **Step 3: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): notify GameManager of death before respawn`
- `Assets/Scripts/Platformer/PlayerDeathHandler.cs`

---

## Task 5: `BattleEntry` — add `BattleTutorialMode` plumbing

**Files:**
- Create: `Assets/Scripts/Battle/BattleTutorialMode.cs`
- Modify: `Assets/Scripts/Data/BattleEntry.cs`
- Create: `Assets/Tests/Editor/Battle/BattleEntryTutorialModeTests.cs`

- [ ] **Step 1: Create `BattleTutorialMode.cs`**

Create `Assets/Scripts/Battle/BattleTutorialMode.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Set by ExplorationEnemyCombatTrigger and propagated through BattleEntry.
    /// Read by BattleTutorialController on Battle scene load to choose the
    /// scripted tutorial flow (or none).
    /// </summary>
    public enum BattleTutorialMode
    {
        None,
        FirstBattle,
        SpellTutorial
    }
}
```

- [ ] **Step 2: Write the failing test**

Create `Assets/Tests/Editor/Battle/BattleEntryTutorialModeTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class BattleEntryTutorialModeTests
    {
        [Test]
        public void Constructor_WithoutTutorialMode_DefaultsToNone()
        {
            var entry = new BattleEntry(CombatStartState.Surprised, enemyData: null);
            Assert.AreEqual(BattleTutorialMode.None, entry.TutorialMode);
        }

        [Test]
        public void Constructor_WithExplicitTutorialMode_StoresIt()
        {
            var entry = new BattleEntry(
                CombatStartState.Advantaged,
                enemyData: null,
                enemyId: "meltspawn_01",
                enemyCurrentHp: -1,
                environmentData: null,
                tutorialMode: BattleTutorialMode.SpellTutorial);

            Assert.AreEqual(BattleTutorialMode.SpellTutorial, entry.TutorialMode);
        }
    }
}
```

- [ ] **Step 3: Update the `Axiom.Data.asmdef`** to reference `Axiom.Battle` if it doesn't already.

In Unity Editor, open `Assets/Scripts/Data/Data.asmdef` Inspector. If `Axiom.Battle` is **not** in `Assembly Definition References`, add it (search for `Axiom.Battle`, drag onto the list). Apply.

> **Note:** if this creates a circular reference (because `Axiom.Battle` already references `Axiom.Data`), instead of the above, **move `BattleTutorialMode.cs` from `Assets/Scripts/Battle/` to `Assets/Scripts/Data/`** and change its namespace to `Axiom.Data`. Update the test's `using` accordingly. The enum has no behavior beyond being a discriminator, so its location is flexible.

- [ ] **Step 4: Run the test to verify it fails**

Test Runner → run the new file. Expected: failure (compile errors — `TutorialMode` property and constructor parameter don't exist).

- [ ] **Step 5: Modify `BattleEntry.cs`**

Replace `Assets/Scripts/Data/BattleEntry.cs` contents with:

```csharp
using Axiom.Battle;

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
    /// Zero or greater means "resume at this HP" (re-engaging a damaged enemy after flee).
    ///
    /// TutorialMode (DEV-46): set on tutorial-flagged enemies in Level_1-1; consumed by
    /// BattleTutorialController to drive a scripted tutorial flow. Defaults to None.
    /// </summary>
    public sealed class BattleEntry
    {
        public CombatStartState StartState { get; }
        public EnemyData EnemyData { get; }
        public string EnemyId { get; }
        public int EnemyCurrentHp { get; }
        public BattleEnvironmentData EnvironmentData { get; }
        public BattleTutorialMode TutorialMode { get; }

        public BattleEntry(CombatStartState startState, EnemyData enemyData,
                           string enemyId = null, int enemyCurrentHp = -1,
                           BattleEnvironmentData environmentData = null,
                           BattleTutorialMode tutorialMode = BattleTutorialMode.None)
        {
            StartState = startState;
            EnemyData = enemyData;
            EnemyId = enemyId;
            EnemyCurrentHp = enemyCurrentHp;
            EnvironmentData = environmentData;
            TutorialMode = tutorialMode;
        }
    }
}
```

- [ ] **Step 6: Re-run tests**

Test Runner → re-run `BattleEntryTutorialModeTests` and existing `BattleEntryTests`. Expected: all PASS. The existing `BattleEntryTests` should still pass because the new parameter is optional with a default.

- [ ] **Step 7: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add BattleTutorialMode plumbing through BattleEntry`
- `Assets/Scripts/Battle/BattleTutorialMode.cs`
- `Assets/Scripts/Battle/BattleTutorialMode.cs.meta`
- `Assets/Scripts/Data/BattleEntry.cs`
- `Assets/Scripts/Data/Data.asmdef` (if modified in Step 3)
- `Assets/Tests/Editor/Battle/BattleEntryTutorialModeTests.cs`
- `Assets/Tests/Editor/Battle/BattleEntryTutorialModeTests.cs.meta`

---

## Task 6: `BattleTutorialAction` struct

**Files:**
- Create: `Assets/Scripts/Battle/BattleTutorialAction.cs`

This is a pure DTO — no test file needed (covered indirectly by `BattleTutorialFlowTests` in Task 7).

- [ ] **Step 1: Create the file**

Create `Assets/Scripts/Battle/BattleTutorialAction.cs`:

```csharp
namespace Axiom.Battle
{
    /// <summary>
    /// Output of one BattleTutorialFlow step. Each field is "no change" when null;
    /// non-null fields are applied by BattleTutorialController to the UI.
    /// PromptText: null = leave panel as-is, "" = hide, otherwise = show with this text.
    /// MarkComplete = true tells the controller to flip the persisted PlayerState flag.
    /// </summary>
    public readonly struct BattleTutorialAction
    {
        public string PromptText        { get; }
        public bool? AttackInteractable { get; }
        public bool? SpellInteractable  { get; }
        public bool? ItemInteractable   { get; }
        public bool? FleeInteractable   { get; }
        public bool MarkComplete        { get; }

        public BattleTutorialAction(
            string promptText = null,
            bool? attackInteractable = null,
            bool? spellInteractable = null,
            bool? itemInteractable = null,
            bool? fleeInteractable = null,
            bool markComplete = false)
        {
            PromptText = promptText;
            AttackInteractable = attackInteractable;
            SpellInteractable  = spellInteractable;
            ItemInteractable   = itemInteractable;
            FleeInteractable   = fleeInteractable;
            MarkComplete       = markComplete;
        }

        public static readonly BattleTutorialAction NoChange = new BattleTutorialAction();
    }
}
```

- [ ] **Step 2: Verify compile**

Save and let Unity recompile. Expected: no errors. (No tests run yet; consumed by Task 7.)

> No UVCS check-in for this task standalone — it commits with Task 7.

---

## Task 7: `BattleTutorialFlow` — pure C# state machine (TDD)

**Files:**
- Create: `Assets/Scripts/Battle/BattleTutorialFlow.cs`
- Create: `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`

This is the meatiest task — the state machine has many branches, and every prompt-buttons combination is tested. Build it incrementally: write all tests first, watch them all fail, implement state by state until they all pass.

- [ ] **Step 1: Write the failing test file**

Create `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class BattleTutorialFlowTests
    {
        // ── FirstBattle (IceSlime, Surprised) ──────────────────────────────────

        [Test]
        public void FirstBattle_OnInit_ShowsSurprisedPromptAndLocksToAttack()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            BattleTutorialAction a = flow.OnInit();
            StringAssert.Contains("surprised", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
            Assert.IsFalse(a.MarkComplete);
        }

        [Test]
        public void FirstBattle_OnPlayerTurnStarted_FirstCall_ShowsPressAttackPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnPlayerTurnStarted();
            StringAssert.Contains("attack", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void FirstBattle_OnPlayerTurnStarted_SecondCall_ReappliesButtonLockOnly()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerTurnStarted();
            // No new prompt, but the button lock must still be re-applied because
            // BattleController re-enables all buttons after EnemyTurn.
            Assert.IsNull(a.PromptText, "Prompt should not change on re-entry to PlayerTurn.");
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
        }

        [Test]
        public void FirstBattle_OnPlayerAttackHit_HidesPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackHit();
            Assert.AreEqual(string.Empty, a.PromptText, "Empty string means hide.");
        }

        [Test]
        public void FirstBattle_OnBattleEnded_Victory_MarksComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: true);
            Assert.IsTrue(a.MarkComplete);
            Assert.AreEqual(string.Empty, a.PromptText);
        }

        [Test]
        public void FirstBattle_OnBattleEnded_Defeat_DoesNotMarkComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.FirstBattle, CombatStartState.Surprised);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: false);
            Assert.IsFalse(a.MarkComplete);
        }

        // ── SpellTutorial (Meltspawn, Advantaged, Liquid innate) ───────────────

        [Test]
        public void SpellTutorial_OnInit_ShowsLiquidPromptAndAttackOnly()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            BattleTutorialAction a = flow.OnInit();
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsFalse(a.SpellInteractable);
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void SpellTutorial_OnPlayerAttackImmune_ShowsLiquidBlocksPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            BattleTutorialAction a = flow.OnPlayerAttackImmune();
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            StringAssert.Contains("spell", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_PlayerTurn2_UnlocksSpellAndPromptsCast()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();              // turn 1
            flow.OnPlayerAttackImmune();             // attack bounced
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 2
            StringAssert.Contains("freeze", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsTrue(a.SpellInteractable, "Spell button must unlock at turn 2.");
            Assert.IsFalse(a.ItemInteractable);
            Assert.IsFalse(a.FleeInteractable);
        }

        [Test]
        public void SpellTutorial_OnSpellCast_HidesPromptDuringResolve()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnSpellCast(spellName: "Freeze");
            Assert.AreEqual(string.Empty, a.PromptText);
        }

        [Test]
        public void SpellTutorial_OnConditionsChanged_AfterFreeze_ShowsFrozenSolidPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            BattleTutorialAction a = flow.OnConditionsChanged();
            string lower = (a.PromptText ?? string.Empty).ToLowerInvariant();
            StringAssert.Contains("frozen", lower);
            StringAssert.Contains("solid", lower);
        }

        [Test]
        public void SpellTutorial_PlayerTurn3_PromptsToAttackWhileSolid()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            flow.OnConditionsChanged();
            BattleTutorialAction a = flow.OnPlayerTurnStarted(); // turn 3
            StringAssert.Contains("solid", (a.PromptText ?? string.Empty).ToLowerInvariant());
            Assert.IsTrue(a.AttackInteractable);
            Assert.IsTrue(a.SpellInteractable);
        }

        [Test]
        public void SpellTutorial_OnPlayerAttackHit_AfterTurn3_ShowsClosingLine()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            flow.OnSpellCast(spellName: "Freeze");
            flow.OnConditionsChanged();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackHit();
            StringAssert.Contains("spell", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_AttackOnTurn2_RefiresLiquidBlocksPrompt()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            flow.OnPlayerTurnStarted();
            flow.OnPlayerAttackImmune();
            flow.OnPlayerTurnStarted();
            BattleTutorialAction a = flow.OnPlayerAttackImmune(); // player attacked again
            StringAssert.Contains("liquid", (a.PromptText ?? string.Empty).ToLowerInvariant());
        }

        [Test]
        public void SpellTutorial_OnBattleEnded_Victory_MarksComplete()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.SpellTutorial, CombatStartState.Advantaged);
            flow.OnInit();
            BattleTutorialAction a = flow.OnBattleEnded(victory: true);
            Assert.IsTrue(a.MarkComplete);
        }

        // ── Mode = None ────────────────────────────────────────────────────────

        [Test]
        public void NoneMode_OnInit_ReturnsNoChange()
        {
            var flow = new BattleTutorialFlow(BattleTutorialMode.None, CombatStartState.Surprised);
            BattleTutorialAction a = flow.OnInit();
            Assert.IsNull(a.PromptText);
            Assert.IsNull(a.AttackInteractable);
            Assert.IsNull(a.SpellInteractable);
            Assert.IsFalse(a.MarkComplete);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → run the new file. Expected: 16 failures (compile errors — class doesn't exist).

- [ ] **Step 3: Implement `BattleTutorialFlow.cs`**

Create `Assets/Scripts/Battle/BattleTutorialFlow.cs`:

```csharp
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# state machine driving the in-Battle tutorial. Pure logic — no Unity types
    /// beyond CombatStartState (an enum from Axiom.Data). All side effects (prompt show/hide,
    /// button lock toggles, persisted-flag flips) are returned as BattleTutorialAction values
    /// for BattleTutorialController to apply. Per CLAUDE.md: MonoBehaviours only handle
    /// Unity lifecycle; logic lives in plain C#.
    ///
    /// Source of truth for all prompt strings — the Inspector cannot override these because
    /// the flow needs to react to specific contents (e.g., "freeze" vs other spells). If
    /// you want designer-tweakable copy, route the prompts through the controller and replace
    /// these constants with method parameters.
    /// </summary>
    public sealed class BattleTutorialFlow
    {
        // Prompt copy — keep in one place for review.
        private const string FirstBattle_Init        = "The Frostbug surprised you — it acts first.";
        private const string FirstBattle_PressAttack = "Press Attack to strike.";

        private const string SpellTutorial_Init             = "This Meltspawn is Liquid — physical attacks pass right through. Try Attack to see.";
        private const string SpellTutorial_LiquidBlocks     = "Liquid blocks physical damage. Next turn, cast a spell.";
        private const string SpellTutorial_PressSpellFreeze = "Press Spell, then say 'Freeze' aloud.";
        private const string SpellTutorial_FrozenSolid      = "Frozen — enemy skips a turn. Solid — physical attacks now hit.";
        private const string SpellTutorial_StrikeWhileSolid = "Strike while it's Solid!";
        private const string SpellTutorial_ClosingLine      = "Each spell turns the tide differently. Use the right one.";

        private readonly BattleTutorialMode _mode;
        private readonly CombatStartState _startState;

        // FirstBattle state
        private bool _firstBattle_pressAttackShown;

        // SpellTutorial state — counts player turns we've handled so we know which prompt to show.
        private int _spell_playerTurnsObserved;
        private bool _spell_waitingForFreezeConditions;
        private bool _spell_postFreezeTurnReached;

        public BattleTutorialFlow(BattleTutorialMode mode, CombatStartState startState)
        {
            _mode = mode;
            _startState = startState;
        }

        public BattleTutorialMode Mode => _mode;

        public BattleTutorialAction OnInit()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    return new BattleTutorialAction(
                        promptText: FirstBattle_Init,
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);
                case BattleTutorialMode.SpellTutorial:
                    return new BattleTutorialAction(
                        promptText: SpellTutorial_Init,
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);
                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnPlayerTurnStarted()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    if (!_firstBattle_pressAttackShown)
                    {
                        _firstBattle_pressAttackShown = true;
                        return new BattleTutorialAction(
                            promptText: FirstBattle_PressAttack,
                            attackInteractable: true,
                            spellInteractable: false,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    // Subsequent player turns — re-apply button lock only.
                    return new BattleTutorialAction(
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);

                case BattleTutorialMode.SpellTutorial:
                    _spell_playerTurnsObserved++;
                    if (_spell_playerTurnsObserved == 1)
                    {
                        // Turn 1: same as init prompt — already shown by OnInit. Re-apply button lock only.
                        return new BattleTutorialAction(
                            attackInteractable: true,
                            spellInteractable: false,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    if (_spell_playerTurnsObserved == 2)
                    {
                        // Turn 2: unlock Spell, prompt to cast Freeze.
                        return new BattleTutorialAction(
                            promptText: SpellTutorial_PressSpellFreeze,
                            attackInteractable: true,
                            spellInteractable: true,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    // Turn 3+: post-Freeze world. Strike while Solid.
                    _spell_postFreezeTurnReached = true;
                    return new BattleTutorialAction(
                        promptText: SpellTutorial_StrikeWhileSolid,
                        attackInteractable: true,
                        spellInteractable: true,
                        itemInteractable: false,
                        fleeInteractable: false);

                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnPlayerAttackImmune()
        {
            if (_mode != BattleTutorialMode.SpellTutorial)
                return BattleTutorialAction.NoChange;
            return new BattleTutorialAction(promptText: SpellTutorial_LiquidBlocks);
        }

        public BattleTutorialAction OnPlayerAttackHit()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    return new BattleTutorialAction(promptText: string.Empty);
                case BattleTutorialMode.SpellTutorial when _spell_postFreezeTurnReached:
                    return new BattleTutorialAction(promptText: SpellTutorial_ClosingLine);
                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnSpellCast(string spellName)
        {
            if (_mode != BattleTutorialMode.SpellTutorial) return BattleTutorialAction.NoChange;
            // Hide prompt while the cast resolves; OnConditionsChanged will show the next prompt.
            _spell_waitingForFreezeConditions = true;
            return new BattleTutorialAction(promptText: string.Empty);
        }

        public BattleTutorialAction OnConditionsChanged()
        {
            if (_mode != BattleTutorialMode.SpellTutorial) return BattleTutorialAction.NoChange;
            if (!_spell_waitingForFreezeConditions) return BattleTutorialAction.NoChange;
            _spell_waitingForFreezeConditions = false;
            return new BattleTutorialAction(promptText: SpellTutorial_FrozenSolid);
        }

        public BattleTutorialAction OnBattleEnded(bool victory)
        {
            if (_mode == BattleTutorialMode.None)
                return BattleTutorialAction.NoChange;
            return new BattleTutorialAction(
                promptText: string.Empty,
                markComplete: victory);
        }
    }
}
```

- [ ] **Step 4: Re-run tests**

Test Runner → re-run `BattleTutorialFlowTests`. Expected: all 16 PASS. If any fail, read the failure message and fix the corresponding state-machine branch — do not rewrite the test.

- [ ] **Step 5: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add BattleTutorialFlow state machine`
- `Assets/Scripts/Battle/BattleTutorialAction.cs`
- `Assets/Scripts/Battle/BattleTutorialAction.cs.meta`
- `Assets/Scripts/Battle/BattleTutorialFlow.cs`
- `Assets/Scripts/Battle/BattleTutorialFlow.cs.meta`
- `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs`
- `Assets/Tests/Editor/Battle/BattleTutorialFlowTests.cs.meta`

---

## Task 8: `ActionMenuUI` — per-button setters

**Files:**
- Modify: `Assets/Scripts/Battle/UI/ActionMenuUI.cs`

Single-line setters; no test file (covered by play-mode behavior of the controller).

- [ ] **Step 1: Add three methods**

In `Assets/Scripts/Battle/UI/ActionMenuUI.cs`, immediately after the existing `SetSpellInteractable` method (around line 53):

```csharp
        /// <summary>Enables or disables only the Attack button. Used by BattleTutorialController.</summary>
        public void SetAttackInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
        }

        /// <summary>Enables or disables only the Item button. Used by BattleTutorialController.</summary>
        public void SetItemInteractable(bool interactable)
        {
            _itemButton.interactable = interactable;
        }

        /// <summary>Enables or disables only the Flee button. Used by BattleTutorialController.</summary>
        public void SetFleeInteractable(bool interactable)
        {
            _fleeButton.interactable = interactable;
        }
```

- [ ] **Step 2: Verify compile**

Save and let Unity recompile. Expected: no errors.

- [ ] **Step 3: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add per-button setters to ActionMenuUI`
- `Assets/Scripts/Battle/UI/ActionMenuUI.cs`

---

## Task 9: `BattleTutorialPromptUI` — Battle-scene panel

**Files:**
- Create: `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`

- [ ] **Step 1: Create the file**

Create `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene tutorial prompt panel. Mirrors the platformer's TutorialPromptPanelUI
    /// but lives in the Battle Canvas. BattleTutorialController calls Show/Hide as the
    /// state machine emits prompts.
    /// </summary>
    public class BattleTutorialPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no errors.

> No UVCS check-in for this task standalone — it commits with Task 10.

---

## Task 10: `BattleTutorialController` — wire flow + UI to BattleController events

**Files:**
- Create: `Assets/Scripts/Battle/BattleTutorialController.cs`
- Modify: `Assets/Scripts/Battle/BattleController.cs`

- [ ] **Step 1: Create the controller**

Create `Assets/Scripts/Battle/BattleTutorialController.cs`:

```csharp
using Axiom.Battle.UI;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Drives the in-Battle tutorial. Subscribes to BattleController events, forwards them
    /// to BattleTutorialFlow, applies returned BattleTutorialAction to ActionMenuUI and
    /// BattleTutorialPromptUI. On Victory, flips the matching persisted PlayerState flag.
    ///
    /// Setup() is called by BattleController.Start after pending battle context is read.
    /// If PlayerState already has the matching flag set, the controller self-disables —
    /// this handles the post-victory respawn-into-trigger case.
    /// </summary>
    public class BattleTutorialController : MonoBehaviour
    {
        [SerializeField] private BattleController _battleController;
        [SerializeField] private ActionMenuUI _actionMenu;
        [SerializeField] private BattleTutorialPromptUI _promptUI;

        private BattleTutorialFlow _flow;
        private bool _isActive;
        private BattleState _currentBattleState;

        public void Setup(BattleTutorialMode requestedMode)
        {
            BattleTutorialMode resolvedMode = ResolveMode(requestedMode);
            if (resolvedMode == BattleTutorialMode.None)
            {
                _isActive = false;
                return;
            }

            _isActive = true;
            _flow = new BattleTutorialFlow(resolvedMode, GuessStartState(resolvedMode));

            SubscribeToBattleEvents();
            Apply(_flow.OnInit());
        }

        private BattleTutorialMode ResolveMode(BattleTutorialMode requestedMode)
        {
            if (GameManager.Instance == null) return BattleTutorialMode.None;
            PlayerState ps = GameManager.Instance.PlayerState;
            return requestedMode switch
            {
                BattleTutorialMode.FirstBattle when !ps.HasCompletedFirstBattleTutorial =>
                    BattleTutorialMode.FirstBattle,
                BattleTutorialMode.SpellTutorial when !ps.HasCompletedSpellTutorialBattle =>
                    BattleTutorialMode.SpellTutorial,
                _ => BattleTutorialMode.None,
            };
        }

        private static CombatStartState GuessStartState(BattleTutorialMode mode) => mode switch
        {
            BattleTutorialMode.FirstBattle   => CombatStartState.Surprised,
            BattleTutorialMode.SpellTutorial => CombatStartState.Advantaged,
            _                                => CombatStartState.Surprised,
        };

        private void SubscribeToBattleEvents()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged    += HandleStateChanged;
            _battleController.OnPhysicalAttackImmune  += HandlePhysicalAttackImmune;
            _battleController.OnDamageDealt           += HandleDamageDealt;
            _battleController.OnConditionsChanged     += HandleConditionsChanged;
            _battleController.OnSpellRecognized       += HandleSpellRecognized;
        }

        private void OnDestroy()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged    -= HandleStateChanged;
            _battleController.OnPhysicalAttackImmune  -= HandlePhysicalAttackImmune;
            _battleController.OnDamageDealt           -= HandleDamageDealt;
            _battleController.OnConditionsChanged     -= HandleConditionsChanged;
            _battleController.OnSpellRecognized       -= HandleSpellRecognized;
        }

        private void HandleStateChanged(BattleState state)
        {
            // Track current state BEFORE the active gate so HandleDamageDealt and
            // HandlePhysicalAttackImmune can use it to disambiguate player vs enemy actions.
            _currentBattleState = state;
            if (!_isActive) return;
            switch (state)
            {
                case BattleState.PlayerTurn:
                    Apply(_flow.OnPlayerTurnStarted());
                    break;
                case BattleState.Victory:
                    Apply(_flow.OnBattleEnded(victory: true));
                    _isActive = false;
                    break;
                case BattleState.Defeat:
                    Apply(_flow.OnBattleEnded(victory: false));
                    _isActive = false;
                    break;
            }
        }

        private void HandlePhysicalAttackImmune(CharacterStats attacker, CharacterStats target)
        {
            if (!_isActive) return;
            // Only react during PlayerTurn — that's when the player's attack is what bounced.
            // (Enemy attacks against the player during EnemyTurn could also fire this event
            // if the player ever becomes Liquid, which the tutorial doesn't expect — but the
            // gate keeps the flow correct under any future content.)
            if (_currentBattleState != BattleState.PlayerTurn) return;
            Apply(_flow.OnPlayerAttackImmune());
        }

        private void HandleDamageDealt(CharacterStats target, int damage, bool isCrit)
        {
            if (!_isActive) return;
            // Filter zero-damage pings (BattleController fires these to refresh the MP bar).
            if (damage <= 0) return;
            // Only react to damage dealt during PlayerTurn — that's the player's attack
            // landing on the enemy. EnemyTurn damage is the enemy hitting the player
            // and must NOT fire OnPlayerAttackHit (which in SpellTutorial turn 3+ would
            // incorrectly show the closing line).
            if (_currentBattleState != BattleState.PlayerTurn) return;
            Apply(_flow.OnPlayerAttackHit());
        }

        private void HandleConditionsChanged(CharacterStats stats)
        {
            if (!_isActive) return;
            Apply(_flow.OnConditionsChanged());
        }

        private void HandleSpellRecognized(SpellData spell)
        {
            if (!_isActive) return;
            string name = spell != null ? spell.spellName : null;
            Apply(_flow.OnSpellCast(name));
        }

        private void Apply(BattleTutorialAction action)
        {
            if (_promptUI != null)
            {
                if (action.PromptText == string.Empty)      _promptUI.Hide();
                else if (action.PromptText != null)         _promptUI.Show(action.PromptText);
            }

            if (_actionMenu != null)
            {
                if (action.AttackInteractable.HasValue) _actionMenu.SetAttackInteractable(action.AttackInteractable.Value);
                if (action.SpellInteractable.HasValue)  _actionMenu.SetSpellInteractable(action.SpellInteractable.Value);
                if (action.ItemInteractable.HasValue)   _actionMenu.SetItemInteractable(action.ItemInteractable.Value);
                if (action.FleeInteractable.HasValue)   _actionMenu.SetFleeInteractable(action.FleeInteractable.Value);
            }

            if (action.MarkComplete && GameManager.Instance != null && _flow != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                switch (_flow.Mode)
                {
                    case BattleTutorialMode.FirstBattle:   ps.MarkFirstBattleTutorialCompleted(); break;
                    case BattleTutorialMode.SpellTutorial: ps.MarkSpellTutorialBattleCompleted(); break;
                }
                GameManager.Instance.PersistToDisk();
            }
        }
    }
}
```

- [ ] **Step 2: Modify `BattleController.cs` to host and call `Setup`**

In `Assets/Scripts/Battle/BattleController.cs`, add a serialized field with the other `[SerializeField]` battle dependencies near the top of the class (after `_postBattleFlow`, around line 75):

```csharp
        [SerializeField]
        [Tooltip("Optional. When the BattleEntry has a TutorialMode, this controller drives " +
                 "the scripted in-Battle tutorial. Leave unassigned to skip tutorials in standalone testing.")]
        private BattleTutorialController _tutorialController;
```

Then in `Start()`, immediately after the `GameManager.Instance.ClearPendingBattle();` line (around line 235), add:

```csharp
                if (_tutorialController != null)
                    _tutorialController.Setup(pending.TutorialMode);
```

(Place inside the existing `if (pending != null)` block — alongside the other `pending.*` reads.)

- [ ] **Step 3: Verify compile**

Save and let Unity recompile. Expected: no errors.

- [ ] **Step 4: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add BattleTutorialController and BattleTutorialPromptUI`
- `Assets/Scripts/Battle/BattleTutorialController.cs`
- `Assets/Scripts/Battle/BattleTutorialController.cs.meta`
- `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs`
- `Assets/Scripts/Battle/UI/BattleTutorialPromptUI.cs.meta`
- `Assets/Scripts/Battle/BattleController.cs`

---

## Task 11: `HazardTrigger` — static event for first-spike-hit

**Files:**
- Modify: `Assets/Scripts/Platformer/HazardTrigger.cs`

- [ ] **Step 1: Add the static event and fire it**

In `Assets/Scripts/Platformer/HazardTrigger.cs`, after the `using` block near the top (line 1-3), add `using System;`. Then immediately after the `public class HazardTrigger : MonoBehaviour` opening brace and the `[SerializeField]` block, add:

```csharp
        /// <summary>
        /// Fires once per first-contact spike damage frame. Used by
        /// FirstSpikeHitPromptController to show the "spikes deal DoT" prompt
        /// at most once per save (further gated by PlayerState.HasSeenFirstSpikeHit).
        /// Does NOT fire for InstantKO (pit) hazards or for DoT tick frames.
        /// </summary>
        public static event Action OnPlayerFirstHitFrame;
```

Then in `OnTriggerEnter2D`, after the existing `ApplyPercentDamage(_firstHitDamagePercent, HazardMode.PercentMaxHpDamage);` call (currently around line 70), add:

```csharp
            OnPlayerFirstHitFrame?.Invoke();
```

(Place it immediately after `ApplyPercentDamage` and before the `_feedback?.PlayHurtAnimation();` line, so subscribers see the event with the new HP already applied.)

- [ ] **Step 2: Verify compile**

Save and let Unity recompile. Expected: no errors. Existing HazardDamageResolverTests still pass.

> No UVCS check-in for this task standalone — it commits with Task 14.

---

## Task 12: `TutorialOneShotFlagResolver` + extend `TutorialPromptTrigger`

**Files:**
- Create: `Assets/Scripts/Platformer/TutorialOneShotFlagResolver.cs`
- Modify: `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`
- Create: `Assets/Tests/Editor/Platformer/TutorialOneShotFlagResolverTests.cs`

- [ ] **Step 1: Write the resolver test**

Create `Assets/Tests/Editor/Platformer/TutorialOneShotFlagResolverTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Core;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    public class TutorialOneShotFlagResolverTests
    {
        private static PlayerState NewState() =>
            new PlayerState(maxHp: 40, maxMp: 33, attack: 5, defense: 3, speed: 4);

        [Test]
        public void None_AlwaysReturnsFalse()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.None));
        }

        [Test]
        public void FirstBattle_ReadsHasCompletedFirstBattleTutorial()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstBattle));
            ps.MarkFirstBattleTutorialCompleted();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstBattle));
        }

        [Test]
        public void SpellTutorialBattle_ReadsHasCompletedSpellTutorialBattle()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.SpellTutorialBattle));
            ps.MarkSpellTutorialBattleCompleted();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.SpellTutorialBattle));
        }

        [Test]
        public void FirstSpikeHit_ReadsHasSeenFirstSpikeHit()
        {
            PlayerState ps = NewState();
            ps.MarkFirstSpikeHitSeen();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstSpikeHit));
        }

        [Test]
        public void FirstDeath_ReadsHasSeenFirstDeath()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstDeath));
        }

        [Test]
        public void NullPlayerState_AlwaysReturnsFalse()
        {
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(null, OneShotTutorialFlag.FirstDeath));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Test Runner → run the new file. Expected: failures (compile errors — neither type exists).

- [ ] **Step 3: Create `TutorialOneShotFlagResolver.cs`**

Create `Assets/Scripts/Platformer/TutorialOneShotFlagResolver.cs`:

```csharp
using Axiom.Core;

namespace Axiom.Platformer
{
    /// <summary>
    /// Inspector-selectable identifier for "this trigger is a one-shot tutorial moment."
    /// Maps to a persisted bool flag on PlayerState via TutorialOneShotFlagResolver.
    /// None = the trigger replays every time the player enters its zone.
    /// </summary>
    public enum OneShotTutorialFlag
    {
        None,
        FirstBattle,
        SpellTutorialBattle,
        FirstSpikeHit,
        FirstDeath
    }

    /// <summary>
    /// Pure static helper — resolves a OneShotTutorialFlag against PlayerState.
    /// Extracted from TutorialPromptTrigger so it's unit-testable without spinning
    /// up a Unity scene (per CLAUDE.md: logic in plain C#).
    /// </summary>
    public static class TutorialOneShotFlagResolver
    {
        public static bool IsFlagSet(PlayerState ps, OneShotTutorialFlag flag)
        {
            if (ps == null) return false;
            return flag switch
            {
                OneShotTutorialFlag.FirstBattle          => ps.HasCompletedFirstBattleTutorial,
                OneShotTutorialFlag.SpellTutorialBattle  => ps.HasCompletedSpellTutorialBattle,
                OneShotTutorialFlag.FirstSpikeHit        => ps.HasSeenFirstSpikeHit,
                OneShotTutorialFlag.FirstDeath           => ps.HasSeenFirstDeath,
                _                                        => false,
            };
        }
    }
}
```

- [ ] **Step 4: Re-run tests**

Test Runner → re-run. Expected: 6 PASS.

- [ ] **Step 5: Add `SetTutorialMovementLocked` to `PlayerController`**

In `Assets/Scripts/Platformer/PlayerController.cs`, add a public method near the bottom of the class (just before the closing brace of the class — after the existing private helpers but before the final `}`):

```csharp
    /// <summary>
    /// Locks/unlocks player movement AND jump input for tutorial purposes — leaves
    /// Attack input alive so the player can engage the locked-near-enemy battle trigger.
    /// Different from the attack-anim lock (which uses _movement.SetMovementLocked alone).
    /// Called by TutorialPromptTrigger when its _lockMovementWhileInside flag is true.
    /// </summary>
    public void SetTutorialMovementLocked(bool locked)
    {
        _movement.SetMovementLocked(locked);
        if (locked) _input.Player.Jump.Disable();
        else        _input.Player.Jump.Enable();
    }
```

- [ ] **Step 6: Modify `TutorialPromptTrigger.cs`**

Replace `Assets/Scripts/Platformer/TutorialPromptTrigger.cs` with:

```csharp
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Trigger zone that displays a tutorial prompt on the shared panel while the
    /// player is inside. Place in levels to teach movement, combat entry, or
    /// chemistry puzzle mechanics.
    ///
    /// Two optional behaviors layered on top:
    ///   _oneShotFlag: when set, the trigger self-disables on Awake if the matching
    ///                 PlayerState flag is already true. Use for tutorials that should
    ///                 not replay after completion (FirstBattle, SpellTutorialBattle).
    ///   _lockMovementWhileInside: when true, calls PlayerController.SetTutorialMovementLocked
    ///                 on enter/exit. Use for the Tutorial_Advantaged zone in front of
    ///                 the spell-tutorial Meltspawn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TutorialPromptTrigger : MonoBehaviour
    {
        [SerializeField, TextArea(2, 6)] private string _message = string.Empty;
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField]
        [Tooltip("When set, this trigger disables itself on Awake if the matching PlayerState flag is already true.")]
        private OneShotTutorialFlag _oneShotFlag = OneShotTutorialFlag.None;
        [SerializeField]
        [Tooltip("When true, locks player movement and jump while the player is inside this zone. " +
                 "Attack stays enabled so the player can engage a nearby battle trigger.")]
        private bool _lockMovementWhileInside = false;
        [SerializeField]
        [Tooltip("Required when _lockMovementWhileInside is true. Reference to the player's PlayerController.")]
        private PlayerController _playerController;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            if (_oneShotFlag == OneShotTutorialFlag.None) return;
            if (GameManager.Instance == null) return;
            if (TutorialOneShotFlagResolver.IsFlagSet(GameManager.Instance.PlayerState, _oneShotFlag))
                gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel != null) _panel.Show(_message);
            if (_lockMovementWhileInside && _playerController != null)
                _playerController.SetTutorialMovementLocked(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel != null) _panel.Hide();
            if (_lockMovementWhileInside && _playerController != null)
                _playerController.SetTutorialMovementLocked(false);
        }
    }
}
```

- [ ] **Step 7: Verify compile**

Save and let Unity recompile. Expected: no errors. Existing `TutorialOneShotFlagResolverTests` PASS.

> No UVCS check-in for this task standalone — it commits with Task 14.

---

## Task 13: `ExplorationEnemyCombatTrigger` — pass `TutorialMode` through

**Files:**
- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`

- [ ] **Step 1: Add the field**

In `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`, add to the `using` block at the top:

```csharp
using Axiom.Battle;
```

Then in the `[SerializeField]` block (after `_battleEnvironment` around line 25):

```csharp
        [SerializeField]
        [Tooltip("DEV-46. Set to FirstBattle on the IceSlime in Level_1-1 (locks Spell/Item/Flee). " +
                 "Set to SpellTutorial on the Meltspawn in Level_1-1 (multi-step Liquid → Freeze → Solid flow). " +
                 "Leave None for all other enemies.")]
        private BattleTutorialMode _tutorialMode = BattleTutorialMode.None;
```

- [ ] **Step 2: Pass through to `BattleEntry`**

Find the `GameManager.Instance.SetPendingBattle(...)` call in `TriggerBattle` (currently around line 130), and replace the `BattleEntry` constructor call so it passes `_tutorialMode`:

```csharp
            GameManager.Instance.SetPendingBattle(
                new BattleEntry(startState, _enemyData, enemyId, enemyCurrentHp,
                    _battleEnvironment, _tutorialMode));
```

- [ ] **Step 3: Verify compile**

Save and let Unity recompile. Expected: no errors. Existing `BattleEntryTests` and `BattleEntryTutorialModeTests` still PASS.

> No UVCS check-in for this task standalone — it commits with Task 14.

---

## Task 14: `FirstDeathPromptController` + `FirstSpikeHitPromptController`

**Files:**
- Create: `Assets/Scripts/Platformer/FirstDeathPromptController.cs`
- Create: `Assets/Scripts/Platformer/FirstSpikeHitPromptController.cs`

- [ ] **Step 1: Create `FirstDeathPromptController.cs`**

Create `Assets/Scripts/Platformer/FirstDeathPromptController.cs`:

```csharp
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// On scene load, consumes the GameManager._firstDeathPromptPending signal and
    /// shows a one-shot prompt. Sets PlayerState.HasSeenFirstDeath so subsequent
    /// deaths don't re-queue the prompt. Auto-hides after _displaySeconds.
    /// Place one instance in any level where the prompt should appear (Level_1-1).
    /// </summary>
    public class FirstDeathPromptController : MonoBehaviour
    {
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField, TextArea]
        private string _message =
            "Defeated. You respawned at your last lit torch. Light new torches to save your progress — the first touch on each also fully restores HP and MP.";
        [SerializeField] private float _displaySeconds = 6f;

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            // Check _panel BEFORE consuming the pending flag — ConsumeFirstDeathPromptPending
            // has a side effect (clears the flag), so a missing panel must not silently
            // discard the pending signal.
            if (_panel == null) return;
            if (!gm.ConsumeFirstDeathPromptPending()) return;
            _panel.Show(_message);
            gm.PlayerState.MarkFirstDeathSeen();
            gm.PersistToDisk();
            Invoke(nameof(Hide), _displaySeconds);
        }

        private void Hide()
        {
            if (_panel != null) _panel.Hide();
        }
    }
}
```

- [ ] **Step 2: Create `FirstSpikeHitPromptController.cs`**

Create `Assets/Scripts/Platformer/FirstSpikeHitPromptController.cs`:

```csharp
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Subscribes to HazardTrigger.OnPlayerFirstHitFrame. On the first event after
    /// the controller is enabled, shows a one-shot prompt and sets
    /// PlayerState.HasSeenFirstSpikeHit. No collider needed — this is a passive listener.
    /// Place one instance in any level where the spike prompt should appear (Level_1-1).
    /// </summary>
    public class FirstSpikeHitPromptController : MonoBehaviour
    {
        [SerializeField] private TutorialPromptPanelUI _panel;
        [SerializeField, TextArea]
        private string _message =
            "Spikes hurt on touch and keep ticking while you stand on them. Move off quickly.";
        [SerializeField] private float _displaySeconds = 6f;

        private void OnEnable()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.PlayerState.HasSeenFirstSpikeHit) return;
            HazardTrigger.OnPlayerFirstHitFrame += HandleHit;
        }

        private void OnDisable()
        {
            HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
        }

        private void HandleHit()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.PlayerState.HasSeenFirstSpikeHit) return;
            if (_panel == null) return;
            _panel.Show(_message);
            gm.PlayerState.MarkFirstSpikeHitSeen();
            gm.PersistToDisk();
            Invoke(nameof(Hide), _displaySeconds);
            HazardTrigger.OnPlayerFirstHitFrame -= HandleHit;
        }

        private void Hide()
        {
            if (_panel != null) _panel.Hide();
        }
    }
}
```

- [ ] **Step 3: Verify compile**

Save and let Unity recompile. Expected: no errors. Run all EditMode tests to verify nothing else broke.

- [ ] **Step 4: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add platformer tutorial prompts and TutorialPromptTrigger one-shot guard`
- `Assets/Scripts/Platformer/HazardTrigger.cs`
- `Assets/Scripts/Platformer/TutorialOneShotFlagResolver.cs`
- `Assets/Scripts/Platformer/TutorialOneShotFlagResolver.cs.meta`
- `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`
- `Assets/Scripts/Platformer/PlayerController.cs`
- `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
- `Assets/Scripts/Platformer/FirstDeathPromptController.cs`
- `Assets/Scripts/Platformer/FirstDeathPromptController.cs.meta`
- `Assets/Scripts/Platformer/FirstSpikeHitPromptController.cs`
- `Assets/Scripts/Platformer/FirstSpikeHitPromptController.cs.meta`
- `Assets/Tests/Editor/Platformer/TutorialOneShotFlagResolverTests.cs`
- `Assets/Tests/Editor/Platformer/TutorialOneShotFlagResolverTests.cs.meta`

---

## Task 15: Editor wiring — Level_1-1, ED_Meltspawn, Battle scene

> All steps in this task are **Unity Editor tasks (user)**. Claude does not perform Editor changes; this section is the user's checklist.

### 15A. Level_1-1 trigger zone updates

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. In the Hierarchy, expand `Tutorial_Colliders`.

- [ ] **Step 1: `Tutorial_Surprised`** — set Inspector field `One Shot Flag` to `FirstBattle`. Leave `Lock Movement While Inside` unchecked.

- [ ] **Step 2: `Tutorial_Advantaged`** — set `One Shot Flag` to `SpellTutorialBattle`. Check `Lock Movement While Inside`. Drag the Player GameObject (the one with `PlayerController`) into the `Player Controller` field.

- [ ] **Step 3: `IceSlime_01`** — select the `ExplorationEnemyCombatTrigger` component. Set `Tutorial Mode` to `FirstBattle`.

- [ ] **Step 4: `Meltspawn_01`** — select the `ExplorationEnemyCombatTrigger` component. Set `Tutorial Mode` to `SpellTutorial`.

### 15B. Add platformer prompt controllers

> **Unity Editor task (user):** still in `Level_1-1.unity`.

- [ ] **Step 5:** GameObject → Create Empty. Name it `FirstDeathPrompt`. Add component `FirstDeathPromptController`. Drag the existing `TutorialPromptPanel` (already in your hierarchy under the Canvas) into the `Panel` field.

- [ ] **Step 6:** GameObject → Create Empty. Name it `FirstSpikeHitPrompt`. Add component `FirstSpikeHitPromptController`. Drag `TutorialPromptPanel` into the `Panel` field.

- [ ] **Step 7:** Save scene (`Cmd+S` / `Ctrl+S`).

### 15C. ED_Meltspawn — innate Liquid

> **Unity Editor task (user):** open `Assets/Data/Enemies/ED_Meltspawn.asset` (or wherever the asset lives).

- [ ] **Step 8:** In the Inspector's `Innate Conditions` list, click `+` and add `Liquid`. Apply.

> **Verify:** open `BattleController` Inspector in `Assets/Scenes/Battle.unity` afterward to make sure no regression — the spell tutorial battle expects this.

### 15D. Battle scene — tutorial UI panel and controller

> **Unity Editor task (user):** open `Assets/Scenes/Battle.unity`.

- [ ] **Step 9:** Under the existing Battle Canvas, create a UI Panel (`GameObject → UI → Panel`). Name it `BattleTutorialPromptPanel`. Style to match your existing battle UI conventions (background sprite, padding). Add a child `Text - TextMeshPro` named `Body` with a wrap-friendly width.

- [ ] **Step 10:** On `BattleTutorialPromptPanel`, add component `BattleTutorialPromptUI`. Drag the panel root into `Root` and the TMP child into `Body Label`. Set the panel root inactive in the Inspector (uncheck the GameObject).

- [ ] **Step 11:** Create an empty GameObject under the Battle scene root named `BattleTutorialController`. Add component `BattleTutorialController`. Drag:
  - `BattleController` (the existing GameObject with the `BattleController` MonoBehaviour) → `Battle Controller` field.
  - The `ActionMenuUI` GameObject → `Action Menu` field.
  - `BattleTutorialPromptPanel` → `Prompt UI` field.

- [ ] **Step 12:** Select the `BattleController` GameObject. In the Inspector, drag `BattleTutorialController` into the new `Tutorial Controller` field.

- [ ] **Step 13:** Save scene.

### 15E. UVCS check-in for Editor changes

- [ ] **Step 14: Check in via UVCS:**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): wire Level_1-1 and Battle scene tutorial Inspector fields`
- `Assets/Scenes/Level_1-1.unity`
- `Assets/Scenes/Battle.unity`
- `Assets/Data/Enemies/ED_Meltspawn.asset`

---

## Task 16: Manual play-test — golden path and edge cases

> **Unity Editor task (user):** Enter Play Mode and walk through the scenarios below. Use the project's standard "New Game" flow from MainMenu to start fresh. After each scenario, capture a screenshot or note any divergence from expected behavior.

- [ ] **Scenario 1 — All forced prompts still fire**

Start a New Game. Walk left→right through Level_1-1. Confirm: `Tutorial_Movement`, `Tutorial_HighJump`, `Tutorial_Surprised`, `Tutorial_Advantaged`, `Tutorial_IceWall` all fire as before.

- [ ] **Scenario 2 — First spike hit fires once**

Walk over a spike cluster. Confirm the panel shows the spike prompt. Walk over another spike — no prompt. Save (touch a torch), quit Play Mode, restart Play Mode via Continue. Walk over a spike — no prompt.

- [ ] **Scenario 3 — First death fires once**

From a fresh New Game (post-Scenario 2 if your save still has spike-flag set), die in a pit. Confirm the post-respawn panel shows the torch prompt. Die again — no prompt. Save, quit, reload — no prompt on next death.

- [ ] **Scenario 4 — First battle (IceSlime) — Surprised, Attack-only**

Walk into IceSlime's path so the patrol enemy collides with you (Surprised path). Confirm in Battle scene:
- Tutorial panel shows "The Frostbug surprised you — it acts first."
- Spell, Item, and Flee buttons are grayed out; Attack is enabled.
- After the enemy's first turn, your turn shows "Press Attack to strike."
- Spell/Item/Flee remain locked the entire battle.
- Win the battle. Confirm: `Tutorial_Surprised` is gone next reload (its GameObject self-disabled), and the IceSlime is also gone (existing defeated-enemy persistence).

- [ ] **Scenario 5 — Spell tutorial battle (Meltspawn) — Advantaged**

After Scenario 4, walk toward `Meltspawn_01`. Hit the `Tutorial_Advantaged` collider. Confirm:
- Panel shows the Advantaged tutorial text.
- Movement is locked: A/D doesn't move you; Space doesn't jump; the run/jump animations don't play.
- Pressing the Attack key triggers the attack animation, then the battle starts.

In the battle:
- Panel shows "This Meltspawn is Liquid — physical attacks pass right through. Try Attack to see."
- Only Attack is enabled.
- Press Attack — the attack animation plays and the panel shows "Liquid blocks physical damage. Next turn, cast a spell."
- Enemy turn fires (Meltspawn attacks you).
- Your turn 2: panel shows "Press Spell, then say 'Freeze' aloud." Spell button is now enabled.
- Press Spell, say "Freeze" — spell resolves; enemy gets Frozen + Solid badges. Panel shows "Frozen — enemy skips a turn. Solid — physical attacks now hit."
- Enemy turn is skipped (Frozen).
- Your turn 3: panel shows "Strike while it's Solid!"
- Press Attack — damage lands. Panel shows "Each spell turns the tide differently. Use the right one."
- Win the battle (continue Attacking until Meltspawn HP = 0).
- Return to platformer. `Tutorial_Advantaged` is gone next reload. Movement is no longer locked anywhere.

- [ ] **Scenario 6 — Defeat replay**

Lose the spell tutorial battle on purpose (by repeatedly pressing Attack against an enemy that's now hard to read because Liquid blocks. If Meltspawn's stats make this impossible to lose, skip this scenario.). Confirm: respawn at last torch, walk back to Meltspawn area, the entire Tutorial_Advantaged + battle flow re-runs. (Flag stays false because no Victory.)

- [ ] **Scenario 7 — Save persistence**

Complete both battle tutorials. Save (touch a torch). Quit Play Mode. Continue from MainMenu. Walk back to where Tutorial_Surprised was — no prompt. Walk back to where Tutorial_Advantaged was — no prompt, no movement lock.

- [ ] **Step 8:** Note any divergences in your playtest log. If everything passes, the implementation is complete.

---

## Self-review checklist (run after writing this plan)

- [ ] **Spec coverage:** every numbered item in the spec's "Goal" and "In scope" sections has a corresponding task here.
- [ ] **No placeholders:** no "TBD", "TODO", or "implement later" anywhere.
- [ ] **Type consistency:** `BattleTutorialMode`, `BattleTutorialAction`, `OneShotTutorialFlag`, `BattleTutorialFlow` method signatures match between definition tasks and consumer tasks.
- [ ] **UVCS files audit:** every `.cs` created in a task is listed in a UVCS check-in step alongside its `.cs.meta`. Editor changes (scene/asset modifications in Task 15) are in the Editor check-in step.
- [ ] **MonoBehaviour separation:** `BattleTutorialFlow` is plain C#; `BattleTutorialController` is the MonoBehaviour wrapper. `TutorialOneShotFlagResolver` is plain C# static. `BattleTutorialPromptUI`, `FirstDeathPromptController`, `FirstSpikeHitPromptController` are MonoBehaviours that delegate to the persisted state.
- [ ] **No new singletons:** all cross-scene reads go through `GameManager.Instance` (already allowed). Static C# events on `HazardTrigger` are events, not singletons.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-27-dev-46-level-1-1-tutorials.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — execute tasks in this session using `executing-unity-game-dev-plans`, batch execution with checkpoints.

Which approach?
