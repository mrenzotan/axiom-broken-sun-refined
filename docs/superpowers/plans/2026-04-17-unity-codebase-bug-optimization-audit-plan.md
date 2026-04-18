# DEV-## Unity Codebase Bug + Optimization Plan

## Scope

Full codebase remediation plan based on a Unity-focused scan of `Assets/Scripts/` and `Assets/Tests/`, sorted by severity/priority.

> Jira issue key was not provided in the request. Replace `DEV-##` with the actual key before execution.

## Context Gathered Before Planning

- Read project architecture constraints in `docs/GAME_PLAN.md` and `CLAUDE.md`.
- Verified existing asmdef patterns in:
  - `Assets/Scripts/Battle/Battle.asmdef`
  - `Assets/Scripts/Core/Axiom.Core.asmdef`
  - `Assets/Scripts/Data/Axiom.Data.asmdef`
  - `Assets/Scripts/Platformer/Platformer.asmdef`
  - `Assets/Scripts/Voice/Axiom.Voice.asmdef`
- Verified test asmdef structure in `Assets/Tests/**/**/*.asmdef` (runtime references + `optionalUnityReferences` test runner pattern).
- Verified existing scenes:
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/Platformer.unity`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/SampleScene.unity`
- Checked current Unity guidance using Context7 (`/websites/unity3d_manual`) and recent implementation references with Exa.

## Findings Sorted by Priority / Severity

### P0 Critical

1. Save data corruption risk from non-atomic writes in `Assets/Scripts/Core/SaveService.cs`.

### P1 High

1. Voice bootstrap null-reference risk in `Assets/Scripts/Voice/BattleVoiceBootstrap.cs` (missing serialized dependency validation before injection).
2. Push-to-talk action null-reference risk in `Assets/Scripts/Voice/MicrophoneInputHandler.cs` (`_pushToTalkAction.action` dereference without guards).
3. Main-thread teardown stall risk in `Assets/Scripts/Voice/VoskRecognizerService.cs` (`Task.Wait()` in `Stop()` can block/hang during shutdown edge cases).

### P2 Medium

1. Runtime debug hooks in production script tree:
   - `Assets/Scripts/Battle/UI/_DevLevelUpTrigger.cs`
   - `Assets/Scripts/Core/SpellUnlockDebug.cs`
2. Voice path GC pressure due to repeated buffer allocations in hot paths:
   - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
   - `Assets/Scripts/Voice/MicrophoneCapture.cs`
3. Architecture drift (logic in MonoBehaviours instead of lifecycle-only wrappers):
   - `Assets/Scripts/Battle/BattleController.cs`
   - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
   - `Assets/Scripts/Platformer/PlayerController.cs`

### P3 Low

1. UI allocation churn in condition badge refresh:
   - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`

---

## Implementation Plan (Execute in Order)

## Task 1 — P0 Save Integrity Hardening

- [ ] Update `SaveService.Save` to use crash-safe write flow:
  - Write JSON to temp file in same directory.
  - Flush to disk.
  - Replace primary save atomically (keep backup copy).
  - Clean up temp file on failure/success paths.
- [ ] Update `TryLoad` to fall back to backup file when primary load fails.
- [ ] Keep behavior stable: invalid save still treated as "no save" with warning.

- [ ] Add EditMode tests in `Assets/Tests/Editor/Core/`:
  - `SaveServiceAtomicWriteTests.cs`
  - Covers: successful save, primary-corrupt backup-recovery, null data guard.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-##): harden save writes with atomic replace and backup recovery`
  - `Assets/Scripts/Core/SaveService.cs`
  - `Assets/Scripts/Core/SaveService.cs.meta`
  - `Assets/Tests/Editor/Core/SaveServiceAtomicWriteTests.cs`
  - `Assets/Tests/Editor/Core/SaveServiceAtomicWriteTests.cs.meta`

## Task 2 — P1 Voice Bootstrap/Input Null-Safety

- [ ] Add early guard validation in `BattleVoiceBootstrap.Start` for required references before async model work:
  - `_microphoneInputHandler`
  - `_spellCastController`
  - `_actionMenuUI` (if spell-disabling path is required)
- [ ] Ensure failure path disables spell UI and exits cleanly without throwing.
- [ ] Add null-safe wiring guards in `MicrophoneInputHandler`:
  - Handle null `InputActionReference`.
  - Handle null `.action`.
  - Disable component after fatal wiring misconfiguration to prevent repeated exceptions.

- [ ] Add EditMode tests in `Assets/Tests/Editor/Voice/`:
  - `BattleVoiceBootstrapValidationTests.cs`
  - `MicrophoneInputHandlerLifecycleTests.cs`
  - Covers: missing reference guards, no crash on lifecycle callbacks with null action.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-##): prevent voice pipeline null reference startup failures`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs`
  - `Assets/Scripts/Voice/BattleVoiceBootstrap.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
  - `Assets/Tests/Editor/Voice/BattleVoiceBootstrapValidationTests.cs`
  - `Assets/Tests/Editor/Voice/BattleVoiceBootstrapValidationTests.cs.meta`
  - `Assets/Tests/Editor/Voice/MicrophoneInputHandlerLifecycleTests.cs`
  - `Assets/Tests/Editor/Voice/MicrophoneInputHandlerLifecycleTests.cs.meta`

## Task 3 — P1/P2 Recognizer Shutdown Stability + Voice GC Reduction

- [ ] Refactor `VoskRecognizerService.Stop` to avoid unbounded main-thread blocking:
  - Use cancellation + bounded wait strategy.
  - Handle task fault/aggregate exceptions explicitly.
  - Ensure idempotent disposal behavior.
- [ ] Keep all recognizer processing off main thread.
- [ ] Reduce per-frame allocations in voice capture path:
  - Introduce reusable buffers/ring-buffer strategy where safe.
  - Avoid `new float[]`/`new short[]` in high-frequency loops where possible.

- [ ] Add tests in `Assets/Tests/Editor/Voice/`:
  - `VoskRecognizerServiceShutdownTests.cs`
  - `MicrophoneCaptureBufferReuseTests.cs`
  - Covers: cancellation/shutdown path does not throw; repeated processing avoids runaway allocations.

> **Unity Editor task (user):** In Unity Profiler, capture a 60-second push-to-talk combat session before and after Task 3. Verify reduced GC alloc spikes and no teardown hitch during scene exit.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-##): stabilize recognizer shutdown and reduce voice input allocations`
  - `Assets/Scripts/Voice/VoskRecognizerService.cs`
  - `Assets/Scripts/Voice/VoskRecognizerService.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs.meta`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceShutdownTests.cs`
  - `Assets/Tests/Editor/Voice/VoskRecognizerServiceShutdownTests.cs.meta`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs.meta`

## Task 4 — P2 Runtime Debug Surface Cleanup

- [ ] Move or gate debug scripts from runtime:
  - `_DevLevelUpTrigger.cs`
  - `SpellUnlockDebug.cs`
- [ ] Use editor/dev compile guards or editor-only placement so they cannot execute in production play paths.

> **Unity Editor task (user):** Open battle/core prefabs and scenes and remove any runtime references to debug-only components; then save modified assets.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-##): isolate debug utilities from runtime builds`
  - `Assets/Scripts/Battle/UI/_DevLevelUpTrigger.cs`
  - `Assets/Scripts/Battle/UI/_DevLevelUpTrigger.cs.meta`
  - `Assets/Scripts/Core/SpellUnlockDebug.cs`
  - `Assets/Scripts/Core/SpellUnlockDebug.cs.meta`
  - `Assets/Scenes/Battle.unity` (if modified)
  - `Assets/Scenes/Battle.unity.meta` (if modified)
  - `Assets/Scenes/Platformer.unity` (if modified)
  - `Assets/Scenes/Platformer.unity.meta` (if modified)

## Task 5 — P2 Architecture Conformance Refactor

- [ ] Extract business logic from MonoBehaviour-heavy classes into plain C# services:
  - `BattleController`
  - `ExplorationEnemyCombatTrigger`
  - `PlayerController`
- [ ] Keep MonoBehaviour responsibilities to lifecycle/event wiring (`Start`, `Update`, `OnDestroy`).
- [ ] Ensure extracted logic is covered by EditMode tests.

- [ ] Add tests:
  - `Assets/Tests/Editor/Battle/BattleControllerServiceTests.cs`
  - `Assets/Tests/Editor/Platformer/ExplorationCombatEntryServiceTests.cs`
  - `Assets/Tests/Editor/Platformer/PlayerControllerLogicTests.cs`

> **Unity Editor task (user):** Rewire serialized references for any newly introduced service wrapper components in affected prefabs/scenes, then save scenes.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-##): enforce lifecycle-only monobehaviour architecture boundaries`
  - `Assets/Scripts/Battle/BattleController.cs`
  - `Assets/Scripts/Battle/BattleController.cs.meta`
  - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
  - `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs.meta`
  - `Assets/Scripts/Platformer/PlayerController.cs`
  - `Assets/Scripts/Platformer/PlayerController.cs.meta`
  - `Assets/Scripts/Battle/Services.meta` (if folder is new)
  - `Assets/Scripts/Platformer/Services.meta` (if folder is new)
  - `Assets/Scripts/Battle/Services/*.cs` (all newly created service files)
  - `Assets/Scripts/Battle/Services/*.cs.meta` (matching metas for all created service files)
  - `Assets/Scripts/Platformer/Services/*.cs` (all newly created service files)
  - `Assets/Scripts/Platformer/Services/*.cs.meta` (matching metas for all created service files)
  - `Assets/Tests/Editor/Battle/BattleControllerServiceTests.cs`
  - `Assets/Tests/Editor/Battle/BattleControllerServiceTests.cs.meta`
  - `Assets/Tests/Editor/Platformer/ExplorationCombatEntryServiceTests.cs`
  - `Assets/Tests/Editor/Platformer/ExplorationCombatEntryServiceTests.cs.meta`
  - `Assets/Tests/Editor/Platformer/PlayerControllerLogicTests.cs`
  - `Assets/Tests/Editor/Platformer/PlayerControllerLogicTests.cs.meta`

## Task 6 — P3 Condition Badge UI Allocation Optimization

- [ ] Replace destroy/recreate badge behavior with reuse/pool strategy.
- [ ] Remove avoidable LINQ allocations in frequent refresh paths.
- [ ] Keep visuals and ordering behavior identical.

- [ ] Add EditMode tests:
  - `Assets/Tests/Editor/UI/ConditionBadgeUIReuseTests.cs`

> **Unity Editor task (user):** In Battle scene Play Mode, trigger repeated status condition updates and verify badge visuals and ordering are unchanged.

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `refactor(DEV-##): reduce condition badge UI allocation churn`
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs`
  - `Assets/Scripts/Battle/UI/ConditionBadgeUI.cs.meta`
  - `Assets/Tests/Editor/UI/ConditionBadgeUIReuseTests.cs`
  - `Assets/Tests/Editor/UI/ConditionBadgeUIReuseTests.cs.meta`

---

## Validation Gate (Before Marking Plan Complete)

- [ ] Run Unity EditMode tests for touched modules:
  - Core, Voice, Battle, Platformer, UI.
- [ ] Run targeted PlayMode checks:
  - Voice cast flow in `Battle.unity`.
  - Scene transition + teardown stability.
- [ ] Verify no new asmdef needed for this plan scope. If a new folder is introduced, add matching `Axiom.<Module>.asmdef` and test asmdef references.
- [ ] Re-profile voice capture path after Task 3 and attach before/after screenshots to the Jira ticket.

---

## Rollout Order Recommendation

1. Task 1 (P0)  
2. Task 2 (P1)  
3. Task 3 (P1/P2)  
4. Task 4 (P2)  
5. Task 5 (P2)  
6. Task 6 (P3)

