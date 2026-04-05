# DEV-8: Tilemap World Design Spec

**Jira:** DEV-8 — Tilemap World — test level using Unity Tilemap and Rule Tiles
**Phase:** 1 — Platformer Foundation
**Status:** Approved for implementation

---

## Goal

Build a playable, linear scrolling test level using Unity 2D Tilemap and Rule Tiles that exercises all player movement from DEV-7 (walk, jump, fall, coyote time, jump buffering).

---

## Scene Structure

Delete the existing placeholder `Ground` GameObject. Replace with:

```
Grid (GameObject + Grid component, cell size 1×1)
└── Ground (GameObject)
    ├── Tilemap
    ├── TilemapRenderer       (sorting layer: Default, order 0)
    ├── TilemapCollider2D     (Used By Composite: true)
    ├── CompositeCollider2D   (geometry type: Polygons)
    └── Rigidbody2D           (body type: Static)
```

- Ground Tilemap GameObject is assigned **Layer 7 (Ground)** so the player's existing `GroundCheck` layer detection works without modification.

---

## Assets

Both saved to `Assets/Art/Tilemaps/`:

| Asset | Type | Details |
|---|---|---|
| `PlaceholderTile.png` | Texture2D → Sprite | 16×16 solid gray, pivot: center, PPU: 16 |
| `GroundRuleTile.asset` | RuleTile ScriptableObject | Uses PlaceholderTile as default sprite; neighbor rules configured for all 8 positions. Visually uniform (one placeholder sprite) but system is functionally correct. Phase 6 art swaps in real variants. |

---

## Level Layout

Linear, ~80 tiles wide. Ground floor at y=0. Left-to-right flow.

```
                     [P2]         [P4a][P4b][P4c]
          [P1][P1]         [P3]                  [P5][P5][P5][P5]
[START════]        [══]        [══════]                          [══════END]
           GAP-1        GAP-2           GAP-3+4
```

| Segment | X Range | Y | Width | Purpose |
|---|---|---|---|---|
| Start ground | 0–9 | 0 | 10 | Spawn area, walk left/right |
| Gap #1 | 10–11 | — | 2 | Basic jump |
| Platform P1 | 12–15 | 2 | 4 | Raised surface (walk up) |
| Gap #2 | 16–18 | — | 3 | Jump + fall |
| Platform P2 | 19–21 | 4 | 3 | Higher jump, coyote time on edges |
| Short ground | 22–24 | 0 | 3 | Landing pad |
| Gap #3 | 25–28 | — | 4 | Jump buffering (press jump before land) |
| Platform P3 | 26–28 | 2 | 3 | Mid-gap stepping stone |
| Long ground | 29–38 | 0 | 10 | Recovery run |
| Step P4a | 39 | 1 | 1 | Step 1 of ascending stairs |
| Step P4b | 40 | 2 | 1 | Step 2 |
| Step P4c | 41 | 3 | 1 | Step 3 — consecutive jump practice |
| Gap #4 | 42–44 | — | 3 | Final gap |
| Platform P5 | 45–48 | 2 | 4 | Coyote-time edge practice |
| End ground | 49–58 | 0 | 10 | Exit area |

---

## Implementation Approach

An **Editor-only utility script** (`Assets/Editor/LevelBuilderTool.cs`) with a `[MenuItem("Tools/Build Test Level (DEV-8)")]` that runs the full build in one click:

1. Generate `PlaceholderTile.png` texture → save as sprite asset
2. Create `GroundRuleTile.asset` with neighbor rules
3. Delete old `Ground` placeholder GameObject
4. Create `Grid > Ground` hierarchy with all required components
5. Assign Layer 7 (Ground) to the Ground Tilemap GameObject
6. Paint the level layout via `Tilemap.SetTile()` using the Rule Tile
7. Apply `AssetDatabase.SaveAssets()` and `AssetDatabase.Refresh()`

The script is Editor-only (`#if UNITY_EDITOR`) and does not ship in builds.

---

## Acceptance Criteria Mapping

| AC | Satisfied by |
|---|---|
| Tilemap created using Unity 2D Tilemap system | Grid > Ground Tilemap hierarchy |
| Rule Tiles auto-select based on neighbours | `GroundRuleTile.asset` with 8-neighbor rules |
| TilemapCollider2D + CompositeCollider2D merged | Components on Ground Tilemap, Used By Composite enabled |
| Varied platform heights, gaps, surfaces | Level layout: 4 gaps, 5 platform segments, step stairs |
| Sufficient to verify Phase 1 exit criteria | All movement mechanics (walk/jump/fall/coyote/buffering) exercised |

---

## Out of Scope

- Background/decorative tilemap (Phase 6)
- Real tileset art (Phase 6)
- Level design beyond Phase 1 testing needs
