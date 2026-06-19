# Ice Wall: Tilemap → Animated SpriteRenderer

**Date:** 2026-06-18
**Status:** Approved design, ready for implementation plan
**Scope:** Rendering swap only — no gameplay/behavior change.

## Goal

Replace the Tilemap-based visual of `P_IceWall.prefab` with the animated
`ice wall-Sheet.png` sprite, driven the same way `P_WaterPlatform_Long.prefab`
is driven: a `SpriteRenderer` whose frames are swapped **in code** by the
owning controller. The ice wall keeps its existing meltable-obstacle gameplay
(permanent melt on the correct spell, solved-state persistence via `_puzzleId`).

## Reference pattern

`FreezablePlatformController` renders via a `SpriteRenderer` + `BoxCollider2D`
and animates by swapping `Sprite[]` frames in coroutines at a configurable FPS
(`WaterLoopCoroutine`, `PlayFreezeFrames`). No Unity Animator, no AnimationClip.
This is the deliberate project convention and the pattern we mirror here.

## Asset facts

- `ice wall-Sheet.png` (guid `768f8c08b0bc245bba7e7d03723bc312`) is already
  sliced into **6 sprites** `ice wall-Sheet_0..5`, each 64×96 px at 16 PPU
  (= 4×6 world units per frame). No re-slicing needed.
- The 6 frames are a **melt progression**: frame 0 = full intact wall →
  frame 5 = fully melted/gone.

## Current vs. target structure

**Current `P_IceWall`:**
- Root `P_IceWall` (layer 0): Transform, `Grid`, `MeltableObstacleController`, `AudioSource`
  - child `Tilemap` (layer 7): Transform, `Tilemap`, `TilemapRenderer`, `TilemapCollider2D`
  - child `ProximityTrigger`: trigger `BoxCollider2D` + `MeltableObstacleProximityForwarder`
  - child `SuccessVFX_Melt`: `ParticleSystem`

**Target `P_IceWall`** (renderer + collider on the root, mirroring the water platform):
- Root `P_IceWall` (layer 7): Transform, `SpriteRenderer`, `BoxCollider2D`, `MeltableObstacleController`, `AudioSource`
  - child `ProximityTrigger`: unchanged
  - child `SuccessVFX_Melt`: unchanged
- `Grid` component and the `Tilemap` child are removed.

## Chosen approach

**Code-driven frame swap.** Rewrite `MeltableObstacleController` to swap the 6
melt frames on a `SpriteRenderer`, exactly mirroring
`FreezablePlatformController.PlayFreezeFrames`. No Animator, no new assets.

Rejected alternatives:
- **Unity Animator + AnimationClip** — diverges from the water-platform pattern,
  adds an Animator/clip/controller asset trio, and reintroduces Any-State /
  self-transition footguns. Rejected.
- **Animated Tiles on the existing Tilemap** — keeps the tilemap, which is
  exactly what we are removing. Rejected.

## Script changes — `MeltableObstacleController.cs`

The **public API is unchanged** — every external consumer
(`PlatformerSpellWorldCaster`, `PlatformerWorldRestoreController`,
`MeltableObstacleDebugCaster`, `MeltableObstacleProximityForwarder`) uses only
`TryMelt`, `CanMeltWith`, `SetPlayerInRange`, `ApplySolvedImmediate`,
`IsMelted`, `PuzzleId`. Only the private rendering backend changes.

Remove:
- `using UnityEngine.Tilemaps;`
- field `_tilemap` (`Tilemap`)
- field `_fadeDuration`
- `FadeAndSinkCoroutine`, `EaseOutQuad`, the `SinkScaleY` constant

Retype:
- `_solidCollider`: `TilemapCollider2D` → `BoxCollider2D` (matches the water platform)

Add:
- `_spriteRenderer` (`SpriteRenderer`)
- `_meltFrames` (`Sprite[]`) — the 6 melt frames
- `_meltFps` (`float`, default 10, `[Min(0.1f)]`)

Behavior:
- **Idle:** static frame 0 (no idle loop — the sheet has no shimmer frames).
  The renderer simply shows `ice wall-Sheet_0` until melted.
- **`MeltCoroutine`** (on successful `TryMelt`):
  1. Ice-blue **flash** — retarget the existing `FlashCoroutine` to tint
     `_spriteRenderer.color`, then reset to white.
  2. **Play melt frames 0→5** at `_meltFps` (new `PlayMeltFrames` coroutine,
     same shape as `PlayFreezeFrames`).
  3. Disable `_solidCollider` at the **midpoint frame** (wall becomes passable
     as it visibly melts).
  4. After the last frame, set `_spriteRenderer.enabled = false` (wall fully
     hidden — matches the old "deactivate" end state).
- **`ApplySolvedImmediate`** (restore path, no animation/cue):
  `_solidCollider.enabled = false; _spriteRenderer.enabled = false;`
- `Start` (audio-bus routing) and `PlaySuccessCue` (VFX + SFX) unchanged.
  Success VFX/SFX still fire at melt start.

## Prefab Editor steps — `P_IceWall.prefab`

Done by hand in the Unity Editor (not applied programmatically):

1. Remove the `Grid` component from the root.
2. Delete the child `Tilemap` GameObject (removes Tilemap + TilemapRenderer +
   TilemapCollider2D).
3. On the root: add a `SpriteRenderer` — assign `ice wall-Sheet_0`, the same
   sprite material the water platform uses (guid `a97c105638bdf8b4a8650670310a4cd3`),
   sorting order ~7.
4. On the root: add a `BoxCollider2D` sized to the solid wall.
5. Set the root (the object holding the `BoxCollider2D`) to **layer 7** — the
   layer the old `TilemapCollider2D` used, so player collision is preserved.
6. Keep `ProximityTrigger` and `SuccessVFX_Melt` children unchanged.
7. Rewire `MeltableObstacleController`:
   - `_spriteRenderer` → root SpriteRenderer
   - `_solidCollider` → root BoxCollider2D
   - `_meltFrames` → the 6 sliced `ice wall-Sheet_0..5` sprites
   - `_meltFps` → 10

## Caveats

- **Idle is static frame 0** — the only intentional deviation from the water
  platform (which has a looping idle). A resting shimmer would need new art.
- **Sizing:** old wall ≈ 2×5 units, new sprite = 4×6 units. Reposition/scale
  each placed instance to fit its level.
- **Multi-scene:** the prefab is instanced in 5 scenes — `Level_1-1`,
  `Level_1-1 Redesign`, `Level_1-2`, `Level_1-2 Redesign`, `Level_1-3 Redesign`.
  After editing the prefab, open each scene and confirm the instance renders and
  collides correctly with no orphaned overrides from the removed Tilemap/Grid.

## Success criteria

In Play Mode, in a level containing an ice wall:
1. At rest the wall shows frame 0 (static).
2. Casting the correct melt spell while in range: ice-blue flash → frames 0→5
   melt → collider disables mid-melt so the player can pass → renderer hides at
   the end → success VFX burst + SFX fire.
3. Casting the wrong spell, or casting out of range, does nothing.
4. A melt persists across a Battle round-trip: on return the wall is already
   hidden and passable (puzzleId restore via `ApplySolvedImmediate`).
5. All 5 scenes' ice-wall instances behave identically.

## Out of scope

- Any change to the freeze/water-platform mechanic.
- New idle-shimmer art for the ice wall.
- Changes to spell data, melt-detection rules, or the persistence system.
