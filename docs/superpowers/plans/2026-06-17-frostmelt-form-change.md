# Frostmelt Spawn Chemistry-Driven Form Change — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-unity-game-dev-plans` together with `superpowers:executing-plans` (or `superpowers:subagent-driven-development`) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Frost-Melt Spawn reliably morph back to its liquid form when the `Solid` condition expires, by decoupling the visual form from the action state machine.

**Architecture:** Chemistry conditions remain the single source of truth. `BattleController.SyncEnemyFormToConditions()` writes the `Phase` int (0=Liquid, 1=Ice) and pulses a `PhaseChange` trigger; a rebuilt Animator routes that through an **AnyState** morph that cannot be blocked by walk/attack/hurt transitions. On the enemy's turn the morph plays fully (signalled by an Animation Event) before the enemy acts.

**Tech Stack:** Unity 6 LTS, C# (Mono), Unity Test Framework (Edit Mode / NUnit), Animator Controller, Unity Version Control (UVCS).

**Source spec:** `docs/superpowers/specs/2026-06-17-frostmelt-form-change-design.md`

---

## Implementation status — updated 2026-06-17 (revised after in-place controller fix)

**Tasks 1–3 (all C#) are DONE and checked in;** Edit Mode tests pass green.

**Deviation from the original plan:** the user fixed the Animator by **editing the existing controller in place** (`Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnBattle.controller`) using the spec's Approach A (`Phase` int + `PhaseChange` trigger + AnyState morph routing), instead of authoring a brand-new controller. That made the reverse morph (Ice→Liquid) fire — but it fired *late* (the enemy attacked in ice form, then morphed during its retreat). The Task 3 C# turn-sequencing fix is what now makes the enemy morph **fully before** it attacks.

Consequences for the remaining tasks:
- **Task 4 (author new controller)** and **Task 6 (assign new controller / delete old)** are **DROPPED** — there is no new controller to create, reassign, or delete. The existing controller is already on the battle prefab with `Phase` defaulting to 0.
- **Task 5 (Animation Events on the morph clips)** is now **OPTIONAL**: `BattleController._morphDelay` (2 s fallback) already sequences morph-then-attack. Adding the events only makes the attack start frame-accurately when the morph ends, with `_morphDelay` as a pure safety net. Keep `_morphDelay` ≥ the morph clip length.
- **Task 7 (Play Mode acceptance)** remains the final gate and can run without Task 5.

**Check-in convention used:** no Jira ticket exists for this work, so check-ins use a `frostmelt` scope (e.g. `fix(frostmelt): …`) rather than `DEV-##`.

---

## Global Constraints

- **Scope:** Frost-Melt Spawn (Level 1) only. Do **not** touch the Frostmelt Sentinel or the HP-threshold phase system.
- **No chemistry changes:** do not modify Freeze data, condition durations, or chemistry rules.
- **MonoBehaviour separation (GAME_PLAN):** `EnemyBattleAnimator` and `BattleController` stay lifecycle/event adapters; no game logic added to them beyond animator wiring and turn sequencing already present.
- **Version control is UVCS, never git.** Every check-in: Unity Version Control → Pending Changes → stage listed files → Check in with `<type>(DEV-##): <desc>` per `docs/VERSION_CONTROL.md`.
- **`DEV-##`** in every check-in message below — no Jira ticket exists for this work, so check-ins use a `frostmelt` scope instead (e.g. `fix(frostmelt): …`). See the implementation-status note above.
- **Claude writes all C# directly. The user performs all Unity Editor tasks** (marked `> **Unity Editor task (user):**`).
- **Form numbering is fixed:** `Phase == 0` is Liquid, `Phase == 1` is Ice, everywhere (animator + code).

---

## Task 1: Data-layer regression tests (Edit Mode)

These guard the chemistry/form intent at the data level. The underlying logic is already correct, so **these tests pass on the current code** — they exist to document the intent (a freeze must revert after 2 turns) and to catch any future regression in `CharacterStats` / `EnemyData`. (The actual reported bug lives in the Animator + turn sequencing, fixed in later tasks, which are verified in Play Mode.)

**Files:**
- Modify: `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- Modify: `Assets/Tests/Editor/Data/EnemyDataTests.cs`

**Interfaces:**
- Consumes (existing, unchanged): `CharacterStats.Initialize(List<ChemicalCondition>)`, `CharacterStats.ConsumeCondition(ChemicalCondition)`, `CharacterStats.ApplyMaterialTransformation(ChemicalCondition transformsTo, ChemicalCondition suppressed, int duration)`, `CharacterStats.ProcessConditionTurn()`, `CharacterStats.HasCondition(ChemicalCondition)`, `CharacterStats.ActiveMaterialConditions`, `EnemyData.GetFormIndexForConditions(List<ChemicalCondition>)`, and the existing `EnemyDataTests.MakeTwoFormEnemy()` helper.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Add the 2-turn Solid lifecycle test to `CharacterStatsTests.cs`**

Add this method inside the `CharacterStatsTests` class, after the existing
`ProcessConditionTurn_ExpiredMaterialTransformation_RestoresInnateCondition` test (~line 517):

```csharp
[Test]
public void ProcessConditionTurn_TwoTurnTransformation_RemainsSolidThroughTurnOneThenRestoresLiquid()
{
    var stats = MakeStats();
    stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
        { Axiom.Data.ChemicalCondition.Liquid });

    // Freeze reaction: consume Liquid, apply Solid for 2 turns.
    stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
    stats.ApplyMaterialTransformation(
        Axiom.Data.ChemicalCondition.Solid,
        Axiom.Data.ChemicalCondition.Liquid,
        duration: 2);

    // Enemy turn 1: Solid 2 -> 1. Still Solid — the enemy must stay iced this turn.
    stats.ProcessConditionTurn();
    Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Solid),
        "Solid must persist through turn 1 of a 2-turn freeze.");
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));

    // Enemy turn 2: Solid 1 -> 0. Expires; innate Liquid restored — enemy must revert.
    stats.ProcessConditionTurn();
    Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Solid),
        "After 2 turns the Solid condition must be gone.");
    Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid),
        "When Solid expires the innate Liquid condition must be restored.");
}
```

- [ ] **Step 2: Add the chemistry→form arc test to `EnemyDataTests.cs`**

Add this method inside the `EnemyDataTests` class (it reuses the existing `MakeTwoFormEnemy()` helper;
`DataTests.asmdef` already references `Axiom.Battle`, so `CharacterStats` is available):

```csharp
[Test]
public void GetFormIndexForConditions_FollowsSolidLifecycle_RevertsToLiquidAfterTwoTurns()
{
    // The reported bug: after the 2-turn Solid wears off, the enemy's form must
    // return to liquid (0). This ties CharacterStats' condition lifecycle to the
    // form index BattleController reads each turn.
    var data = MakeTwoFormEnemy();
    var stats = new Axiom.Battle.CharacterStats { MaxHP = 25, MaxMP = 0, ATK = 3, DEF = 5, SPD = 5 };
    stats.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });

    Assert.AreEqual(0, data.GetFormIndexForConditions(stats.ActiveMaterialConditions),
        "A liquid enemy starts in its liquid form (0).");

    // Freeze reaction: Liquid -> Solid for 2 turns -> ice form (1).
    stats.ConsumeCondition(ChemicalCondition.Liquid);
    stats.ApplyMaterialTransformation(ChemicalCondition.Solid, ChemicalCondition.Liquid, 2);
    Assert.AreEqual(1, data.GetFormIndexForConditions(stats.ActiveMaterialConditions),
        "Freeze transforms the enemy to Solid — it must show the ice form (1).");

    // Enemy turn 1: still Solid -> ice form (1).
    stats.ProcessConditionTurn();
    Assert.AreEqual(1, data.GetFormIndexForConditions(stats.ActiveMaterialConditions),
        "Mid-freeze (turn 1 of 2) the enemy must remain in ice form (1).");

    // Enemy turn 2: Solid expires, Liquid restored -> liquid form (0). The fix's target.
    stats.ProcessConditionTurn();
    Assert.AreEqual(0, data.GetFormIndexForConditions(stats.ActiveMaterialConditions),
        "Once Solid expires the enemy MUST revert to its liquid form (0).");

    Object.DestroyImmediate(data);
}
```

- [ ] **Step 3: Run the new Edit Mode tests**

In Unity: Window → General → Test Runner → EditMode. Run
`CharacterStatsTests.ProcessConditionTurn_TwoTurnTransformation_RemainsSolidThroughTurnOneThenRestoresLiquid`
and `EnemyDataTests.GetFormIndexForConditions_FollowsSolidLifecycle_RevertsToLiquidAfterTwoTurns`.
Expected: **both PASS** (they confirm the data layer is correct).

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message:
`test(DEV-##): add frostmelt 2-turn solid and form-arc regression tests`
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs`
- `Assets/Tests/Editor/Data/EnemyDataTests.cs`

---

## Task 2: Add the enemy morph-complete signal to `EnemyBattleAnimator`

Additive only — exposes the Animation-Event hook the morph clips will call and the event
`BattleController` subscribes to. Mirrors the existing `OnAttackSequenceComplete` / `AnimEvent_OnHit`
pattern. (MonoBehaviour adapter — no Edit Mode test; verified by compile and later Play Mode.)

**Files:**
- Modify: `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

**Interfaces:**
- Produces: `event System.Action EnemyBattleAnimator.OnPhaseChangeComplete` and
  `void EnemyBattleAnimator.AnimEvent_OnPhaseChangeComplete()` — consumed by Task 3 and by the
  Animation Events added in Task 5.

- [ ] **Step 1: Add the event and Animation-Event method**

In `EnemyBattleAnimator.cs`, after the existing `OnAttackSequenceComplete` event and
`AnimEvent_OnHit()` method (near lines 46–52), add:

```csharp
/// <summary>
/// Fired by a Unity Animation Event on the last frame of each phase-change (morph) clip.
/// BattleController subscribes so it can wait for the morph to finish before the enemy acts.
/// </summary>
public event System.Action OnPhaseChangeComplete;

/// <summary>
/// Called by Unity Animation Event on the final frame of the morph clips
/// (FrostmeltSpawnPhaseChange / FrostmeltSpawnPhaseChange2).
/// The method name must match exactly what is set in the Animation Event inspector.
/// </summary>
public void AnimEvent_OnPhaseChangeComplete() => OnPhaseChangeComplete?.Invoke();
```

Leave `SetPhase`, `TriggerFormChange`, and (for now) `SetPhaseChangeTarget` unchanged.

- [ ] **Step 2: Verify it compiles**

Return to Unity, let it recompile. Console shows **no errors**. (Nothing references the new members
yet — that comes in Task 3.)

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message:
`feat(DEV-##): add enemy morph-complete animation signal`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

---

## Task 3: Sequence the morph before the enemy acts; drop dead `PhaseChangeTarget`

Make `SyncEnemyFormToConditions()` report whether a morph started, wait for that morph to finish
before `ExecuteEnemyTurn()`, wire the new event, and remove the dead `PhaseChangeTarget` wiring
(parameter never existed in the controller).

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`
- Modify: `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

**Interfaces:**
- Consumes: `EnemyBattleAnimator.OnPhaseChangeComplete` (Task 2), `EnemyBattleAnimator.SetPhase(int)`,
  `EnemyBattleAnimator.TriggerFormChange()`, `EnemyData.GetFormIndexForConditions(...)`.
- Produces: `bool BattleController.SyncEnemyFormToConditions()` (now returns whether a morph began).

- [ ] **Step 1: Make `SyncEnemyFormToConditions()` return `bool` and drop the dead call**

Replace the whole method (currently `private void` at ~lines 1085–1099) with:

```csharp
private bool SyncEnemyFormToConditions()
{
    if (_enemyData == null || _enemyAnimator == null) return false;
    if (_enemyData.formDefinitions == null || _enemyData.formDefinitions.Count == 0) return false;

    int targetForm = _enemyData.GetFormIndexForConditions(_enemyStats.ActiveMaterialConditions);
    if (targetForm == _currentEnemyForm) return false;

    _currentEnemyForm = targetForm;
    _enemyAnimator.SetPhase(targetForm);
    _enemyAnimator.TriggerFormChange();

    Debug.Log($"[Form] Enemy visual synced to form {_currentEnemyForm} (driven by chemistry conditions)");
    return true;
}
```

(This removes the `_enemyAnimator.SetPhaseChangeTarget(targetForm);` line.)

- [ ] **Step 2: Add the morph fallback field and runtime flag**

Near the existing serialized timing fields (`_spellFireTimeout` at ~line 251 / `_actionDelay` at ~line 69), add:

```csharp
[SerializeField]
[Tooltip("Fallback seconds to wait for the enemy's form-change (morph) animation to finish before " +
         "it acts, used only if the morph clip's Animation Event never fires. Set at or above the " +
         "morph clip length.")]
private float _morphDelay = 2f;

private bool _enemyMorphComplete;
```

- [ ] **Step 3: Add the morph-complete handler and the sequencing coroutine**

Near `ExecuteEnemyTurn()` / `CompleteEnemyAction()` (~lines 956–983), add:

```csharp
private void OnEnemyPhaseChangeComplete() => _enemyMorphComplete = true;

private System.Collections.IEnumerator PlayMorphThenExecuteEnemyTurn()
{
    _enemyMorphComplete = false;
    float elapsed = 0f;
    while (!_enemyMorphComplete && elapsed < _morphDelay)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }
    _enemyMorphComplete = false;
    ExecuteEnemyTurn();
}
```

- [ ] **Step 4: Branch `ProcessEnemyTurnStart()` to morph-then-act**

In `ProcessEnemyTurnStart()` (~lines 919–954): change the form-sync call (line ~930) from
`SyncEnemyFormToConditions();` to capture the result:

```csharp
bool morphStarted = SyncEnemyFormToConditions();
```

Then, at the end of the method, replace the final unconditional `ExecuteEnemyTurn();` (line ~953) so it reads:

```csharp
OnConditionsChanged?.Invoke(_enemyStats);
OnConditionsChanged?.Invoke(_playerStats);
if (morphStarted) StartCoroutine(PlayMorphThenExecuteEnemyTurn());
else              ExecuteEnemyTurn();
```

(The existing `IsDefeated` and `ActionSkipped` early-returns above stay exactly as they are.)

- [ ] **Step 5: Subscribe to the morph-complete event**

In the animator-subscription block, after `_enemyAnimator.OnAttackSequenceComplete += OnEnemySequenceComplete;` (~line 460), add:

```csharp
_enemyAnimator.OnPhaseChangeComplete += OnEnemyPhaseChangeComplete;
```

- [ ] **Step 6: Unsubscribe in both teardown sites**

In the `Initialize()` teardown block, after `_enemyAnimator.OnAttackSequenceComplete -= OnEnemySequenceComplete;` (~line 335), add:

```csharp
_enemyAnimator.OnPhaseChangeComplete -= OnEnemyPhaseChangeComplete;
```

In `OnDestroy()`, after `if (_enemyAnimator != null) _enemyAnimator.OnAttackSequenceComplete -= OnEnemySequenceComplete;` (~line 1132), add:

```csharp
if (_enemyAnimator != null) _enemyAnimator.OnPhaseChangeComplete -= OnEnemyPhaseChangeComplete;
```

- [ ] **Step 7: Remove the dead `PhaseChangeTarget` wiring from `EnemyBattleAnimator.cs`**

Delete these two lines (no remaining callers after Step 1):
- `private static readonly int PhaseChangeTargetHash = Animator.StringToHash("PhaseChangeTarget");` (~line 32)
- `public void SetPhaseChangeTarget(int target) => _animator.SetInteger(PhaseChangeTargetHash, target);` (~line 60)

- [ ] **Step 8: Verify it compiles**

Return to Unity, recompile. Console shows **no errors** and **no warnings** about a missing
`SetPhaseChangeTarget` caller.

- [ ] **Step 9: Run the full Edit Mode suite**

Test Runner → EditMode → Run All. Expected: **green** (Task 1's tests plus all existing tests;
the C# changes here must not break any).

- [ ] **Step 10: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message:
`fix(DEV-##): sequence frostmelt morph before action and drop dead PhaseChangeTarget`
- `Assets/Scripts/Battle/BattleController.cs`
- `Assets/Scripts/Battle/EnemyBattleAnimator.cs`

---

## Task 4: Build the new Animator Controller

> ❌ **DROPPED (2026-06-17).** Superseded — the user fixed the bug by editing the **existing** controller in place (spec Approach A). No new controller is created. The steps below are retained for historical context only; do **not** execute them.

> **Unity Editor task (user):** All steps in this task are done in the Unity Editor. Create a brand-new controller and author it per the spec.

Reference: spec §5. Form numbering is fixed: `Phase == 0` = Liquid, `Phase == 1` = Ice.

- [ ] **Step 1: Create the controller**

In `Assets/Animations/Enemies/Frostmelt spawn/`, right-click → Create → Animator Controller →
name it `FrostMeltSpawnBattle` (correct spelling). Do **not** reuse the old `FrostmetlSpawnBattle`.

- [ ] **Step 2: Add parameters** (Animator window → Parameters → +)
- `Attack` (Trigger), `Hurt` (Trigger), `Defeat` (Trigger), `PhaseChange` (Trigger)
- `IsRunning` (Bool), `MoveRight` (Bool)
- `Phase` (Int) — leave default value **0**

- [ ] **Step 3: Create the two form sub-state machines**

In the Base Layer, create two sub-state machines: **Liquid** and **Ice**. Inside **each**, create
states: `Idle`, `Move Right`, `Move Left`, `Attack`, `Hurt`, `Death`. Assign the matching art clip
to each (see §5 "Clip → state mapping"; confirm the `…2`-suffixed clips are the Ice set). Set
**Liquid → Idle** as the **Base Layer default state** (right-click → Set As Layer Default State).

- [ ] **Step 4: Create the two morph states (Base Layer, not inside a sub-machine)**
- `Morph To Ice` — motion = the Liquid→Ice phase-change clip
- `Morph To Liquid` — motion = the Ice→Liquid phase-change clip

- [ ] **Step 5: Per-form action transitions** (build identically inside **both** Liquid and Ice; all `Has Exit Time = OFF` unless noted)

| From → To | Conditions |
|---|---|
| Idle → Attack | `Attack` |
| Idle → Hurt | `Hurt` |
| Idle → Move Right | `IsRunning` true, `MoveRight` true |
| Idle → Move Left | `IsRunning` true, `MoveRight` false |
| Move Right → Attack | `Attack` |
| Move Right → Idle | `IsRunning` false |
| Move Left → Idle | `IsRunning` false |
| Attack → Move Left | `IsRunning` true, `MoveRight` false |
| Attack → Idle | `Has Exit Time = ON`, Exit Time `1.0` (fallback) |
| Hurt → Idle | `Has Exit Time = ON`, Exit Time `1.0` |
| Death | no outgoing transitions |

> ⚠️ **Bug-fix invariant:** no state — especially `Idle` — may have a transition with **empty conditions + exit time**. Idle leaves only on a real action/morph/defeat condition. (This is exactly what broke the old Ice Idle.)

- [ ] **Step 6: Morph state exits**

| From | To | Settings |
|---|---|---|
| `Morph To Ice` | `Ice / Idle` | `Has Exit Time = ON`, Exit Time `1.0`, no conditions |
| `Morph To Liquid` | `Liquid / Idle` | `Has Exit Time = ON`, Exit Time `1.0`, no conditions |

- [ ] **Step 7: AnyState transitions — create in THIS order** (priority is top-down). For each: `Has Exit Time = OFF`, `Transition Duration = 0`. On the AnyState node, set **Can Transition To Self = OFF**.

| # | AnyState → Destination | Conditions |
|---|---|---|
| 1 | `Liquid / Death` | `Defeat` + `Phase == 0` |
| 2 | `Ice / Death` | `Defeat` + `Phase == 1` |
| 3 | `Morph To Ice` | `PhaseChange` + `Phase == 1` |
| 4 | `Morph To Liquid` | `PhaseChange` + `Phase == 0` |

- [ ] **Step 8: Sanity check in the Animator window**

Confirm: default state is Liquid/Idle; `Phase` default is 0; no orphan/empty-condition exits on any
Idle; the four AnyState transitions are ordered 1→4 as above.

(Check-in for the controller happens in Task 6, bundled with the prefab reassignment and old-controller deletion.)

---

## Task 5: Add Animation Events to the two morph clips

> ⚠️ **OPTIONAL (2026-06-17).** The Task 3 C# fix sequences morph-then-attack via `_morphDelay` even without these events. Add them only for frame-accurate timing (and keep `_morphDelay` ≥ the morph clip length as the safety net). See the implementation-status note above.

> **Clip directions (confirmed 2026-06-17):** Liquid→Ice = `FrostmeltSpawnPhaseChange2.anim`; Ice→Liquid = `FrostmeltSpawnPhaseChange.anim`. Both clips get the same `AnimEvent_OnPhaseChangeComplete`, so the event goes on both either way — the mapping only matters for labeling.

> **Unity Editor task (user):** Add the morph-complete signal to both morph clips so C# knows when the morph finished.

- [ ] **Step 1: Event on the Liquid→Ice clip**

Open `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange2.anim` in the Animation
window with the battle prefab selected. Move the playhead to the **last frame**, click **Add Event**,
and set Function = `AnimEvent_OnPhaseChangeComplete` (no parameters).

- [ ] **Step 2: Event on the Ice→Liquid clip**

Repeat for `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange.anim` — last frame,
Function = `AnimEvent_OnPhaseChangeComplete`.

> Note: the event binds to `EnemyBattleAnimator.AnimEvent_OnPhaseChangeComplete()` (added in Task 2),
> which lives on the same GameObject as the Animator — the same wiring the existing `AnimEvent_OnHit` uses.

(Check-in for these clips happens in Task 6.)

---

## Task 6: Assign the controller, set defaults, delete the old controller

> ❌ **DROPPED (2026-06-17).** The existing controller is already assigned to the battle prefab with `Phase` defaulting to 0, and there is no separate old controller to delete. Do **not** execute these steps.

> **Unity Editor task (user):** Wire the new controller into the battle prefab and remove the old one.

- [ ] **Step 1: Assign the new controller**

Open `Assets/Prefabs/Enemies/Level 1/FrostMeltSpawnBattle.prefab`. On the Animator component, set
`Controller` = the new `FrostMeltSpawnBattle.controller`.

- [ ] **Step 2: Confirm Phase default**

With the prefab still open, confirm the Animator's `Phase` parameter default is **0** (Liquid).

- [ ] **Step 3: Delete the old controller**

Delete `Assets/Animations/Enemies/Frostmelt spawn/FrostmetlSpawnBattle.controller` (the misspelled one).
Confirm nothing else references it (only the battle prefab did, now repointed).

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message:
`feat(DEV-##): rebuild frostmelt battle animator (chemistry-driven forms)`
- `Assets/Animations/Enemies/Frostmelt spawn/FrostMeltSpawnBattle.controller`
- `Assets/Animations/Enemies/Frostmelt spawn/FrostMeltSpawnBattle.controller.meta`
- `Assets/Animations/Enemies/Frostmelt spawn/FrostmetlSpawnBattle.controller` *(deletion)*
- `Assets/Animations/Enemies/Frostmelt spawn/FrostmetlSpawnBattle.controller.meta` *(deletion)*
- `Assets/Prefabs/Enemies/Level 1/FrostMeltSpawnBattle.prefab`
- `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange.anim`
- `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange2.anim`

---

## Task 7: Play Mode verification (acceptance criteria)

> **Unity Editor task (user):** Enter Play Mode in the Battle scene against the Frost-Melt Spawn and confirm every criterion. (No code changes — this is the final gate.) Can be run without Task 5; the `_morphDelay` fallback sequences the morph either way.

- [ ] **Step 1: Run the full Edit Mode suite once more** — Test Runner → EditMode → Run All → green.

- [ ] **Step 2: Walk the acceptance criteria** (all must pass):
  1. Battle start: liquid form, `Phase = 0`, no Solid badge.
  2. Cast Freeze → Liquid→Ice morph plays, settles in **ice idle** (stable, no walk jitter); Solid (2) + Frozen (1) badges appear.
  3. Enemy turn 1: Frozen → skips action, stays ice.
  4. Player turn 2: hit the iced enemy → **ice Hurt** plays; if it dies → **ice Death** plays.
  5. Enemy turn 2: Solid badge clears → **ice→liquid morph plays fully first**, *then* the enemy attacks in **liquid** form.
  6. Console shows `[Form] … form 1` then `… form 0`; **no "parameter does not exist" warnings**.
  7. Re-freeze works across multiple cycles; normal (un-frozen) liquid attack/hurt/death unaffected.

- [ ] **Step 3: If any criterion fails**, capture the Console output and the failing step, and stop — do not patch blindly. Re-enter `superpowers:systematic-debugging` with that evidence.

---

## Notes

- **Graceful degradation:** if a morph clip's Animation Event is missing/misnamed, `_morphDelay` (2 s) still advances the turn — no hard-lock. Keep `_morphDelay` ≥ the morph clip length.
- **Why Edit Mode only for automated tests:** the AnyState morph routing, the `OnPhaseChangeComplete` event, and `PlayMorphThenExecuteEnemyTurn` are Animator/MonoBehaviour-bound and are verified in Play Mode (Task 7), per the project's MonoBehaviour-separation rule.
