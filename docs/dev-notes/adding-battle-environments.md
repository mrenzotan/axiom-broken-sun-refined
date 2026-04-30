# Adding a New Battle Environment

Each game level/region needs its own battle background. This doc walks through the full process for a developer adding one.

**System overview:** `BattleEnvironmentData` ScriptableObjects carry a background sprite + ambient tint. The `ExplorationEnemyCombatTrigger` on each enemy references its region's BED. When battle starts, `BattleEnvironmentService` applies it to the background `SpriteRenderer` in the Battle scene.

---

## Step 1 — Create the background sprite

Add the region's background image as a **Sprite** in the Unity project (usually under `Assets/Art/Tilemaps/Backgrounds/`). Import settings: `Sprite (2D and UI)`, filter mode as appropriate for the art style.

---

## Step 2 — Create the ScriptableObject asset

1. In the Project window, navigate to `Assets/Data/BattleEnvironments/`
2. Right-click → **Create → Axiom → Data → Battle Environment Data**
3. Name it `BED_<RegionName>` (e.g., `BED_CombustionLabs`, `BED_AcidCaverns`)

**Naming convention:** `BED_<PascalCaseRegion>` — BED prefix = Battle Environment Data.

---

## Step 3 — Assign sprite and tint

Select the new `.asset` file. In the Inspector:

| Field | What to set |
|-------|-------------|
| **Background Sprite** | Drag the region's background sprite here |
| **Ambient Tint** | Dominant atmosphere colour. White (`#FFFFFF`) for neutral; e.g. cold blue `#C8DFFF` for Snow Mountain, orange `#FFD699` for Combustion Labs |

---

## Step 4 — Wire enemies to the BED

For every `ExplorationEnemyCombatTrigger` in this region's platformer scene:

1. Select the enemy GameObject in the Hierarchy
2. In the Inspector, find the **Battle Environment** field (under `ExplorationEnemyCombatTrigger`)
3. Drag the new `BED_<RegionName>` asset into that slot

All enemies in the same region should reference the same BED.

---

## Step 5 — Verify

1. Enter Play Mode from the platformer scene
2. Engage a wired enemy
3. Confirm the Battle scene shows the correct background sprite and tint
4. **Fallback test:** Leave one trigger unconfigured → battle should show the default static background with no errors

---

## Architecture Reference

| File | Role |
|------|------|
| `Assets/Scripts/Data/BattleEnvironmentData.cs` | ScriptableObject definition — `backgroundSprite` + `ambientTint` |
| `Assets/Scripts/Battle/BattleEnvironmentService.cs` | Plain C# class — `Apply(BattleEnvironmentData, SpriteRenderer)` |
| `Assets/Scripts/Data/BattleEntry.cs` | Carries `EnvironmentData` property across scene load |
| `Assets/Scripts/Platformer/ExplorationEnemyCombatTrigger.cs` | Serialized `_battleEnvironment` field per trigger |
| `Assets/Scripts/Battle/BattleController.cs` | Applies environment in `Start()` before battle initializes |

**Null-safety:** `BattleEnvironmentService.Apply()` is fully null-guarded — if either argument is null, it no-ops silently. An unconfigured trigger keeps the static background.

**Planned BED assets by level (from ENEMY_ROSTER.md):**

| Level | Region | BED Asset | Status |
|-------|--------|-----------|--------|
| 1 | Snow Mountain (Phase Change) | `BED_SnowMountain` | Done (DEV-80) |
| 2 | Combustion Labs | `BED_CombustionLabs` | Not yet created |
| 3 | Acid Caverns | `BED_AcidCaverns` | Not yet created |
| 4 | The Null-Void | `BED_NullVoid` | Not yet created |
