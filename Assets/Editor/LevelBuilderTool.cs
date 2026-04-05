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
        CreatePlaceholderSprite();
        CreateRuleTile();
        var tilemap = BuildSceneHierarchy();
        PaintLevel(tilemap);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            System.IO.File.WriteAllBytes(PlaceholderSpritePath, png);
            AssetDatabase.ImportAsset(PlaceholderSpritePath);
            AssetDatabase.Refresh();  // ensure asset is registered before GetAtPath

            var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderSpritePath);
            importer.textureType          = TextureImporterType.Sprite;
            importer.spriteImportMode     = SpriteImportMode.Single;
            importer.spritePixelsPerUnit  = 16;
            importer.filterMode           = FilterMode.Point;
            importer.textureCompression   = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void CreateRuleTile()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[LevelBuilderTool] Sprite not found at {PlaceholderSpritePath}. Run BuildLevel() to generate it first.");
            return;
        }

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
            (new Vector3Int(0,  1, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int(0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));   // top-surface tile

        // Cardinal: above + below
        AddRule(ruleTile, sprite,
            (new Vector3Int(0,  1, 0), RuleTile.TilingRuleOutput.Neighbor.This),
            (new Vector3Int(0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));   // interior tile

        // Cardinal: left (-1, 0, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int(-1, 0, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int( 1, 0, 0), RuleTile.TilingRuleOutput.Neighbor.This));   // left-edge tile

        // Cardinal: right (1, 0, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int( 1, 0, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int(-1, 0, 0), RuleTile.TilingRuleOutput.Neighbor.This));   // right-edge tile

        // Diagonal: top-left (-1, 1, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int(-1,  1, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int( 0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));  // top-left concave corner

        // Diagonal: top-right (1, 1, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int(1,  1, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int(0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));   // top-right concave corner

        // Diagonal: bottom-left (-1, -1, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int(-1, -1, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int(-1,  0, 0), RuleTile.TilingRuleOutput.Neighbor.This),
            (new Vector3Int( 0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));  // bottom-left inner corner

        // Diagonal: bottom-right (1, -1, 0)
        AddRule(ruleTile, sprite,
            (new Vector3Int( 1, -1, 0), RuleTile.TilingRuleOutput.Neighbor.NotThis),
            (new Vector3Int( 1,  0, 0), RuleTile.TilingRuleOutput.Neighbor.This),
            (new Vector3Int( 0, -1, 0), RuleTile.TilingRuleOutput.Neighbor.This));  // bottom-right inner corner

        EditorUtility.SetDirty(ruleTile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Tilemap BuildSceneHierarchy()
    {
        // Remove old placeholder Ground if it has no Tilemap (pre-DEV-8 scene object)
        var oldGround = GameObject.Find("Ground");
        if (oldGround != null && oldGround.GetComponent<Tilemap>() == null)
            Object.DestroyImmediate(oldGround);

        // Rebuild Grid fresh every call to stay idempotent
        var existingGrid = GameObject.Find("Grid");
        if (existingGrid != null)
            Object.DestroyImmediate(existingGrid);

        var gridGO = new GameObject("Grid");
        gridGO.AddComponent<Grid>();

        var groundGO = new GameObject("Ground");
        groundGO.transform.SetParent(gridGO.transform, false);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
            Debug.LogError("[LevelBuilderTool] Layer 'Ground' not found in Project Settings. Add it under Edit → Project Settings → Tags and Layers.");
        groundGO.layer = groundLayer; // Must match player GroundCheck layer mask

        groundGO.AddComponent<Tilemap>();
        groundGO.AddComponent<TilemapRenderer>();

        var col = groundGO.AddComponent<TilemapCollider2D>();
        col.compositeOperation = Collider2D.CompositeOperation.Merge;

        var rb = groundGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        groundGO.AddComponent<CompositeCollider2D>(); // Must be added after Rigidbody2D

        return groundGO.GetComponent<Tilemap>();
    }

    private static void PaintLevel(Tilemap tilemap)
    {
        var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(RuleTilePath);
        if (tile == null)
        {
            Debug.LogError($"[LevelBuilderTool] RuleTile not found at {RuleTilePath}. Run BuildLevel() to generate it first.");
            return;
        }
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

    private static void AddRule(RuleTile ruleTile, Sprite sprite,
        params (Vector3Int pos, int neighbor)[] neighbors)
    {
        var rule = new RuleTile.TilingRule();
        rule.m_Sprites[0] = sprite;
        rule.m_Output     = RuleTile.TilingRuleOutput.OutputSprite.Single;

        foreach (var (pos, n) in neighbors)
        {
            rule.m_NeighborPositions.Add(pos);
            rule.m_Neighbors.Add(n);
        }

        ruleTile.m_TilingRules.Add(rule);
    }
}
