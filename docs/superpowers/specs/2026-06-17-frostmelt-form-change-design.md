# Frostmelt Spawn — Chemistry-Driven Form Change (Design)

- **Date:** 2026-06-17
- **Status:** Approved design — ready for implementation plan
- **Scope:** Frost-Melt Spawn (Level 1) battle enemy only
- **Related:** branch `fix-enemy-form-chemistry-conflict`; chemistry spell/combat system (`docs/game-mechanics/chemistry-spell-combat-system.md`)

## 1. Problem

In the Battle scene the Frost-Melt Spawn correctly morphs **Liquid → Ice** when hit by
**Freeze** (which applies the `Solid` material condition for 2 turns). When `Solid` expires,
it should morph **back to Liquid** — but it stays stuck in its ice form.

### Root cause (confirmed by investigation)

The chemistry/C# layer is **already correct**:

- `CharacterStats.ProcessConditionTurn()` decrements the `Solid` transformation, removes it
  after 2 enemy turns, and restores `Liquid` from `InnateConditions`.
- `EnemyData.GetFormIndexForConditions()` maps `Liquid → 0`, `Solid → 1`.
- `BattleController.SyncEnemyFormToConditions()` reads those conditions and writes the result
  to the animator. Both directions are computed correctly. (Existing Edit Mode tests pass.)

The failure is entirely in the **Animator controller** (`FrostmetlSpawnBattle.controller`),
and it is asymmetric:

- **Forward (Liquid → Ice)** fires on the **player's** turn from a stable "Liquid Idle" → works.
- **Reverse (Ice → Liquid)** fires on the **enemy's** turn, where:
  1. "Ice Idle" has a transition with **empty conditions + exit time** (its Liquid twin
     correctly requires `IsRunning`), so Ice Idle is unstable and keeps bouncing to "Move Left".
  2. The reverse morph competes with the enemy's attack animation in the **same frame**
     (`IsRunning`/`MoveRight` are set as the attack starts), and the movement transitions are
     ordered **before** the phase-change transition — so the morph loses.
- Dead wiring: `EnemyBattleAnimator.SetPhaseChangeTarget()` writes a `PhaseChangeTarget`
  parameter that does not exist in the controller (silent no-op).

## 2. Goals / Non-goals

**Goals**

- The Frost-Melt Spawn's visual form is a pure function of its chemistry state, reliable in
  **both** directions, independent of turn timing or which animation is currently playing.
- On the enemy's turn, when the form changes, the morph animation plays **fully first**, then
  the enemy takes its action in the new form.
- Remove the dead `PhaseChangeTarget` wiring.

**Non-goals**

- The Frostmelt Sentinel (Level 2) and the HP-threshold phase system are out of scope.
- No change to the chemistry rules, Freeze data, or condition durations.

## 3. Chosen approach — AnyState-driven morph (Approach A)

Make the morph an **AnyState** transition gated by the `PhaseChange` trigger + the `Phase`
int. When C# requests a form change, the animator interrupts whatever it is doing and routes
into the correct morph clip → settles into the new form's Idle. The form is fully decoupled
from the action state machine, so it cannot be blocked by walk/attack/hurt transitions.
The morph clip ends with an Animation Event that signals C#, enforcing
"morph-first-then-attack".

Rejected alternatives: **B** (int-gated morph from each Idle, done correctly) — works but still
assumes the enemy is in Idle; **C** (minimal patch) — leaves the fragile structure in place.

## 4. Architecture & data flow

**Single source of truth:** `CharacterStats.ActiveMaterialConditions`.
**Mapping:** `EnemyData.GetFormIndexForConditions()` → `0 = Liquid`, `1 = Ice`.
**Carrier:** animator `Phase` int + `PhaseChange` trigger. C# never touches sprites/states.

```
Player casts Freeze (player turn)
  → resolver: Liquid consumed, Solid applied (2 turns)
  → SyncEnemyFormToConditions(): form 0→1, Phase=1 + PhaseChange
  → animator: AnyState → Morph To Ice clip → Ice/Idle

Enemy turn 1: Frozen ticks out, Solid 2→1  → form still 1, no morph

Enemy turn 2: Solid 1→0, Liquid restored
  → SyncEnemyFormToConditions(): form 1→0, Phase=0 + PhaseChange
  → animator: AnyState → Morph To Liquid clip → Animation Event "morph complete"
  → C# waits for that signal, THEN runs the enemy attack in liquid form
```

## 5. Animator controller spec

A **new** controller is authored (replacing `FrostmetlSpawnBattle.controller`) and assigned to
`FrostMeltSpawnBattle.prefab`'s Animator `Controller` slot. The old controller is deleted.

### Parameters

| Name | Type | Notes |
|---|---|---|
| `Attack`, `Hurt`, `Defeat`, `PhaseChange` | Trigger | one-shot pulses |
| `IsRunning`, `MoveRight` | Bool | run clips during attack approach |
| `Phase` | Int | **0 = Liquid, 1 = Ice** — default **0** |

(No `PhaseChangeTarget` — it was dead.)

### Layout (one Base Layer)

```
Base Layer
├─ [Default] Liquid (sub-state machine)
│   ├─ Idle · Move Right · Move Left · Attack · Hurt · Death
├─ Ice (sub-state machine)
│   ├─ Idle · Move Right · Move Left · Attack · Hurt · Death
├─ Morph To Ice     (clip: Liquid→Ice morph)
└─ Morph To Liquid  (clip: Ice→Liquid morph)
```

### AnyState transitions — create in this order (priority is top-down)

All: `Has Exit Time = OFF`, `Transition Duration = 0`. Set AnyState **Can Transition To Self = OFF**.

| # | Destination | Conditions |
|---|---|---|
| 1 | `Liquid/Death` | `Defeat` + `Phase == 0` |
| 2 | `Ice/Death` | `Defeat` + `Phase == 1` |
| 3 | `Morph To Ice` | `PhaseChange` + `Phase == 1` |
| 4 | `Morph To Liquid` | `PhaseChange` + `Phase == 0` |

### Morph state exits (play clip, then settle into form)

| From | To | Conditions |
|---|---|---|
| `Morph To Ice` | `Ice/Idle` | `Has Exit Time = ON`, Exit Time `1.0`, no other conditions |
| `Morph To Liquid` | `Liquid/Idle` | `Has Exit Time = ON`, Exit Time `1.0`, no other conditions |

### Per-form action transitions — identical in BOTH sub-machines (`Has Exit Time = OFF` unless noted)

| From → To | Conditions |
|---|---|
| Idle → Attack | `Attack` |
| Idle → Hurt | `Hurt` |
| Idle → Move Right | `IsRunning == true` + `MoveRight == true` |
| Idle → Move Left | `IsRunning == true` + `MoveRight == false` |
| Move Right → Attack | `Attack` |
| Move Right → Idle | `IsRunning == false` |
| Move Left → Idle | `IsRunning == false` |
| Attack → Move Left | `IsRunning == true` + `MoveRight == false` |
| Attack → Idle | `Has Exit Time = ON`, Exit Time `1.0` (fallback) |
| Hurt → Idle | `Has Exit Time = ON`, Exit Time `1.0` |
| Death | terminal — no outgoing transitions |

**Invariant that fixes the bug:** no state — especially Idle — may have an *unconditional*
exit (empty conditions + exit time). Idle leaves only on a real action/morph/defeat condition.

### Animation Events (added on the two morph clips, last frame)

- `FrostmeltSpawnPhaseChange.anim` (Liquid→Ice) → calls `AnimEvent_OnPhaseChangeComplete`
- `FrostmeltSpawnPhaseChange2.anim` (Ice→Liquid) → calls `AnimEvent_OnPhaseChangeComplete`

### Clip → state mapping

Assign art per role. Working assumption (to confirm in the Editor when assigning): the
`…2`-suffixed clips are the **Ice** form (Idle2 / Hurt2 / Attack2 / Move2 / Death2); the
non-suffixed are **Liquid**. Confirm which of `PhaseChange` / `PhaseChange2` is Liquid→Ice vs
Ice→Liquid and assign to `Morph To Ice` / `Morph To Liquid` accordingly.

## 6. C# changes

Three surgical edits. Form-detection logic is unchanged.

### `EnemyBattleAnimator.cs`

- **Remove** `PhaseChangeTargetHash` and `SetPhaseChangeTarget()`.
- **Add** a morph-complete signal (mirrors existing `OnAttackSequenceComplete` / `AnimEvent_OnHit`):
  ```csharp
  public event System.Action OnPhaseChangeComplete;
  public void AnimEvent_OnPhaseChangeComplete() => OnPhaseChangeComplete?.Invoke();
  ```
- Keep `SetPhase(int)` and `TriggerFormChange()`.

### `BattleController.SyncEnemyFormToConditions()` — return whether a morph started

```csharp
private bool SyncEnemyFormToConditions()
{
    if (_enemyData == null || _enemyAnimator == null) return false;
    if (_enemyData.formDefinitions == null || _enemyData.formDefinitions.Count == 0) return false;
    int targetForm = _enemyData.GetFormIndexForConditions(_enemyStats.ActiveMaterialConditions);
    if (targetForm == _currentEnemyForm) return false;
    _currentEnemyForm = targetForm;
    _enemyAnimator.SetPhase(targetForm);
    _enemyAnimator.TriggerFormChange();   // pulses PhaseChange
    Debug.Log($"[Form] Enemy visual synced to form {_currentEnemyForm} (driven by chemistry)");
    return true;
}
```

The forward/player-turn caller (`FireSpellVisuals`) ignores the return — forward already works;
the AnyState morph plays during the existing player-action delay.

### `BattleController.ProcessEnemyTurnStart()` — wait for the morph before acting

```csharp
bool morphStarted = SyncEnemyFormToConditions();
// ... existing IsDefeated early-return ...
// ... existing ActionSkipped (Frozen) early-return ...
if (morphStarted) StartCoroutine(PlayMorphThenExecuteEnemyTurn());
else              ExecuteEnemyTurn();
```

New field/handler/coroutine (follows the existing timed-fallback pattern, e.g. `_spellFireTimeout`):

```csharp
[SerializeField] private float _morphDelay = 2f;   // fallback if the Animation Event never fires
private bool _enemyMorphComplete;
private void OnEnemyPhaseChangeComplete() => _enemyMorphComplete = true;

private System.Collections.IEnumerator PlayMorphThenExecuteEnemyTurn()
{
    _enemyMorphComplete = false;
    float elapsed = 0f;
    while (!_enemyMorphComplete && elapsed < _morphDelay) { elapsed += Time.deltaTime; yield return null; }
    _enemyMorphComplete = false;
    ExecuteEnemyTurn();
}
```

Wire `+= OnEnemyPhaseChangeComplete` where the enemy animator is hooked up, and
`-= OnEnemyPhaseChangeComplete` in `OnDestroy`.

**Defeat-during-morph** is handled for free: AnyState `Defeat` transitions are listed above the
morph transitions, so a killed enemy plays Death, not a morph.

## 7. Testing

**Edit Mode (extend existing files):**

- `CharacterStatsTests.cs` — add a **2-turn** transformation lifecycle test (existing one only
  covers duration 1): apply `Solid(2)` → after tick 1 still `Solid` → after tick 2 `Solid` gone
  and `Liquid` restored.
- **Chemistry→form arc** test (in `EnemyDataTests` or alongside the above, using a two-form
  enemy mirroring `ED_FrostMeltspawn`): feed `stats.ActiveMaterialConditions` into
  `GetFormIndexForConditions` at each tick and assert the form index goes **1 → 1 → 0**. This is
  the precise regression guard for the reported bug at the data level.

**Not unit-testable** (MonoBehaviour/Animator-bound): AnyState morph routing,
`OnPhaseChangeComplete`, and the morph-then-attack coroutine — verified in Play Mode.

**Play Mode acceptance criteria (all must pass):**

1. Battle start: liquid form, `Phase = 0`, no Solid badge.
2. Cast Freeze → Liquid→Ice morph plays, settles in **ice idle** (stable, no walk jitter);
   Solid badge (2) + Frozen badge (1) appear.
3. Enemy turn 1: Frozen → skips action, stays ice.
4. Player turn 2: hit the iced enemy → **ice Hurt** plays; if it dies → **ice Death** plays.
5. Enemy turn 2: Solid badge clears → **ice→liquid morph plays fully first**, then the enemy
   attacks in **liquid** form.
6. Console shows `[Form] … form 1` then `… form 0`; **no "parameter does not exist" warnings**.
7. Re-freeze works again across multiple cycles; normal liquid attack/hurt/death unaffected.

## 8. Rollout order

1. C# edits first (compiles on its own; `AnimEvent_OnPhaseChangeComplete` exists for clips to bind to).
2. Author the new animator controller (§5) → assign to `FrostMeltSpawnBattle.prefab` Animator;
   set `Phase` default = 0.
3. Add the Animation Event to both morph clips.
4. Delete the old `FrostmetlSpawnBattle.controller`.
5. Run Edit Mode tests (Test Runner) → all green.
6. Play Mode verification against §7 criteria.
7. UVCS check-in, semantic commits per `docs/VERSION_CONTROL.md`.

**Graceful degradation:** if a morph clip's Animation Event is missing, the `_morphDelay` (2 s)
fallback still advances the turn — no hard-lock. Set `_morphDelay` ≥ the morph clip length, or
rely on the event.

## 9. Open items (confirm during implementation)

- Confirm the `…2` = Ice clip-naming assumption when assigning clips.
- Confirm which phase-change clip is Liquid→Ice vs Ice→Liquid.
- The new controller is authored as a new asset and reassigned to the battle prefab (user's choice).
