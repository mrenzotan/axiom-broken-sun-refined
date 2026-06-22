# DEV-94 Level 2 — Combustion Environmental Puzzles

**Date:** 2026-06-19
**Status:** Approved design, ready for implementation plan
**Jira:** DEV-94 — Chemistry based Environmental Puzzles (Level 2 acceptance criteria)
**Scope:** New platformer puzzle objects (burnable obstacle + steam vent) and their
spell dispatch, persistence, and feedback — mirroring the existing Level 1
Freeze/Melt pattern. Plus a reusable gradient sky-fill technique for the volcanic
parallax background.

## Goal

Add the Combustion half of DEV-94 so the player clears fire-themed platforming
obstacles by speaking a combustion spell (`combust` / `ancient burn`) into the mic,
exactly as Level 1 clears Freeze/Melt obstacles. Two new interactable types:

1. **Burnable obstacle** — a wooden crate blocking the path; the player ignites it
   directly (in range + correct spell) and it burns away.
2. **Steam vent** — an always-ignitable volcanic vent; igniting it triggers an
   **explosion** that destroys one or more separate, otherwise-unreachable
   obstacles (a crate or a rubble barrier).

Both teach the Combustion concept without combat, are telegraphed, escalate in
difficulty across sub-levels 2-1 → 2-3, and confirm success with audio/visual
feedback — per the DEV-94 General Requirements.

## Decisions locked (from brainstorming, 2026-06-19)

- **Geyser sprite = steam-vent explosion.** Igniting the vent explodes and clears a
  *separate* obstacle. (Chosen over "thermal updraft".)
- **Vents are always ignitable** — no timing/venting-window gate in this pass.
- **Thermal-updraft AC bullet is DEFERRED** — the volcanic parallax background is
  only ~17 world units tall and the geyser art is a short puff, not a lift column.
  Tracked as future work, not implemented here.
- **Tar-pit obstacle is DEFERRED** — no art. Burnable crates satisfy the
  "burnable obstacles" bullet.
- **No new art is required to ship.** The explosion blast uses procedural particles
  (and optionally the existing `+40FXPack_NYKNCK/Explosion` frames); cleared
  obstacles reuse the crate burn frames or a rubble barrier built from the existing
  `lava-ground` terrain tiles, faded out procedurally.
- **Deliverable = C# scripts + Editor wiring instructions.** Scripts are written
  here; prefab/scene authoring is done by hand in the Unity Editor (same division
  of labor as the Ice Wall work).

## Reference pattern (the convention we mirror)

`MeltableObstacleController` and `FreezablePlatformController` are the templates.
Every new object copies their shape exactly:

- **MonoBehaviour controller** holds a `SpriteRenderer` + `BoxCollider2D`, a
  `List<SpellData>` of triggering spells, a `_puzzleId` for persistence, and a
  `ParticleSystem`/`AudioClip`/`AudioSource` success cue.
- **Animation** is code-driven `Sprite[]` frame-swapping in coroutines at a
  configurable FPS — **no Unity Animator, no AnimationClip** (deliberate project
  convention; see `2026-06-18-ice-wall-animated-sprite-design.md`).
- **Spell matching** is a pure static helper (`MeltableObstacle.CanMelt`) that
  string-matches the spoken spell name against the object's allowed-spell list.
- **Proximity** is a child trigger collider + a `*ProximityForwarder` that calls
  `SetPlayerInRange(bool)`.
- **Dispatch** flows through `PlatformerVoiceSpellController` (polls the Vosk result
  queue) → `PlatformerSpellWorldCaster.TryCast(...)`.
- **Persistence** uses `GameManager.MarkPuzzleSolved(puzzleId)` /
  `IsPuzzleSolved(puzzleId)`; `PlatformerWorldRestoreController` re-applies solved
  state on scene load via `ApplySolvedImmediate()`.
- **Conditions are battle-scoped only** (chemistry doc invariant) — the platformer
  does NOT touch `ChemicalCondition`/`SpellEffectResolver`. Puzzle objects only
  string-match `spell.spellName`, exactly like Melt does today.

## Asset facts

**Sprite sheet** `Assets/Art/Sprites/Platformer/burnable and geyser-Sheet.png`
(guid `c8e664447ed144d078945121605e96bd`), spriteMode = Multiple, 16 PPU, each cell
128×128 px = **8×8 world units**:

- `geyser-0 … geyser-5` — **6 frames**, split into two clips: `geyser-0,1,2` =
  looping **idle puff** (continuous), `geyser-3,4,5` = **one-shot eruption** played
  once when the vent ignites (the geyser visibly erupts), after which the idle loop
  resumes.
- `burnable-0 … burnable-5` — **6 frames**, a one-shot burn-down: frame 0 = intact
  crate → frame 5 = charred remains.

**Combustion spells** (platformer matches on `spellName`, lower-case):
- `SD_Combust.asset` (guid `26e26c17650f746e69129fb1b9f59964`) — `spellName: combust`
- `SD_AncientBurn.asset` (guid `c92edc10ea99747c0aa89bc9b7403161`) — `spellName: ancient burn`

**Rubble-barrier source art** (no dedicated boulder prop exists — built from terrain):
- `Assets/Art/AssetPacks/Volcanic Area Files/Assets/layers/lava-ground.png`
  (guid `0537f1aabc3b24635ae9bfb229e3e87f`) — dark volcanic rock, sliced into
  `lava-ground_0..9`; in-game tiles at
  `Assets/Art/Tilemaps/Level2_VolcanicArea/Tiles/lava-ground_0..9.asset`.
- `lava-tileset.png` (guid `e18e6063e11284d2aa3d98132e4b5b29`) — molten lava surface.
- `lava-ball1/2/3` (`Volcanic Area Files/Assets/Sprites/lavaball/`) — good ejected
  debris.

**Optional blast art** (sprite alternative to particles; `.gif`, must be converted
to a sprite sheet on import): `Assets/Art/AssetPacks/+40FXPack_NYKNCK/Explosion/EX011..EX081`,
plus `Fire/F0xx` and `Smoke/SM0xx`.

**Sprite material** used by the platformer sprites: guid
`a97c105638bdf8b4a8650670310a4cd3` (same as water platform / parallax layers).

**Background prefab** `P_VolcanicAreaParallax.prefab`
(guid `6ed3e7f02b1ff4439b28ab7d7abdaeac`) — 4 tiled layers at `m_Size {x:200, y:17}`;
the horizon strip tiles horizontally only.

## Architecture — new components

### 1. `IExplosionDestructible.cs` (interface, `Axiom.Platformer`)

```csharp
public interface IExplosionDestructible
{
    void Detonate();   // clear/destroy self as a consequence of a nearby explosion
}
```

Implemented by `BurnableObstacleController` and `ExplodableBarrierController`. Two
real implementers — not premature abstraction. The steam vent talks only to this
interface, so any future destructible plugs in with no vent change.

### 2. `BurnableObstacle.cs` (static pure C#, `Axiom.Platformer`)

`public static bool CanIgnite(string spellId, IReadOnlyList<string> igniteSpellIds)`
— exact twin of `MeltableObstacle.CanMelt`. Unit-testable, no Unity types.

### 3. `BurnableObstacleController.cs` (MonoBehaviour, `Axiom.Platformer`)

Near-clone of `MeltableObstacleController`. Fields:
- `_spriteRenderer` (`SpriteRenderer`), `_solidCollider` (`BoxCollider2D`)
- `_burnFrames` (`Sprite[]`) — the 6 `burnable-0..5` frames
- `_burnFps` (`float`, `[Min(0.1f)]`, default 10)
- `_igniteSpells` (`List<SpellData>`) — e.g. `[SD_Combust, SD_AncientBurn]`
- `_puzzleId` (`string`), success cue (`_successVfx`, `_successSfx`, `_audioSource`)
- `_onIgnited` (`UnityEvent`) — fired by the success cue when the crate ignites (direct cast or vent blast); wire to a `CinemachineImpulseSource.GenerateImpulse()` for camera shake (keeps the asmdef Cinemachine-free)
- flash tint = warm orange (vs. Melt's ice-blue)

API (same names/shape as Melt so dispatch reuse is trivial):
- `SetPlayerInRange(bool)`, `CanIgniteWith(string spellId)`, `bool TryIgnite(string spellId)`
- `ApplySolvedImmediate()`, `bool IsBurned`, `string PuzzleId`

Behavior:
- **Idle:** static `burnable-0`.
- **`TryIgnite`** (in range + `CanIgnite`): set burned, `MarkPuzzleSolved`, play
  success cue, run burn coroutine: orange flash → frames 0→5 at `_burnFps` →
  disable `_solidCollider` at the collapse frame (~frame 3) so the path opens as it
  visibly burns.
- **End state:** leave the final charred frame (`burnable-5`) **visible** as a
  flat, non-colliding scorch mark (renderer stays on; collider off). This differs
  from Melt (which hides the renderer) and reads as "burned, walkable."
- **`ApplySolvedImmediate`:** set sprite = `burnable-5`, collider off, renderer on.
- Implements `IExplosionDestructible.Detonate()` → runs the same ignite path
  (used when a vent's explosion sets this crate ablaze), guarded so it is idempotent.

### 4. `BurnableObstacleProximityForwarder.cs` (MonoBehaviour, `Axiom.Platformer`)

Verbatim twin of `MeltableObstacleProximityForwarder` retargeted to
`BurnableObstacleController` (trigger collider → `SetPlayerInRange`).

### 5. `ExplodableBarrierController.cs` (MonoBehaviour, `Axiom.Platformer`)

A rubble/boulder barrier that is **vent-only** (the player cannot ignite it
directly — no spell list, no proximity forwarder). Built as a **parent container +
tiled child block SpriteRenderers**. Fields:
- `_blockRenderers` (`SpriteRenderer[]`) — the visual child blocks; empty = auto-collect every child `SpriteRenderer` on `Awake`
- `_solidCollider` (`BoxCollider2D`) — one collider on the parent spanning the whole barrier
- `_debrisVfx` (`ParticleSystem`), `_destroySfx` (`AudioClip`), `_audioSource`
- `_onDetonated` (`UnityEvent`) — fired by `Detonate()` when a vent blast clears the barrier; wire to a `CinemachineImpulseSource.GenerateImpulse()` for camera shake (keeps the asmdef Cinemachine-free)
- `_fadeDuration` (`float`, default 0.4)
- `_puzzleId` (`string`)

Behavior:
- Implements `IExplosionDestructible.Detonate()`: play debris VFX + SFX, disable
  `_solidCollider` immediately, fade-and-shrink **all child blocks together** over
  `_fadeDuration`, then disable every block renderer. `MarkPuzzleSolved(_puzzleId)`.
- `ApplySolvedImmediate()`: collider off, all block renderers off (no animation).
- `bool IsCleared`, `string PuzzleId`.

The barrier is composed of the AI-generated `explodable-block.png` sprite (dark basalt
with glowing lava cracks) tiled into the barrier/boulder shape — one child
`SpriteRenderer` per block, all under one parent controller. Set the block's import
PPU to 32 to match the 32-px lava-ground tiles (1 block = 1×1 world unit). Flip/rotate
blocks (or use crack variants) to break visible repetition.

### 6. `SteamVent.cs` (static pure C#, `Axiom.Platformer`)

Reuses ignite-matching logic. To avoid duplication, `BurnableObstacle.CanIgnite`
is the shared helper for both the crate and the vent (rename to a neutral
`CombustionSpellMatch.Matches(spellId, ids)` if a shared home reads better — decide
in the plan; do not duplicate the loop).

### 7. `SteamVentController.cs` (MonoBehaviour, `Axiom.Platformer`)

Fields:
- `_spriteRenderer` (`SpriteRenderer`)
- `_ventFrames` (`Sprite[]`) — looping idle-puff frames (`geyser-0,1,2`)
- `_ventFps` (`float`, `[Min(0.1f)]`, default 6) — looping idle puff
- `_eruptionFrames` (`Sprite[]`) — one-shot eruption frames (`geyser-3,4,5`), played
  once on ignite then the idle loop resumes; empty = skip the sprite eruption
- `_eruptionFps` (`float`, `[Min(0.1f)]`, default 10) — eruption playback rate
- `_igniteSpells` (`List<SpellData>`)
- `_linkedTargets` (`List<MonoBehaviour>` constrained to `IExplosionDestructible`,
  or a typed wrapper) — explicit obstacles this vent clears
- `_blastRadius` (`float`, default 0 = disabled) — optional `Physics2D.OverlapCircle`
  auto-collect of `IExplosionDestructible` in range at ignite time
- `_blastMask` (`LayerMask`) for the overlap query
- `_blastVfx` (`ParticleSystem`), `_blastSfx` (`AudioClip`), `_audioSource`
- `_onIgnited` (`UnityEvent`) — fired on ignite; wire to `CinemachineImpulseSource.GenerateImpulse()` for camera shake (keeps the asmdef Cinemachine-free)

API: `SetPlayerInRange(bool)`, `CanIgniteWith(string)`, `bool TryIgnite(string)`.
**Re-ignitable / stateless** — no `IsSpent`, no `PuzzleId`, no `ApplySolvedImmediate`.

Behavior:
- **Idle:** loops `_ventFrames` (`geyser-0,1,2` steam puff) continuously, like the water loop.
- **`TryIgnite`** (in range + match — **re-ignitable**, no spent gate):
  play `_blastVfx` + `_blastSfx`, fire `_onIgnited` for camera shake,
  play the one-shot `_eruptionFrames` (`geyser-3,4,5`) once then resume the idle loop,
  then `Detonate()` every target — both `_linkedTargets` and (if `_blastRadius > 0`)
  the overlap-collected destructibles, de-duplicated. Each cast spends MP and re-runs
  the cue; clearing already-gone targets is a harmless no-op (`Detonate()` is idempotent).
- **No persisted state.** The vent is a permanent fixture — never marked solved, never
  restored. The obstacles it clears persist independently via their own `_puzzleId`
  (see Persistence).

Camera shake is **optional and Cinemachine-native** (the project already uses
Cinemachine) and follows the **same decoupled pattern on all three puzzle prefabs**
(vent `_onIgnited`, crate `_onIgnited`, barrier `_onDetonated`): assign a
`CinemachineImpulseSource` on the prefab root, wire the controller's UnityEvent →
`CinemachineImpulseSource.GenerateImpulse()`, and add **one** shared
`CinemachineImpulseListener` on the vcam (one listener serves every source). If a
UnityEvent is left unwired, that object produces no shake — no custom shake code is
added to `Axiom.Platformer`. **Stacking caveat:** impulses are additive — a vent that
detonates crates/barriers fires its own shake *plus* each cleared object's, so keep
per-source magnitudes modest (≈0.2–0.3).

### 8. `SteamVentProximityForwarder.cs` (MonoBehaviour, `Axiom.Platformer`)

Twin forwarder retargeted to `SteamVentController`.

## Modified scripts (3 edits, surgical, public-API-additive)

### `PlatformerSpellWorldCaster.cs`

Extend `TryCast` to also accept
`IReadOnlyList<BurnableObstacleController>` and
`IReadOnlyList<SteamVentController>`. Add them to both passes exactly alongside the
existing meltable/freezable loops:
1. **probe pass** — set `hasWorldTarget` if any burnable `CanIgniteWith` or any vent
   `CanIgniteWith`.
2. spend MP once (`playerState.TrySpendMp`).
3. **apply pass** — `TryIgnite` on burnables and vents; OR into `handled`.

Keep the existing meltable/freezable handling untouched.

### `PlatformerVoiceSpellController.cs`

Add `_burnableObstacles` / `_steamVents` serialized arrays + `_sceneBurnables` /
`_sceneSteamVents` scene-resolve lists + `ResolveBurnables()` / `ResolveSteamVents()`
(mirror the meltable resolver: explicit array if set, else `FindObjectsByType`).
Pass them into the extended `PlatformerSpellWorldCaster.TryCast(...)` call.

### `PlatformerWorldRestoreController.cs`

In `ReapplySolvedPuzzles()`, also iterate `BurnableObstacleController` and
`ExplodableBarrierController`; for each with a non-blank `PuzzleId` that
`IsPuzzleSolved`, call `ApplySolvedImmediate()`. **Steam vents are NOT iterated** —
they are re-ignitable and stateless. (Cleared barriers restore themselves by their own
`PuzzleId` — they do not depend on the vent.)

## Data flow

```
Mic → Vosk → PlatformerVoiceSpellController.Update() (polls ConcurrentQueue<string>)
   → SpellResultMatcher.Match → SpellData
   → PlatformerSpellWorldCaster.TryCast(spell, meltables, freezables,
                                         burnables, vents, playerState)
       probe → spend MP once →
       BurnableObstacleController.TryIgnite()  → orange flash → burn 0→5 → collider off
       SteamVentController.TryIgnite()         → blast VFX + impulse shake
                                                → foreach IExplosionDestructible.Detonate()
                                                     crate.Detonate() → burns
                                                     barrier.Detonate() → debris + fade-out
   → each solved object persists via its own puzzleId
   → after a Battle round-trip, PlatformerWorldRestoreController re-applies each
```

## Puzzle design — difficulty escalation (2-1 → 2-2; 2-3 waived)

Each escalates simple → complex per the DEV-94 General Requirements.

- **2-1 — teach direct ignite.** A lone wooden crate blocks a narrow ledge. Walk up,
  say *combust*, crate burns away and the path opens. Optionally a second crate
  shortly after to reinforce. (Burnable obstacle only.)
- **2-2 — teach the vent explosion.** A rubble barrier blocks the path, placed
  *out of direct cast range* (across a gap / above a ledge), with a steam vent within
  range at its base. Igniting the vent clears the barrier. Teaches "ignite the vent,
  not the wall." (Steam vent → one linked barrier.)
- **2-3 — WAIVED (2026-06-21).** Level 2-3 is the boss level (flat ground leading to
  the boss arena, no platforming), so no combustion puzzle is authored there. The
  combine beat is dropped; DEV-94 ACs are met by 2-1 + 2-2. The vent multi-target
  path (`_linkedTargets` with 2+ entries, or `_blastRadius`) stays code-complete for
  a future platforming level.

Scene authoring (placing prefabs, building the rubble barriers, assigning
`_linkedTargets`, setting per-instance `_puzzleId`s) is Editor work, guided by the
prefab steps below.

## Prefab Editor steps (done by hand; not applied programmatically)

### `P_BurnableCrate.prefab` (new)
1. Root: `SpriteRenderer` (assign `burnable-0`, sprite material
   `a97c105638bdf8b4a8650670310a4cd3`, volcanic sorting order) + `BoxCollider2D`
   sized to the crate + `BurnableObstacleController` + an `AudioSource`
   + optional `CinemachineImpulseSource` (for camera shake).
2. Root layer = the platformer "solid/ground" collision layer used by other
   obstacles (confirm against `P_IceWall`).
3. Child `ProximityTrigger`: trigger `BoxCollider2D` (a bit larger than the crate)
   + `BurnableObstacleProximityForwarder` (wire `_controller` = root).
4. Child `SuccessVFX_Burn`: `ParticleSystem` (fire/smoke burst; can use `F0xx`/`SM0xx`
   frames or procedural).
5. Wire controller: `_spriteRenderer`, `_solidCollider`, `_burnFrames` = the 6
   `burnable-0..5` sprites, `_burnFps` = 10, `_igniteSpells` = `[SD_Combust,
   SD_AncientBurn]`, success cue refs, and a unique-per-instance `_puzzleId`.
   For shake: add a persistent listener to `_onIgnited` → the root
   `CinemachineImpulseSource.GenerateImpulse()`.

### `P_ExplodableBarrier.prefab` (new) — parent container + tiled child blocks
1. Root (empty container at the barrier center): `ExplodableBarrierController` + one
   `BoxCollider2D` spanning the whole barrier + `AudioSource` + optional
   `CinemachineImpulseSource` (camera shake). **No SpriteRenderer on the root.**
2. Child **blocks**: one `SpriteRenderer` per block using `explodable-block.png` (32 PPU
   → 1 block = 1×1 unit), placed on the tile grid into the barrier/boulder shape;
   flip/rotate per block to break repetition.
3. Child `DebrisVFX`: one `ParticleSystem` at the barrier center.
4. No proximity trigger, no spell list (vent-only).
5. Wire: `_blockRenderers` = the block children (or leave empty to auto-collect on
   Awake), `_solidCollider`, `_debrisVfx`, `_destroySfx`, `_fadeDuration`, unique
   `_puzzleId`. For shake: add a persistent listener to `_onDetonated` → the root
   `CinemachineImpulseSource.GenerateImpulse()`. All blocks fade out together on detonate.

### `P_SteamVent.prefab` (new)
1. Root: `SpriteRenderer` (assign `geyser-0`) + `SteamVentController` + `AudioSource`
   + optional `CinemachineImpulseSource`. (The vent itself has no solid collider —
   it sits on the ground; the player walks past it.)
2. Child `ProximityTrigger`: trigger `BoxCollider2D` + `SteamVentProximityForwarder`.
3. Child `BlastVFX`: `ParticleSystem`.
4. Wire: `_ventFrames` = `geyser-0,1,2` (idle loop), `_eruptionFrames` = `geyser-3,4,5`
   (one-shot eruption), `_ventFps` = 6, `_eruptionFps` = 10, `_igniteSpells`,
   `_blastVfx`, `_blastSfx`, `_blastRadius` (0 unless a radius puzzle), `_blastMask`.
   Assign `_linkedTargets` = the barrier/crate instances this vent clears (per-scene).
   For shake, wire `_onIgnited` → root `CinemachineImpulseSource.GenerateImpulse()`.
   **No `_puzzleId`** — the vent is stateless / re-ignitable.
5. vcam: add a `CinemachineImpulseListener` once if using shake.

### Audio bus
All new `AudioSource`s are routed through the SFX bus in `Start` via
`GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource)` — copy
the Melt controller's `Start`.

## Background — gradient sky-fill (reusable technique)

Lets Level 2 scenes gain a little vertical headroom above the 17-unit horizon strip
without distorting it (transform-Y scaling stretches the single strip; raising
tiled `m_Size.y` repeats the mountains — both wrong).

1. **Texture:** a 1×256 px PNG, vertical gradient: top = upper-sky color, bottom =
   the exact color of the *top edge* of the existing volcanic strip (eyedrop from a
   parallax layer). (A 1×2 px top/bottom image also works via bilinear filtering;
   256-tall just avoids banding.)
2. **Import:** Sprite (2D and UI), Filter **Bilinear**, Wrap **Clamp**, Compression
   **None**, Mesh Type Full Rect.
3. **Scene object `SkyFill`:** `SpriteRenderer`, Draw Mode Simple, same Sorting Layer
   as the BG, **Order in Layer below `Layer_Far` (e.g. −50)**. Scale X to level width,
   scale Y to the headroom; position so its **bottom edge overlaps the top of the
   horizon strip** (matched colors → invisible seam).
4. **Parallax:** parent under `Layer_Far` or give it a `ParallaxController` with
   `parallaxFactor ≈ 0` (static far sky).
5. **Camera:** set the Cinemachine confiner / vertical dead-zone so the camera never
   scrolls above `SkyFill`'s top.

Optional shader alternative: an Unlit Shader Graph with two `Color` props `Lerp`'d by
UV.y on a Quad — only worth it for many per-scene sky tints.

## Feedback (DEV-94: "environmental audio/visual feedback confirms reactions")

Reuse the Melt cue rig: a color flash + `ParticleSystem` + a one-shot SFX through the
SFX bus. The vent adds a heavier blast burst + optional camera shake. All VFX/SFX are
serialized references — swap in `EX0xx` explosion frames or new art later with no code
change.

## Persistence

Each solvable object owns a unique `_puzzleId` and calls `MarkPuzzleSolved` when
solved. `PlatformerWorldRestoreController.ReapplySolvedPuzzles()` re-applies all of
them on scene load. Crates cleared *by a vent* persist via their **own** `_puzzleId`
(the vent does not need to re-fire on restore). Leaving `_puzzleId` blank opts an
object out of persistence (e.g. pure-decoration vents).

## Acceptance-criteria mapping (DEV-94 Level 2)

| AC bullet | Covered by |
|---|---|
| Steam vents that cause explosions to destroy obstacles | `SteamVentController` → `IExplosionDestructible.Detonate()` |
| Burnable obstacles (wooden crates) cleared with fire | `BurnableObstacleController` |
| Burnable obstacles (tar pits) | **Deferred** — no art |
| Thermal updrafts that lift the player | **Deferred** — BG height + art constraints |
| Teaches without combat / telegraphed / escalates / A-V feedback | Puzzle design 2-1→2-2 + feedback rig |

## Success criteria

In Play Mode, in a Level 2 scene:
1. Crate at rest shows `burnable-0`; casting *combust*/*ancient burn* in range →
   orange flash → frames 0→5 → collider opens mid-burn → charred frame remains
   walkable → success VFX + SFX.
2. Wrong spell, or out of range → nothing happens; MP is not spent.
3. Steam vent loops its puff; igniting it in range → blast VFX (+ shake if wired) →
   every linked/in-radius obstacle clears (crate burns, barrier debris+fades).
4. MP is spent **once** per successful cast even when a vent clears multiple targets.
5. All solved states persist across a Battle round-trip (each restored by its
   `puzzleId`).
6. The gradient `SkyFill` extends apparent height with no visible seam and no camera
   over-scroll past its top.

## Out of scope / deferred

- Thermal-updraft traversal mechanic (separate future ticket).
- Tar-pit obstacle (needs art).
- Converting `+40FXPack_NYKNCK` GIFs to sprite sheets (optional polish; procedural
  VFX ships first).
- Any change to battle chemistry (`ChemicalCondition`, `SpellEffectResolver`) — the
  platformer only string-matches spell names.
- Full authoring of the 2-1/2-2/2-3 scene layouts beyond the sample beats above
  (Editor work, can be its own follow-up).

## Open risks

- **Barrier readability:** rubble built from `lava-ground` can read as permanent
  terrain. Mitigate with a distinct silhouette / crack-glow, or commission one static
  cracked-boulder sprite (the single worthwhile art ask).
- **`combust` unlock gating:** `SD_Combust` is `requiredLevel: 3`; `ancient burn` is
  `requiredLevel: 0`. Confirm the player's unlocked-spell vocabulary in Level 2
  includes at least one combustion spell so the vent/crate are castable. (Content
  check, not code.)
- **Vent collider:** the vent has no solid collider by design; confirm placement so
  the player can stand within the proximity trigger to cast.
