# DEV-105 to DEV-110 Follow-Up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve Jira tickets `DEV-105` through `DEV-110` with small, isolated fixes that remove debug-only runtime behavior, document the one accepted architecture exception, harden Vosk teardown, decouple shared UI from `Axiom.Battle`, and persist pickup collection immediately.

**Architecture:** Keep each ticket independently shippable. Do not invent a new shared UI assembly; move genuinely shared widgets into `Axiom.Core`, which both `Axiom.Battle` and `Axiom.Platformer` already reference. Keep the existing Vosk producer/consumer queue pattern, but make ownership and teardown explicit so native resources are never disposed by the caller while a background task may still be using them.

**Tech Stack:** Unity 6 LTS, C#, Unity Test Framework (Edit Mode first; manual Unity Editor verification for scene/prefab wiring), UVCS

---

## Assumptions

- `DEV-105` takes the simpler production fix: remove the debug caster components from shipped prefabs and remove their now-unused input actions, instead of adding development-build-only gates.
- `DEV-106` takes the minimal path explicitly allowed by the Jira issue: document `StateBasedCursorUI` as a presentation-layer exception instead of refactoring cursor persistence behind `GameManager`.
- `DEV-107` and `DEV-108` keep the current `ConcurrentQueue<short[]>` architecture from `docs/GAME_PLAN.md`; the fixes harden ownership and teardown rather than replacing the voice pipeline.
- `DEV-109` moves shared widgets into `Axiom.Core` because `Axiom.Core.asmdef` already references `Axiom.Data`, `Unity.TextMeshPro`, `UnityEngine.UI`, and `Unity.InputSystem`.
- `DEV-110` assumes pickup collection is meant to survive a quit/crash immediately after collection, which matches the original `DEV-66` goal that pickups despawn across saves.

## Recommended Execution Order

1. `DEV-108` — fix the lower-level Vosk shutdown ownership race first.
2. `DEV-107` — then harden `BattleVoiceBootstrap` against late async results.
3. `DEV-105` — remove debug-only runtime hooks from production content.
4. `DEV-110` — persist pickup collection immediately after collect.
5. `DEV-109` — move shared UI widgets into `Axiom.Core` and remove the `Platformer -> Battle` assembly dependency.
6. `DEV-106` — document the accepted `StateBasedCursorUI` exception once the codebase is otherwise clean.

---

### Task 1: DEV-105 Remove Debug Spell-Caster Hooks from Production Content

**Files:**
- Modify: `Assets/Prefabs/Platformer/P_IceWall.prefab`
- Modify: `Assets/Prefabs/Platformer/P_WaterPlatform.prefab`
- Modify: `Assets/InputSystem_Actions.inputactions`
- Create: `Assets/Tests/Editor/Platformer/PlatformerDebugContentTests.cs`
- Create: `Assets/Tests/Editor/Platformer/PlatformerDebugContentTests.cs.meta`

- [ ] **Step 1: Write the failing Edit Mode content test**

Write `Assets/Tests/Editor/Platformer/PlatformerDebugContentTests.cs`:

```csharp
using System.IO;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PlatformerTests
{
    public class PlatformerDebugContentTests
    {
        [Test]
        public void ProductionPrefabs_DoNotContainDebugSpellCasterComponents()
        {
            GameObject iceWall = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Platformer/P_IceWall.prefab");
            GameObject waterPlatform = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Platformer/P_WaterPlatform.prefab");

            Assert.IsNotNull(iceWall);
            Assert.IsNotNull(waterPlatform);
            Assert.IsNull(iceWall.GetComponent<MeltableObstacleDebugCaster>());
            Assert.IsNull(waterPlatform.GetComponent<FreezablePlatformDebugCaster>());
        }

        [Test]
        public void InputActionsAsset_DoesNotContainDebugSpellBindings()
        {
            string json = File.ReadAllText("Assets/InputSystem_Actions.inputactions");

            StringAssert.DoesNotContain("\"DebugMeltCast\"", json);
            StringAssert.DoesNotContain("\"DebugFreezeCast\"", json);
        }
    }
}
```

- [ ] **Step 2: Run the test to confirm the current content still exposes debug hooks**

> **Unity Editor task (user):** Open Test Runner → Edit Mode → run `PlatformerTests.PlatformerDebugContentTests`. Expected: both tests fail because the two prefabs still carry debug components and the input actions asset still contains `DebugMeltCast` / `DebugFreezeCast`.

- [ ] **Step 3: Remove the debug components from the production prefabs**

> **Unity Editor task (user):** Open `Assets/Prefabs/Platformer/P_IceWall.prefab` and remove `MeltableObstacleDebugCaster`. Open `Assets/Prefabs/Platformer/P_WaterPlatform.prefab` and remove `FreezablePlatformDebugCaster`. Save both prefabs.

- [ ] **Step 4: Remove the now-unused debug actions and keyboard bindings**

Delete the `DebugMeltCast` and `DebugFreezeCast` action entries plus their `M` / `F` bindings from `Assets/InputSystem_Actions.inputactions`.

- [ ] **Step 5: Re-run the content test and spot-check the prefabs**

> **Unity Editor task (user):** Re-run `PlatformerTests.PlatformerDebugContentTests`. Then open each prefab once more and confirm there is no Missing Script component left behind after removal.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-105): remove debug spell caster hooks`
- `Assets/Prefabs/Platformer/P_IceWall.prefab`
- `Assets/Prefabs/Platformer/P_WaterPlatform.prefab`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Tests/Editor/Platformer/PlatformerDebugContentTests.cs`
- `Assets/Tests/Editor/Platformer/PlatformerDebugContentTests.cs.meta`

---

### Task 2: DEV-106 Document `StateBasedCursorUI` as the Accepted Presentation-Layer Exception

**Files:**
- Modify: `AGENTS.md`
- Modify: `docs/GAME_PLAN.md`
- Modify: `Assets/Scripts/Core/StateBasedCursorUI.cs`

- [ ] **Step 1: Document the exception where future architecture scans actually read it**

Add one short note to the architecture rules in both `AGENTS.md` and `docs/GAME_PLAN.md` that:

- `GameManager` remains the only gameplay-state singleton.
- `StateBasedCursorUI` is the lone accepted presentation-layer exception because it owns only cursor visuals plus optional `DontDestroyOnLoad` behavior.
- No other class may copy this pattern without another explicit rule change.

- [ ] **Step 2: Align the code comment with the documented exception**

Update the XML summary and the `_persistAcrossScenes` tooltip in `Assets/Scripts/Core/StateBasedCursorUI.cs` so the code itself explains that the self-persistence path is a documented UI-only exception, not a second game-state singleton.

- [ ] **Step 3: Verify the exception is discoverable**

Run:

```powershell
rg -n "StateBasedCursorUI|presentation-layer exception|GameManager" AGENTS.md docs/GAME_PLAN.md Assets/Scripts/Core/StateBasedCursorUI.cs
```

Expected: both docs files and the script comment clearly mention the exception.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `docs(DEV-106): document cursor singleton exception`
- `AGENTS.md`
- `docs/GAME_PLAN.md`
- `Assets/Scripts/Core/StateBasedCursorUI.cs`

---

### Task 3: DEV-107 Clean Up Late Vosk Startup Results When `BattleVoiceBootstrap` Is Destroyed

**Files:**
- Create: `Assets/Scripts/Voice/BootstrapResourceGuard.cs`
- Create: `Assets/Scripts/Voice/BootstrapResourceGuard.cs.meta`
- Modify: `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
- Modify: `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs`

- [ ] **Step 1: Add failing Edit Mode tests for late-result ownership**

Append the following test helpers and tests to `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs`:

```csharp
private sealed class FakeDisposable : System.IDisposable
{
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}

[Test]
public void BootstrapResourceGuard_TakeOrDispose_WhenAlive_ReturnsCandidate()
{
    var guard = new BootstrapResourceGuard();
    var candidate = new FakeDisposable();

    FakeDisposable adopted = guard.TakeOrDispose(candidate);

    Assert.AreSame(candidate, adopted);
    Assert.IsFalse(candidate.Disposed);
}

[Test]
public void BootstrapResourceGuard_TakeOrDispose_AfterTeardown_DisposesCandidateAndReturnsNull()
{
    var guard = new BootstrapResourceGuard();
    var candidate = new FakeDisposable();
    guard.RequestTeardown();

    FakeDisposable adopted = guard.TakeOrDispose(candidate);

    Assert.IsNull(adopted);
    Assert.IsTrue(candidate.Disposed);
}
```

- [ ] **Step 2: Run the Edit Mode fixture and confirm the new helper does not exist yet**

> **Unity Editor task (user):** Run `Axiom.Voice.Tests.BattleVoiceBootstrapTests`. Expected: compile failure because `BootstrapResourceGuard` is not implemented yet.

- [ ] **Step 3: Add a plain C# guard for late async results**

Create `Assets/Scripts/Voice/BootstrapResourceGuard.cs`:

```csharp
using System;

namespace Axiom.Voice
{
    public sealed class BootstrapResourceGuard
    {
        private bool _teardownRequested;

        public void RequestTeardown() => _teardownRequested = true;

        public T TakeOrDispose<T>(T candidate) where T : class, IDisposable
        {
            if (candidate == null)
                return null;

            if (_teardownRequested)
            {
                candidate.Dispose();
                return null;
            }

            return candidate;
        }
    }
}
```

- [ ] **Step 4: Route every startup and rebuild result through the guard**

Update `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` so that:

- a private `BootstrapResourceGuard _resourceGuard = new BootstrapResourceGuard();` field is added,
- `OnDestroy()` calls `_resourceGuard.RequestTeardown()` before disposing current owned resources,
- after `modelTask` completes, `modelTask.Result` is passed through `TakeOrDispose(...)` before assigning `_voskModel`,
- after each recognizer build task completes, the returned `VoskRecognizer` is passed through `TakeOrDispose(...)` before constructing a new `VoskRecognizerService`,
- `RebuildRecognizer(...)` exits cleanly if teardown was requested during the async build instead of disposing the current live service and then adopting a recognizer that no longer has an owner.

- [ ] **Step 5: Re-run tests and manually exercise the destroy-during-load path**

> **Unity Editor task (user):** Re-run `Axiom.Voice.Tests.BattleVoiceBootstrapTests`. Then open `Assets/Scenes/Battle.unity`, enter Play Mode, and exit immediately while the Vosk model is still starting. Expected: no late `Model` / `VoskRecognizer` leak warnings and no follow-up log from a destroyed bootstrap continuing startup.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-107): guard late Vosk startup resources`
- `Assets/Scripts/Voice/BootstrapResourceGuard.cs`
- `Assets/Scripts/Voice/BootstrapResourceGuard.cs.meta`
- `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
- `Assets/Tests/Editor/Voice/BattleVoiceBootstrapTests.cs`

---

### Task 4: DEV-108 Make Vosk Shutdown Ownership Explicit When `Stop()` Times Out

**Files:**
- Modify: `Assets/Scripts/Voice/VoskRecognizerService.cs`
- Modify: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`

- [ ] **Step 1: Add a failing timeout-path test that uses a synthetic in-flight task**

Append the following test to `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`:

```csharp
[Test]
public void Dispose_WhenRecognitionTaskMissesTimeout_LeavesTaskOwnedByWorker()
{
    var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();
    var cts = new CancellationTokenSource();
    var taskField = typeof(VoskRecognizerService)
        .GetField("_recognitionTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var ctsField = typeof(VoskRecognizerService)
        .GetField("_cts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    taskField.SetValue(_service, tcs.Task);
    ctsField.SetValue(_service, cts);

    Assert.DoesNotThrow(() => _service.Dispose());
    Assert.AreSame(tcs.Task, taskField.GetValue(_service),
        "Timed-out shutdown must leave the in-flight task owned by the worker until it really exits.");

    tcs.SetResult(null);
}
```

- [ ] **Step 2: Re-run the Voice fixture and confirm the timeout path is still caller-owned**

> **Unity Editor task (user):** Run `Axiom.Voice.Tests.VoskRecognizerServiceTests`. Expected: the new test fails because the current timeout path clears `_recognitionTask` immediately, which means the caller still acts like teardown finished even though the worker has not exited yet.

- [ ] **Step 3: Split normal-stop cleanup from timeout cleanup**

Refactor `Assets/Scripts/Voice/VoskRecognizerService.cs` so that:

- `Stop()` returns a local `completed` result and only clears `_recognitionTask` / disposes `_cts` when the worker actually exited,
- `Dispose()` only disposes `_recognizer` immediately when `Stop()` completed successfully,
- when `Stop()` times out, `Dispose()` schedules a one-shot continuation on the captured `_recognitionTask` that disposes `_recognizer` and finalizes `_cts` only after the worker really exits,
- the worker path never races a caller-thread `_recognizer.Dispose()` while `AcceptWaveform(...)`, `Result()`, or `FinalResult()` may still be running.

Use this exact ownership rule in the code comment above the helper you add:

```csharp
// Caller-thread disposal is only allowed after _recognitionTask has actually completed.
// If Stop() times out, the background task owns final recognizer disposal.
```

- [ ] **Step 4: Update the service comment to match the new behavior**

Remove or narrow any comment that says the same `VoskRecognizerService` instance can always be restarted after `Stop()`. The current codebase recreates the service on rebuild, so the comments should describe the actual supported path rather than an overly broad contract.

- [ ] **Step 5: Re-run the full Voice fixture and manually bounce the Battle scene**

> **Unity Editor task (user):** Run `Axiom.Voice.Tests.VoskRecognizerServiceTests`. Then enter and leave `Assets/Scenes/Battle.unity` several times. Expected: no scene-exit freeze, no worker-thread use-after-dispose error, and no leaked recognizer after teardown.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-108): harden Vosk timeout shutdown ownership`
- `Assets/Scripts/Voice/VoskRecognizerService.cs`
- `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`

---

### Task 5: DEV-109 Move Shared UI Widgets into `Axiom.Core` and Remove `Platformer -> Battle`

**Files:**
- Move: `Assets/Scripts/Battle/UI/HealthBarUI.cs` → `Assets/Scripts/Core/UI/HealthBarUI.cs`
- Move: `Assets/Scripts/Battle/UI/HealthBarUI.cs.meta` → `Assets/Scripts/Core/UI/HealthBarUI.cs.meta`
- Move: `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs` → `Assets/Scripts/Core/UI/SpellListPanelLogic.cs`
- Move: `Assets/Scripts/Battle/UI/SpellListPanelLogic.cs.meta` → `Assets/Scripts/Core/UI/SpellListPanelLogic.cs.meta`
- Move: `Assets/Scripts/Battle/UI/SpellListPanelUI.cs` → `Assets/Scripts/Core/UI/SpellListPanelUI.cs`
- Move: `Assets/Scripts/Battle/UI/SpellListPanelUI.cs.meta` → `Assets/Scripts/Core/UI/SpellListPanelUI.cs.meta`
- Move: `Assets/Scripts/Battle/UI/ItemMenuUI.cs` → `Assets/Scripts/Core/UI/ItemMenuUI.cs`
- Move: `Assets/Scripts/Battle/UI/ItemMenuUI.cs.meta` → `Assets/Scripts/Core/UI/ItemMenuUI.cs.meta`
- Move: `Assets/Scripts/Battle/UI/ItemSlotUI.cs` → `Assets/Scripts/Core/UI/ItemSlotUI.cs`
- Move: `Assets/Scripts/Battle/UI/ItemSlotUI.cs.meta` → `Assets/Scripts/Core/UI/ItemSlotUI.cs.meta`
- Create: `Assets/Scripts/Core/UI.meta`
- Modify: `Assets/Scripts/Platformer/Platformer.asmdef`
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/UI/BattleHUD.cs`
- Modify: `Assets/Scripts/Platformer/UI/ExplorationMenuController.cs`
- Modify: `Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs`
- Modify: `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs`
- Create: `Assets/Tests/Editor/Platformer/PlatformerAssemblyBoundaryTests.cs`
- Create: `Assets/Tests/Editor/Platformer/PlatformerAssemblyBoundaryTests.cs.meta`

- [ ] **Step 1: Add a failing test that encodes the assembly seam**

Create `Assets/Tests/Editor/Platformer/PlatformerAssemblyBoundaryTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;

namespace PlatformerTests
{
    public class PlatformerAssemblyBoundaryTests
    {
        [Test]
        public void PlatformerAsmdef_DoesNotReferenceAxiomBattle()
        {
            string json = File.ReadAllText("Assets/Scripts/Platformer/Platformer.asmdef");
            StringAssert.DoesNotContain("\"Axiom.Battle\"", json);
        }
    }
}
```

- [ ] **Step 2: Move the shared scripts into `Assets/Scripts/Core/UI/` without changing GUIDs**

Move the five shared UI scripts and their `.meta` files into `Assets/Scripts/Core/UI/`. Keep the original `.meta` files with the moved scripts so prefab and scene references keep the same GUIDs.

- [ ] **Step 3: Change the moved widget namespaces from `Axiom.Battle` to `Axiom.Core`**

Update the namespace in:

- `HealthBarUI.cs`
- `SpellListPanelLogic.cs`
- `SpellListPanelUI.cs`
- `ItemMenuUI.cs`
- `ItemSlotUI.cs`

Then update `using` directives in the Battle and Platformer callers to import `Axiom.Core` instead of `Axiom.Battle` for those widget types.

- [ ] **Step 4: Remove the battle assembly reference from platformer**

Delete `"Axiom.Battle"` from `Assets/Scripts/Platformer/Platformer.asmdef`.

- [ ] **Step 5: Re-run the relevant tests and verify scene bindings survived the move**

> **Unity Editor task (user):** Run `PlatformerTests.PlatformerAssemblyBoundaryTests` and `Axiom.Battle.Tests.SpellListPanelLogicTests`. Then open `Assets/Scenes/Battle.unity` and `Assets/Scenes/Platformer.unity`, select the HUD / spell list / item menu objects, and confirm there are no Missing Script references after the move.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-109): move shared ui widgets into core`
- `Assets/Scripts/Core/UI.meta`
- `Assets/Scripts/Core/UI/HealthBarUI.cs`
- `Assets/Scripts/Core/UI/HealthBarUI.cs.meta`
- `Assets/Scripts/Core/UI/SpellListPanelLogic.cs`
- `Assets/Scripts/Core/UI/SpellListPanelLogic.cs.meta`
- `Assets/Scripts/Core/UI/SpellListPanelUI.cs`
- `Assets/Scripts/Core/UI/SpellListPanelUI.cs.meta`
- `Assets/Scripts/Core/UI/ItemMenuUI.cs`
- `Assets/Scripts/Core/UI/ItemMenuUI.cs.meta`
- `Assets/Scripts/Core/UI/ItemSlotUI.cs`
- `Assets/Scripts/Core/UI/ItemSlotUI.cs.meta`
- `Assets/Scripts/Platformer/Platformer.asmdef`
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/UI/BattleHUD.cs`
- `Assets/Scripts/Platformer/UI/ExplorationMenuController.cs`
- `Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs`
- `Assets/Tests/Editor/Battle/SpellListPanelLogicTests.cs`
- `Assets/Tests/Editor/Platformer/PlatformerAssemblyBoundaryTests.cs`
- `Assets/Tests/Editor/Platformer/PlatformerAssemblyBoundaryTests.cs.meta`

---

### Task 6: DEV-110 Persist Pickup Collection Immediately After Pickup

**Files:**
- Modify: `Assets/Scripts/Platformer/ItemPickup.cs`
- Create: `Assets/Tests/Editor/Platformer/ItemPickupPersistenceTests.cs`
- Create: `Assets/Tests/Editor/Platformer/ItemPickupPersistenceTests.cs.meta`

- [ ] **Step 1: Write a failing Edit Mode integration test around the actual pickup flow**

Create `Assets/Tests/Editor/Platformer/ItemPickupPersistenceTests.cs`:

```csharp
using System.IO;
using System.Reflection;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace PlatformerTests
{
    public class ItemPickupPersistenceTests
    {
        private GameObject _gameManagerGo;
        private GameManager _gameManager;
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _gameManagerGo = new GameObject("GameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateCharacterData());
            _gameManager.SetSaveServiceForTests(new SaveService(_tempDirectory));

            typeof(GameManager)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_gameManager, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameManagerGo);
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
        }

        [Test]
        public void OnTriggerEnter2D_PlayerCollectsPickup_PersistsSaveImmediately()
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = "potion_hp";

            GameObject pickupGo = new GameObject("Pickup");
            var pickup = pickupGo.AddComponent<ItemPickup>();
            pickupGo.AddComponent<BoxCollider2D>().isTrigger = true;

            typeof(ItemPickup).GetField("_itemData", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, item);
            typeof(ItemPickup).GetField("_pickupId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pickup, "pickup_01");

            typeof(ItemPickup).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pickup, null);

            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            Collider2D playerCollider = playerGo.AddComponent<BoxCollider2D>();

            typeof(ItemPickup).GetMethod("OnTriggerEnter2D", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pickup, new object[] { playerCollider });

            var reloaded = new SaveService(_tempDirectory);
            Assert.IsTrue(reloaded.TryLoad(out SaveData saveData));
            CollectionAssert.Contains(saveData.collectedPickupIds, "pickup_01");
            Assert.AreEqual(1, _gameManager.PlayerState.Inventory.GetQuantity("potion_hp"));

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(pickupGo);
        }

        private static CharacterData CreateCharacterData()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.baseMaxHP = 100;
            data.baseMaxMP = 50;
            data.baseATK = 10;
            data.baseDEF = 5;
            data.baseSPD = 8;
            return data;
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm the save file is still missing the pickup event**

> **Unity Editor task (user):** Run `PlatformerTests.ItemPickupPersistenceTests`. Expected: the save reload step fails because `ItemPickup` currently marks the pickup collected but never calls `PersistToDisk()`.

- [ ] **Step 3: Persist immediately in the pickup success path**

In `Assets/Scripts/Platformer/ItemPickup.cs`, insert:

```csharp
GameManager.Instance.PersistToDisk();
```

immediately after:

```csharp
GameManager.Instance.MarkPickupCollected(_pickupId);
```

and before the optional animator trigger and `Destroy(gameObject);`.

- [ ] **Step 4: Re-run the pickup persistence test and the existing controller unit tests**

> **Unity Editor task (user):** Re-run `PlatformerTests.ItemPickupPersistenceTests` and `PlatformerTests.ItemPickupControllerTests`. Expected: the save file now contains `pickup_01`, and the existing item-grant controller tests still pass unchanged.

- [ ] **Step 5: Manually verify the player cannot lose the pickup on a crash path**

> **Unity Editor task (user):** In a platformer level containing a pickup, collect it, immediately stop Play Mode without touching a save point, restart the game, and confirm both the inventory item and the pickup despawn survived.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-110): persist pickup collection on collect`
- `Assets/Scripts/Platformer/ItemPickup.cs`
- `Assets/Tests/Editor/Platformer/ItemPickupPersistenceTests.cs`
- `Assets/Tests/Editor/Platformer/ItemPickupPersistenceTests.cs.meta`

---

## Final Verification Pass

- [ ] Run all touched Edit Mode fixtures:
  `PlatformerDebugContentTests`, `BattleVoiceBootstrapTests`, `VoskRecognizerServiceTests`, `PlatformerAssemblyBoundaryTests`, `ItemPickupPersistenceTests`, `ItemPickupControllerTests`, and `SpellListPanelLogicTests`.
- [ ] Open `Assets/Scenes/Battle.unity` and `Assets/Scenes/Platformer.unity` and confirm no Missing Script references on HUD, spell list, item menu, or voice bootstrap objects.
- [ ] Re-check the architecture notes with:

```powershell
rg -n "StateBasedCursorUI|Axiom.Battle|DebugMeltCast|DebugFreezeCast|PersistToDisk\\(" AGENTS.md docs/GAME_PLAN.md Assets/Scripts Assets/InputSystem_Actions.inputactions
```

Expected:

- `StateBasedCursorUI` is only documented in the explicit exception note.
- `Assets/Scripts/Platformer/Platformer.asmdef` no longer references `Axiom.Battle`.
- `DebugMeltCast` and `DebugFreezeCast` no longer exist in the input actions asset.
- `ItemPickup.cs` now persists immediately after `MarkPickupCollected(...)`.
