# DEV-46 — Level 1 (Snow Mountain) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Build Level 1 — four connected platformer scenes (Level_1-1 through Level_1-4) using the Pixelart Snow Mountain asset pack, introducing movement, combat-start states, and chemistry spells via a tutorial substage, culminating in the Frost-Melt Sentinel boss.

**Architecture:** All scripts follow the project's hard split — plain C# classes own logic (Edit Mode tested), MonoBehaviour wrappers own lifecycle only. Hazards mutate the persistent `GameManager.PlayerState.CurrentHp` so damage carries seamlessly between platformer and battle. Level exits reuse `SceneTransitionService`. Boss victory gates off the existing `GameManager.IsEnemyDefeated` flag.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, Unity 2D Tilemap + Tile Palettes (manual paint; no RuleTiles — the Snow Mountain sheets use a blob/wang layout, not a 3×3 neighbor pattern), Cinemachine, New Input System, TextMeshPro, Unity Test Framework (Edit Mode, NUnit).

**Related tickets:**

- **DEV-80** — Dynamic Battle background (depends on this plan's Snow Mountain assets; separate ticket)
- **DEV-81** — Narrative cutscene system (will replace DEV-46's placeholder completion card)
- **DEV-82** — Environmental chemistry-spell puzzles (future extension)

**Prerequisite:** DEV-84 (Sprite Pixels Per Unit Consistency) is merged, but this plan **deviates from DEV-84's unified PPU = 16 rule for the Snow Mountain pack**: sheets are imported at a PPU that matches their source pixel size so one sprite always equals one world-unit tile. Concretely, `16x16 tiles/*.png` → **PPU 16**; `Tilesets/*.png` (all 8×8 sheets) → **PPU 8**. `Background/` and `Decor/` keep DEV-84's default (PPU 16). Task 1 Step 2 sets these explicitly — override the Preset Manager default for the 8×8 sheets.

---

## File Structure

### New runtime scripts (`Assets/Scripts/Platformer/`, covered by existing `Platformer.asmdef`)

| File                                    | Responsibility                                                                                    |
| --------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `HazardDamageResolver.cs`               | Pure damage calculation for hazards (reused for first-hit and per-tick spike damage)              |
| `HazardTrigger.cs`                      | MonoBehaviour — pits: instant KO on enter; spikes: first-hit damage + DoT ticks while overlapping (see spike-DoT spec) |
| `PlayerHurtFeedback.cs`                 | MonoBehaviour on Player root — plays `Hurt` animator trigger and applies a sustained sprite tint while overlapping any spike hazard |
| `LevelExitTrigger.cs`                   | MonoBehaviour — OnTriggerEnter2D → `SceneTransition.BeginTransition`                              |
| `PlayerDeathResolver.cs`                | Decides respawn vs. Game Over from player state                                                   |
| `PlayerDeathHandler.cs`                 | MonoBehaviour — polls HP each frame, invokes resolver                                             |
| `TutorialPromptTrigger.cs`              | MonoBehaviour — shows a prompt panel on enter, hides on exit                                      |
| `UI/PlatformerHpHudFormatter.cs`        | Formats "HP {current}/{max}" string                                                               |
| `UI/PlatformerHpHudUI.cs`               | MonoBehaviour — binds `PlayerState.CurrentHp` to TMP text                                         |
| `UI/TutorialPromptPanelUI.cs`           | MonoBehaviour — shows/hides a TMP panel with a message                                            |
| `UI/ChapterCompleteCardUI.cs`           | MonoBehaviour — shows the "Chapter 1 Complete" card, returns to MainMenu                          |
| `BossVictoryChecker.cs`                 | Pure check: is a boss ID in the defeated set?                                                     |
| `BossVictoryTrigger.cs`                 | MonoBehaviour — on scene ready, if boss defeated, shows completion card                           |
| `MeltableObstacle.cs`                   | Pure decision: does a spell ID melt this obstacle?                                                |
| `MeltableObstacleController.cs`         | MonoBehaviour — owns melted state, fade coroutine, exposes `TryMelt(spellId)` seam                |
| `MeltableObstacleDebugCaster.cs`        | MonoBehaviour — DEV-46 stub; fires `TryMelt` from `DebugMeltCast` InputAction (deleted in DEV-82) |
| `MeltableObstacleProximityForwarder.cs` | MonoBehaviour on child trigger — forwards enter/exit to parent controller's `_isPlayerInRange`    |

### New tests (`Assets/Tests/Editor/Platformer/`, covered by existing `PlatformerTests.asmdef`)

| File                               | Tests                                                              |
| ---------------------------------- | ------------------------------------------------------------------ |
| `HazardDamageResolverTests.cs`     | Pit KO, spike % damage, clamping to 0                              |
| `PlatformerHpHudFormatterTests.cs` | Format correctness, edge values                                    |
| `PlayerDeathResolverTests.cs`      | Respawn vs. Game Over decision                                     |
| `BossVictoryCheckerTests.cs`       | Found vs. not found, null/empty id handling                        |
| `MeltableObstacleTests.cs`         | `CanMelt` decision logic — null id, empty id, in-list, not-in-list |

### New Unity Editor assets

| Asset                            | Location                                                                                                                               |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Palette_SnowMountain.prefab`    | `Assets/Art/Tilemaps/SnowMountain/Palettes/` — single Tile Palette containing every sliced Snow Mountain sprite, painted manually from |
| `Tiles/*.asset` (auto-generated) | `Assets/Art/Tilemaps/SnowMountain/Tiles/` — one `.asset` per sliced sprite, auto-created when sprites are dragged onto the palette     |
| `P_SnowMountainParallax.prefab`  | `Assets/Prefabs/Platformer/`                                                                                                           |
| `ED_Meltspawn.asset`             | `Assets/Data/Enemies/`                                                                                                                 |
| `ED_FrostbiteCreeper.asset`      | `Assets/Data/Enemies/`                                                                                                                 |
| `ED_VoidWraith.asset`            | `Assets/Data/Enemies/`                                                                                                                 |
| `ED_FrostMeltSentinel.asset`     | `Assets/Data/Enemies/`                                                                                                                 |
| `P_IceWall.prefab`               | `Assets/Prefabs/Platformer/` — meltable obstacle (Grid + Tilemap + ProximityTrigger)                                                   |
| `Level_1-1.unity`                | `Assets/Scenes/` — exists, will be built out                                                                                           |
| `Level_1-2.unity`                | `Assets/Scenes/` — new                                                                                                                 |
| `Level_1-3.unity`                | `Assets/Scenes/` — new                                                                                                                 |
| `Level_1-4.unity`                | `Assets/Scenes/` — new                                                                                                                 |

### Modified

| File                                                    | Change                                                                              |
| ------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef` | Add `Axiom.Core` reference (tests use `PlayerState`)                                |
| `Assets/InputSystem_Actions.inputactions`               | Add `DebugMeltCast` action (Player map) bound to `<Keyboard>/m` (deleted in DEV-82) |
| Build Settings scene list                               | Add Level_1-1 through Level_1-4                                                     |

### Deleted (cleanup)

| File                                         | Reason                                                                          |
| -------------------------------------------- | ------------------------------------------------------------------------------- |
| `Assets/Editor/LevelBuilderTool.cs`          | Phase 1 stub, replaced by real content; docstring explicitly marks for deletion |
| `Assets/Art/Tilemaps/GroundRuleTile.asset`   | Placeholder rule tile; replaced by Snow Mountain Tile Palette                   |
| `Assets/Art/Tilemaps/PlaceholderTile.png`    | Placeholder gray sprite                                                         |
| `Assets/Data/Enemies/ED_MeltspawnTest.asset` | Test placeholder; replaced by `ED_Meltspawn.asset`                              |

---

## Task 1: UVCS branch + import Snow Mountain pack + create Tile Palette

> **Update (2026-04-28):** Platform-palette tiles are now painted onto a sibling `Tilemap_OneWayPlatforms` GameObject (under `Grid`, on the `OneWayPlatform` layer) rather than `Tilemap_Ground`. The tile assets in `Assets/Art/Tilemaps/SnowMountain/Tiles/` are reused as-is — only the active paint target changes. See [`docs/superpowers/specs/2026-04-28-one-way-platforms-design.md`](../specs/2026-04-28-one-way-platforms-design.md) and [`docs/superpowers/plans/2026-04-28-one-way-platforms.md`](2026-04-28-one-way-platforms.md) for the mechanic and per-scene tilemap structure.

**Files:**

- Create: `Assets/Art/Tilemaps/SnowMountain/Palettes/Palette_SnowMountain.prefab` (Tile Palette)
- Create: `Assets/Art/Tilemaps/SnowMountain/Tiles/*.asset` (one auto-generated Tile per sliced sprite)
- Modify sprite import settings on `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/*.png` and `Tilesets/*.png`

- [ ] **Step 1: Create UVCS feature branch**

> **Unity Editor task (user):** In Unity Version Control, create a new child branch from `main/dev` named `main/dev/DEV-46-level-1-snow-mountain`. Switch your workspace to this branch before starting.

- [ ] **Step 2: Import and configure Snow Mountain tile sheets**

> **Unity Editor task (user):** Each sheet's **Pixels Per Unit** must match its source pixel size so 1 sprite = 1 tile (1 world unit). DEV-84 left every sheet at PPU 16, which is correct for the `16x16 tiles/` group but **wrong for the `Tilesets/` 8×8 group** — you must change those to PPU 8 here. Select each texture, apply the shared settings plus the group-specific PPU, then slice using the pixel size that matches the source artwork.

**Shared Inspector settings (apply to every sheet below):**

- **Texture Type:** Sprite (2D and UI)
- **Sprite Mode:** Multiple
- **Filter Mode:** Point (no filter)
- **Compression:** None
- **Pixels Per Unit:** _set per group below — not shared_

**Group A — 16×16 source sheets** (`16x16 tiles/`) — **PPU 16** (verify — should already be correct post-DEV-84), slice at **Pixel Size 16×16**:

- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/IceTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/RockTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/BackgroundTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/SpikeTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/PlatformTiles.png`

**Group B — 8×8 source sheets** (`Tilesets/`) — **change PPU from 16 → 8** (overrides the DEV-84 default for these sheets), slice at **Pixel Size 8×8**:

- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/iceBGtiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles1.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles2.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/PlatformTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles1.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles2.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles1.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles2.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SlipperyIceTiles.png`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SpikeTiles.png`

For each sheet in its group: click **Sprite Editor** → **Slice** menu → Type: Grid By Cell Size → enter the group's pixel size → Slice → Apply. The slice size reflects the _source sprite dimensions on the sheet_ and is independent of PPU.

> **World-space sizing (PPU matches source size):** because each sheet's PPU equals its source pixel size, **every sliced sprite renders at exactly 1 × 1 world unit** — a 16×16 sprite at PPU 16 and an 8×8 sprite at PPU 8 both fill one cell on a Grid with cell size = 1. The two folders are fully interchangeable on the same tilemap grid: `16x16 tiles/` and `Tilesets/` sheets mix freely at the same painted size. The only remaining difference is art density (8×8 art has half the pixel resolution of 16×16 art at the same on-screen size).

- [ ] **Step 3: Create the SnowMountain folders and build one Tile Palette for the pack**

> **Why Tile Palettes (not RuleTiles):** the Snow Mountain sheets are laid out as blob/wang tiles (edge + corner variants surrounding a hollow interior — see `Tilesets/icetiles1.png`), not Unity's expected 3×3 neighbor grid. RuleTile's default brush pattern does not match this layout, so authoring RuleTiles would require per-tile custom rule setup that duplicates what a simple Tile Palette gives for free. For DEV-46 we paint manually from a palette and defer any autotile work.

> **Unity Editor task (user):**
>
> 1. Right-click in `Assets/Art/Tilemaps/` → Create → Folder → name it `SnowMountain`. Inside `SnowMountain/`, create two subfolders: `Palettes/` and `Tiles/`.
> 2. Open **Window → 2D → Tile Palette**. In the palette window's top dropdown choose **Create New Palette**, name it `Palette_SnowMountain`, Grid = Rectangle, Cell Size = **Manual 1, 1, 0**, save into `Assets/Art/Tilemaps/SnowMountain/Palettes/`. Unity will create `Palette_SnowMountain.prefab`.
> 3. With the palette open, drag the sliced sprite sheets below onto the palette canvas. Unity will prompt for a folder to save the auto-generated Tile `.asset` files — point each prompt at `Assets/Art/Tilemaps/SnowMountain/Tiles/`. After each drag, arrange the tiles on the palette so rock, ice, platform, spike, and BG groups are visually clustered (this is just organization — painting is manual).

**Sprite sheets to drag onto the palette:**

| Source sheet                                                      | Purpose on the palette                                                                |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `Tilesets/rocktiles1.png` + `Tilesets/rocktiles2.png`             | Rock ground (blob layout — paint edges/corners/interior manually)                     |
| `Tilesets/icetiles1.png` + `Tilesets/icetiles2.png`               | Ice ground (same blob pattern as rock)                                                |
| `Tilesets/iceBGtiles.png`, `rockBGtiles1.png`, `rockBGtiles2.png` | Cavern/background fill behind the playfield                                           |
| `Tilesets/SlipperyIceTiles.png`                                   | Optional slippery-ice variant (reserved for later tasks if needed)                    |
| `16x16 tiles/PlatformTiles.png` (or `Tilesets/PlatformTiles.png`) | Drop-through platform tops                                                            |
| `16x16 tiles/SpikeTiles.png` (or `Tilesets/SpikeTiles.png`)       | Decorative spike tiles (actual hazards are `HazardTrigger` objects in Task 12 Step 3) |
| `16x16 tiles/BackgroundTiles.png`                                 | Flat background fill                                                                  |

All sliced sprites render at 1 world unit per the PPU decision in Step 2, so 16×16 and 8×8 tiles can mix freely on the same painted tilemap.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `chore(DEV-46): import Snow Mountain pack and build tile palette`

- `Assets/Art/AssetPacks/Pixelart Snow Mountain/**/*.png.meta` (all modified meta files for reimport)
- `Assets/Art/Tilemaps/SnowMountain/` (the folder, plus the folder's .meta sibling)
- `Assets/Art/Tilemaps/SnowMountain/Palettes/` + `.meta`
- `Assets/Art/Tilemaps/SnowMountain/Palettes/Palette_SnowMountain.prefab` + `.meta`
- `Assets/Art/Tilemaps/SnowMountain/Tiles/` + `.meta`
- `Assets/Art/Tilemaps/SnowMountain/Tiles/*.asset` + `.meta` (every auto-generated Tile asset from Step 3)

---

## Task 2: Build 3-layer parallax prefab

**Files:**

- Create: `Assets/Prefabs/Platformer/P_SnowMountainParallax.prefab`

**Sizing context (why Tiled draw mode + 1.5× root scale are required):** the `Background{1,2,3}.png` files are authored at **320×180 pixels**. At the project PPU of 16 (per DEV-84) this gives a natural sprite size of **20 × 11.25 world units**. But the Pixel Perfect Camera in `Platformer.unity` was set to `RefResolution 480×270` (DEV-84 end-of-plan note) to keep Kaelen at a ~24% screen-height framing — this yields a **30 × 16.875 unit** viewport. A single bare copy of the background therefore covers only ~66% of the viewport in both axes. Horizontally we tile via `SpriteRenderer.DrawMode = Tiled`. Vertically we cannot tile (the Snow Mountain BG art is not designed to seam top-to-bottom — mountain silhouettes would inject mid-sky), so instead we **uniformly scale the ParallaxRoot by 1.5×** so `11.25 × 1.5 = 16.875 units` exactly fills the viewport height. This is the standard pixel-art parallax convention (Celeste / Dead Cells / Hollow Knight): distant layers accept slight non-pixel-perfect scaling, which reads as atmospheric depth rather than as softness.

- [ ] **Step 1: Configure the 3 background textures**

> **Unity Editor task (user):** For each of `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background1.png`, `Background2.png`, `Background3.png`: set Texture Type = Sprite (2D and UI), **Mesh Type = Full Rect** (required for `SpriteRenderer.DrawMode = Tiled`), Wrap Mode = Repeat, Filter Mode = Point, Compression = None. Click Apply.

- [ ] **Step 2: Build the parallax prefab hierarchy**

> **Unity Editor task (user):** In an empty scene (or a temporary scratch scene), create this hierarchy and save it as a prefab at `Assets/Prefabs/Platformer/P_SnowMountainParallax.prefab`.

```
ParallaxRoot (empty GameObject; Transform.scale = (1.5, 1.5, 1) to fill the 16.875-unit viewport height at PPU 16 / Ref 480×270 — see sizing context above)
├── Layer_Far  (SpriteRenderer: Background3.png, Draw Mode = Tiled, Size = (80, 11.25), Sorting Layer = Background, Order = -30; ParallaxController: parallaxFactor = 0.9)
├── Layer_Mid  (SpriteRenderer: Background2.png, Draw Mode = Tiled, Size = (80, 11.25), Sorting Layer = Background, Order = -20; ParallaxController: parallaxFactor = 0.7)
└── Layer_Near (SpriteRenderer: Background1.png, Draw Mode = Tiled, Size = (80, 11.25), Sorting Layer = Background, Order = -10; ParallaxController: parallaxFactor = 0.4)
```

Notes on the existing parallax scripts (`Assets/Scripts/Platformer/ParallaxController.cs` and `ParallaxBackground.cs`):

- `ParallaxController` is the `MonoBehaviour` — attach one per layer (not on the root). It exposes a single serialized field `parallaxFactor`. The camera is resolved automatically via `Camera.main` in `Start()`; there is no reference to wire.
- `ParallaxBackground` is a **plain C# class** (not a component) used internally by `ParallaxController`. Do not try to add it as a component — the Inspector will not offer it.
- Semantics: `parallaxFactor = 0` pins the layer to the world (scrolls with the camera at normal speed, like foreground), `parallaxFactor = 1` makes the layer appear infinitely distant (stationary). Farther = higher factor. Tune the 0.9 / 0.7 / 0.4 values to taste during playtest.

Notes on the Tiled draw mode sizing and root scale:

- `Size = (80, 11.25)` on each layer — 80 units wide in local space; at the 1.5× root scale this becomes an effective **120 world units**, which covers the 30-unit camera viewport plus parallax drift across a ~60-tile level (Near layer at factor 0.4 drifts up to `60 × (1 − 0.4) = 36` units relative to the camera). If later levels grow past ~100 tiles, widen to 120+ local units. Horizontal tiling is free — the texture is authored to loop at the seam via `Wrap Mode = Repeat` (Step 1).
- `Size.y = 11.25` — the natural art height. **Do not stretch vertically via Size**; the Snow Mountain BG art (sky → mountains → foreground silhouette) is not designed to seam top-to-bottom.
- The vertical fit is achieved by the **1.5× uniform scale on ParallaxRoot**, not by Size.y. This scales every pixel of the art, not just height, so the horizontal tile seam remains aligned. The scale is **applied to the root, not per-layer**, so all three layers stay synchronized.
- Pixel density caveat: at 1.5× scale each BG source pixel renders as 1.5 screen pixels — the BG will read as very slightly softer than the foreground (tiles, Kaelen). For a distant parallax layer this is the intended "atmospheric distance" look in pixel-art platformers; do not attempt to compensate with Pixel Snapping tweaks.

- [ ] **Step 3: Position the parallax prefab and set the camera clear color**

> **Unity Editor task (user):**
>
> 1. Inside the prefab, leave each layer's local position at `(0, 0, 0)` initially. The 1.5× root scale centers the scaled BG on the root's origin; tune the **root** Y position (not per-layer) during playtest if the BG vertical framing needs to shift relative to the gameplay line (e.g. `ParallaxRoot.y ≈ -0.5` to nudge mountains below the horizon, `+1` to lift clouds higher). The exact Y is art-direction, not a fixed number.
> 2. Open `Assets/Scenes/Platformer.unity`. Select `Main Camera`. On the `Camera` component, set **Environment → Background Type = Solid Color** and pick a **Background Color** matching the darkest sky tone in `Background3.png` (hex roughly `#0F0F2A` — sample the top-left pixel of the art in any editor to match precisely). With the 1.5× scale applied the BG now fills the viewport height, so the clear color mainly affects the letterbox regions outside the 480×270 reference area — but matching it keeps the edge invisible if the viewport ever exceeds the BG (e.g. wider aspect ratios, camera shake).
> 3. Repeat the same Background Color on `Main Camera` in any other gameplay scene that uses this parallax prefab (Level_1-1 through 1-4 in Task 12 onward). Alternatively, bake the color into the `P_PlatformerCamera` prefab in Task 3 so every level inherits it.

- [ ] **Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-46): add P_SnowMountainParallax prefab`

- `Assets/Prefabs/Platformer/P_SnowMountainParallax.prefab` + `.meta`
- `Assets/Prefabs/Platformer/` folder `.meta` if newly created
- Any modified `Background*.png.meta` files

---

## Task 3: Build P_PlatformerCamera prefab

**Files:**

- Create: `Assets/Prefabs/Platformer/P_PlatformerCamera.prefab`

`Assets/Scenes/Platformer.unity` already contains a working Cinemachine follow camera rig (`Main Camera` + `CM Follow Camera`). Extract it into a prefab so every Level 1 scene (and future worlds) shares one point of tuning. Platformer.unity itself stays untouched — it's a regression harness.

- [ ] **Step 1: Duplicate the camera rig into a scratch scene**

> **Unity Editor task (user):** Open `Assets/Scenes/Platformer.unity`. Select both `Main Camera` (tag `MainCamera`; has `Camera` orthographic size ≈ 10.875, `AudioListener`, `CinemachineBrain`, URP `UniversalAdditionalCameraData`) and `CM Follow Camera` (has `CinemachineCamera` orthographic size ≈ 8.44, `CinemachinePositionComposer` with DeadZone 0.2 × 0.2). Ctrl+D to duplicate. File → New Scene (Empty) → don't save — this is a scratch scene. Drag the two duplicated GOs from Platformer.unity's hierarchy into the scratch scene.

- [ ] **Step 2: Wrap both GOs under a single prefab root**

> **Unity Editor task (user):** In the scratch scene, create an empty GameObject named `CameraRig` at the scene root with position (0, 0, 0). Re-parent both `Main Camera` and `CM Follow Camera` as children of `CameraRig`. Because `CameraRig` is at origin, the children keep their original world positions (Main Camera z = -20, etc.). Final structure:
>
> ```
> CameraRig
> ├── Main Camera         (tag = MainCamera; Camera + AudioListener + CinemachineBrain + URP data)
> └── CM Follow Camera    (CinemachineCamera + CinemachinePositionComposer)
> ```

- [ ] **Step 3: Clear the scene-bound Tracking Target**

> **Unity Editor task (user):** On `CM Follow Camera`'s `CinemachineCamera` component, set `Tracking Target` to None. Prefabs cannot hold references to scene objects — leaving a reference in place would break the prefab on instantiation. The target is re-wired per-level when the prefab is placed in a scene (Task 12 Step 1).

- [ ] **Step 4: Save as prefab**

> **Unity Editor task (user):** Drag `CameraRig` from the Hierarchy into the Project view at `Assets/Prefabs/Platformer/` to create `P_PlatformerCamera.prefab`. Discard the scratch scene without saving.

- [ ] **Step 5: Verify Platformer.unity still works**

> **Unity Editor task (user):** Reopen `Assets/Scenes/Platformer.unity`. Its original `Main Camera` + `CM Follow Camera` should still be present and untouched (Step 1 only duplicated them). Enter Play mode — camera should follow the Player exactly as before. Do **not** replace the rig in Platformer.unity with an instance of the new prefab; leave the test scene as-is.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add P_PlatformerCamera prefab`

- `Assets/Prefabs/Platformer/P_PlatformerCamera.prefab` + `.meta`

---

## Task 4: Add `Axiom.Core` reference to PlatformerTests asmdef

**Files:**

- Modify: `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`

The platformer tests will reference `Axiom.Core.PlayerState`. Without this asmdef reference, tests won't compile.

- [ ] **Step 1: Edit the asmdef**

Replace the contents of `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef` with:

```json
{
  "name": "PlatformerTests",
  "references": ["Axiom.Platformer", "Axiom.Core"],
  "testReferences": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
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

- [ ] **Step 2: Verify compile**

> **Unity Editor task (user):** Return to the Unity Editor, wait for recompile, confirm no compile errors in the Console.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the file below → Check in with message: `chore(DEV-46): add Axiom.Core reference to PlatformerTests asmdef`

- `Assets/Tests/Editor/Platformer/PlatformerTests.asmdef`

---

## Task 5: HazardDamageResolver + HazardTrigger + PlayerHurtFeedback

**Spec:** [`docs/superpowers/specs/2026-04-26-dev-46-spike-hazard-dot-design.md`](../specs/2026-04-26-dev-46-spike-hazard-dot-design.md) — the spike hazard model evolved from one-shot 20% damage to a configurable first-hit + DoT system with sustained sprite-tint feedback. The spec is the source of truth for the `HazardTrigger` Inspector fields, the per-frame data flow, the `PlayerHurtFeedback` contract, and the full test list. Pit hazards (`HazardMode.InstantKO`) are unchanged.

**Files:**

- Create: `Assets/Scripts/Platformer/HazardDamageResolver.cs`
- Create: `Assets/Scripts/Platformer/HazardTrigger.cs`
- Create: `Assets/Scripts/Platformer/PlayerHurtFeedback.cs`
- Create: `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class HazardDamageResolverTests
    {
        [Test]
        public void Resolve_InstantKoMode_ReturnsZeroHp()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 80,
                maxHp: 100,
                mode: HazardMode.InstantKO,
                percentMaxHpDamage: 0);

            Assert.AreEqual(0, result.NewHp);
            Assert.IsTrue(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentDamage_SubtractsPercentOfMax()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 80,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 20);

            Assert.AreEqual(60, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentDamageExceedingCurrentHp_ClampsToZeroAndIsFatal()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 10,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 50);

            Assert.AreEqual(0, result.NewHp);
            Assert.IsTrue(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentRoundsUp_SoOneHpDamageNeverZero()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 100,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 1);

            Assert.AreEqual(99, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }

        [Test]
        public void Resolve_MaxHpZero_ThrowsArgumentOutOfRange()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                HazardDamageResolver.Resolve(
                    currentHp: 0,
                    maxHp: 0,
                    mode: HazardMode.PercentMaxHpDamage,
                    percentMaxHpDamage: 20));
        }
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

> **Unity Editor task (user):** Window → General → Test Runner → Edit Mode → Run All. Expect compile errors (types don't exist yet) or test failures.

- [ ] **Step 3: Create `HazardDamageResolver.cs` and `HazardMode`**

Create `Assets/Scripts/Platformer/HazardDamageResolver.cs`:

```csharp
using System;

namespace Axiom.Platformer
{
    public enum HazardMode
    {
        InstantKO,
        PercentMaxHpDamage,
    }

    public readonly struct HazardDamageResult
    {
        public int NewHp { get; }
        public bool IsFatal { get; }

        public HazardDamageResult(int newHp, bool isFatal)
        {
            NewHp = newHp;
            IsFatal = isFatal;
        }
    }

    public static class HazardDamageResolver
    {
        public static HazardDamageResult Resolve(
            int currentHp,
            int maxHp,
            HazardMode mode,
            int percentMaxHpDamage)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "maxHp must be greater than zero.");

            if (mode == HazardMode.InstantKO)
                return new HazardDamageResult(newHp: 0, isFatal: true);

            int damage = (maxHp * percentMaxHpDamage + 99) / 100;
            int newHp = Math.Max(0, currentHp - damage);
            return new HazardDamageResult(newHp, isFatal: newHp == 0);
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. All 5 `HazardDamageResolverTests` must pass.

- [ ] **Step 5: Create `HazardTrigger` MonoBehaviour wrapper, `PlayerHurtFeedback`, and the additional tick-damage tests**

> **Source of truth:** [`docs/superpowers/specs/2026-04-26-dev-46-spike-hazard-dot-design.md`](../specs/2026-04-26-dev-46-spike-hazard-dot-design.md). The implementation plan generated from that spec covers the full `HazardTrigger` DoT lifecycle (enter/stay/exit + tick timer with `while`-loop overshoot), the `PlayerHurtFeedback` overlap counter and tint, the four new `HazardDamageResolverTests` cases, and the Player prefab inspector wiring (`Hurt` animator trigger). Do not implement from this plan doc directly — execute the spec's plan instead.

> **Legacy note:** earlier drafts of this plan listed a one-shot `OnTriggerEnter2D` HazardTrigger here. That sketch has been removed because it conflicts with the current spec. The pit-hazard path (`HazardMode.InstantKO`) is unchanged in behavior; the spike-hazard path is fully redesigned.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files below → Check in with message: `feat(DEV-46): add HazardDamageResolver, HazardTrigger (DoT), PlayerHurtFeedback`

- `Assets/Scripts/Platformer/HazardDamageResolver.cs` + `.meta`
- `Assets/Scripts/Platformer/HazardTrigger.cs` + `.meta`
- `Assets/Scripts/Platformer/PlayerHurtFeedback.cs` + `.meta`
- `Assets/Tests/Editor/Platformer/HazardDamageResolverTests.cs` + `.meta`

---

## Task 6: LevelExitTrigger

**Files:**

- Create: `Assets/Scripts/Platformer/LevelExitTrigger.cs`

No logic beyond event plumbing — no resolver/tests needed per project's "no premature abstraction" rule.

- [ ] **Step 1: Create the MonoBehaviour**

Create `Assets/Scripts/Platformer/LevelExitTrigger.cs`:

```csharp
using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Trigger collider placed at a level's exit. On player contact, transitions to
    /// the configured scene using the shared SceneTransitionController on GameManager.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExitTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Exact scene name to load. Must be added to Build Settings.")]
        private string _targetSceneName = string.Empty;

        [SerializeField]
        [Tooltip("Visual style of the scene transition. Defaults to WhiteFlash to match battle transitions.")]
        private TransitionStyle _transitionStyle = TransitionStyle.WhiteFlash;

        private bool _triggered;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            if (string.IsNullOrWhiteSpace(_targetSceneName))
            {
                Debug.LogWarning("[LevelExitTrigger] _targetSceneName is empty — exit ignored.", this);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.SceneTransition == null)
            {
                Debug.LogWarning("[LevelExitTrigger] GameManager or SceneTransition missing — exit ignored.", this);
                return;
            }

            _triggered = true;

            GameManager.Instance.CaptureWorldSnapshot(other.transform.position);
            GameManager.Instance.PersistToDisk();
            GameManager.Instance.SceneTransition.BeginTransition(_targetSceneName, _transitionStyle);
        }
    }
}
```

- [ ] **Step 2: Verify compile**

> **Unity Editor task (user):** Return to Unity, wait for recompile, confirm no errors.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add LevelExitTrigger`

- `Assets/Scripts/Platformer/LevelExitTrigger.cs` + `.meta`

---

## Task 7: PlatformerHpHudFormatter + PlatformerHpHudUI

**Files:**

- Create: `Assets/Scripts/Platformer/UI/PlatformerHpHudFormatter.cs`
- Create: `Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs`
- Create: `Assets/Tests/Editor/Platformer/PlatformerHpHudFormatterTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/Editor/Platformer/PlatformerHpHudFormatterTests.cs`:

```csharp
using NUnit.Framework;
using Axiom.Platformer.UI;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class PlatformerHpHudFormatterTests
    {
        [Test]
        public void Format_FullHp_ReturnsCurrentSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(100, 100);
            Assert.AreEqual("HP 100/100", result);
        }

        [Test]
        public void Format_PartialHp_ReturnsCurrentSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(37, 100);
            Assert.AreEqual("HP 37/100", result);
        }

        [Test]
        public void Format_ZeroHp_ReturnsZeroSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(0, 100);
            Assert.AreEqual("HP 0/100", result);
        }

        [Test]
        public void Format_MaxHpZero_ThrowsArgumentOutOfRange()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                PlatformerHpHudFormatter.Format(0, 0));
        }
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Expect compile errors.

- [ ] **Step 3: Create `PlatformerHpHudFormatter.cs`**

Create `Assets/Scripts/Platformer/UI/PlatformerHpHudFormatter.cs`:

```csharp
using System;

namespace Axiom.Platformer.UI
{
    public static class PlatformerHpHudFormatter
    {
        public static string Format(int currentHp, int maxHp)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "maxHp must be greater than zero.");

            return $"HP {currentHp}/{maxHp}";
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. All 4 `PlatformerHpHudFormatterTests` must pass.

- [ ] **Step 5: Create the MonoBehaviour**

Create `Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs`:

```csharp
using Axiom.Core;
using TMPro;
using UnityEngine;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// HUD display for the player's current HP in platformer scenes. Polls
    /// GameManager.Instance.PlayerState each frame and refreshes the TMP label
    /// when HP changes. PlayerState exposes no change event, so polling is the
    /// simplest viable approach.
    /// </summary>
    public class PlatformerHpHudUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("TextMeshProUGUI element that renders the HP line.")]
        private TMP_Text _hpLabel;

        private int _lastRenderedHp = -1;
        private int _lastRenderedMaxHp = -1;

        private void Update()
        {
            if (_hpLabel == null) return;
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            if (state.CurrentHp == _lastRenderedHp && state.MaxHp == _lastRenderedMaxHp)
                return;

            _hpLabel.text = PlatformerHpHudFormatter.Format(state.CurrentHp, state.MaxHp);
            _lastRenderedHp = state.CurrentHp;
            _lastRenderedMaxHp = state.MaxHp;
        }
    }
}
```

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add PlatformerHpHud formatter and UI`

- `Assets/Scripts/Platformer/UI/PlatformerHpHudFormatter.cs` + `.meta`
- `Assets/Scripts/Platformer/UI/PlatformerHpHudUI.cs` + `.meta`
- `Assets/Scripts/Platformer/UI/` folder `.meta` if newly created
- `Assets/Tests/Editor/Platformer/PlatformerHpHudFormatterTests.cs` + `.meta`

---

## Task 8: PlayerDeathResolver + PlayerDeathHandler

**Files:**

- Create: `Assets/Scripts/Platformer/PlayerDeathResolver.cs`
- Create: `Assets/Scripts/Platformer/PlayerDeathHandler.cs`
- Create: `Assets/Tests/Editor/Platformer/PlayerDeathResolverTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/Editor/Platformer/PlayerDeathResolverTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class PlayerDeathResolverTests
    {
        [Test]
        public void Resolve_HpAboveZero_ReturnsNone()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 10,
                activatedCheckpointIds: new List<string>());

            Assert.AreEqual(PlayerDeathOutcome.None, outcome);
        }

        [Test]
        public void Resolve_HpZeroAndNoActivatedCheckpoints_ReturnsGameOver()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: new List<string>());

            Assert.AreEqual(PlayerDeathOutcome.GameOver, outcome);
        }

        [Test]
        public void Resolve_HpZeroAndCheckpointActivated_ReturnsRespawn()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: new List<string> { "CP_Level_1_1_Start" });

            Assert.AreEqual(PlayerDeathOutcome.RespawnAtLastCheckpoint, outcome);
        }

        [Test]
        public void Resolve_NullCheckpointList_TreatedAsEmpty()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: null);

            Assert.AreEqual(PlayerDeathOutcome.GameOver, outcome);
        }
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Expect compile errors.

- [ ] **Step 3: Create `PlayerDeathResolver.cs`**

Create `Assets/Scripts/Platformer/PlayerDeathResolver.cs`:

```csharp
using System.Collections.Generic;

namespace Axiom.Platformer
{
    public enum PlayerDeathOutcome
    {
        None,
        RespawnAtLastCheckpoint,
        GameOver,
    }

    public static class PlayerDeathResolver
    {
        public static PlayerDeathOutcome Resolve(
            int currentHp,
            IReadOnlyList<string> activatedCheckpointIds)
        {
            if (currentHp > 0)
                return PlayerDeathOutcome.None;

            if (activatedCheckpointIds == null || activatedCheckpointIds.Count == 0)
                return PlayerDeathOutcome.GameOver;

            return PlayerDeathOutcome.RespawnAtLastCheckpoint;
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. All 4 `PlayerDeathResolverTests` must pass.

- [ ] **Step 5: Create `PlayerDeathHandler` MonoBehaviour**

Create `Assets/Scripts/Platformer/PlayerDeathHandler.cs`:

```csharp
using Axiom.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Platformer
{
    /// <summary>
    /// Polls PlayerState.CurrentHp each frame while on the platformer side.
    /// On HP reaching zero, delegates to PlayerDeathResolver and either reloads
    /// the current scene (respawn) or loads MainMenu (game over).
    /// Heals to full on respawn so the player has a recoverable slate.
    /// </summary>
    public class PlayerDeathHandler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene loaded when no checkpoint has been activated — game over path.")]
        private string _gameOverSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Visual style for respawn/game-over transitions.")]
        private TransitionStyle _transitionStyle = TransitionStyle.WhiteFlash;

        private bool _dispatched;

        private void Update()
        {
            if (_dispatched) return;
            if (GameManager.Instance == null) return;

            PlayerState state = GameManager.Instance.PlayerState;
            if (state == null) return;

            PlayerDeathOutcome outcome = PlayerDeathResolver.Resolve(
                currentHp: state.CurrentHp,
                activatedCheckpointIds: state.ActivatedCheckpointIds);

            if (outcome == PlayerDeathOutcome.None) return;

            SceneTransitionController transition = GameManager.Instance.SceneTransition;
            if (transition == null)
            {
                Debug.LogWarning("[PlayerDeathHandler] SceneTransition missing — death dispatch skipped.", this);
                return;
            }

            _dispatched = true;

            if (outcome == PlayerDeathOutcome.RespawnAtLastCheckpoint)
            {
                state.SetCurrentHp(state.MaxHp);
                string sceneName = SceneManager.GetActiveScene().name;
                transition.BeginTransition(sceneName, _transitionStyle);
                return;
            }

            transition.BeginTransition(_gameOverSceneName, _transitionStyle);
        }
    }
}
```

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add PlayerDeathResolver and PlayerDeathHandler`

- `Assets/Scripts/Platformer/PlayerDeathResolver.cs` + `.meta`
- `Assets/Scripts/Platformer/PlayerDeathHandler.cs` + `.meta`
- `Assets/Tests/Editor/Platformer/PlayerDeathResolverTests.cs` + `.meta`

---

## Task 9: BossVictoryChecker + BossVictoryTrigger + ChapterCompleteCardUI

**Files:**

- Create: `Assets/Scripts/Platformer/BossVictoryChecker.cs`
- Create: `Assets/Scripts/Platformer/BossVictoryTrigger.cs`
- Create: `Assets/Scripts/Platformer/UI/ChapterCompleteCardUI.cs`
- Create: `Assets/Tests/Editor/Platformer/BossVictoryCheckerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Assets/Tests/Editor/Platformer/BossVictoryCheckerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class BossVictoryCheckerTests
    {
        [Test]
        public void IsVictorious_BossIdInDefeatedSet_ReturnsTrue()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "frost_melt_sentinel_01" },
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsVictorious_BossIdNotInDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "meltspawn_01", "meltspawn_02" },
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_EmptyDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string>(),
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_NullBossId_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "frost_melt_sentinel_01" },
                bossEnemyId: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_NullDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: null,
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }
    }
}
```

- [ ] **Step 2: Run tests, confirm failure**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. Expect compile errors.

- [ ] **Step 3: Create `BossVictoryChecker.cs`**

Create `Assets/Scripts/Platformer/BossVictoryChecker.cs`:

```csharp
using System.Collections.Generic;

namespace Axiom.Platformer
{
    public static class BossVictoryChecker
    {
        public static bool IsVictorious(IEnumerable<string> defeatedEnemyIds, string bossEnemyId)
        {
            if (string.IsNullOrWhiteSpace(bossEnemyId))
                return false;

            if (defeatedEnemyIds == null)
                return false;

            foreach (string id in defeatedEnemyIds)
            {
                if (string.Equals(id, bossEnemyId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests, confirm pass**

> **Unity Editor task (user):** Test Runner → Edit Mode → Run All. All 5 `BossVictoryCheckerTests` must pass.

- [ ] **Step 5: Create `ChapterCompleteCardUI.cs`**

Create `Assets/Scripts/Platformer/UI/ChapterCompleteCardUI.cs`:

```csharp
using Axiom.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Placeholder chapter-complete card shown after a level boss is defeated.
    /// Disabled by default — BossVictoryTrigger activates it. Clicking the continue
    /// button transitions to a configured next scene (MainMenu for DEV-46 scope).
    /// Will be replaced by the DEV-81 cutscene system once built.
    /// </summary>
    public class ChapterCompleteCardUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _continueButton;

        [SerializeField]
        [Tooltip("Scene to load when the continue button is pressed.")]
        private string _continueSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Default title to display.")]
        private string _chapterTitle = "Chapter 1 Complete";

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        public void Show()
        {
            if (_titleLabel != null) _titleLabel.text = _chapterTitle;
            if (_root != null) _root.SetActive(true);
        }

        private void OnContinueClicked()
        {
            if (GameManager.Instance == null || GameManager.Instance.SceneTransition == null) return;
            GameManager.Instance.SceneTransition.BeginTransition(_continueSceneName, TransitionStyle.WhiteFlash);
        }
    }
}
```

- [ ] **Step 6: Create `BossVictoryTrigger.cs`**

Create `Assets/Scripts/Platformer/BossVictoryTrigger.cs`:

```csharp
using Axiom.Core;
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Place in Level_1-4 alongside the boss. On scene ready (after a returning
    /// post-battle transition), checks whether the boss's enemy id is present in
    /// GameManager.DefeatedEnemyIds. If so, shows the ChapterCompleteCardUI.
    /// </summary>
    public class BossVictoryTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Must match the EnemyController.EnemyId on the boss instance in this scene.")]
        private string _bossEnemyId = string.Empty;

        [SerializeField]
        [Tooltip("Card to show on confirmed boss victory.")]
        private ChapterCompleteCardUI _completeCard;

        private bool _shown;

        private void Start()
        {
            CheckAndShow();

            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady += CheckAndShow;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= CheckAndShow;
        }

        private void CheckAndShow()
        {
            if (_shown) return;
            if (GameManager.Instance == null) return;

            bool victorious = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: GameManager.Instance.DefeatedEnemyIds,
                bossEnemyId: _bossEnemyId);

            if (!victorious) return;

            _shown = true;
            if (_completeCard != null) _completeCard.Show();
        }
    }
}
```

- [ ] **Step 7: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add BossVictoryChecker, BossVictoryTrigger, ChapterCompleteCardUI`

- `Assets/Scripts/Platformer/BossVictoryChecker.cs` + `.meta`
- `Assets/Scripts/Platformer/BossVictoryTrigger.cs` + `.meta`
- `Assets/Scripts/Platformer/UI/ChapterCompleteCardUI.cs` + `.meta`
- `Assets/Tests/Editor/Platformer/BossVictoryCheckerTests.cs` + `.meta`

---

## Task 10: TutorialPromptTrigger + TutorialPromptPanelUI

**Files:**

- Create: `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`
- Create: `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs`

Thin MonoBehaviours — no logic requiring unit tests.

- [ ] **Step 1: Create `TutorialPromptPanelUI.cs`**

Create `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace Axiom.Platformer.UI
{
    /// <summary>
    /// Simple prompt panel anchored to the platformer HUD. Shown when the player
    /// enters a TutorialPromptTrigger zone; hidden when they leave it.
    /// </summary>
    public class TutorialPromptPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _bodyLabel;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Show(string body)
        {
            if (_bodyLabel != null) _bodyLabel.text = body;
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: Create `TutorialPromptTrigger.cs`**

Create `Assets/Scripts/Platformer/TutorialPromptTrigger.cs`:

```csharp
using Axiom.Platformer.UI;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Trigger zone that displays a tutorial prompt on the shared panel while the
    /// player is inside. Place in levels to teach movement, combat entry, or
    /// (future) chemistry puzzle mechanics.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TutorialPromptTrigger : MonoBehaviour
    {
        [SerializeField, TextArea(2, 6)] private string _message = string.Empty;
        [SerializeField] private TutorialPromptPanelUI _panel;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel == null) return;
            _panel.Show(_message);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (_panel == null) return;
            _panel.Hide();
        }
    }
}
```

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add TutorialPromptTrigger and panel UI`

- `Assets/Scripts/Platformer/TutorialPromptTrigger.cs` + `.meta`
- `Assets/Scripts/Platformer/UI/TutorialPromptPanelUI.cs` + `.meta`

---

## Task 11: Create Level 1 EnemyData assets

**Files:**

- Create: `Assets/Data/Enemies/ED_Meltspawn.asset`
- Create: `Assets/Data/Enemies/ED_FrostbiteCreeper.asset`
- Create: `Assets/Data/Enemies/ED_VoidWraith.asset`
- Create: `Assets/Data/Enemies/ED_FrostMeltSentinel.asset`

All values sourced from `docs/ENEMY_ROSTER.md` § LEVEL 1. Keep the existing `ED_MeltspawnTest.asset` for now — it's deleted in Task 17.

- [ ] **Step 1: Create `ED_Meltspawn.asset`**

> **Unity Editor task (user):** `Assets/Data/Enemies/` → right-click → Create → Axiom → Data → Enemy Data (or whatever the existing `EnemyData` CreateAsset menu path is — see `Assets/Scripts/Data/EnemyData.cs` for the `[CreateAssetMenu]` attribute). Name it `ED_Meltspawn`. Fill fields in Inspector:

| Field              | Value                            |
| ------------------ | -------------------------------- |
| `enemyName`        | Meltspawn                        |
| `maxHP`            | 20                               |
| `maxMP`            | 0                                |
| `atk`              | 3                                |
| `def`              | 2                                |
| `spd`              | 2                                |
| `xpReward`         | 20                               |
| `innateConditions` | `[Liquid]` (single element list) |

Leave sprite/animator fields empty for now — art pass is Phase 7.

- [ ] **Step 2: Create `ED_FrostbiteCreeper.asset`**

> **Unity Editor task (user):** Same menu path, name `ED_FrostbiteCreeper`. Fields:

| Field              | Value             |
| ------------------ | ----------------- |
| `enemyName`        | Frostbite Creeper |
| `maxHP`            | 22                |
| `maxMP`            | 0                 |
| `atk`              | 4                 |
| `def`              | 1                 |
| `spd`              | 3                 |
| `xpReward`         | 25                |
| `innateConditions` | `[Liquid]`        |

- [ ] **Step 3: Create `ED_VoidWraith.asset`**

> **Unity Editor task (user):** Same menu path, name `ED_VoidWraith`. Fields:

| Field              | Value       |
| ------------------ | ----------- |
| `enemyName`        | Void Wraith |
| `maxHP`            | 24          |
| `maxMP`            | 0           |
| `atk`              | 4           |
| `def`              | 1           |
| `spd`              | 4           |
| `xpReward`         | 30          |
| `innateConditions` | `[Vapor]`   |

- [ ] **Step 4: Create `ED_FrostMeltSentinel.asset`**

> **Unity Editor task (user):** Same menu path, name `ED_FrostMeltSentinel`. Fields:

| Field              | Value                                                                                                                  |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `enemyName`        | Frost-Melt Sentinel                                                                                                    |
| `maxHP`            | 100                                                                                                                    |
| `maxMP`            | 20                                                                                                                     |
| `atk`              | 10                                                                                                                     |
| `def`              | 5                                                                                                                      |
| `spd`              | 6                                                                                                                      |
| `xpReward`         | 175                                                                                                                    |
| `innateConditions` | `[Solid]` (initial phase — phase-cycling behavior belongs to a future boss AI ticket; use single-condition v1 for now) |

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add Level 1 EnemyData assets`

- `Assets/Data/Enemies/ED_Meltspawn.asset` + `.meta`
- `Assets/Data/Enemies/ED_FrostbiteCreeper.asset` + `.meta`
- `Assets/Data/Enemies/ED_VoidWraith.asset` + `.meta`
- `Assets/Data/Enemies/ED_FrostMeltSentinel.asset` + `.meta`

---

## Task 11A: MeltableObstacle scripts and tests

**Files:**

- Create: `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs`
- Create: `Assets/Scripts/Platformer/MeltableObstacle.cs`
- Create: `Assets/Scripts/Platformer/MeltableObstacleController.cs`
- Create: `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs`
- Create: `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs`

Implements the meltable Ice Wall scaffold per `docs/superpowers/specs/2026-04-25-dev-46-ice-wall-puzzle-scaffold-design.md`. The decision logic is a pure static class; the three MonoBehaviours are thin Unity-side glue and are verified manually. The debug caster is a DEV-46 stub that DEV-82 will replace with the voice caster.

- [ ] **Step 1: Write `MeltableObstacleTests.cs`**

Four NUnit cases against `MeltableObstacle.CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)`:

- `CanMelt_NullSpellId_ReturnsFalse`
- `CanMelt_EmptySpellId_ReturnsFalse`
- `CanMelt_SpellInList_ReturnsTrue`
- `CanMelt_SpellNotInList_ReturnsFalse`

- [ ] **Step 2: Write `MeltableObstacle.cs`**

Static class in namespace `Axiom.Platformer`, single public method `CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)`. Early-exit on null/empty spellId, then linear scan of `meltSpellIds` (defensive null check on the list returns false).

> **Unity Editor task (user):** Window → General → Test Runner → EditMode → run `MeltableObstacleTests`. All four cases must pass.

- [ ] **Step 3: Write `MeltableObstacleController.cs`**

MonoBehaviour with serialized `_tilemap` (`UnityEngine.Tilemaps.Tilemap`), `_solidCollider` (`TilemapCollider2D`), `_meltSpells` (`List<SpellData>` — Inspector takes real spell assets so typos are impossible), `_fadeDuration` (float, default 0.7s). Public `TryMelt(string spellId)` per spec; gates on `_isMelted`, `_isPlayerInRange`, then `MeltableObstacle.CanMelt(spellId, [s.spellName for s in _meltSpells if s != null])`. Public `SetPlayerInRange(bool)` for the proximity forwarder. `MeltCoroutine` runs flash (0.0–0.15 s) → disable solid collider → fade alpha + sink scale.y to 0.6 (0.15–`_fadeDuration` s) → disable Tilemap GameObject.

- [ ] **Step 4: Write `MeltableObstacleDebugCaster.cs`**

`[RequireComponent(typeof(MeltableObstacleController))]`. Serialized `InputActionReference _debugMeltAction` and `string _debugSpellId = "combust"`. OnEnable subscribes to `_debugMeltAction.action.performed` and enables the action; OnDisable mirrors. Handler calls `_controller.TryMelt(_debugSpellId)` — proximity gate lives in `TryMelt`, no privileged access here.

- [ ] **Step 5: Write `MeltableObstacleProximityForwarder.cs`**

`[RequireComponent(typeof(Collider2D))]` on a child GameObject. `Reset()` sets `isTrigger = true` and auto-wires `_controller` via `GetComponentInParent<MeltableObstacleController>()`. `OnTriggerEnter2D`/`OnTriggerExit2D` filter by `Player` tag and call `_controller.SetPlayerInRange(true|false)`.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add MeltableObstacle scripts and tests`

- `Assets/Scripts/Platformer/MeltableObstacle.cs` + `.meta`
- `Assets/Scripts/Platformer/MeltableObstacleController.cs` + `.meta`
- `Assets/Scripts/Platformer/MeltableObstacleDebugCaster.cs` + `.meta`
- `Assets/Scripts/Platformer/MeltableObstacleProximityForwarder.cs` + `.meta`
- `Assets/Tests/Editor/Platformer/MeltableObstacleTests.cs` + `.meta`
- `docs/superpowers/plans/2026-04-20-dev-46-level-1-snow-mountain.md`

---

## Task 11B: InputAction + P_IceWall prefab

**Files:**

- Modify: `Assets/InputSystem_Actions.inputactions` (add `DebugMeltCast` action and `<Keyboard>/m` binding under the Player map)
- Create: `Assets/Prefabs/Platformer/P_IceWall.prefab`

- [ ] **Step 1: Add `DebugMeltCast` InputAction**

Insert a Button action named `DebugMeltCast` in the Player map's `actions` array, plus a `<Keyboard>/m` binding (Keyboard&Mouse group) in the `bindings` array.

> **Unity Editor task (user):** Open `Assets/InputSystem_Actions.inputactions` — confirm `DebugMeltCast` shows under the Player map and the `m` key binding is listed. Unity will regenerate `Assets/Scripts/Platformer/InputSystem_Actions.cs` automatically on import.

- [ ] **Step 2: Build `P_IceWall.prefab`**

> **Unity Editor task (user):** In `Assets/Prefabs/Platformer/`, right-click → Create → Prefab → name it `P_IceWall`. Open it in Prefab Mode and assemble:

```
P_IceWall (root)
├── Components: Grid (cell size 1,1,0), MeltableObstacleController, MeltableObstacleDebugCaster
├── Tilemap (child)
│     Components: Tilemap, TilemapRenderer, TilemapCollider2D (solid — leave isTrigger off)
│     Paint a small placeholder shape from Palette_SnowMountain ice tiles so the prefab is non-empty.
└── ProximityTrigger (child)
      Components: BoxCollider2D (isTrigger = true), MeltableObstacleProximityForwarder
      Size the box to the painted Tilemap bounds + ~1 unit buffer on each side.
```

- [ ] **Step 3: Wire serialized fields**

> **Unity Editor task (user):** On the prefab root:
>
> - `MeltableObstacleController._tilemap` → drag the child `Tilemap`
> - `MeltableObstacleController._solidCollider` → drag the child `Tilemap`'s `TilemapCollider2D`
> - `MeltableObstacleController._meltSpells` → set size 1, drag `Assets/Data/Spells/SD_Combust.asset` into element 0
> - `MeltableObstacleController._fadeDuration` → `0.7`
> - `MeltableObstacleDebugCaster._debugMeltAction` → `InputSystem_Actions/Player/DebugMeltCast` (drag from the Project view)
> - `MeltableObstacleDebugCaster._debugSpellId` → `combust`
> - `MeltableObstacleProximityForwarder._controller` → drag the prefab root (auto-fills via `Reset()` on first add)

- [ ] **Step 4: Manual verification in a scratch scene**

> **Unity Editor task (user):** Drop the `P_IceWall` prefab into any scene that has a `Player` (e.g. the existing platformer test scene). Enter Play mode, walk into the proximity trigger, press **M**. Expected: brief cyan flash → collider disables → wall fades and sinks → Tilemap GameObject deactivates after ~0.7 s. Press M again outside the proximity trigger or with no `_meltSpellIds` match: nothing happens.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): add P_IceWall prefab and DebugMeltCast input action`

- `Assets/InputSystem_Actions.inputactions`
- `Assets/Scripts/Platformer/InputSystem_Actions.cs` (auto-regenerated)
- `Assets/Prefabs/Platformer/P_IceWall.prefab` + `.meta`

---

## Task 12: Build Level_1-1 (tutorial stage)

**Files:**

- Modify: `Assets/Scenes/Level_1-1.unity` (currently stub)

Tutorial scope: movement controls, Advantaged combat, Surprised combat, plus the first voice-cast moment (handled by existing Battle UI — Level_1-1 just delivers the first Meltspawn encounter).

- [ ] **Step 1: Open Level_1-1 and set up base hierarchy**

> **Unity Editor task (user):** Open `Assets/Scenes/Level_1-1.unity`. If the scene contains leftover placeholder content from `LevelBuilderTool`, delete everything. Set up this root hierarchy:

```
Level_1-1 (scene root)
├── GameManager (instance of GameManager prefab; required — battle triggers abort without it)
├── Grid (Grid component, cell size 1×1)
│   ├── Tilemap_Ground        (TilemapCollider2D + CompositeCollider2D, painted manually from Palette_SnowMountain using rock/ice ground tiles)
│   ├── Tilemap_Platforms     (TilemapCollider2D + PlatformEffector2D on the collider, Layer = "OneWayPlatform", painted from Palette_SnowMountain using platform tiles)
│   ├── Tilemap_Hazards       (painted from Palette_SnowMountain using spike tiles — hazards themselves are HazardTrigger objects in Step 3; the tilemap is decorative only)
│   └── Tilemap_BG            (no collider, painted from Palette_SnowMountain using background fill tiles)
├── Player (instance of the existing player prefab)
├── CameraRig (instance of P_PlatformerCamera prefab from Task 3; after placement, select the child `CM Follow Camera` and wire its `Tracking Target` to the Player transform)
├── Parallax (instance of P_SnowMountainParallax prefab from Task 2)
├── HUD_Canvas (Screen Space - Overlay)
│   ├── HpHud (GameObject with PlatformerHpHudUI; TMP child for HP label)
│   ├── TutorialPromptPanel (GameObject with TutorialPromptPanelUI; TMP child for body; root panel initially inactive)
├── EventSystem (auto-created by Unity when you add the Canvas; required for UI input — leave as-is)
├── PlayerDeathHandler (empty GO with PlayerDeathHandler component; _gameOverSceneName = "MainMenu")
├── Checkpoint_Start (SavePointTrigger with _checkpointId = "CP_Level_1-1_Start")
├── Tutorial_Movement (TutorialPromptTrigger at spawn, message = "Move with A/D. Jump with Space.")
├── Tutorial_Advantaged (TutorialPromptTrigger near the first enemy, message = "Strike the enemy before it sees you — you'll act first in battle.")
├── Tutorial_Surprised (TutorialPromptTrigger in a corridor where a patrol enemy can walk into the player, message = "If an enemy touches you first, they act first in battle.")
├── Secret_Area (hidden path — details in Step 4)
├── Meltspawn_01 (existing Enemy prefab instance — see Step 5)
├── IceWall_01 (P_IceWall prefab instance — see Step 5.5)
├── Tutorial_IceWall (TutorialPromptTrigger near IceWall_01, message = "An icy wall blocks your path. Press M to test-melt it. (Voice cast comes later.)")
├── Pit_Hazards (empty parent, child HazardTrigger objects in InstantKO mode — see Step 3)
└── LevelExit_To_1-2 (LevelExitTrigger, _targetSceneName = "Level_1-2")
```

- [ ] **Step 2: Paint level geometry**

> **Unity Editor task (user):** Using Window → 2D → Tile Palette, paint the layout:

- Ground: a readable L-shaped path from left spawn (checkpoint) to right exit, ~60 tiles long. Keep gaps small (2-tile max) for tutorial forgiveness.
- Platforms: 1–2 drop-through platforms for "jump up, drop down" practice.
- Hazards (visual spikes): 1 cluster of 3 spike tiles on the floor in the central area.
- BG fill: below-ground rows, plus decorative snow drifts using `Decor/SnowTiles.png` if you want (decor optional).

**Jump-arc validation:** open `Assets/Scripts/Platformer/PlayerMovement.cs` and note the serialized `_jumpForce`, `_moveSpeed`, gravity scale. Enter Play mode mid-paint and test — no gap should exceed reachable horizontal distance during a full-height jump. Adjust tile spacing, not movement values.

- [ ] **Step 3: Place hazards**

> **Unity Editor task (user):** For each pit area, create an empty GameObject with a `BoxCollider2D` (Is Trigger = true) spanning the bottom of the pit, plus a `HazardTrigger` component with `_mode = InstantKO` (other fields ignored). For each visual spike cluster, place **one** HazardTrigger covering the whole cluster (one box, not one per tile — see the spec's "accepted limitations" on stacking). Use `_mode = PercentMaxHpDamage` and the **Tutorial gentle preset** from the spec: `_firstHitDamagePercent = 20`, `_damagePerTickPercent = 10`, `_tickIntervalSeconds = 0.5`. Time-to-die from full HP if the player just stands on spikes: ~4s.

Checkpoint: tutorial stage is forgiving. No more than 2 spike clusters and 1 pit.

- [ ] **Step 4: Place the secret area**

> **Unity Editor task (user):** Secret pattern for Level_1-1 (simplest — fake wall):

- Create a section of Ground tiles that extends behind what appears to be a wall, containing 1 pickup or chest (use a placeholder sprite).
- Add a narrow vertical gap one tile wide that the player can enter by jumping into it.
- Optionally add a TutorialPromptTrigger hinting at exploration (e.g. "Some walls hide more than they show.").

Since DEV-82 puzzles aren't built yet, the reward is visual/spatial only — an area the player earned by exploring. No loot drop required for tutorial level.

- [ ] **Step 5: Place the single Meltspawn encounter**

> **Unity Editor task (user):** In the area covered by the Tutorial_Advantaged prompt:

- Instance the existing Enemy prefab (the one used in the Platformer test scene with `EnemyController`, `EnemyPatrolBehavior`, `ExplorationEnemyCombatTrigger`, `EnemyAnimator`).
- On the instance's `EnemyController`, set `EnemyId = "L1-1_Meltspawn_01"`.
- On `ExplorationEnemyCombatTrigger`, set `_enemyData = ED_Meltspawn`.
- On `EnemyPatrolBehavior`, configure a short 2-waypoint patrol.

- [ ] **Step 5.5: Place the Ice Wall obstacle (DEV-46 scaffold)**

> **Unity Editor task (user):** Between the Meltspawn encounter and `LevelExit_To_1-2`:

- Drop a `P_IceWall` prefab instance into the scene (from `Assets/Prefabs/Platformer/`). Position it spanning the level's vertical playfield so the player cannot bypass it.
- Open the instance's child `Tilemap` and paint the wall shape using `Palette_SnowMountain` ice tiles, sized to fully block the corridor.
- Resize the child `ProximityTrigger`'s `BoxCollider2D` to the painted wall bounds + ~1 unit on each side.
- On `MeltableObstacleDebugCaster`, confirm `_debugMeltAction` is wired to `InputSystem_Actions/Player/DebugMeltCast` (the prefab default should carry through; re-wire if the field shows missing).
- Add a `TutorialPromptTrigger` child GameObject (or sibling, with a `BoxCollider2D` `isTrigger=true`) immediately before the wall, message: `"An icy wall blocks your path. Press M to test-melt it. (Voice cast comes later.)"`. Wire its `_panel` to the scene's `TutorialPromptPanel`.

- [ ] **Step 6: Wire the exit trigger**

> **Unity Editor task (user):** On `LevelExit_To_1-2`, set `_targetSceneName = "Level_1-2"` and `_transitionStyle = WhiteFlash`. Place it at the right edge of the level.

- [ ] **Step 7: Playtest and iterate**

> **Unity Editor task (user):** Enter Play mode from Level_1-1. Verify:

- Tutorial prompts appear in sequence as you walk past triggers.
- Meltspawn encounter loads Battle scene (Advantaged if you strike, Surprised if you walk into it).
- Spike cluster: contact deals an immediate 20% first-hit, then ticks 10% every 0.5s while the player remains on spikes; player can run off and survive. Sprite tints red while overlapping; HUD HP visibly drops on first hit and on each tick.
- Pit fall KOs player; if you touched the start checkpoint, scene reloads and you respawn at the checkpoint with full HP.
- Ice Wall hint appears as you approach; pressing **M** while inside the proximity trigger fades the wall and lets you walk through. Pressing M outside the trigger does nothing.
- `LevelExit_To_1-2` prints a warning (target scene not yet in Build Settings) — this is expected until Task 16.

- [ ] **Step 8: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): build Level_1-1 tutorial stage`

- `Assets/Scenes/Level_1-1.unity`

---

## Task 13: Build Level_1-2

**Files:**

- Create: `Assets/Scenes/Level_1-2.unity`

Level_1-2 focus: Frostbite Creeper introduces the DoT status effect. Narrative beat: "things escalate." No tutorial prompts — player has the basics now.

- [ ] **Step 1: Create the scene**

> **Unity Editor task (user):** File → New Scene → save as `Assets/Scenes/Level_1-2.unity`. Copy the root hierarchy from Level*1-1 as a starting template, then remove all `Tutorial*\*` triggers and change:

- `Checkpoint_Start._checkpointId = "CP_Level_1-2_Start"`
- `LevelExit_To_1-2` → rename to `LevelExit_To_1-3`, `_targetSceneName = "Level_1-3"`
- Remove the Meltspawn instance
- Re-wire `CameraRig` → `CM Follow Camera` → `Tracking Target` to the new scene's Player (the copied reference points at Level_1-1's Player and is now broken). Same applies to any Level_1-3/1-4 scenes spun up from this template.

- [ ] **Step 2: Paint geometry**

> **Unity Editor task (user):** Longer level than 1-1 (~100 tiles). Introduce:

- Elevated platforms requiring ~70% of max jump height
- 2–3 drop-through platforms in a vertical sequence
- 2 spike clusters — use the **Moderate preset** from the spike-DoT spec (`_firstHitDamagePercent = 20`, `_damagePerTickPercent = 15`, `_tickIntervalSeconds = 0.5`; ~3s time-to-die from full HP)
- 1 pit hazard

- [ ] **Step 3: Place 2–3 Frostbite Creeper instances**

> **Unity Editor task (user):** For each instance:

- Instance the Enemy prefab
- `EnemyController.EnemyId` = `"L1-2_FrostbiteCreeper_01"`, `_02`, etc.
- `ExplorationEnemyCombatTrigger._enemyData = ED_FrostbiteCreeper`
- Patrol paths across platforms (so some can be struck Advantaged from below, some corner the player into Surprised encounters)

- [ ] **Step 4: Place secret area**

> **Unity Editor task (user):** Hidden branch off the main path — a vertical drop-through that leads to a pocket with a pickup placeholder. Rewards drop-through platform understanding.

- [ ] **Step 5: Playtest**

> **Unity Editor task (user):** Play from Level_1-1 → exit to Level_1-2 (requires Build Settings update in Task 16; use File → Build Settings → drag Level_1-2.unity in temporarily to test, or test from Level_1-2 directly). Verify all enemies, hazards, exit function.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): build Level_1-2`

- `Assets/Scenes/Level_1-2.unity` + `.meta`

---

## Task 14: Build Level_1-3

**Files:**

- Create: `Assets/Scenes/Level_1-3.unity`

Level_1-3 focus: Void Wraith introduces Vapor condition and the Combustion-on-Vapor elemental synergy. Harder geometry — the last level before the boss.

- [ ] **Step 1: Create the scene**

> **Unity Editor task (user):** Same template as Level_1-2. Rename checkpoint to `CP_Level_1-3_Start`, exit trigger to `_targetSceneName = "Level_1-4"`.

- [ ] **Step 2: Paint geometry**

> **Unity Editor task (user):** Hardest non-boss level. Introduce:

- Multi-segment vertical climbing with drop-through platforms
- 3–4 hazards including a "spike tunnel" forcing precise jumps — use the **Spike tunnel preset** from the spike-DoT spec (`_firstHitDamagePercent = 25`, `_damagePerTickPercent = 15`, `_tickIntervalSeconds = 0.4`; ~2s time-to-die — real urgency, the player must move immediately)
- Use `Decor/ClimberDecor.png` or `iceDecor.png` for mood

- [ ] **Step 3: Place 2–3 Void Wraith instances**

> **Unity Editor task (user):** For each instance:

- `EnemyController.EnemyId` = `"L1-3_VoidWraith_01"`, `_02`, etc.
- `ExplorationEnemyCombatTrigger._enemyData = ED_VoidWraith`
- Void Wraiths are faster (SPD 4) — place them in open arenas where SPD matters

- [ ] **Step 4: Place secret area**

> **Unity Editor task (user):** Behind the most vertical section of the level, a hidden upper ledge accessible only via a specific drop-through-then-jump sequence. Reward: placeholder pickup.

- [ ] **Step 5: Playtest**

> **Unity Editor task (user):** Play Level_1-3. Verify difficulty escalation — several pits, tight timing, at least one Surprised encounter opportunity.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): build Level_1-3`

- `Assets/Scenes/Level_1-3.unity` + `.meta`

---

## Task 15: Build Level_1-4 (boss stage)

**Files:**

- Create: `Assets/Scenes/Level_1-4.unity`

Boss arena. Minimal platforming — walk/jump to the boss, engage, win, see the completion card.

- [ ] **Step 1: Create the scene**

> **Unity Editor task (user):** Same template. Scene is much shorter (~30 tiles wide). No `LevelExitTrigger`.

- [ ] **Step 2: Paint the boss arena**

> **Unity Editor task (user):**

- Flat ground arena with a decorative backdrop (use rock + ice tiles together for the "Frost-Melt" visual theme)
- Checkpoint_Start near the left edge with `_checkpointId = "CP_Level_1-4_Start"` — respawn target if the player flees/dies before the boss triggers
- No hazards, no minions — the boss is the only encounter

- [ ] **Step 3: Place the Frost-Melt Sentinel**

> **Unity Editor task (user):**

- Instance the Enemy prefab at arena center-right
- `EnemyController.EnemyId = "L1-4_FrostMeltSentinel"`
- `ExplorationEnemyCombatTrigger._enemyData = ED_FrostMeltSentinel`
- `EnemyPatrolBehavior`: set a single waypoint (it stays in place) or a short 2-waypoint patrol

- [ ] **Step 4: Add the ChapterCompleteCard and BossVictoryTrigger**

> **Unity Editor task (user):**

- Add the `ChapterCompleteCardUI` to the HUD_Canvas (root panel inactive by default). Fill fields: `_chapterTitle = "Chapter 1 Complete"`, `_continueSceneName = "MainMenu"`.
- Add an empty GameObject `BossVictoryTrigger_Scene` with `BossVictoryTrigger` component. Set `_bossEnemyId = "L1-4_FrostMeltSentinel"` (must match the EnemyController.EnemyId), and wire `_completeCard` to the ChapterCompleteCardUI in the Canvas.

- [ ] **Step 5: Playtest the full Level 1 flow**

> **Unity Editor task (user):** Play from Level_1-1, progress through 1-2, 1-3, 1-4. Engage and defeat the Sentinel in battle. On return to Level_1-4, the ChapterCompleteCardUI should appear. Click Continue → MainMenu loads.

- [ ] **Step 6: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `feat(DEV-46): build Level_1-4 boss stage`

- `Assets/Scenes/Level_1-4.unity` + `.meta`

---

## Task 16: Register scenes in Build Settings and final playtest

- [ ] **Step 1: Add scenes to Build Settings**

> **Unity Editor task (user):** File → Build Settings → drag the following into the Scenes In Build list, in order:

- `Assets/Scenes/MainMenu.unity` (if not already)
- `Assets/Scenes/Level_1-1.unity`
- `Assets/Scenes/Level_1-2.unity`
- `Assets/Scenes/Level_1-3.unity`
- `Assets/Scenes/Level_1-4.unity`
- `Assets/Scenes/Battle.unity` (if not already)

Close the Build Settings window.

- [ ] **Step 2: End-to-end playtest**

> **Unity Editor task (user):** Start from MainMenu → New Game. Play all four substages. Checklist:

- Movement feels responsive on all levels
- All 3 tutorial prompts fire in Level_1-1
- All enemies transition to Battle scene with correct EnemyData
- Advantaged and Surprised paths both produce correct battle start state
- Spikes deal a configurable first-hit + DoT ticks while standing on them (per-instance Inspector tuning; see [spike-DoT spec](../specs/2026-04-26-dev-46-spike-hazard-dot-design.md)); pits instant-KO; death → respawn at last save point with full HP
- HpHud reflects HP changes in real time on the platformer side
- Each substage's `LevelExitTrigger` transitions to the correct next scene
- Frost-Melt Sentinel battle completes; ChapterCompleteCardUI appears on return; Continue loads MainMenu

Fix any issues inline (small scene tweaks, Inspector wiring) before the next step.

- [ ] **Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `chore(DEV-46): register Level 1 scenes in Build Settings`

- `ProjectSettings/EditorBuildSettings.asset`

---

## Task 17: Cleanup

**Files (deletions):**

- Delete: `Assets/Editor/LevelBuilderTool.cs` and `.meta`
- Delete: `Assets/Editor/LevelBuilderTool.asmdef` and `.meta` (if only used for LevelBuilderTool; check whether it also contains other editor tools first)
- Delete: `Assets/Art/Tilemaps/GroundRuleTile.asset` and `.meta`
- Delete: `Assets/Art/Tilemaps/PlaceholderTile.png` and `.meta`
- Delete: `Assets/Data/Enemies/ED_MeltspawnTest.asset` and `.meta`

- [ ] **Step 1: Verify LevelBuilderTool and placeholders have no remaining references**

Run these searches to confirm deletion is safe:

```
Grep: "LevelBuilderTool" in Assets/
Grep: "GroundRuleTile" in Assets/
Grep: "PlaceholderTile" in Assets/
Grep: "MeltspawnTest" in Assets/
Grep: "ED_MeltspawnTest" in Assets/
```

Expected: only the files themselves should match. If any scene or asset references these, resolve the reference first (point to the SnowMountain palette tiles / `ED_Meltspawn` instead).

- [ ] **Step 2: Delete the files**

> **Unity Editor task (user):** In the Project window, right-click → Delete on each of the files listed above. Confirm.

- [ ] **Step 3: Verify compile**

> **Unity Editor task (user):** Wait for Unity to reimport. Confirm no compile errors in the Console. If `LevelBuilderTool.asmdef` contained other types, revert its deletion and only delete `LevelBuilderTool.cs`.

- [ ] **Step 4: Final playtest**

> **Unity Editor task (user):** Replay the full flow from MainMenu → Level_1-1 → ... → boss → MainMenu to confirm nothing broke.

- [ ] **Step 5: Check in via UVCS**

Unity Version Control → Pending Changes → Check in with message: `chore(DEV-46): remove Phase 1 LevelBuilderTool and placeholder tiles`

- Deleted: `Assets/Editor/LevelBuilderTool.cs` + `.meta`
- Deleted: `Assets/Editor/LevelBuilderTool.asmdef` + `.meta` (if applicable)
- Deleted: `Assets/Art/Tilemaps/GroundRuleTile.asset` + `.meta`
- Deleted: `Assets/Art/Tilemaps/PlaceholderTile.png` + `.meta`
- Deleted: `Assets/Data/Enemies/ED_MeltspawnTest.asset` + `.meta`

- [ ] **Step 6: Merge feature branch → main/dev in UVCS**

> **Unity Editor task (user):** With all tasks complete, merge `main/dev/DEV-46-level-1-snow-mountain` → `main/dev` via the UVCS panel.

- [ ] **Step 7: Optional — mirror to git dev**

Because DEV-46 includes script and asmdef changes:

```bash
git checkout dev
git add Assets/Scripts/ Assets/Tests/
git commit -m "feat(DEV-46): add Level 1 platformer scripts and tests"
git push origin dev
```

See `docs/VERSION_CONTROL.md` for why this is optional but encouraged.

---

## Acceptance Criteria Mapping

| DEV-46 AC bullet                                                                | Covered by                                                                                                                                 |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| At least one complete, playable level built using Unity 2D Tilemap + Rule Tiles | Tasks 1, 12–15 — AC satisfied by Unity 2D Tilemap; RuleTiles deferred (blob-layout sheets require manual palette paint, see Task 1 Step 3) |
| Variety of platform types: solid ground, elevated, drop-through                 | Tasks 1 (tile palette), 12–15 (placement)                                                                                                  |
| Environmental hazards (pits, damaging terrain)                                  | Tasks 5 (script), 12–14 (placement)                                                                                                        |
| At least one secret area per level                                              | Tasks 12.4, 13.4, 14.4; Level_1-4 is boss arena (no secret required)                                                                       |
| Enemy spawn positions defined                                                   | Tasks 12.5, 13.3, 14.3, 15.3 — existing `EnemyPatrolBehavior` from DEV-32                                                                  |
| Battle trigger zones linked to enemy data                                       | Tasks 12.5, 13.3, 14.3, 15.3 — existing `ExplorationEnemyCombatTrigger` + new EnemyData assets from Task 11                                |
| Level exit/progression trigger                                                  | Task 6 (script), 12.6, 13.1, 14.1 (placement); Level_1-4 uses `BossVictoryTrigger` instead                                                 |
| Final tileset (or placeholder pending Phase 7)                                  | Task 1 — Snow Mountain pack is final for Level 1                                                                                           |
| Jump distances match player jump arc from DEV-7                                 | Task 12.2, 16.2 (playtest validation)                                                                                                      |
| (Added) HP persists between platformer hazards and battle                       | Task 5 writes through `PlayerState`, which is already persistent                                                                           |
| (Added) Tutorial stage for Level_1-1                                            | Task 10 (script), 12 (placement)                                                                                                           |
| (Added) Boss stage completion → placeholder card → MainMenu                     | Task 9 (scripts), 15 (scene)                                                                                                               |

---

## End-of-plan Notes

_Fill in during execution:_

- Per-source-size PPU decision (Task 1 Step 2): confirmed — `16x16 tiles/` imported at PPU 16, `Tilesets/` 8×8 sheets imported at PPU 8, so every sprite renders at 1 world unit. This deviates from DEV-84's unified PPU 16 for the `Tilesets/` folder specifically.
- Tile Palette vs RuleTile decision (Task 1 Step 3): confirmed — Snow Mountain pack uses a blob/wang layout (edges and corners ringing a hollow interior, see `Tilesets/icetiles1.png`), not Unity's 3×3 RuleTile neighbor pattern. Authoring RuleTiles would require bespoke per-tile rules for every edge/corner variant. DEV-46 ships with a manual Tile Palette; revisit RuleTiles in a later ticket only if authoring speed becomes a bottleneck.
