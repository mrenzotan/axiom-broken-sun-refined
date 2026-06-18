# Handoff — Frostmelt Spawn Chemistry-Driven Form Change

Date: 2026-06-17 · Branch (UVCS): `fix-enemy-form-chemistry-conflict`

A fresh session can continue this work using only this doc + the linked files. Read the spec and plan first; they are the source of truth.

---

## Task (verbatim from user)

Original bug report:

> "There's one bug that has been discovered by the dev team during play mode. We have successfully corrected the logic of the form changing of frostmelt spawn. It successfully turns into its ice form when a Freeze spell is casted, and the 'Solid' condition is inflicted for two turns. However, the expected result after the 'Solid' condition ends, the frostmelt spawn should go back to its liquid form, but currently, it's staying in its ice form."

Chosen direction (redesign request):

> "What if we redo the Frostmelt Spawn animations with complete phase / form changing logic? The form changing logic must completely be tied up with the existing Chemistry Spell logic. I.E., frostmelt spawn's form only changes depending on its condition. On battle start, its initial form will be liquid with innate condition of liquid, but when cast by a Freeze spell, it will be inflicted a 'Solid' condition which transforms it to its ice (solid) form for 2 rounds. After the 2 rounds, the Solid condition badge is gone, so it will go back to its liquid form."

Execution mode chosen: inline, C# first — implement Tasks 1–3 (C#) in-session pausing after each for the user to run Test Runner + UVCS check-in; Tasks 4–7 are the user's Unity Editor / Play Mode work.

---

## Root cause (confirmed — do not re-investigate the data layer)

- The chemistry/C# layer is ALREADY CORRECT. `CharacterStats.ProcessConditionTurn()` expires `Solid` after 2 enemy turns and restores `Liquid`; `EnemyData.GetFormIndexForConditions()` maps `Liquid→0`, `Solid→1`; `BattleController.SyncEnemyFormToConditions()` computes the right form both ways. Existing Edit Mode tests pass.
- The bug is in the **Animator controller** (`FrostmetlSpawnBattle.controller`) + **turn sequencing**, and it is asymmetric:
  - Forward (Liquid→Ice) fires on the player's turn from a stable "Liquid Idle" → works.
  - Reverse (Ice→Liquid) fires on the enemy's turn, where (1) "Ice Idle" has a transition with EMPTY conditions + exit time (its Liquid twin requires `IsRunning`) so Ice Idle is unstable, and (2) the reverse morph competes with the enemy's attack animation in the same frame, with movement transitions ordered before the phase-change transition → the morph loses.
  - Dead wiring: `EnemyBattleAnimator.SetPhaseChangeTarget()` writes a `PhaseChangeTarget` parameter that does not exist in the controller (silent no-op).

Fix = decouple form from the action state machine via an AnyState-driven morph + sequence the morph before the enemy's action.

---

## Status of each sub-task

| Step | Status |
| --- | --- |
| Brainstorming (design) | DONE. Spec written, **NOT committed** (user: "We're not committing the spec file yet"). |
| Implementation plan | DONE. Plan written, **NOT committed**. |
| Task 1 — data regression tests | **Code-complete, NOT run, NOT checked in.** Two tests added (see Reference files). User must run Test Runner (EditMode) and check in. |
| Task 2 — `EnemyBattleAnimator` morph-complete signal | NOT started. |
| Task 3 — `BattleController` morph sequencing + drop dead `PhaseChangeTarget` | NOT started. |
| Task 4 — author new Animator Controller (user, Editor) | NOT started. |
| Task 5 — Animation Events on the two morph clips (user, Editor) | NOT started. |
| Task 6 — assign controller to prefab, set `Phase` default 0, delete old controller (user, Editor) | NOT started. |
| Task 7 — Play Mode acceptance verification (user) | NOT started. |

### Decisions locked during brainstorming (do not re-ask)
- Scope: **Frostmelt Spawn (Level 1) only**. Frostmelt Sentinel + HP-phase system out of scope.
- Animator approach: **Approach A — AnyState-driven morph** (gated by `PhaseChange` trigger + `Phase` int).
- Morph timing: **morph plays fully first, then the enemy attacks** (enforced in C# via a morph-complete Animation Event + coroutine wait).
- Ice form animation set: **full set mirroring liquid** (Idle/Move Right/Move Left/Attack/Hurt/Death).
- Controller authoring: user creates a **new** controller asset and reassigns it on the battle prefab (not in-place).

### Task 1 — exact edits already made
- `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` — added test `ProcessConditionTurn_TwoTurnTransformation_RemainsSolidThroughTurnOneThenRestoresLiquid` (after `ProcessConditionTurn_ExpiredMaterialTransformation_RestoresInnateCondition`).
- `Assets/Tests/Editor/Data/EnemyDataTests.cs` — added test `GetFormIndexForConditions_FollowsSolidLifecycle_RevertsToLiquidAfterTwoTurns` (after `GetFormIndexForConditions_NoMatchingForm_ReturnsZero`; reuses the existing `MakeTwoFormEnemy()` helper; asserts form index 0→1→1→0 across the freeze cycle).
- These pass on current code (regression guards encoding intent — Rule 9). They were NOT run yet (no Unity CLI in repo; user runs Test Runner).

---

## Reference files

| Path | What it is | How to use / gotchas |
| --- | --- | --- |
| [docs/superpowers/specs/2026-06-17-frostmelt-form-change-design.md](../specs/2026-06-17-frostmelt-form-change-design.md) | Approved design spec | Source of truth for design. §5 animator, §6 C#, §7 testing, §9 open items. |
| [docs/superpowers/plans/2026-06-17-frostmelt-form-change.md](../plans/2026-06-17-frostmelt-form-change.md) | Step-by-step implementation plan | Execute task-by-task. Has full code blocks + exact edit anchors + UVCS check-in steps with `DEV-##` placeholders. |
| `Assets/Scripts/Battle/BattleController.cs` | Battle MonoBehaviour adapter | `SyncEnemyFormToConditions()` at ~1085–1099 (make it `bool`, drop `SetPhaseChangeTarget` call at 1094). `ProcessEnemyTurnStart()` ~919–954 (branch to morph-then-act; `SyncEnemyFormToConditions()` called at ~930, `ExecuteEnemyTurn()` at ~953). Enemy-animator event wiring: teardown ~335, subscribe ~460, `OnDestroy` ~1132. Serialized timing fields: `_actionDelay` line 69, `_spellFireTimeout` line 251 (add `_morphDelay` near these). `_currentEnemyForm` field line 246 / reset 423. `CheckEnemyPhaseTransition()` early-returns for non-boss (Frostmelt `isBoss:0`) — inert here. |
| `Assets/Scripts/Battle/EnemyBattleAnimator.cs` | MonoBehaviour animator adapter | Add `OnPhaseChangeComplete` event + `AnimEvent_OnPhaseChangeComplete()` (Task 2). Remove `PhaseChangeTargetHash` (line 32) + `SetPhaseChangeTarget()` (line 60) (Task 3). Existing pattern to mirror: `OnHitFrame`/`AnimEvent_OnHit`, `OnAttackSequenceComplete`. Animator + this component are on the same GameObject (proven by working `AnimEvent_OnHit`). |
| `Assets/Scripts/Battle/CharacterStats.cs` | Plain-C# combat state | `ProcessConditionTurn()` material-transform expiry at ~284–300 (CORRECT — restores innate `Liquid`). Do not change. |
| `Assets/Scripts/Data/EnemyData.cs` | Enemy ScriptableObject | `GetFormIndexForConditions()` ~101–126 (CORRECT). `GetInnateConditionsForForm()` returns the SO's list reference — fine because `CharacterStats.Initialize` copies it into `ActiveMaterialConditions`. |
| `Assets/Scripts/Battle/BattleTurnProcessor.cs` | Turn tick wrapper | `ProcessEnemyTurnStart()` just calls `enemyStats.ProcessConditionTurn()`. |
| `Assets/Data/Enemies/ED_FrostMeltspawn.asset` | Frostmelt enemy data | `isBoss:0`; `innateConditions: 01000000` (=Liquid); formDefinitions: form 0 Liquid (`01000000`), form 1 Ice (`02000000`). NOTE: still has orphan serialized fields `changesFormsRandomly` + `spellFormReactions` from a removed system — dead, no script reads them. |
| `Assets/Data/Spells/SD_Freeze.asset` | Freeze spell data | `inflictsCondition: 9` (Frozen) for `1` turn; reaction `reactsWith:1` (Liquid) → `transformsTo:2` (Solid), `transformationDuration:2`. Do not change. |
| `Assets/Animations/Enemies/Frostmelt spawn/FrostmetlSpawnBattle.controller` | OLD battle animator (misspelled "Frostmetl") | Currently referenced by the battle prefab (GUID `3d0125cf8df2c9443938fea09e2d3cf5`). To be REPLACED by a new controller and DELETED in Task 6. |
| `Assets/Prefabs/Enemies/Level 1/FrostMeltSpawnBattle.prefab` | Battle prefab | Has Animator + `EnemyBattleAnimator` (only MonoBehaviour). Reassign the new controller here in Task 6. |
| `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange.anim` | Morph clip — **Ice→Liquid** (confirmed 2026-06-17) | Add Animation Event `AnimEvent_OnPhaseChangeComplete` on last frame (Task 5, now optional). |
| `Assets/Animations/Enemies/Frostmelt spawn/FrostmeltSpawnPhaseChange2.anim` | Morph clip — **Liquid→Ice** (confirmed 2026-06-17) | Same Animation Event (now optional). |
| `Assets/Tests/Editor/Battle/CharacterStatsTests.cs` | Edit Mode tests (BattleTests asmdef → Axiom.Battle + Axiom.Core + Axiom.Data) | Task 1 test already added here. |
| `Assets/Tests/Editor/Data/EnemyDataTests.cs` | Edit Mode tests (DataTests asmdef → Axiom.Battle + Axiom.Data) | Task 1 test already added here. Has `MakeTwoFormEnemy()` helper. |

---

## Project context

- Repo: `/Users/markrenzotan/Unity Projects/Axiom of the Broken Sun Refined/axiom-broken-sun-refined`
- Unity 6.0.4 LTS, URP 2D, Mono backend, New Input System, Cinemachine. 2D platformer + turn-based RPG with voice-cast spells.
- Authoritative docs: `CLAUDE.md` (12-rule template + architecture standards), `docs/GAME_PLAN.md`, `docs/game-mechanics/chemistry-spell-combat-system.md`, `docs/VERSION_CONTROL.md`.
- Architecture rule: MonoBehaviours = lifecycle/event wiring only; all logic in plain C#. Only `GameManager` is a singleton.
- Version control: **UVCS is the source of truth.** Git is a scripts/docs-only mirror (write-only).

---

## Constraints (from user + `~/.claude/.../memory/MEMORY.md`)

- **Commit/check-in format:** `<type>(DEV-###): <desc>` (types: feat/fix/chore/docs/refactor/test). **No `Co-Authored-By` trailer.** See `docs/VERSION_CONTROL.md`.
- **Unity Editor changes:** describe as step-by-step instructions for the user to apply; do NOT use MCP tools (coplay) or CLI to mutate the project or read Unity state. (User rejected a coplay `get_unity_logs` call this session.)
- **Git usage:** do not use git log/history to assess dev state; UVCS is source of truth.
- **Animator AnyState gotcha (from memory + applies directly to Approach A):** uncheck **"Can Transition To Self"** on AnyState bool/trigger transitions to prevent re-triggering every frame. The plan already specifies this for the AnyState morph node.
- **Phase docs are stale:** `GAME_PLAN.md` says "Phase 4 / GameManager not implemented" but the full GameManager + save/restore stack already exists. Grep code; don't trust the phase list.
- **No Unity CLI** test/build pipeline — tests run via Editor Test Runner only; never fabricate CLI test output.
- **Do not commit** the spec, plan, or this handoff to git unless the user explicitly asks. User is keeping these uncommitted for now.

---

## Suggested next steps (ordered)

1. Tell the new session the user has (or hasn't) run Test Runner + checked in Task 1. If not done: user runs Unity → Window → General → Test Runner → EditMode → Run All; expect green; then UVCS check-in `test(DEV-##): add frostmelt 2-turn solid and form-arc regression tests` staging the two test files.
2. Resume execution with skill `executing-unity-game-dev-plans` (+ `superpowers:executing-plans`). Read the plan file first.
3. Implement **Task 2** (additive `EnemyBattleAnimator` change) → compile check → UVCS check-in.
4. Implement **Task 3** (BattleController sequencing + remove dead `PhaseChangeTarget`, two files) → compile + full EditMode run → UVCS check-in.
5. Hand the user **Tasks 4–6** (Editor: build new controller per spec §5, add Animation Events, assign prefab/set Phase default 0/delete old controller) with the plan's checklists. Stay on call for animator wiring + the clip-binding issue below.
6. User runs **Task 7** Play Mode acceptance criteria (plan §7). If any fail, re-enter `superpowers:systematic-debugging` with the Console output.

---

## Open questions for the user (resolve before/while implementing)

1. **`DEV-##` ticket number** — no Jira ticket was provided. Every UVCS check-in message in the plan uses a `DEV-##` placeholder. Get the real number (or decide on a convention) before checking in.
2. **Morph clip directions** — RESOLVED (2026-06-17): Liquid→Ice = `FrostmeltSpawnPhaseChange2.anim`; Ice→Liquid = `FrostmeltSpawnPhaseChange.anim`.
3. **Clip preview / binding issue (unresolved this session)** — user dragged the prefab into the Inspector preview but the sprite was static. Next session should have the user open the clip in the **Animation window** (prefab selected) and scrub: if the Sprite track shows **"Missing!"**/highlighted, the morph/action clips are bound to a hierarchy path that does not match the prefab's SpriteRenderer and will need re-binding — this would be extra work not yet in the plan. If scrubbing shows frames, bindings are fine.
4. **Commit policy for docs** — confirm whether the spec/plan/handoff should ever be committed to the git mirror, or kept UVCS-only / uncommitted.

---

## Files NOT to touch

- `Assets/Scripts/Battle/CharacterStats.cs` `ProcessConditionTurn()` and `Assets/Scripts/Data/EnemyData.cs` `GetFormIndexForConditions()` — the chemistry/form logic is correct. Do NOT "fix" it; the bug is in the animator + sequencing.
- `Assets/Data/Spells/SD_Freeze.asset` and condition durations — no chemistry/data changes (spec non-goal).
- Frostmelt Sentinel (Level 2) assets and the HP-threshold phase system (`CheckEnemyPhaseTransition`) — out of scope.
- The spec and plan markdown — treat as source of truth; only revise via the proper skills if the user asks, not ad hoc.
- Do not hand-edit the old `FrostmetlSpawnBattle.controller` YAML — it is being replaced by a new controller authored in the Editor.
