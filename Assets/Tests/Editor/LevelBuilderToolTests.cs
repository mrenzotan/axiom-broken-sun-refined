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

    [Test]
    public void BuildLevel_CreatesPlaceholderSpriteAsset()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/Tilemaps/PlaceholderTile.png");
        Assert.IsNotNull(sprite,
            "PlaceholderTile.png should exist as a Sprite asset after BuildLevel");
    }

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
        Assert.IsNotNull(ruleTile, "Precondition: GroundRuleTile.asset must exist");
        Assert.IsNotNull(ruleTile.m_DefaultSprite,
            "GroundRuleTile default sprite must be assigned");
    }

    [Test]
    public void BuildLevel_RuleTileHasEightNeighborRules()
    {
        var ruleTile = AssetDatabase.LoadAssetAtPath<RuleTile>(
            "Assets/Art/Tilemaps/GroundRuleTile.asset");
        Assert.IsNotNull(ruleTile, "Precondition: GroundRuleTile.asset must exist");
        Assert.AreEqual(8, ruleTile.m_TilingRules.Count,
            "GroundRuleTile must have exactly 8 tiling rules covering all 8 neighbor positions");
    }

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
        var grid = GameObject.Find("Grid");
        Assert.IsNotNull(grid, "Precondition: Grid must exist");
        var ground = grid.transform.Find("Ground");
        Assert.IsNotNull(ground, "Ground child must exist under Grid");
        Assert.IsNotNull(ground.GetComponent<Tilemap>(),            "Ground must have Tilemap");
        Assert.IsNotNull(ground.GetComponent<TilemapRenderer>(),    "Ground must have TilemapRenderer");
        Assert.IsNotNull(ground.GetComponent<TilemapCollider2D>(),  "Ground must have TilemapCollider2D");
        Assert.IsNotNull(ground.GetComponent<CompositeCollider2D>(),"Ground must have CompositeCollider2D");
        Assert.IsNotNull(ground.GetComponent<Rigidbody2D>(),        "Ground must have Rigidbody2D");
    }

    [Test]
    public void BuildLevel_GroundTilemapIsOnLayer7()
    {
        var grid = GameObject.Find("Grid");
        Assert.IsNotNull(grid, "Precondition: Grid must exist");
        var ground = grid.transform.Find("Ground");
        Assert.IsNotNull(ground, "Precondition: Ground must exist");
        Assert.AreEqual(LayerMask.NameToLayer("Ground"), ground.gameObject.layer,
            "Ground Tilemap must be on the 'Ground' layer for player GroundCheck to work");
    }

    [Test]
    public void BuildLevel_ColliderUsesCompositeOperation()
    {
        var grid = GameObject.Find("Grid");
        Assert.IsNotNull(grid, "Precondition: Grid must exist");
        var ground = grid.transform.Find("Ground");
        Assert.IsNotNull(ground, "Precondition: Ground must exist");
        var col = ground.GetComponent<TilemapCollider2D>();
        Assert.IsNotNull(col, "Precondition: TilemapCollider2D must exist");
        Assert.AreEqual(Collider2D.CompositeOperation.Merge, col.compositeOperation,
            "TilemapCollider2D must use CompositeOperation.Merge");
    }

    [Test]
    public void BuildLevel_RigidbodyIsStatic()
    {
        var grid = GameObject.Find("Grid");
        Assert.IsNotNull(grid, "Precondition: Grid must exist");
        var ground = grid.transform.Find("Ground");
        Assert.IsNotNull(ground, "Precondition: Ground must exist");
        var rb = ground.GetComponent<Rigidbody2D>();
        Assert.IsNotNull(rb, "Precondition: Rigidbody2D must exist");
        Assert.AreEqual(RigidbodyType2D.Static, rb.bodyType,
            "Rigidbody2D must be Static (required by CompositeCollider2D)");
    }

    private Tilemap GetGroundTilemap()
    {
        var grid = GameObject.Find("Grid");
        Assert.IsNotNull(grid, "Precondition: Grid must exist");
        var ground = grid.transform.Find("Ground");
        Assert.IsNotNull(ground, "Precondition: Ground must exist");
        return ground.GetComponent<Tilemap>();
    }

    [Test]
    public void BuildLevel_StartGroundTilesExist()
    {
        var tm = GetGroundTilemap();
        Assert.IsNotNull(tm.GetTile(new Vector3Int(0, 0, 0)), "Tile at (0,0)  — start ground");
        Assert.IsNotNull(tm.GetTile(new Vector3Int(9, 0, 0)), "Tile at (9,0)  — end of start ground");
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
        Assert.IsNotNull(tm.GetTile(new Vector3Int(26, 2, 0)), "(26,2) — P3 start");
        Assert.IsNotNull(tm.GetTile(new Vector3Int(28, 2, 0)), "(28,2) — P3 end");
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
}
