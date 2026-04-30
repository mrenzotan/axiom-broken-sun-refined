# Volcanic Area Parallax Background — Design

**Date:** 2026-04-30
**Status:** Approved
**Jira:** DEV-90 (Sub-task of DEV-51, Phase 6)
**Branch:** `feat-level-2-1` (child of `main/dev/`)
**Scope:** Asset / prefab work. Zero new C# scripts.

## Goal

Add a volcanic-themed parallax background usable across Level_2-1, Level_2-2, and Level_2-3, built from the recently installed `Volcanic Area Files` asset pack. Deliver a reusable prefab `P_VolcanicAreaParallax` that mirrors the existing Snow Mountain / Dark Cave biome prefab pattern.

Source assets (under [Assets/Art/AssetPacks/Volcanic Area Files/Assets/layers/](Assets/Art/AssetPacks/Volcanic%20Area%20Files/Assets/layers/)):

| Asset | Pixels (PPU 16) | Role |
|---|---|---|
| `back.png` | 48 × 272 | Tiled sky strip (deepest layer) |
| `far-mountains.png` | 192 × 272 | Distant mountain silhouettes |
| `volcanos.png` | 496 × 272 | Closer volcanos / mid-range silhouettes |
| `vapor-a.png` | 282 × 213 | Decorative smoke plume A (static) |
| `vapor-b.png` | 173 × 225 | Decorative smoke plume B (static) |

## Context

### Existing parallax system

- [ParallaxController.cs](Assets/Scripts/Platformer/ParallaxController.cs) — MonoBehaviour driving each layer's X position from `Camera.main`.
- [ParallaxBackground.cs](Assets/Scripts/Platformer/ParallaxBackground.cs) — plain-C# offset math (`world.x = startX + cameraX * (1 - factor)`), already covered by [ParallaxBackgroundTests.cs](Assets/Tests/Editor/SceneSetup/ParallaxBackgroundTests.cs).
- [P_SnowMountainParallax.prefab](Assets/Prefabs/Platformer/P_SnowMountainParallax.prefab) and [P_DarkCaveParallax.prefab](Assets/Prefabs/Platformer/P_DarkCaveParallax.prefab) — existing biome prefabs to follow as templates.

### Parallax factor convention (verified empirically)

The codebase uses **low factor = far, high factor = near**:

- `factor = 0` → layer moves with the camera in world space → appears stationary on screen → infinitely far (sky).
- `factor = 1` → layer fixed in world space → scrolls past at full camera speed → ground / foreground.

This is confirmed by the `Parallax` instance in [Level_1-1.unity](Assets/Scenes/Level_1-1.unity) (lines 400-415), which overrides the prefab defaults to `Layer_Far = 0.1`, `Layer_Mid = 0.5`, `Layer_Near = 0.85`.

⚠️ The [ParallaxBackground.cs:3-4](Assets/Scripts/Platformer/ParallaxBackground.cs#L3-L4) docstring is inverted from the actual math (it claims `0 = pinned to world; 1 = infinitely distant`, which is backwards). Fixing the docstring is a separate follow-up; the math itself is correct and unchanged by this work.

⚠️ The Snow / Cave prefab assets carry **incorrect default factors** (`Layer_Near = 0.3`, `Layer_Far = 0.9`) that scenes patch over with per-instance overrides. The new volcanic prefab will be authored with **correct defaults** so dropping it into a scene needs no override.

### Source asset gotcha

[back.png.meta](Assets/Art/AssetPacks/Volcanic%20Area%20Files/Assets/layers/back.png.meta) currently has `spriteMode: 1` (Single) but the embedded `spriteSheet` declares a stray `FarBackground_0` 64×64 sub-rect inherited from the asset pack. This must be cleared during import or the SpriteRenderer will pick the wrong sub-region.

## Design

### Architecture

No new C# scripts. The deliverable is:

1. A new prefab `Assets/Prefabs/Platformer/P_VolcanicAreaParallax.prefab` with 4 child layers.
2. Texture-import fixups on the 5 source PNGs.
3. Prefab instantiated in `Level_2-1.unity` (and later in 2-2 / 2-3 when those scenes exist).

The existing `ParallaxController` and `ParallaxBackground` components are reused unchanged.

### Prefab structure

**Root:** `P_VolcanicAreaParallax` GameObject — Transform only. Local scale `(2, 2, 1)` matching Snow/Cave convention.

**Children (4 layer GameObjects):**

| Child | Sprite | Draw mode | `m_Size` (units) | Parallax factor | Sorting order |
|---|---|---|---|---|---|
| `Layer_Far` | `back.png` | Tiled | 200 × 17 | **0.05** | -40 |
| `Layer_MidFar` | `far-mountains.png` | Tiled | 200 × 17 | **0.25** | -30 |
| `Layer_MidNear` | `volcanos.png` | Tiled | 200 × 17 | **0.5** | -20 |
| `Layer_Near` | *(empty container; see below)* | n/a | n/a | **0.8** | n/a (children at -10) |

- Each layer GameObject carries a `ParallaxController` component with the listed `parallaxFactor`.
- `Layer_Far` / `Layer_MidFar` / `Layer_MidNear` each have a `SpriteRenderer` in **Tiled** draw mode with the listed `m_Size`. Unity natively repeats the sprite across that size — no script needed.
- All layers share the project's existing background sorting layer (`m_SortingLayerID: 1001`).
- Material: `Sprite-Lit-Default` (the same material referenced by Snow/Cave: GUID `a97c105638bdf8b4a8650670310a4cd3`).

**Why `m_Size.x = 200` (× 2 root scale = 400 world units of coverage):**

- For the slowest layer (factor 0.05): layer travels `cameraX × 0.95`, so 400 units of layer covers a level up to ~420 units long.
- For the fastest backdrop layer (factor 0.5): layer travels `cameraX × 0.5`, so 400 units covers a level up to ~800 units long.
- Sufficient for typical Level-2 scene widths. If any scene exceeds this, widen the affected layer's `m_Size.x` per-instance.

**Why these specific factors (0.05 / 0.25 / 0.5 / 0.8):** extends the validated Level_1-1 spread (0.1 / 0.5 / 0.85) across 4 layers — sky pushed slightly farther than `far-mountains`, vapor slightly less foreground than Level_1-1's near. Designers can override per-scene if a level wants a different feel.

### Vapor placement (`Layer_Near` contents)

`Layer_Near` is an **empty container**: it has a Transform + `ParallaxController` only, no `SpriteRenderer`. Two child GameObjects sit underneath:

```
Layer_Near
├── ParallaxController (parallaxFactor: 0.8)
├── VaporPlume_A
│   └── SpriteRenderer: vapor-a, draw mode = Simple, sortingOrder = -10
└── VaporPlume_B
    └── SpriteRenderer: vapor-b, draw mode = Simple, sortingOrder = -10
```

Both vapor children use **Simple** draw mode (single sprite at native size, not tiled). They are static decorations — their parallax motion comes entirely from the parent `Layer_Near`'s transform updates.

**Starting local positions** (tune in Scene view against the artist demo):

- `VaporPlume_A`: approximately `(-20, +6, 0)` — left of center, plume base near mountain tops.
- `VaporPlume_B`: approximately `(+20, +7, 0)` — right of center, plume base near mountain tops.

Final values are determined by eye, not numeric spec. Acceptance criterion is "matches the artist's demo composition."

### Texture import fixes

| File | Required changes |
|---|---|
| `back.png` | Re-import: `Sprite Mode = Single`, clear the stray `FarBackground_0` sub-rect from the spritesheet. Verify Pivot Center, PPU 16. |
| `far-mountains.png` | Verify only — no change expected. `Sprite Mode = Single`, PPU 16, Filter Point, Compression None, `alphaIsTransparency = 1`. |
| `volcanos.png` | Verify only — same checks as `far-mountains.png`. |
| `vapor-a.png` | Verify only — same checks. |
| `vapor-b.png` | Verify only — same checks. |

## Per-scene integration

| Scene | Action |
|---|---|
| `Level_2-1.unity` | Drop `P_VolcanicAreaParallax` into the Hierarchy. Parent under whatever world-root grouping the scene uses (mirroring Level_1-1's `Parallax` placement). Position root X/Y by eye to align horizon with the level's playable area. |
| `Level_2-2.unity` | Drop in when the scene is created. Out of scope until then. |
| `Level_2-3.unity` | Drop in when the scene is created. Out of scope until then. |

No `parallaxFactor` overrides are expected — the prefab carries correct defaults. If a designer wants a different feel for a specific level, override per-instance (same pattern Level_1-1 uses).

## Acceptance criteria

- Walking left/right in Level_2-1 Play mode shows each layer scrolling at visibly different rates: sky barely moves, vapor moves close to player speed.
- `back` / `far-mountains` / `volcanos` tile horizontally without visible seams. If a seam appears in `far-mountains` or `volcanos`, the fallback is to widen the affected layer's `m_Size.x` to one un-tiled instance and accept that it doesn't loop.
- Vapor plumes are positioned to match the artist's demo composition (one left of center, one right, plume bases sitting behind the mountain silhouettes).
- `ParallaxBackgroundTests.cs` continues to pass (math unchanged).
- `back.png` import shows a single sprite with no stray sub-rect.

## Out of scope

- Animator / animation on vapor (vapor is static per artist intent).
- Updating Snow or Cave prefab default factors (separate ticket).
- Fixing the inverted docstring in [ParallaxBackground.cs:3-4](Assets/Scripts/Platformer/ParallaxBackground.cs#L3-L4) (separate ticket).
- Automated tests for the new prefab's structure (manual editor verification is sufficient given existing math coverage).
- Camera-attached or infinite-scrolling parallax variants.
- Volcanic-themed FX beyond the parallax band (lava drips, ember particles, screen tint, etc.).

## Follow-up tickets to file (not this work)

- Fix `ParallaxBackground` docstring to reflect actual math.
- Update Snow Mountain / Dark Cave prefab default factors so scenes don't need per-instance overrides.
