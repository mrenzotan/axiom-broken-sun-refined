# DEV-8: Tilemap World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable linear scrolling test level using Unity 2D Tilemap and Rule Tiles that exercises all Phase 1 player movement mechanics (walk, jump, fall, coyote time, jump buffering).

**Architecture:** An Editor-only utility script (`LevelBuilderTool.cs`) is invoked via a `[MenuItem]`. It generates a placeholder sprite asset, creates a `RuleTile` ScriptableObject with neighbor rules, builds the `Grid > Ground` Tilemap hierarchy with `TilemapCollider2D + CompositeCollider2D`, and paints the full level layout via `Tilemap.SetTile()`. Zero runtime code is added — the level is pure scene data.

**Tech Stack:** Unity 6.3 LTS, `com.unity.2d.tilemap` 1.0.0, `com.unity.2d.tilemap.extras` 7.0.1 (assembly: `Unity.2D.Tilemap.Extras`), NUnit via Unity Test Framework (Edit Mode).

**Spec:** `docs/superpowers/specs/2026-03-20-dev8-tilemap-world-design.md`

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Editor/LevelBuilderTool.cs` | Create | MenuItem entry point + all asset creation, hierarchy building, tile painting |
| `Assets/Editor/LevelBuilderTool.asmdef` | Create | Editor-only assembly definition for the tool |
| `Assets/Tests/Editor/LevelBuilderToolTests.cs` | Create | Edit Mode tests: sprite asset, Rule Tile, hierarchy, tile positions |
| `Assets/Tests/Editor/LevelBuilderTests.asmdef` | Create | Test assembly definition referencing tool + Test Runner |
| `Assets/Art/Tilemaps/` | Create dir | Output folder for `PlaceholderTile.png` and `GroundRuleTile.asset` |
| `Assets/Scenes/Platformer.unity` | Modify | Scene saved after running the tool; Player Y position adjusted |

---

## API Reference (verified from installed package source)

```csharp
// RuleTile (UnityEngine.Tilemaps, assembly Unity.2D.Tilemap.Extras)
ruleTile.m_DefaultSprite = sprite;
ruleTile.m_TilingRules   // List<RuleTile.TilingRule>

// RuleTile.TilingRule (extends TilingRuleOutput)
rule.m_NeighborPositions  // List<Vector3Int> — parallel with m_Neighbors
rule.m_Neighbors          // List<int>         — use TilingRuleOutput.Neighbor constants
rule.m_Sprites            // Sprite[] (initialized as new Sprite[1])
rule.m_Output             // TilingRuleOutput.OutputSprite (default: Single)

// Neighbor constants (int)
TilingRuleOutput.Neighbor.This    = 1
TilingRuleOutput.Neighbor.NotThis = 2
```

---

## Task 1: Scaffold — folder structure, assembly definitions, skeleton files

**Files:**
- Create: `Assets/Editor/LevelBuilderTool.asmdef`
- Create: `Assets/Editor/LevelBuilderTool.cs`
- Create: `Assets/Tests/Editor/LevelBuilderTests.asmdef`
- Create: `Assets/Tests/Editor/LevelBuilderToolTests.cs`

- [ ] **Step 1: Create `Assets/Editor/LevelBuilderTool.asmdef`**

```json
{
    "name": "LevelBuilderTool",
    "references": [
        "Unity.2D.Tilemap.Extras",
        "Unity.2D.Tilemap.Extras.Editor"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Create skeleton `Assets/Editor/LevelBuilderTool.cs`**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Phase 1 test level builder — delete when replaced by Phase 6 real levels.
/// </summary>
public static class LevelBuilderTool
{
    private const string TilemapArtPath       = "Assets/Art/Tilemaps";
    private const string PlaceholderSpritePath = "Assets/Art/Tilemaps/PlaceholderTile.png";
    private const string RuleTilePath          = "Assets/Art/Tilemaps/GroundRuleTile.asset";

    [MenuItem("Tools/Build Test Level (DEV-8)")]
    public static void BuildLevel()
    {
        // Tasks 2–5 will fill this in
    }
}
#endif
```

- [ ] **Step 3: Create `Assets/Tests/Editor/LevelBuilderTests.asmdef`**

```json
{
    "name": "LevelBuilderTests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Unity.2D.Tilemap.Extras",
        "LevelBuilderTool"
    ],
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

- [ ] **Step 4: Create skeleton `Assets/Tests/Editor/LevelBuilderToolTests.cs`**

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelBuilderToolTests
{
    [SetUp]
    public void SetUp()
    {
        LevelBuilderTool.BuildLevel();
    }

    [TearDown]
    public void TearDown()
    {
        // Destroy the Grid created by BuildLevel so tests don't side-effect the scene
        var grid = GameObject.Find("Grid");
        if (grid != null)
            Object.DestroyImmediate(grid);
    }

    // Tests added in subsequent tasks
}
```

- [ ] **Step 5: Verify zero compile errors**

Unity Editor Console — confirm no errors appear after Unity reimports the new files.

- [ ] **Step 6: Check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Editor/`, `Assets/Tests/Editor/`, `Assets/Art/` → Summary: `scaffold: add LevelBuilderTool skeleton, test assembly, and art folder for DEV-8` → **Check in**

---

## Task 2: Placeholder sprite asset

**Files:**
- Modify: `Assets/Editor/LevelBuilderTool.cs`
- Modify: `Assets/Tests/Editor/LevelBuilderToolTests.cs`

- [ ] **Step 1: Write failing test**

Add inside `LevelBuilderToolTests`:

```csharp
[Test]
public void BuildLevel_CreatesPlaceholderSpriteAsset()
{
    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
        "Assets/Art/Tilemaps/PlaceholderTile.png");
    Assert.IsNotNull(sprite,
        "PlaceholderTile.png should exist as a Sprite asset after BuildLevel");
}
```

- [ ] **Step 2: Run test — verify it FAILS**

Unity Editor → Window → General → Test Runner → EditMode → run `BuildLevel_CreatesPlaceholderSpriteAsset`
Expected: **FAIL** — "PlaceholderTile.png should exist as a Sprite asset after BuildLevel"

- [ ] **Step 3: Implement `CreatePlaceholderSprite()` in `LevelBuilderTool.cs`**

Replace the empty `BuildLevel()` and add the method:

```csharp
[MenuItem("Tools/Build Test Level (DEV-8)")]
public static void BuildLevel()
{
    CreatePlaceholderSprite();
}

private static void CreatePlaceholderSprite()
{
    System.IO.Directory.CreateDirectory(TilemapArtPath);

    if (!System.IO.File.Exists(PlaceholderSpritePath))
    {
        var texture = new Texture2D(16, 16);
        var pixels = new Color[256];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.6f, 0.6f, 0.6f, 1f); // mid-gray placeholder
        texture.SetPixels(pixels);
        System.IO.File.WriteAllBytes(PlaceholderSpritePath, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(PlaceholderSpritePath);
    }

    var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderSpritePath);
    importer.textureType          = TextureImporterType.Sprite;
    importer.spriteImportMode     = SpriteImportMode.Single;
    importer.spritePixelsPerUnit  = 16;
    importer.filterMode           = FilterMode.Point;
    importer.textureCompression   = TextureImporterCompression.Uncompressed;
    importer.SaveAndReimport();
}
```

- [ ] **Step 4: Run test — verify it PASSES**

Unity Test Runner → EditMode → run `BuildLevel_CreatesPlaceholderSpriteAsset`
Expected: **PASS**

- [ ] **Step 5: Check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Editor/LevelBuilderTool.cs`, `Assets/Tests/Editor/LevelBuilderToolTests.cs` → Summary: `feat: generate placeholder tile sprite asset for DEV-8` → **Check in**

---

## Task 3: Rule Tile asset

**Files:**
- Modify: `Assets/Editor/LevelBuilderTool.cs`
- Modify: `Assets/Tests/Editor/LevelBuilderToolTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `LevelBuilderToolTests`:

```csharp
[Test]
public void BuildLevel_CreatesRuleTileAsset()
{
    var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(
        "Assets/Art/Tilemaps/GroundRuleTile.asset");
    Assert.IsNotNull(ruleTile, "GroundRuleTile.asset should exist after BuildLevel");
}

[Test]
public void BuildLevel_RuleTileHasDefaultSprite()
{
    var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(
        "Assets/Art/Tilemaps/GroundRuleTile.asset");
    Assert.IsNotNull(ruleTile.m_DefaultSprite,
        "GroundRuleTile default sprite must be assigned");
}

[Test]
public void BuildLevel_RuleTileHasEightNeighborRules()
{
    var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(
        "Assets/Art/Tilemaps/GroundRuleTile.asset");
    Assert.AreEqual(8, ruleTile.m_TilingRules.Count,
        "GroundRuleTile must have exactly 8 tiling rules covering all 8 neighbor positions");
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Run all three new tests. Expected: all **FAIL**.

- [ ] **Step 3: Implement `CreateRuleTile()` in `LevelBuilderTool.cs`**

Add `using UnityEngine.Tilemaps;` at the top. Add the method and call it from `BuildLevel()`:

```csharp
[MenuItem("Tools/Build Test Level (DEV-8)")]
public static void BuildLevel()
{
    CreatePlaceholderSprite();
    CreateRuleTile();
}

private static void CreateRuleTile()
{
    var sprite   = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
    var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);

    if (ruleTile == null)
    {
        ruleTile = ScriptableObject.CreateInstance<RuleTile>();
        AssetDatabase.CreateAsset(ruleTile, RuleTilePath);
    }

    ruleTile.m_DefaultSprite = sprite;
    ruleTile.m_TilingRules.Clear();

    // 8 rules covering all 8 surrounding neighbor positions.
    // All output the same placeholder sprite — Phase 6 art swaps in real variants.

    // Cardinal: above (0, 1, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int(0,  1, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int(0, -1, 0), TilingRuleOutput.Neighbor.This));   // top-surface tile

    // Cardinal: above + below
    AddRule(ruleTile, sprite,
        (new Vector3Int(0,  1, 0), TilingRuleOutput.Neighbor.This),
        (new Vector3Int(0, -1, 0), TilingRuleOutput.Neighbor.This));   // interior tile

    // Cardinal: left (-1, 0, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int(-1, 0, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int( 1, 0, 0), TilingRuleOutput.Neighbor.This));   // left-edge tile

    // Cardinal: right (1, 0, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int( 1, 0, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int(-1, 0, 0), TilingRuleOutput.Neighbor.This));   // right-edge tile

    // Diagonal: top-left (-1, 1, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int(-1,  1, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int( 0, -1, 0), TilingRuleOutput.Neighbor.This));  // top-left concave corner

    // Diagonal: top-right (1, 1, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int(1,  1, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int(0, -1, 0), TilingRuleOutput.Neighbor.This));   // top-right concave corner

    // Diagonal: bottom-left (-1, -1, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int(-1, -1, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int(-1,  0, 0), TilingRuleOutput.Neighbor.This),
        (new Vector3Int( 0, -1, 0), TilingRuleOutput.Neighbor.This));  // bottom-left inner corner

    // Diagonal: bottom-right (1, -1, 0)
    AddRule(ruleTile, sprite,
        (new Vector3Int( 1, -1, 0), TilingRuleOutput.Neighbor.NotThis),
        (new Vector3Int( 1,  0, 0), TilingRuleOutput.Neighbor.This),
        (new Vector3Int( 0, -1, 0), TilingRuleOutput.Neighbor.This));  // bottom-right inner corner

    EditorUtility.SetDirty(ruleTile);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}

private static void AddRule(RuleTile ruleTile, Sprite sprite,
    params (Vector3Int pos, int neighbor)[] neighbors)
{
    var rule = new RuleTile.TilingRule();
    rule.m_Sprites[0] = sprite;
    rule.m_Output     = TilingRuleOutput.OutputSprite.Single;

    foreach (var (pos, n) in neighbors)
    {
        rule.m_NeighborPositions.Add(pos);
        rule.m_Neighbors.Add(n);
    }

    ruleTile.m_TilingRules.Add(rule);
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Run all three Rule Tile tests. Expected: all **PASS**.

- [ ] **Step 5: Check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Editor/LevelBuilderTool.cs`, `Assets/Tests/Editor/LevelBuilderToolTests.cs` → Summary: `feat: create GroundRuleTile asset with neighbor rules for DEV-8` → **Check in**

---

## Task 4: Scene hierarchy

**Files:**
- Modify: `Assets/Editor/LevelBuilderTool.cs`
- Modify: `Assets/Tests/Editor/LevelBuilderToolTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `LevelBuilderToolTests`:

```csharp
[Test]
public void BuildLevel_CreatesGridGameObject()
{
    var grid = GameObject.Find("Grid");
    Assert.IsNotNull(grid, "Grid GameObject must exist in scene");
    Assert.IsNotNull(grid.GetComponent<Grid>(), "Grid must have a Grid component");
}

[Test]
public void BuildLevel_GroundTilemapExistsUnderGrid()
{
    var grid   = GameObject.Find("Grid");
    var ground = grid.transform.Find("Ground");
    Assert.IsNotNull(ground, "Ground child must exist under Grid");
    Assert.IsNotNull(ground.GetComponent<Tilemap>(),          "Ground must have Tilemap");
    Assert.IsNotNull(ground.GetComponent<TilemapRenderer>(),  "Ground must have TilemapRenderer");
    Assert.IsNotNull(ground.GetComponent<TilemapCollider2D>(),"Ground must have TilemapCollider2D");
    Assert.IsNotNull(ground.GetComponent<CompositeCollider2D>(),"Ground must have CompositeCollider2D");
    Assert.IsNotNull(ground.GetComponent<Rigidbody2D>(),      "Ground must have Rigidbody2D");
}

[Test]
public void BuildLevel_GroundTilemapIsOnLayer7()
{
    var ground = GameObject.Find("Grid").transform.Find("Ground");
    Assert.AreEqual(7, ground.gameObject.layer,
        "Ground Tilemap must be on Layer 7 (Ground) for player GroundCheck to work");
}

[Test]
public void BuildLevel_ColliderUsesCompositeOperation()
{
    var ground = GameObject.Find("Grid").transform.Find("Ground");
    var col    = ground.GetComponent<TilemapCollider2D>();
    Assert.AreEqual(Collider2D.CompositeOperation.Merge, col.compositeOperation,
        "TilemapCollider2D must use CompositeOperation.Merge");
}

[Test]
public void BuildLevel_RigidbodyIsStatic()
{
    var ground = GameObject.Find("Grid").transform.Find("Ground");
    var rb     = ground.GetComponent<Rigidbody2D>();
    Assert.AreEqual(RigidbodyType2D.Static, rb.bodyType,
        "Rigidbody2D must be Static (required by CompositeCollider2D)");
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Expected: all **FAIL**.

- [ ] **Step 3: Implement `BuildSceneHierarchy()` in `LevelBuilderTool.cs`**

Add the method and call it from `BuildLevel()`. Return the `Tilemap` for use by the painting task:

```csharp
[MenuItem("Tools/Build Test Level (DEV-8)")]
public static void BuildLevel()
{
    CreatePlaceholderSprite();
    CreateRuleTile();
    var tilemap = BuildSceneHierarchy();
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
}

private static Tilemap BuildSceneHierarchy()
{
    // Remove old placeholder Ground if it has no Tilemap component
    var oldGround = GameObject.Find("Ground");
    if (oldGround != null && oldGround.GetComponent<Tilemap>() == null)
        Object.DestroyImmediate(oldGround);

    // Always rebuild Grid fresh to stay idempotent
    var existingGrid = GameObject.Find("Grid");
    if (existingGrid != null)
        Object.DestroyImmediate(existingGrid);

    var gridGO = new GameObject("Grid");
    gridGO.AddComponent<Grid>();

    var groundGO = new GameObject("Ground");
    groundGO.transform.SetParent(gridGO.transform, false);
    groundGO.layer = 7; // Ground layer

    var tilemap = groundGO.AddComponent<Tilemap>();
    groundGO.AddComponent<TilemapRenderer>();

    var col = groundGO.AddComponent<TilemapCollider2D>();
    col.compositeOperation = Collider2D.CompositeOperation.Merge;

    var rb    = groundGO.AddComponent<Rigidbody2D>();
    rb.bodyType = RigidbodyType2D.Static;

    groundGO.AddComponent<CompositeCollider2D>(); // Must be added after Rigidbody2D

    return tilemap;
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: all **PASS**.

- [ ] **Step 5: Save scene (Cmd+S) then check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Editor/LevelBuilderTool.cs`, `Assets/Tests/Editor/LevelBuilderToolTests.cs`, `Assets/Scenes/Platformer.unity` → Summary: `feat: build Grid+Tilemap hierarchy with composite collision for DEV-8` → **Check in**

---

## Task 5: Level painting

**Files:**
- Modify: `Assets/Editor/LevelBuilderTool.cs`
- Modify: `Assets/Tests/Editor/LevelBuilderToolTests.cs`

**Level layout reference** (from spec — use this table, not the ASCII diagram):

| Segment | X Start | X End | Y | Purpose |
|---|---|---|---|---|
| Start ground | 0 | 9 | 0 | Spawn area, walk |
| Gap #1 | 10 | 11 | — | Basic jump |
| Platform P1 | 12 | 15 | 2 | Raised surface |
| Gap #2 | 16 | 18 | — | Jump + fall |
| Platform P2 | 19 | 21 | 4 | Higher jump, coyote time |
| Short ground | 22 | 24 | 0 | Landing pad |
| Gap #3 | 25 | 28 | — | Jump buffering test |
| Platform P3 | 26 | 28 | 2 | Mid-gap stepping stone |
| Long ground | 29 | 38 | 0 | Recovery run |
| Step P4a | 39 | 39 | 1 | Step 1 |
| Step P4b | 40 | 40 | 2 | Step 2 |
| Step P4c | 41 | 41 | 3 | Step 3 |
| Gap #4 | 42 | 44 | — | Final gap |
| Platform P5 | 45 | 48 | 2 | Coyote-time edge practice |
| End ground | 49 | 58 | 0 | Exit area |

- [ ] **Step 1: Write failing tests**

Add a helper and spot-check tests to `LevelBuilderToolTests`:

```csharp
private Tilemap GetGroundTilemap()
{
    return GameObject.Find("Grid")
        .transform.Find("Ground")
        .GetComponent<Tilemap>();
}

[Test]
public void BuildLevel_StartGroundTilesExist()
{
    var tm = GetGroundTilemap();
    Assert.IsNotNull(tm.GetTile(new Vector3Int(0,  0, 0)), "Tile at (0,0)  — start ground");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(9,  0, 0)), "Tile at (9,0)  — end of start ground");
}

[Test]
public void BuildLevel_GapsAreEmpty()
{
    var tm = GetGroundTilemap();
    Assert.IsNull(tm.GetTile(new Vector3Int(10, 0, 0)), "(10,0) must be empty — Gap #1");
    Assert.IsNull(tm.GetTile(new Vector3Int(11, 0, 0)), "(11,0) must be empty — Gap #1");
    Assert.IsNull(tm.GetTile(new Vector3Int(25, 0, 0)), "(25,0) must be empty — Gap #3");
    Assert.IsNull(tm.GetTile(new Vector3Int(27, 0, 0)), "(27,0) must be empty — Gap #3 mid");
    Assert.IsNull(tm.GetTile(new Vector3Int(42, 0, 0)), "(42,0) must be empty — Gap #4");
}

[Test]
public void BuildLevel_PlatformsAreAtCorrectHeight()
{
    var tm = GetGroundTilemap();
    Assert.IsNotNull(tm.GetTile(new Vector3Int(12, 2, 0)), "(12,2) — P1 start");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(15, 2, 0)), "(15,2) — P1 end");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(19, 4, 0)), "(19,4) — P2 start");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(21, 4, 0)), "(21,4) — P2 end");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(26, 2, 0)), "(26,2) — P3 start (stepping stone)");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(28, 2, 0)), "(28,2) — P3 end (stepping stone)");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(45, 2, 0)), "(45,2) — P5 start");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(48, 2, 0)), "(48,2) — P5 end");
}

[Test]
public void BuildLevel_StepPlatformsAscend()
{
    var tm = GetGroundTilemap();
    Assert.IsNotNull(tm.GetTile(new Vector3Int(39, 1, 0)), "(39,1) — Step P4a");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(40, 2, 0)), "(40,2) — Step P4b");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(41, 3, 0)), "(41,3) — Step P4c");
}

[Test]
public void BuildLevel_EndGroundExists()
{
    var tm = GetGroundTilemap();
    Assert.IsNotNull(tm.GetTile(new Vector3Int(49, 0, 0)), "(49,0) — end ground start");
    Assert.IsNotNull(tm.GetTile(new Vector3Int(58, 0, 0)), "(58,0) — end ground finish");
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Expected: all **FAIL** ("Tile at X should exist/be null").

- [ ] **Step 3: Implement `PaintLevel()` in `LevelBuilderTool.cs`**

Update `BuildLevel()` to call `PaintLevel(tilemap)`, and add:

```csharp
[MenuItem("Tools/Build Test Level (DEV-8)")]
public static void BuildLevel()
{
    CreatePlaceholderSprite();
    CreateRuleTile();
    var tilemap = BuildSceneHierarchy();
    PaintLevel(tilemap);
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
}

private static void PaintLevel(Tilemap tilemap)
{
    var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
    tilemap.ClearAllTiles();

    // Ground segments (y=0)
    PaintRow(tilemap, tile,  0,  9, 0); // Start ground
    PaintRow(tilemap, tile, 22, 24, 0); // Short ground
    PaintRow(tilemap, tile, 29, 38, 0); // Long ground
    PaintRow(tilemap, tile, 49, 58, 0); // End ground

    // Platforms
    PaintRow(tilemap, tile, 12, 15, 2); // P1
    PaintRow(tilemap, tile, 19, 21, 4); // P2
    PaintRow(tilemap, tile, 26, 28, 2); // P3 — mid-gap stepping stone
    PaintRow(tilemap, tile, 45, 48, 2); // P5

    // Ascending steps
    tilemap.SetTile(new Vector3Int(39, 1, 0), tile); // P4a
    tilemap.SetTile(new Vector3Int(40, 2, 0), tile); // P4b
    tilemap.SetTile(new Vector3Int(41, 3, 0), tile); // P4c
}

private static void PaintRow(Tilemap tilemap, TileBase tile, int xStart, int xEnd, int y)
{
    for (int x = xStart; x <= xEnd; x++)
        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: all **PASS**.

- [ ] **Step 5: Save scene then check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Editor/LevelBuilderTool.cs`, `Assets/Tests/Editor/LevelBuilderToolTests.cs`, `Assets/Scenes/Platformer.unity` → Summary: `feat: paint linear test level layout with Rule Tiles (DEV-8)` → **Check in**

---

## Task 6: Player spawn position + end-to-end playtest + Jira transition

**Files:**
- Modify: `Assets/Scenes/Platformer.unity` (via Unity Editor Inspector)

- [ ] **Step 1: Run the menu tool once more to build the final level**

Unity Editor → Tools → Build Test Level (DEV-8). Verify tiles appear in Scene view.

- [ ] **Step 2: Confirm Player Y position**

Select the `Player` GameObject in the Hierarchy. In the Inspector, check Transform Position Y. The top surface of a tile at y=0 sits at world Y=0.5 (tile cell center at 0, cell size 1 → top edge at 0.5). The player must spawn above this.

- If Player Y < 1.0: set it to **1.0** (feet land cleanly above the tile top surface).
- Confirm `GroundCheck` child is positioned at the player's feet (Y ≈ -0.4 to -0.5 relative to Player, which puts it near Y=0.5–0.6 world space — just above the tile top).

- [ ] **Step 3: Enter Play Mode and walk through the level**

Press Play. Manually verify each acceptance criterion:

- [ ] Player spawns on the ground without falling through tiles
- [ ] Player can walk left and right on flat ground
- [ ] Player can jump the 2-tile Gap #1 and land on Platform P1 (y=2)
- [ ] Player falls when walking off a platform edge (coyote time window is active)
- [ ] Player can reach Platform P2 at y=4 with a full jump from P1 or the short ground
- [ ] Jump buffering works: press Jump just before landing, player jumps on contact
- [ ] Player can climb the step platforms P4a → P4b → P4c with consecutive jumps
- [ ] No edge-snag artifacts: player walks smoothly across multi-tile ground (CompositeCollider working)

- [ ] **Step 4: Exit Play Mode and save scene**

Cmd+S / Ctrl+S.

- [ ] **Step 5: Final check in (UVCS)**

Unity Editor → Unity Version Control → Pending Changes → select `Assets/Scenes/Platformer.unity` → Summary: `feat: complete DEV-8 tilemap world — test level with Rule Tiles, composite collider, verified in Play Mode` → **Check in**

- [ ] **Step 6: Transition DEV-8 to Done in Jira**

Via Atlassian MCP: transition DEV-8 status to Done.
