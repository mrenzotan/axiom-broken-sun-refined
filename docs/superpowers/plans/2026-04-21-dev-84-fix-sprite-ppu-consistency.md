# DEV-84 — Sprite Pixels Per Unit Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Jira:** [DEV-84](https://axiombrokensunrefined.atlassian.net/browse/DEV-84) — Sprite Pixels Per Unit values inconsistent across world-space assets

**Goal:** Unify world-space sprite Pixels Per Unit to **16** across all tilesets, character sprites, enemy sprites, VFX sheets, and backgrounds; align the Pixel Perfect Camera to match; prevent regression via Preset Manager; document the rule in the GDD.

**Architecture:** This is a data/configuration migration — no C# code, no tests to write. Work is split into (1) a Preset Manager registration that enforces PPU 16 on future imports, (2) a per-folder reimport of existing world-space sprites, (3) a scene-level camera update and placement cleanup, (4) a GDD doc update. UI sprites and font atlases are explicitly out of scope (they render via Screen Space Canvas and do not participate in world-space pixel density).

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, `UnityEngine.Rendering.Universal.PixelPerfectCamera`, Unity Preset system.

**No TDD:** This plan has no C# code changes. The usual "write failing test → implement → pass" flow does not apply. Verification is manual + Play Mode visual comparison.

---

## File Structure

**Created:**

- `Assets/Editor/Presets/SpriteImporter_PPU16.preset` — TextureImporter preset with `spritePixelsToUnits: 16`.
- `Assets/Editor/Presets/SpriteImporter_PPU16.preset.meta` — Unity auto-generated meta.
- `Assets/Editor/.meta` / `Assets/Editor/Presets/.meta` — folder metas if folders don't already exist.

**Modified:**

- `ProjectSettings/PresetManager.asset` — adds default-preset entries binding `SpriteImporter_PPU16.preset` to world-space sprite folders.
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/*.png.meta` (9 files) — PPU 8 → 16.
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background1.png.meta`, `Background2.png.meta`, `Background3.png.meta` — PPU 100 → 16.
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/ClimberDecor.png.meta`, `iceDecor.png.meta`, `rockDecor.png.meta`, `SignsDecor.png.meta`, `SnowTiles.png.meta` — PPU 100 → 16.
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Character/IceClimberAnimations.png.meta` — PPU 100 → 16.
- `Assets/Art/Sprites/Player/Kaelen-animations.png.meta`, `Kaelen-charge-animations.png.meta` — PPU 32 → 16.
- `Assets/Art/Sprites/Enemies/Ice Slime/Ice Elemental Sprite Sheet.png.meta` — PPU 32 → 16.
- `Assets/Art/Sprites/VFX/vfx_combust_sheet.png.meta`, `vfx_neutralize_sheet.png.meta` — PPU 32 → 16.
- `Assets/Scenes/Platformer.unity` — Main Camera `PixelPerfectCamera`: `m_AssetsPPU` 100 → 16, `m_RefResolutionX` 1920 → 320, `m_RefResolutionY` 1080 → 180, `m_UpscaleRT` 0 → 1; Transform fixes on placed objects as needed.
- `Assets/Scenes/Battle.unity` — Main Camera `PixelPerfectCamera` same four-field change as Platformer (only if the component is attached); Transform fixes as needed.
- `Assets/Prefabs/**/*.prefab` — any prefab whose sprite-child Transforms carry a compensating scale (non-1 `localScale`) baked in for the old mixed PPU must be reset to (1, 1, 1). See Task 8.
- `docs/GAME_DESIGN_DOCUMENT.md` §4.1 — add PPU 16 + RefResolution 320×180 + UpscaleRT rule; reframe dimension guidance.

**Explicitly NOT touched (out of scope — keep at PPU 100):**

- `Assets/Art/UI/**/*.png.meta` (all UI sprites, book styles pack, mana soul GUI)
- `Assets/Art/Font/**/*.png.meta` (bitmap font atlases)
- `Assets/Art/Backgrounds/MainMenu.png.meta`, `MainMenuTitle.png.meta` (render via Screen Space Canvas in the planned MainMenu scene)
- `Assets/Art/Backgrounds/FarBackground.png.meta`, `MidBackground.png.meta`, `NearBackground.png.meta` — already at PPU 16, no change needed
- `Assets/Art/Tilemaps/PlaceholderTile.png.meta` — already at PPU 16
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/16x16 tiles/*.png.meta` (5 files) — already at PPU 16
- `Assets/Art/Sprites/VFX/vfx_combust_{1..7}.png.meta` — already at PPU 16
- `Assets/Art/Sprites/VFX/vfx_ice/*.png.meta` — already at PPU 16

---

## Task 1: Create PPU 16 Texture Importer Preset

**Goal:** Produce a reusable `.preset` asset that future imports can default to via Preset Manager.

**Files:**

- Create: `Assets/Editor/Presets/SpriteImporter_PPU16.preset`

- [ ] **Step 1.1 — Create the preset via an existing correct sprite**

> **Unity Editor task (user):**
>
> 1. In the Project window, create the folder path `Assets/Editor/Presets/` if it does not already exist (right-click `Assets` → Create → Folder → name it `Editor`; then inside `Editor` create `Presets`).
> 2. Select any sprite that is already at PPU 16 as the source — recommended: `Assets/Art/Backgrounds/FarBackground.png`.
> 3. In the Inspector, scroll to the top-right and click the **Preset selector icon** (small slider/menu icon on the header next to the component name). A **Select Preset…** dialog opens.
> 4. In that dialog, click **Create New Texture Importer Preset…** (this is the current Unity 6 equivalent of the older "Save current to…" wording — it captures the current Inspector values into a new preset asset).
> 5. In the save dialog, navigate to `Assets/Editor/Presets/` and save the file as `SpriteImporter_PPU16`.
> 6. Confirm the new file `Assets/Editor/Presets/SpriteImporter_PPU16.preset` appears.

- [ ] **Step 1.2 — Verify preset contents**

> **Unity Editor task (user):** Select the new preset asset and confirm in the Inspector that **Pixels Per Unit = 16** is listed in the captured properties.

**No UVCS check-in yet** — bundled with Task 2.

---

## Task 2: Register the Preset in Preset Manager

**Goal:** Future sprite imports dropped into world-space folders should inherit PPU 16 automatically.

**Files:**

- Modify: `ProjectSettings/PresetManager.asset`

- [ ] **Step 2.1 — Register the preset for each world-space folder**

> **Unity Editor task (user):**
>
> 1. In the Project window, select `Assets/Editor/Presets/SpriteImporter_PPU16.preset`.
> 2. In the Inspector, click the **Add to Texture Importer default** button at the top. This registers the preset in Preset Manager automatically — avoids the Preset Manager's **Add Default Preset** dialog (which is a class-type picker, not an asset picker; searching for `SpriteImporter_PPU16` there finds nothing).
> 3. Open **Edit → Project Settings → Preset Manager**. You will see a new row under **Texture Importer** with `SpriteImporter_PPU16` already populated in the preset slot and an empty **Filter** field.
> 4. Set the **Filter** on that row to the first path using Unity's **glob:** syntax (plain text filters match asset _names_, not paths — for folder scoping, glob is required): `glob:"Assets/Art/Sprites/**"`
> 5. Add three more entries for the same Texture Importer type — click the **+** button at the end of the row to add another entry. For each new entry, drag `SpriteImporter_PPU16.preset` into the preset slot and set the Filter:
>    - `glob:"Assets/Art/Backgrounds/**"`
>    - `glob:"Assets/Art/Tilemaps/**"`
>    - `glob:"Assets/Art/AssetPacks/**"`
>
>    The `glob:` prefix and surrounding quotation marks are **required**. The `**` wildcard matches any depth of subfolders, so files nested inside pack subfolders also match. Filters are case-sensitive.
>
> 6. Ensure the scoped preset entries are above any broader default Texture Importer preset (if one exists) so they take precedence.
> 7. Close Project Settings — Unity writes `ProjectSettings/PresetManager.asset`.

- [ ] **Step 2.2 — Verify with a test import**

> **Unity Editor task (user):**
>
> 1. Duplicate any small `.png` from `Assets/Art/Sprites/VFX/` in Finder/Explorer to that same folder with a throwaway name (e.g. `_ppu_test.png`).
> 2. Wait for Unity to import it.
> 3. Select the new file in Project → Inspector should show **Pixels Per Unit = 16** automatically applied.
> 4. Delete `_ppu_test.png` (and `_ppu_test.png.meta`). This was a verification step; do not commit.

**No UVCS check-in yet** — bundled with Task 3.

---

## Task 3: Reimport Sprites Currently at PPU 8 → 16

**Goal:** Align the Snow Mountain `Tilesets/` folder (currently PPU 8) to the project standard.

**Files modified (9 `.png.meta` files):**

- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/iceBGtiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/PlatformTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SlipperyIceTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SpikeTiles.png.meta` (already included in scan — confirm PPU)

- [ ] **Step 3.1 — Batch reimport**

> **Unity Editor task (user):**
>
> 1. In Project window, expand `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/`.
> 2. Select all `.png` files in that folder (click first, Shift+click last).
> 3. In the Inspector, change **Pixels Per Unit** from `8` to `16`.
> 4. Click **Apply**.
> 5. Wait for Unity reimport to finish.

- [ ] **Step 3.2 — Verify meta values**

Run (read-only verification, not an edit):

```
grep "spritePixelsToUnits" "Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/"*.png.meta
```

Expected: every line ends in ` 16`.

If any file still shows `8`, it was missed — reselect and Apply again.

**No UVCS check-in yet** — bundled at the end of the sprite reimport tasks.

---

## Task 4: Reimport Sprites Currently at PPU 100 → 16

**Goal:** Align Snow Mountain `Background/`, `Decor/`, `Character/` (currently PPU 100, never changed from Unity default) to the project standard. These are world-space art.

**Files modified (9 `.png.meta` files):**

- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background3.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/ClimberDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/iceDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/rockDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/SignsDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/SnowTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Character/IceClimberAnimations.png.meta`

- [ ] **Step 4.1 — Batch reimport Background**

> **Unity Editor task (user):** Select all `.png` files in `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 4.2 — Batch reimport Decor**

> **Unity Editor task (user):** Select all `.png` files in `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 4.3 — Reimport Character**

> **Unity Editor task (user):** Select `Assets/Art/AssetPacks/Pixelart Snow Mountain/Character/IceClimberAnimations.png` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 4.4 — Verify**

```
grep "spritePixelsToUnits" "Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/"*.png.meta "Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/"*.png.meta "Assets/Art/AssetPacks/Pixelart Snow Mountain/Character/"*.png.meta
```

Expected: every line ends in ` 16`.

---

## Task 5: Reimport Sprites Currently at PPU 32 → 16

**Goal:** Align character, enemy, and VFX sheets (the user's intentional 32-PPU group) to the project standard.

**Files modified (5 `.png.meta` files):**

- `Assets/Art/Sprites/Player/Kaelen-animations.png.meta`
- `Assets/Art/Sprites/Player/Kaelen-charge-animations.png.meta`
- `Assets/Art/Sprites/Enemies/Ice Slime/Ice Elemental Sprite Sheet.png.meta`
- `Assets/Art/Sprites/VFX/vfx_combust_sheet.png.meta`
- `Assets/Art/Sprites/VFX/vfx_neutralize_sheet.png.meta`

- [ ] **Step 5.1 — Reimport Player sprites**

> **Unity Editor task (user):** Multi-select both files under `Assets/Art/Sprites/Player/` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 5.2 — Reimport Enemies**

> **Unity Editor task (user):** Select `Assets/Art/Sprites/Enemies/Ice Slime/Ice Elemental Sprite Sheet.png` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 5.3 — Reimport VFX sheets**

> **Unity Editor task (user):** Multi-select `vfx_combust_sheet.png` and `vfx_neutralize_sheet.png` under `Assets/Art/Sprites/VFX/` → set **Pixels Per Unit = 16** → **Apply**.

- [ ] **Step 5.4 — Verify**

```
grep "spritePixelsToUnits" Assets/Art/Sprites/Player/*.png.meta "Assets/Art/Sprites/Enemies/Ice Slime/Ice Elemental Sprite Sheet.png.meta" Assets/Art/Sprites/VFX/vfx_combust_sheet.png.meta Assets/Art/Sprites/VFX/vfx_neutralize_sheet.png.meta
```

Expected: every line ends in ` 16`.

---

## Task 6: Verify Kaelen Frame Dimensions

**Goal:** Resolve the open AC question: are Kaelen's sheet frames 32x32 or 64x64? This determines the world-space size under PPU 16 (2 units vs. 4 units tall).

- [ ] **Step 6.1 — Inspect the sprite sheet**

> **Unity Editor task (user):**
>
> 1. Select `Assets/Art/Sprites/Player/Kaelen-animations.png` in the Project window.
> 2. In the Inspector, click **Sprite Editor**.
> 3. Observe the slice grid — each cell's pixel dimensions (e.g. 32x32 or 64x64) will be visible in the slice panel.
> 4. Note the frame dimensions and compute expected world-space height under PPU 16:
>    - 32x32 frame → 2 units tall
>    - 48x48 frame → 3 units tall
>    - 64x64 frame → 4 units tall

- [ ] **Step 6.2 — Record the finding**

Record the actual frame dimensions in the **End-of-plan Notes** section at the bottom of this plan file — replace the `Kaelen frame dimensions:` TBD line with the measured value, e.g. `Kaelen frame dimensions: 64x64 → 4 units tall under PPU 16`.

- [ ] **Step 6.3 — Sanity-check the implied size**

> **Unity Editor task (user):** In `Assets/Scenes/Platformer.unity`, temporarily place a Kaelen sprite in the scene next to a 16x16 tile (a 1-unit reference). Observe the scale. If Kaelen is visibly too large (e.g. more than 5× the tile height) or too small for a platformer hero, flag this in the plan's end note and discuss with the team before proceeding to Task 7 — options include rescaling the source art or overriding Kaelen's PPU (breaking the unified rule deliberately, documented as an exception in the GDD).

---

## Task 7: Update Pixel Perfect Camera in Scenes

**Goal:** Match the Pixel Perfect Camera to the new project-wide PPU **and** keep the on-screen visible area roughly unchanged so the scene layouts remain usable. Changing only `AssetsPPU` without adjusting `RefResolution` would zoom the camera out **6.25×** (orthographic size = `RefResolutionY / (2 × AssetsPPU)`, so `1080/(2×100) = 5.4` units tall today vs. `1080/(2×16) = 33.75` units tall under a partial change).

**The fix (pixel-art standard, used by Celeste / Dead Cells / most modern pixel platformers):** drop the Reference Resolution to a low pixel-art canvas (320×180) and enable `UpscaleRT`. Under `PPU 16` + `Ref 320×180`, the orthographic size becomes `180 / (2×16) = 5.625` — nearly identical to today's 5.4, so level layouts remain playable. `UpscaleRT = 1` renders the scene to an offscreen 320×180 render target first, then scales it up with nearest-neighbor to the window (this is what produces the "chunky pixel" look and guarantees each source pixel lands on a whole number of screen pixels).

**Files modified:**

- `Assets/Scenes/Platformer.unity`
- `Assets/Scenes/Battle.unity` (conditionally)

- [ ] **Step 7.1 — Platformer scene camera update (four fields)**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Platformer.unity`.
> 2. Select `Main Camera` in the Hierarchy.
> 3. In the Inspector, locate the **Pixel Perfect Camera** component and set:
>    - **Assets Pixels Per Unit:** `100` → `16`
>    - **Reference Resolution X:** `1920` → `480`
>    - **Reference Resolution Y:** `1080` → `270`
>    - **Upscale Render Texture:** unchecked → **checked** (this is the `m_UpscaleRT` flag)
> 4. Leave other fields (Pixel Snapping, Crop Frame, Stretch Fill, Filter Mode) at their current defaults unless they are off from Unity's defaults — if any are, flag in end-of-plan notes and ask before changing.
> 5. Save the scene (Ctrl/Cmd+S).

- [ ] **Step 7.2 — Battle scene camera check**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Select `Main Camera` in the Hierarchy.
> 3. If a **Pixel Perfect Camera** component is attached: apply the same four-field change as Step 7.1 (`AssetsPPU` → 16, `RefResolutionX` → 480, `RefResolutionY` → 270, `UpscaleRT` → checked), save the scene, and note this in end-of-plan notes.
> 4. If the component is **not** attached, no change is required — note this in the end-of-plan notes.

- [ ] **Step 7.3 — Verify all four fields**

Run:

```
grep -E "m_AssetsPPU|m_RefResolutionX|m_RefResolutionY|m_UpscaleRT" Assets/Scenes/Platformer.unity Assets/Scenes/Battle.unity
```

Expected for Platformer (always) and Battle (only if the Pixel Perfect Camera is attached):

- `m_AssetsPPU: 16`
- `m_RefResolutionX: 480`
- `m_RefResolutionY: 270`
- `m_UpscaleRT: 1`

---

## Task 8: Prefab Audit + Scene Placement Cleanup

**Goal:** Sprites now render at different **world-unit footprints** after the reimport. Because Task 7 keeps the camera's visible world area nearly constant (~5.4 → 5.625 units tall), the **on-screen appearance also changes** — pixel density becomes higher and each source pixel now occupies a larger fraction of the screen. Placed prefabs and scene objects that previously had compensating scale values will now double-compensate, and placements that were aligned to the old per-sprite density will now overlap or leave gaps.

Work through this task in order: **prefabs first**, because incorrect prefab-level scales would re-break every scene that instances them.

**Files modified:**

- `Assets/Prefabs/**/*.prefab` (only those with non-(1,1,1) `localScale` on a sprite-bearing child that was compensating for the old PPU)
- `Assets/Scenes/Platformer.unity`
- `Assets/Scenes/Battle.unity`

### What to expect on-screen

With Task 7 applied, the camera's visible world footprint is ~20 × 11.25 units (essentially unchanged from the current ~19.2 × 10.8). What changes is **world-unit size per sprite** — and because the visible area is constant, the on-screen effect is:

| Sprite group                                                                      | Old PPU | New PPU | World units per source pixel | On-screen appearance change                                                                     |
| --------------------------------------------------------------------------------- | ------- | ------- | ---------------------------- | ----------------------------------------------------------------------------------------------- |
| Snow Mountain `Tilesets/`                                                         | 8       | 16      | 1/8 → 1/16                   | Each tile occupies **half** the world footprint and **half** the screen area it did before.     |
| Snow Mountain `Background/`, `Decor/`, `Character/`                               | 100     | 16      | 1/100 → 1/16                 | Each sprite occupies **6.25×** the world footprint and **6.25×** the screen area it did before. |
| Player Kaelen, Ice Slime, VFX sheets                                              | 32      | 16      | 1/32 → 1/16                  | Each sprite occupies **2×** the world footprint and **2×** the screen area it did before.       |
| Already-PPU-16 sprites (16x16 tiles, VFX individual frames, existing backgrounds) | 16      | 16      | 1/16 (unchanged)             | No change.                                                                                      |

**Rule:** do NOT reintroduce compensating Transform scales to hide these differences. The goal is uniform density. Accept the new sizes, and relayout placed objects to fit.

- [x] **Step 8.1 — Prefab audit (do this first)**

Run this read-only scan to list prefabs in the project:

```
find Assets/Prefabs -type f -name "*.prefab"
```

> **Unity Editor task (user):**
>
> 1. For each prefab returned: open it in the Prefab editor (double-click).
> 2. Inspect every child GameObject that has a `SpriteRenderer`. In the Inspector, check the Transform → **Scale**.
> 3. If Scale is `(1, 1, 1)` → leave as-is.
> 4. If Scale is **any non-(1, 1, 1) value** (including values above 1, below 1, or negative magnitudes from horizontal/vertical flips) and the prefab was authored before this migration, treat it as suspect. Compensating scales run in **both directions**:
>    - **Scale < 1** if the artist shrank an oversized source to fit the old per-sprite density (e.g. `0.5, 0.5, 1` for previously-PPU-8 tile instances, `~0.32, 0.32, 1` for previously-PPU-100 decor instances).
>    - **Scale > 1** if the artist enlarged a too-small source for readability at the old density (e.g. a battle enemy authored at PPU 32 scaled up to 2× so it reads on the battle screen).
>    - **Negative scale component** indicates a flip (e.g. `x: -2`) — the magnitude (2) is the compensation, the sign is a flip and must be preserved.
>
>    In all three cases, reset the magnitude to 1 while preserving any flip sign (e.g. `-2 → -1`, `2 → 1`, `0.5 → 1`).
>
> 5. If Scale is non-1 but the prefab was authored as a **deliberate** art-scale (e.g. a banner sprite intended to be larger than source) — leave as-is and note the prefab path in end-of-plan notes so the Platformer scene layout in Step 8.2 can account for it.
> 6. Save each modified prefab.

**Prefabs known to need audit** (identified during plan review — these reference sprites whose PPU is changing):

> - `Assets/Prefabs/Enemies/Ice Slime (Battle).prefab` — **intentional, do NOT reset.** The Animator child (which holds the `SpriteRenderer`) carries `m_LocalScale: {x: -2, y: 2, z: 2}` as a deliberate art-scale choice: at `(-1, 1, 1)` the Ice Slime reads as too small on the battle screen. Preserve the `(-2, 2, 2)` magnitude and the horizontal flip. This is the documented exception to the Task 8 "reset compensating scales" rule. Revisit only if the Step 8.3 battle visual audit shows the enemy has become oversized under PPU 16 — in which case discuss reducing the magnitude with the team before editing.
> - `Assets/Prefabs/Enemies/Enemy.prefab` — references the Ice Elemental sheet (PPU 32 → 16). Audit scale.
> - `Assets/Prefabs/Player/Player (Battle).prefab` — references Kaelen (PPU 32 → 16). Audit scale.
> - `Assets/Prefabs/Player/Player (Exploration).prefab` — references Kaelen (PPU 32 → 16). Audit scale.
>
> Note that `Assets/Scenes/Battle.unity` also carries a scene-level override on the `Ice Slime (Battle)` prefab instance: `m_LocalScale.x = -2` plus gameplay fields `_attackPositionX = -2` and `_moveDuration = 0.8`. Because the prefab's intentional scale is being preserved, leave the scene-level override alone as well. The gameplay `_attackPositionX = -2` is a world-space tween distance — its meaning in world units does not change with PPU, so leave it as is unless Step 8.3's visual audit shows the attack lunge now looks wrong at the new density.

If `Assets/Prefabs/` is empty or does not exist, skip this step and note `No prefabs exist yet; audit skipped.` in end-of-plan notes.

- [x] **Step 8.2 — Platformer scene visual audit**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Platformer.unity`.
> 2. With Task 7 + Task 8.1 applied, entering Play Mode should already produce a scene where every sprite pixel is the same size on screen. Enter Play Mode briefly to confirm pixel density uniformity before editing placements.
> 3. Back in Edit Mode, walk through the hierarchy and expect these regressions:
>    - Tilemaps painted from `Pixelart Snow Mountain/Tilesets/` (previously PPU 8) now render at **half** their previous on-screen area — tile layouts will look "half-tile stretched" against their previously-PPU-16 grid. Reposition / retile as needed.
>    - Sprites placed from `Background/`, `Decor/`, or `Character/IceClimberAnimations` (previously PPU 100) now render at **6.25× the previous on-screen area** — these will likely be far too large and will need to be moved off-scene or resized by replacing them with smaller art entries, **not** by adding compensating scale.
>    - Kaelen (previously PPU 32) is now **2×** his previous on-screen area.
> 4. For each regression, adjust Transform **position** (not scale) to restore a playable layout. If a sprite is too big to fit the level, crop the art or source a smaller variant instead of scaling.
> 5. If the Platformer scene currently contains only a small test area (expected, since Level 1 Snow Mountain under DEV-46 is still being built), keep the cleanup light — DEV-46 will be the primary level-design pass against the corrected PPU.

- [x] **Step 8.3 — Battle scene visual audit**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Apply the same audit: reposition (not rescale) Ice Slime (previously PPU 32 → 2× on-screen), VFX sheets (previously PPU 32 → 2×), and any background sprites to restore a playable layout.
> 3. Battle UI lives on a Screen Space Canvas and is unaffected by the PPU change — do not rescale UI elements.
> 4. Enter Play Mode briefly and confirm the enemy sprite, VFX, and battle UI render correctly together.

- [x] **Step 8.4 — Save scenes and prefabs**

> **Unity Editor task (user):** Ctrl/Cmd+S in each open scene; confirm each modified prefab shows no dirty-state indicator in the Project window.

---

## Task 9: Update GAME_DESIGN_DOCUMENT.md §4.1

**Goal:** Codify the PPU 16 rule in the spec so future asset authors and importers have an authoritative reference.

**Files modified:**

- `docs/GAME_DESIGN_DOCUMENT.md`

- [x] **Step 9.1 — Rewrite §4.1 Visual Style**

Claude writes this edit directly. Replace the existing §4.1 body with:

```markdown
### 4.1 Visual Style (Pixel Art)

The game uses a mixed sprite-dimension pixel art style with a single, uniform project-wide pixel density.

**Project-wide world-space Pixels Per Unit (PPU): 16.**

This is the **density rule**, not a dimension rule. Every world-space sprite (tilesets, character sprites, enemy sprites, VFX sheets, background parallax layers) imports at `Pixels Per Unit = 16`, regardless of its source pixel dimensions. The `PixelPerfectCamera` component on the Main Camera in each gameplay scene is configured to match:

| Pixel Perfect Camera field | Value       | Why                                                                                                                                                                                |
| -------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Assets Pixels Per Unit     | **16**      | Matches the world-space PPU rule above.                                                                                                                                            |
| Reference Resolution X     | **320**     | Pixel-art canvas width; with PPU 16 this yields a 20-unit wide camera view.                                                                                                        |
| Reference Resolution Y     | **180**     | Pixel-art canvas height; with PPU 16 this yields an 11.25-unit tall camera view (`180 / (2×16)`).                                                                                  |
| Upscale Render Texture     | **enabled** | Renders at 320×180 then nearest-neighbor upscales to the window — guarantees every source pixel lands on a whole number of screen pixels (crisp chunky pixels, no sub-pixel blur). |

Orthographic size is derived: `RefResolutionY / (2 × AssetsPPU) = 180 / 32 = 5.625` units — the visible world is a **~20 × 11.25 unit** area regardless of window size.

**Consequence — on-screen footprint scales with sprite dimension:**

| Sprite dimension | World-space footprint under PPU 16 |
| ---------------- | ---------------------------------- |
| 8x8              | 0.5 units (half a tile)            |
| 16x16            | 1 unit (nominal tile size)         |
| 32x32            | 2 units                            |
| 64x64            | 4 units                            |

All sprites end up at **identical on-screen pixel size** — only the world-space footprint differs. Mixing dimensions is fine; mixing PPU values is not.

**Sprite dimension guidance (non-binding, for art authors):**

- **Environment & tilesets:** 16x16 base tiles are the standard. 8x8 sub-variants (e.g. the Snow Mountain `Tilesets/` folder) are acceptable for extra detail and tile around the 16x16 grid as half-tiles.
- **Characters & enemies:** 32x32 per frame for small enemies (Glimmerlings, slimes); 48x48 or 64x64 for the protagonist and bosses where the Catalyst Arm detail needs more resolution.
- **Reactive VFX:** 16x16 or 32x32 per frame, designed to read at a glance during combat.

**Out of scope for PPU 16 (stay at PPU 100):**

- UI sprites (`Assets/Art/UI/**`) — render in Screen Space Canvas, do not participate in world-space pixel density.
- Font atlases (`Assets/Art/Font/**`).
- Main Menu backgrounds used as full-screen Canvas art.

**Enforcement:** New imports into `Assets/Art/Sprites/`, `Assets/Art/Backgrounds/`, `Assets/Art/Tilemaps/`, and `Assets/Art/AssetPacks/` inherit PPU 16 automatically via a Preset Manager default (`Assets/Editor/Presets/SpriteImporter_PPU16.preset`).
```

Then continue with the existing §4.2 (Color Palette) content unchanged.

- [x] **Step 9.2 — Verify**

Run:

```
grep -n "Project-wide world-space Pixels Per Unit" docs/GAME_DESIGN_DOCUMENT.md
```

Expected: one match.

---

## Task 10: Play Mode Verification

**Goal:** Confirm the migration is complete and visually correct before check-in.

- [ ] **Step 10.1 — Enter Play Mode in Platformer scene**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Platformer.unity`.
> 2. Press Play.
> 3. Confirm the game starts without errors in the Console.
> 4. Visually compare three items in the frame: any 8x8 tile, any 16x16 tile, and Kaelen's sprite. Each pixel of any sprite should appear the same size on screen. If one tile's pixels are visibly larger than another's, the migration is incomplete — return to the relevant reimport task.

- [ ] **Step 10.2 — Enter Play Mode in Battle scene**

> **Unity Editor task (user):**
>
> 1. Open `Assets/Scenes/Battle.unity`.
> 2. Press Play.
> 3. Confirm the enemy sprite and any active VFX render at consistent pixel density with battle UI elements (UI stays at its own size since UI PPU is intentionally 100).

- [ ] **Step 10.3 — Test Runner regression sweep**

> **Unity Editor task (user):**
>
> 1. Open **Window → General → Test Runner**.
> 2. Click **Run All** in the **EditMode** tab.
> 3. Confirm all existing tests pass (no regressions — this is a data-only change, so pass is expected).
> 4. If any test fails, investigate whether the failure is related to PPU (it should not be — tests are pure C#); if unrelated, note in the end-of-plan.

---

## Task 11: Check in via UVCS

- [ ] **Check in via UVCS:**
      Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `fix(DEV-84): unify world-space sprite PPU to 16`

Files to stage:

- `Assets/Editor/Presets/SpriteImporter_PPU16.preset`
- `Assets/Editor/Presets/SpriteImporter_PPU16.preset.meta`
- `Assets/Editor/Presets.meta` (if newly created)
- `Assets/Editor.meta` (if newly created)
- `ProjectSettings/PresetManager.asset`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/iceBGtiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/icetiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/PlatformTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rockBGtiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/rocktiles2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SlipperyIceTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Tilesets/SpikeTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background1.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background2.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Background/Background3.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/ClimberDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/iceDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/rockDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/SignsDecor.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Decor/SnowTiles.png.meta`
- `Assets/Art/AssetPacks/Pixelart Snow Mountain/Character/IceClimberAnimations.png.meta`
- `Assets/Art/Sprites/Player/Kaelen-animations.png.meta`
- `Assets/Art/Sprites/Player/Kaelen-charge-animations.png.meta`
- `Assets/Art/Sprites/Enemies/Ice Slime/Ice Elemental Sprite Sheet.png.meta`
- `Assets/Art/Sprites/VFX/vfx_combust_sheet.png.meta`
- `Assets/Art/Sprites/VFX/vfx_neutralize_sheet.png.meta`
- `Assets/Scenes/Platformer.unity`
- `Assets/Scenes/Battle.unity` (only if modified)
- Any `Assets/Prefabs/**/*.prefab` files modified during Step 8.1 (plus their `.prefab.meta` siblings)
- `docs/GAME_DESIGN_DOCUMENT.md`

---

## End-of-plan Notes

_Fill in during execution (Tasks 6.2, 7.2, 8.1, 10.3):_

- Kaelen frame dimensions: **64x64 → 4 units tall under PPU 16** (confirmed via Sprite Editor, Kaelen-animations.png).
- Battle.unity Pixel Perfect Camera present? **No component attached on Battle.unity's Main Camera — no change applied.** Pixel Perfect Camera only exists on `Platformer.unity → Main Camera`.
- **Reference Resolution chosen: 480 × 270** (not the 320 × 180 suggested in the Task 7 preamble). This yields a 30 × 16.875 unit visible area under PPU 16 (`480/16 × 270/16`) — a wider/taller canvas than the 320 × 180 variant, preferred for framing the platformer at the current world scale. Orthographic size derived: `270 / (2 × 16) = 8.4375`.
- **Cinemachine VCam orthographic size: 5.625 → 8.44.** In the Platformer scene the Cinemachine VCam drives the Main Camera's ortho size; it was manually set to **8.44** (≈ 8.4375) to match the PPC's derived size for `Ref 480×270 / PPU 16` and keep the world-object framing unchanged relative to before (not zoomed in).
- **Grid Snapping: `Pixel Snapping` (not `Upscale Render Texture`).** Initial configuration used `Upscale Render Texture` per Task 7.1, but during Play Mode verification world-space `TextMeshPro` floating numbers (the "HP MAX / MP MAX" feedback spawned by `PlatformerFloatingNumberSpawner`/`PlatformerFloatingNumberInstance` at checkpoints) rendered as illegible colored blobs — the 480×270 intermediate RT squashed glyphs before upscaling. Switched to `Pixel Snapping` mode, which renders at the window's native resolution and snaps sprite positions to the pixel grid. Sprites stayed crisp; world-space text became legible again. In Unity 6 these are mutually exclusive values of a single `Grid Snapping` dropdown (not independent toggles as they were in older Unity versions).
- Prefab audit result (2026-04-23): **no prefab edits required.**
  - `Assets/Prefabs/Enemies/Enemy.prefab` — `(1, 1, 1)`, no change.
  - `Assets/Prefabs/Enemies/Ice Slime (Battle).prefab` — Animator child `(-2, 2, 2)`, **intentional art-scale** (readability on battle screen), preserved per user direction. Battle.unity scene override also preserved.
  - `Assets/Prefabs/Player/Player (Battle).prefab` — `(1, 1, 1)`, no change.
  - `Assets/Prefabs/Player/Player (Exploration).prefab` — `(1, 1, 1)`, no change.
  - `Assets/Prefabs/Items/Ether.prefab` — sprite child `(0.3, 0.3, 1)`. Source spritesheet (`Round Potion - BLUE - Spritesheet.png`) already at PPU 16 — not part of this migration. Scale is deliberate pickup sizing from DEV-66 work; leave as-is.
  - `Assets/Prefabs/Items/Potion.prefab` — sprite child `(0.3, 0.3, 1)`. Same as Ether (source `Round Potion - RED - Spritesheet.png` at PPU 16); leave as-is.
  - UI / Core / Voice prefabs: out of scope (no world-space SpriteRenderer).
- Test Runner result: _TBD during Step 10.3 — "all pass" OR list any failures with notes_.
