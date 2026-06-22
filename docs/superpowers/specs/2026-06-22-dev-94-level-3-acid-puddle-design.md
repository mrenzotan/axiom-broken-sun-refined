# DEV-94 Level 3 — Acid Puddle Hazard

**Date:** 2026-06-22
**Status:** Approved design, ready for implementation plan
**Jira:** DEV-94 — Chemistry based Environmental Puzzles (Level 3 acceptance criteria)
**Branch:** `feat-DEV-94-level-3-acid-base-puzzles` (UVCS child of `dev`)
**Scope:** One new platformer hazard object — the **acid puddle** — and its spell
dispatch, persistence, and feedback. It is a hybrid of the existing DoT-hazard
pattern (`HazardTrigger`) and the spell-removable-obstacle pattern
(`BurnableObstacleController`), reusing the shared, stateless pieces of each.

## Goal

Add the acid-pool portion of DEV-94's Level 3 acceptance criteria so the player
crosses an acidic pool by speaking `neutralize` into the mic. The puddle:

1. **Animates** — loops its 6-frame sprite sheet forever from scene start, with
   each instance desynced (different loop timing), the way the Level 2 animated
   lava tile desyncs.
2. **Hurts** — damages the player with an **escalating** damage-over-time tick the
   longer they stand in it (not a fixed DoT). Stepping out resets the escalation.
3. **Dissolves** — the `neutralize` spell removes it (alpha fade-out + particle
   VFX, no removal animation frames), letting the player cross safely. The cleared
   state persists across a Battle round-trip.

## Scope boundary (the rest of the AC is out of scope)

DEV-94's Level 3 criteria list four mechanics: acidic pools, alkaline residue
deposits, pH-sensitive platforms, and corroded metal barriers. **Only acidic pools
are implemented** — the art team produced only the acid puddle sheet. The other
three are DEFERRED (no art); this spec does not design them.

## Decisions locked (from brainstorming, 2026-06-22)

- **Escalating DoT resets on exit.** Each tick deals more than the last while the
  player stands in the acid; stepping out resets the escalation to the mild base.
  Re-entering ramps up again from scratch. (Chosen over per-puddle persistent
  escalation.)
- **Neutralize from the edge.** A neutralize-eligibility trigger is slightly larger
  than the damage area, so the player can dissolve the acid from the edge *without*
  taking a damage tick first — fits "neutralize to cross safely." (Chosen over a
  single collider that forces standing in the acid.)
- **Cleared state persists** via `puzzleId` + `GameManager.MarkPuzzleSolved`, like
  every other spell-cleared obstacle. (Chosen over respawn-on-reload.)
- **New `AcidPuddleController`, not an extension of `HazardTrigger`.** `HazardTrigger`
  is shared by spikes and pits; adding acid-only escalation/animation/neutralize/fade
  to it would bloat a tested component and risk regressions. The acid controller
  instead *reuses* the shared stateless pieces — `HazardDamageResolver` (damage math)
  and `PlayerHurtFeedback` (tint/anim) — and mirrors the `BurnableObstacle`
  dispatch/persistence pattern.
- **Deliverable = C# scripts + Editor wiring instructions.** Scripts are written by
  Claude; prefab/scene authoring is done by hand in the Unity Editor (same division
  of labor as the Ice Wall and Level 2 combustion work).

## Reference pattern (the convention we mirror)

`HazardTrigger` (DoT) and `BurnableObstacleController` (spell removal + persistence)
are the templates. The acid puddle copies their shape:

- **MonoBehaviour controller** holds a `SpriteRenderer`, a damage trigger
  `Collider2D`, a `Sprite[]` frame array, a `List<SpellData>` of triggering spells,
  a `_puzzleId` for persistence, and a `ParticleSystem`/`AudioClip`/`AudioSource`
  success cue.
- **Animation** is code-driven `Sprite[]` frame-swapping in a coroutine at a
  configurable speed — **no Unity Animator, no AnimationClip** (deliberate project
  convention; see `2026-06-18-ice-wall-animated-sprite-design.md`).
- **Spell matching** is a pure static helper (`AcidPuddle.CanNeutralize`, mirroring
  `BurnableObstacle.CanIgnite`) that string-matches the spoken spell name against the
  object's allowed-spell list. **Lowercase only** (Vosk enforces lowercase; matcher
  is case-insensitive but `spellName` must be lowercase).
- **Proximity** is a child trigger collider + an `AcidPuddleProximityForwarder` that
  calls `SetPlayerInRange(bool)`, mirroring `BurnableObstacleProximityForwarder`.
- **DoT** uses the start-coroutine-on-enter / stop-on-exit pattern from
  `HazardTrigger` — **not** `OnTriggerStay2D`, which Unity stops firing once the
  player's `Rigidbody2D` sleeps at rest.
- **Damage math** reuses `HazardDamageResolver.Resolve(currentHp, maxHp,
  PercentMaxHpDamage, percent)`. The controller only computes *which* percent to pass.
- **Player feedback** reuses `PlayerHurtFeedback`:
  `BeginPainOverlap`/`FlashOnTick`/`EndPainOverlap`.
- **Dispatch** flows through `PlatformerVoiceSpellController` (polls the Vosk result
  queue) → `PlatformerSpellWorldCaster.TryCast(...)`.
- **Persistence** uses `GameManager.MarkPuzzleSolved(puzzleId)` / `IsPuzzleSolved`;
  `PlatformerWorldRestoreController` re-applies solved state on scene load via
  `ApplySolvedImmediate()`.
- **Conditions are battle-scoped only** (chemistry doc invariant) — the platformer
  does NOT touch `ChemicalCondition`/`SpellEffectResolver`. The puddle only
  string-matches `spell.spellName`, exactly like Melt/Ignite do today.

## Asset facts

- `Assets/Art/Sprites/Platformer/acid puddle.png` — already imported and sliced
  (`spriteMode: Multiple`) into **6 frames** (`acid puddle-0..-4`, `-6`;
  `spritePixelsToUnits: 16`). Frames are assigned to the controller's `Sprite[]` in
  the Inspector in display order during Editor wiring.
- `Assets/Data/Spells/SD_Neutralize.asset` — already exists. `spellName: neutralize`,
  `concept: AcidBase`, `mpCost: 6`, unlock `requiredLevel: 3`. No new spell asset
  needed.
- The puddle is a **floor pool the player walks over** — it has **no solid
  collider**; only triggers. It never blocks movement. "Crossing safely" means: once
  neutralized, the damage trigger is disabled so no DoT applies while walking across.

## Components

### New — `AcidPuddleController.cs` (`Assets/Scripts/Platformer/`)

One MonoBehaviour, three concerns kept in separate method/coroutine blocks.

**Serialized fields**

| Field | Default | Purpose |
| --- | --- | --- |
| `_spriteRenderer` | — | Acid sprite renderer |
| `_damageCollider` | — | The trigger sized to the visible acid |
| `_acidFrames` | 6 frames | Looping animation frames |
| `_minSpeed` / `_maxSpeed` | `5` / `7` | Per-instance random frames/sec (mirrors `RT_Level2_LavaTiles`) |
| `_baseTickPercent` | `3` | First (mildest) tick, % of MaxHP |
| `_growthFactor` | `1.6` | Per-tick geometric multiplier |
| `_maxTickPercent` | `25` | Cap, % of MaxHP per tick |
| `_tickIntervalSeconds` | `0.5` | Seconds between ticks |
| `_neutralizeSpells` | `[SD_Neutralize]` | Spells that dissolve the puddle |
| `_puzzleId` | unique per instance | Persistence key; blank opts out |
| `_fadeDuration` | `0.6` | Alpha 1→0 dissolve time |
| `_successVfx` / `_successSfx` / `_audioSource` | — | Neutralize cue |

**1. Looping animation.** In `Start()`: pick `speed = Random.Range(_minSpeed,
_maxSpeed)` and a random start frame index, then run a coroutine that swaps
`_acidFrames[(i++) % len]` every `1f / speed` forever until neutralized. This
desyncs every instance, like the lava tile's `[5,7]` random speed (we add a random
start frame too, which the tile lacks, for stronger desync). Route `_audioSource`
through the SFX bus in `Start()` like `BurnableObstacleController` does.

**2. Escalating DoT.**
- `OnTriggerEnter2D(Player)`: resolve `PlayerHurtFeedback`, set `_tickIndex = 0`,
  apply the first tick immediately (base percent), `BeginPainOverlap()`,
  `PlayHurtAnimation()`, start the tick coroutine.
- Tick coroutine: every `_tickIntervalSeconds`, `_tickIndex++`, compute
  `percent = Mathf.Clamp(Mathf.RoundToInt(_baseTickPercent * Mathf.Pow(_growthFactor,
  _tickIndex)), 0, _maxTickPercent)`, apply via `HazardDamageResolver.Resolve(...)` →
  `PlayerState.SetCurrentHp(...)`, then `FlashOnTick()`.
- `OnTriggerExit2D(Player)`: stop the coroutine, `EndPainOverlap()`, **reset
  `_tickIndex = 0`**, clear cached feedback.
- `OnDisable()`: stop ticking + `EndPainOverlap()` (don't leave the player tinted or
  a coroutine running on a dead/unloaded object — same guard `HazardTrigger` has).
- Guard every HP write on `GameManager.Instance != null`.
- Death is **not** handled here — `PlayerDeathHandler` observes
  `PlayerState.CurrentHp`, exactly as with `HazardTrigger`.

**3. Neutralize + removal.**
- `SetPlayerInRange(bool)` — called by the forwarder.
- `CanNeutralizeWith(spellId)` → `!_isNeutralized && _isPlayerInRange &&
  AcidPuddle.CanNeutralize(spellId, BuildNeutralizeSpellIds())`.
- `TryNeutralize(spellId)` → if `CanNeutralizeWith`, run `Neutralize()`, return true.
- `Neutralize()`: latch `_isNeutralized = true`; if `_puzzleId` set and GameManager
  present, `MarkPuzzleSolved(_puzzleId)`; stop the animation loop; **stop any active
  DoT and `EndPainOverlap()`** (handles neutralizing while standing in it); play
  `_successVfx` + `_successSfx`; disable `_damageCollider` immediately (so no further
  ticks); start the fade coroutine.
- Fade coroutine: lerp `_spriteRenderer.color.a` 1→0 over `_fadeDuration`, then
  disable the renderer. (Particle VFX plays over the fade.)
- `PuzzleId` property + `ApplySolvedImmediate()`: set `_isNeutralized`, disable
  `_damageCollider`, hide the renderer (alpha 0 / disabled) with **no VFX, no fade,
  no animation** — for scene-load restore of an already-solved puddle.
- `BuildNeutralizeSpellIds()`: project `_neutralizeSpells` → lowercase `spellName`
  list, skipping nulls (mirrors `BuildIgniteSpellIds`).

### New — `AcidPuddle.cs` (`Assets/Scripts/Platformer/`)

Pure static helper, mirroring `BurnableObstacle`:

```csharp
public static bool CanNeutralize(string spellId, IReadOnlyList<string> neutralizeSpellIds)
```

String-matches `spellId` against the allowed list (case-insensitive). Kept separate
from the MonoBehaviour so the matching rule is unit-testable without a scene.

### New — `AcidPuddleProximityForwarder.cs` (`Assets/Scripts/Platformer/`)

Near-verbatim copy of `BurnableObstacleProximityForwarder`: `[RequireComponent(
typeof(Collider2D))]`, `Reset()` forces `isTrigger = true` and auto-finds the
controller in parent, `OnTriggerEnter2D/Exit2D` check `CompareTag("Player")` and call
`_controller.SetPlayerInRange(true/false)`. Its collider is **larger** than the
damage collider so the player can stand at the edge (outside the acid) and still
neutralize.

## Modified scripts (dispatch + persistence wiring)

### `PlatformerSpellWorldCaster.TryCast(...)` (`Assets/Scripts/Voice/`)

Add an `IReadOnlyList<AcidPuddleController> acidPuddles` parameter. In **Phase 1**
(has-world-target check) add an early-exit loop calling `CanNeutralizeWith`. In
**Phase 2** (cast-to-all) add a loop calling `TryNeutralize`, OR-ing into `handled`.
MP (`neutralize` = 6) spends through the existing single `TrySpendMp` gate, which
already runs only after a world target is confirmed.

### `PlatformerVoiceSpellController` (`Assets/Scripts/Voice/`)

Add `[SerializeField] private AcidPuddleController[] _acidPuddles;`, a
`ResolveAcidPuddles()` that returns the Inspector array or falls back to
`FindObjectsByType<AcidPuddleController>()` (mirroring the existing resolvers), and
pass the result into the extended `TryCast(...)`.

### `PlatformerWorldRestoreController` (`Assets/Scripts/Platformer/`)

Restore solved puddles on scene load, mirroring how it restores burnable obstacles:
discover `AcidPuddleController`s, and for each whose `PuzzleId` is non-blank and
`GameManager.IsPuzzleSolved(PuzzleId)`, call `ApplySolvedImmediate()`.

## Prefab / scene authoring (Editor handoff — instructions only)

`P_AcidPuddle.prefab`:
- Root: `SpriteRenderer` (frame 0 of the acid sheet) + `AcidPuddleController`.
- Damage trigger `Collider2D` (isTrigger) sized to the visible acid.
- Child GameObject with a **larger** trigger `Collider2D` + `AcidPuddleProximityForwarder`.
- `ParticleSystem` (neutralize dissolve VFX — green/acid splatter) + `AudioSource`.
- Wire `AcidPuddleController` fields: 6 frames in order, `_neutralizeSpells =
  [SD_Neutralize]`, references to renderer/damage collider/VFX/SFX/audio source.

Scene work (Level 3):
- Place instances over the acid-pool sections; give each a **unique `_puzzleId`**.
- Ensure the player object is tagged `Player` (already true for other hazards).
- Either assign the puddles to `PlatformerVoiceSpellController._acidPuddles` or rely
  on the `FindObjectsByType` scene-scan fallback.

## Testing

EditMode unit tests (no scene needed), mirroring existing hazard/obstacle tests:

- `AcidPuddle.CanNeutralize`: matches `neutralize`, rejects unrelated/empty/null
  spell ids, is case-insensitive — encodes *why* (only the AcidBase neutralize spell
  clears acid; mismatches must not).
- **Escalating damage curve** (extract the percent computation into a pure, testable
  method, e.g. `AcidPuddleDamage.PercentForTick(tickIndex, base, growth, cap)`):
  tick 0 = base, each successive tick strictly larger until the cap, then clamped at
  cap, never exceeding `_maxTickPercent`. Encodes *why*: damage must escalate (not be
  flat) and must be bounded (not unbounded).
- Reuse `HazardDamageResolver` tests as-is (unchanged).

PlayMode / manual verification (in Editor, by user):
- Per-instance animation desync visible across multiple puddles.
- Standing in acid ramps damage; stepping out and re-entering restarts mild.
- `neutralize` from the edge dissolves the puddle without taking a tick; crossing
  afterward is damage-free.
- Solve → enter Battle → return: puddle stays dissolved (persistence).

## Files

**New**
- `Assets/Scripts/Platformer/AcidPuddleController.cs`
- `Assets/Scripts/Platformer/AcidPuddle.cs`
- `Assets/Scripts/Platformer/AcidPuddleProximityForwarder.cs`
- `Assets/Scripts/Platformer/AcidPuddleDamage.cs` (pure percent-per-tick helper, for testability)
- EditMode tests for `AcidPuddle` + `AcidPuddleDamage`

**Modified**
- `Assets/Scripts/Voice/PlatformerSpellWorldCaster.cs`
- `Assets/Scripts/Voice/PlatformerVoiceSpellController.cs`
- `Assets/Scripts/Platformer/PlatformerWorldRestoreController.cs`

**Editor (handoff)**
- `Assets/Prefabs/Platformer/P_AcidPuddle.prefab` (new)
- Level 3 scene placement + `PlatformerVoiceSpellController` wiring
</content>
