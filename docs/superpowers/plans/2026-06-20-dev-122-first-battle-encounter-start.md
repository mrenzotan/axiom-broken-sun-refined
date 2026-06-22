# DEV-122 First Battle Encounter Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` together with `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Level_1-1 first Ice Slime encounter lock Kaelen at the moment the enemy notices him, let the enemy close the gap at the existing believable scene speed, and restore controls only after the battle transition has begun.

**Architecture:** Reuse the existing `Tutorial_Surprised` trigger as the scene-specific intro boundary and the existing Ice Slime body trigger as the authoritative contact-to-battle path. Extend the tutorial trigger only enough to lock attack as well as movement/jump, then let `ExplorationEnemyCombatTrigger` release that lock after `BattleTriggerService.TriggerBattle(...)` synchronously starts the white-flash transition. Keep `EnemyController`, `EnemyPatrolBehavior`, and the Ice Slime prefab unchanged; the existing Level_1-1 `chaseSpeed = 3` override is the encounter-only tuning.

**Tech Stack:** Unity 6, C#, Rigidbody2D/Collider2D triggers, Input System, Unity Test Framework, uGUI tutorial prompt, UVCS

---

## Current-state findings and fixed decisions

- `Assets/Scenes/Level_1-1.unity` places Kaelen at X `-61.39`, `IceSlime_01` at X `-41.1`, and gives that instance `aggroRadius = 6` and `chaseSpeed = 3` (the reusable prefab remains `5`).
- `Tutorial_Surprised` currently locks movement/jump, but its effective X position is after the enemy and attack remains enabled. It therefore cannot guarantee the intended enemy-first contact.
- The trigger boundary will be centered at approximately X `-47.0`, just inside the slime's left detection edge (`-41.1 - 6`), so lock and aggro begin together. Final collider height must cover Kaelen's grounded and jumping collider extents.
- `ExplorationEnemyCombatTrigger.OnTriggerEnter2D` remains the only authority that starts the Surprised battle. No timer or distance-based fallback will be added.
- Controls are released after `BattleTriggerService.TriggerBattle(...)` returns because that call ends by invoking `BeginTransition("Battle", WhiteFlash)`.

## File map

- Modify `Assets/Scripts/Platformer/PlayerExplorationAttack.cs` — expose a small, explicit attack-input lock used by the scripted intro.
- Modify `Assets/Scripts/Platformer/TutorialPromptTrigger.cs` — optionally lock attack and provide an idempotent release entry point.
- Modify `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` — release the assigned intro trigger after transition start.
- Modify `Assets/Scripts/Platformer/PlayerController.cs` — expose read-only movement-lock state for focused verification.
- Create `Assets/Tests/PlayMode/Platformer/FirstBattleEncounterLockTests.cs` — verify movement, jump/attack gating, and release behavior through real components.
- Create `Assets/Tests/PlayMode/Platformer/PlatformerPlayModeTests.asmdef` — Play Mode test assembly.
- Modify `Assets/Scenes/Level_1-1.unity` — move/resize the intro boundary and wire explicit references.

### Task 1: Add failing automated lock/release coverage

**Files:**
- Create: `Assets/Tests/PlayMode/Platformer/PlatformerPlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/Platformer/FirstBattleEncounterLockTests.cs`

- [ ] **Step 1: Create the Play Mode assembly**

```json
{
  "name": "PlatformerPlayModeTests",
  "references": [
    "Axiom.Platformer",
    "Axiom.Core",
    "Axiom.Data",
    "Unity.InputSystem",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Create the focused Play Mode test**

```csharp
using System.Collections;
using System.Reflection;
using Axiom.Platformer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlatformerPlayModeTests
{
    public class FirstBattleEncounterLockTests
    {
        private GameObject _player;
        private GameObject _trigger;

        [TearDown]
        public void TearDown()
        {
            if (_trigger != null) Object.DestroyImmediate(_trigger);
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [UnityTest]
        public IEnumerator FirstBattleIntro_EnterLocksMovementAndAttack_ReleaseRestoresBoth()
        {
            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.tag = "Player";
            _player.AddComponent<Rigidbody2D>();
            BoxCollider2D playerCollider = _player.AddComponent<BoxCollider2D>();
            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(_player.transform);

            PlayerController playerController = _player.AddComponent<PlayerController>();
            SetField(playerController, "groundCheck", groundCheck.transform);
            PlayerExplorationAttack playerAttack = _player.AddComponent<PlayerExplorationAttack>();

            _trigger = new GameObject("Tutorial_Surprised");
            _trigger.SetActive(false);
            BoxCollider2D triggerCollider = _trigger.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            TutorialPromptTrigger tutorialPrompt = _trigger.AddComponent<TutorialPromptTrigger>();
            SetField(tutorialPrompt, "_lockMovementWhileInside", true);
            SetField(tutorialPrompt, "_lockAttackWhileInside", true);
            SetField(tutorialPrompt, "_playerController", playerController);
            SetField(tutorialPrompt, "_playerAttack", playerAttack);

            _player.SetActive(true);
            _trigger.SetActive(true);
            _trigger.SendMessage("OnTriggerEnter2D", playerCollider);

            Assert.IsTrue(playerController.IsMovementLocked,
                "Kaelen must not move away while the first enemy closes the gap.");
            Assert.IsTrue(playerAttack.IsInputLocked,
                "Attack would convert the required Surprised contact into an Advantaged battle.");

            tutorialPrompt.ReleasePlayerLock();
            Assert.IsFalse(playerController.IsMovementLocked);
            Assert.IsFalse(playerAttack.IsInputLocked);

            tutorialPrompt.ReleasePlayerLock();
            Assert.IsFalse(playerController.IsMovementLocked,
                "Transition release must be idempotent with trigger exit or teardown.");

            SetField(tutorialPrompt, "_lockAttackWhileInside", false);
            _trigger.SendMessage("OnTriggerEnter2D", playerCollider);
            Assert.IsTrue(playerController.IsMovementLocked);
            Assert.IsFalse(playerAttack.IsInputLocked,
                "Existing movement-only tutorial zones must continue permitting attack.");
            tutorialPrompt.ReleasePlayerLock();

            yield return null;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing serialized field {name}");
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 3: Run the new test and verify the red state**

Unity Editor → Window → General → Test Runner → PlayMode → `FirstBattleEncounterLockTests`.

Expected: compilation fails because `IsMovementLocked`, `IsInputLocked`, `_lockAttackWhileInside`, `_playerAttack`, and `ReleasePlayerLock()` do not exist yet. This is the intended red state.

### Task 2: Add an explicit attack-input lock

**Files:**
- Modify: `Assets/Scripts/Platformer/PlayerExplorationAttack.cs`

- [ ] **Step 1: Add the focused lock state and guard**

Add this property and method beside the existing private fields, and make it the first guard in `Update()`:

```csharp
public bool IsInputLocked { get; private set; }

public void SetInputLocked(bool locked)
{
    IsInputLocked = locked;
}

private void Update()
{
    if (IsInputLocked) return;
    if (!_attackAction.WasPerformedThisFrame()) return;

    Vector2 attackCenter = AttackCenter();
    Collider2D hit = Physics2D.OverlapCircle(attackCenter, _attackRange, _enemyLayer);
    ExplorationEnemyCombatTrigger trigger = hit != null
        ? hit.GetComponent<ExplorationEnemyCombatTrigger>()
        : null;
    _controller.BeginAttack(trigger);
}
```

Do not disable the whole component: keeping lifecycle ownership unchanged avoids an unrelated `OnEnable`/`OnDisable` action-map toggle during the transition.

- [ ] **Step 2: Compile in Unity**

Open the project and wait for script compilation. Expected: no Console errors and no generated Input Actions changes.

### Task 3: Make the tutorial trigger own and release the complete intro lock

**Files:**
- Modify: `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`
- Modify: `Assets/Scripts/Platformer/PlayerController.cs`

- [ ] **Step 1: Expose movement-lock state without changing movement behavior**

Add beside `IsFacingRight` in `PlayerController`:

```csharp
public bool IsMovementLocked => _movement?.IsMovementLocked == true;
```

- [ ] **Step 2: Add optional attack-lock wiring to `TutorialPromptTrigger`**

Add serialized fields next to `_lockMovementWhileInside`:

```csharp
[SerializeField]
[Tooltip("When true, also blocks exploration attacks while this tutorial lock is active.")]
private bool _lockAttackWhileInside;

[SerializeField]
[Tooltip("Required when _lockAttackWhileInside is true.")]
private PlayerExplorationAttack _playerAttack;

private bool _playerLockActive;
```

Replace the lock portions of enter/exit with one helper and add the public release method:

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    if (!other.CompareTag("Player")) return;
    if (_panel != null) _panel.Show(_message);
    SetPlayerLock(true);
}

private void OnTriggerExit2D(Collider2D other)
{
    if (!other.CompareTag("Player")) return;
    if (_panel != null) _panel.Hide();
    SetPlayerLock(false);
}

public void ReleasePlayerLock()
{
    if (_panel != null) _panel.Hide();
    SetPlayerLock(false);
}

private void SetPlayerLock(bool locked)
{
    if (_playerLockActive == locked) return;
    _playerLockActive = locked;

    if (_lockMovementWhileInside && _playerController != null)
        _playerController.SetTutorialMovementLocked(locked);
    if (_lockAttackWhileInside && _playerAttack != null)
        _playerAttack.SetInputLocked(locked);
}
```

This remains optional, so existing tutorial zones retain current behavior. Do not change `SetTutorialMovementLocked`; `Tutorial_Advantaged` must continue leaving attack enabled.

- [ ] **Step 3: Add teardown safety**

Add:

```csharp
private void OnDisable()
{
    SetPlayerLock(false);
}
```

Expected: disabling the zone or tearing down the scene cannot leave either control lock active on a surviving player object.

### Task 4: Release controls only after battle transition start

**Files:**
- Modify: `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`

- [ ] **Step 1: Add an optional scene-specific intro reference**

Add after `_tutorialMode`:

```csharp
[SerializeField]
[Tooltip("Optional tutorial trigger whose player lock is released after this battle transition begins.")]
private TutorialPromptTrigger _encounterIntroTrigger;
```

- [ ] **Step 2: Release after the transition call returns**

At the end of `TriggerBattle(...)`, immediately after `_triggerService.TriggerBattle(...)`, add:

```csharp
_encounterIntroTrigger?.ReleasePlayerLock();
```

Do not release before `CanStartBattle()` or before `TriggerBattle(...)`: if setup is invalid, the player must not regain control and bypass the intended encounter. Do not assign this field on any enemy except `IceSlime_01`.

- [ ] **Step 3: Compile in Unity**

Expected: no Console errors; all other enemy prefab and scene instances show the new reference as `None`.

- [ ] **Step 4: Run the focused Play Mode test and verify green**

Unity Editor → Window → General → Test Runner → PlayMode → `FirstBattleEncounterLockTests`.

Expected: both the complete first-battle lock path and the movement-only regression assertion pass, with zero skipped tests.

### Task 5: Wire and tune the Level_1-1 encounter

**Files:**
- Modify: `Assets/Scenes/Level_1-1.unity`

> **Unity Editor task (user):** Open `Level_1-1`. Select `Tutorial_Surprised`, move its trigger boundary to approximately X `-47.0` (just inside `IceSlime_01`'s left aggro edge), reset the collider X offset to `0`, and use an X width around `1.5`. Keep enough Y coverage to catch Kaelen on the traversable lane. Confirm the trigger is crossed before Kaelen can touch or attack the slime.

> **Unity Editor task (user):** On `Tutorial_Surprised`, keep **Lock Movement While Inside** enabled, enable **Lock Attack While Inside**, assign the scene Player's `PlayerExplorationAttack`, and retain the existing PlayerController and tutorial panel references.

> **Unity Editor task (user):** On `IceSlime_01` → `ExplorationEnemyCombatTrigger`, assign `Tutorial_Surprised` to **Encounter Intro Trigger**. Confirm `_tutorialMode = FirstBattle`, `aggroRadius = 6`, `chaseSpeed = 3`, and no patrol points. Do not apply these overrides to the Ice Slime prefab.

- [ ] **Step 1: Save the scene and inspect overrides**

Expected: only `Level_1-1.unity` changes; `Assets/Prefabs/Enemies/Level 1/Ice Slime.prefab` remains unchanged at its normal chase speed.

### Task 6: Acceptance test from level entry to battle start

> **Unity Editor task (user):** Start Play Mode from `Level_1-1` at the normal level-entry spawn, not from a repositioned Scene view camera.

- [ ] **Step 1: Verify the complete first-run sequence**

Expected observations, in order:

1. Kaelen can move normally from level entry toward `IceSlime_01`.
2. Crossing `Tutorial_Surprised` shows its message and immediately stops horizontal movement and jump.
3. Attack input does nothing while the lock is active.
4. The slime detects Kaelen at the boundary and approaches at `3` units/second; it should take roughly two seconds to close the six-unit detection gap.
5. Kaelen cannot retreat, jump over, or attack the slime to change the encounter outcome.
6. The slime's body trigger touches Kaelen and starts a `Surprised` FirstBattle white-flash transition.
7. The prompt hides and both control locks release only after `BeginTransition` has been called.
8. Battle scene loads with the Ice Slime, FirstBattle tutorial, and enemy-first start state.

- [ ] **Step 2: Verify regression boundaries**

Test one ordinary exploration enemy in another level and `Tutorial_Advantaged` in Level_1-1.

Expected: ordinary enemy patrol/chase remains unchanged; `Tutorial_Advantaged` still locks movement/jump but permits attack; returning from battle does not immediately retrigger the same encounter.

- [ ] **Step 3: Run the complete relevant test set**

Unity Test Runner:

- EditMode → `PlatformerTests` — expected all pass, zero skipped.
- PlayMode → `PlatformerPlayModeTests` — expected all pass, zero skipped.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage only the files below → Check in with message: `fix(DEV-122): script first battle encounter start`

- `Assets/Scripts/Platformer/PlayerController.cs`
- `Assets/Scripts/Platformer/PlayerExplorationAttack.cs`
- `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`
- `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs`
- `Assets/Tests/PlayMode/Platformer.meta`
- `Assets/Tests/PlayMode/Platformer/PlatformerPlayModeTests.asmdef`
- `Assets/Tests/PlayMode/Platformer/PlatformerPlayModeTests.asmdef.meta`
- `Assets/Tests/PlayMode/Platformer/FirstBattleEncounterLockTests.cs`
- `Assets/Tests/PlayMode/Platformer/FirstBattleEncounterLockTests.cs.meta`
- `Assets/Scenes/Level_1-1.unity`
- `docs/superpowers/plans/2026-06-20-dev-122-first-battle-encounter-start.md`

Do not stage the Ice Slime prefab or unrelated scene changes.
