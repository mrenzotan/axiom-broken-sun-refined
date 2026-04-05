# DEV-11: Basic Scene Setup — URP 2D Lights & Background Layers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure a Global Light 2D ambient source and three parallax-scrolling background layers (far, mid, near) with placeholder sprites in `Platformer.unity`, giving the scene correct URP 2D lighting and a sense of depth.

**Architecture:** An Editor-only `SceneSetupTool.cs` (MenuItem) deterministically creates all scene objects: it writes `Background`, `Midground`, and `Foreground` sorting layers to `TagManager.asset`, adds a `Global Light 2D` with dark-fantasy settings, generates placeholder colored sprites under `Assets/Art/Backgrounds/`, and builds a `BackgroundLayers > FarBackground / MidBackground / NearBackground` hierarchy with `SpriteRenderer` + `ParallaxController` on each child. Runtime parallax logic lives in `ParallaxBackground.cs` (plain C# — offset math only), injected into `ParallaxController.cs` (MonoBehaviour — camera reference + Unity lifecycle).

**Tech Stack:** Unity 6.3 LTS, URP 2D (`UnityEngine.Rendering.Universal.Light2D`), Unity 2D Sprite, NUnit via Unity Test Framework (Edit Mode).

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Editor/SceneSetupTool.cs` | Create | MenuItem; sorting layer creation, Global Light 2D, background hierarchy, placeholder sprites |
| `Assets/Editor/SceneSetupTool.asmdef` | Create | Editor-only assembly def with URP runtime reference |
| `Assets/Scripts/Platformer/ParallaxBackground.cs` | Create | Plain C# — per-layer parallax offset formula |
| `Assets/Scripts/Platformer/ParallaxController.cs` | Create | MonoBehaviour — drives layer position from Camera.main each frame |
| `Assets/Scripts/Platformer/Platformer.asmdef` | Create | Runtime assembly def so `ParallaxBackground` is reachable from test assemblies |
| `Assets/Tests/Editor/SceneSetupToolTests.cs` | Create | Edit Mode tests: sorting layers, light settings, BG hierarchy |
| `Assets/Tests/Editor/SceneSetupTests.asmdef` | Create | Test assembly def referencing SceneSetupTool + Platformer + TestRunner |
| `Assets/Tests/Editor/ParallaxBackgroundTests.cs` | Create | Edit Mode tests for pure parallax math — no Unity scene APIs |
| `Assets/Art/Backgrounds/` | Create dir | `FarBackground.png`, `MidBackground.png`, `NearBackground.png` placeholder sprites |
| `Assets/Scenes/Platformer.unity` | Modify | Saved after running the tool |

---

## API Reference

```csharp
// Light2D (UnityEngine.Rendering.Universal — assembly: Unity.RenderPipelines.Universal.Runtime)
light2D.lightType  = Light2D.LightType.Global;
light2D.intensity  = 0.5f;
light2D.color      = new Color(1f, 0.95f, 0.85f); // warm broken-sun tint

// Sorting Layers via TagManager (Editor only)
var tagManager        = new SerializedObject(
    AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");
// insert element, set .name, .uniqueID, then ApplyModifiedProperties()

// Parallax offset formula (plain C#, no Unity dependency)
// parallaxFactor = 0 → layer moves at full camera speed (pinned to world, no parallax)
// parallaxFactor = 1 → layer never moves in world space (infinitely distant)
float offsetX = startPositionX + cameraX * (1f - parallaxFactor);

// SpriteRenderer — sorting layer assignment
renderer.sortingLayerName = "Background";
renderer.sortingOrder     = 0;
```

---

## Task 1: Scaffold — assembly defs and skeleton files

**Files:**
- Create: `Assets/Editor/SceneSetupTool.asmdef`
- Create: `Assets/Editor/SceneSetupTool.cs`
- Create: `Assets/Scripts/Platformer/Platformer.asmdef`
- Create: `Assets/Scripts/Platformer/ParallaxBackground.cs`
- Create: `Assets/Scripts/Platformer/ParallaxController.cs`
- Create: `Assets/Tests/Editor/SceneSetupTests.asmdef`
- Create: `Assets/Tests/Editor/SceneSetupToolTests.cs`
- Create: `Assets/Tests/Editor/ParallaxBackgroundTests.cs`

- [ ] **Step 1: Create `Assets/Editor/SceneSetupTool.asmdef`**

```json
{
    "name": "SceneSetupTool",
    "references": [
        "Unity.RenderPipelines.Universal.Runtime"
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

- [ ] **Step 2: Create skeleton `Assets/Editor/SceneSetupTool.cs`**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// DEV-11: Sets up URP 2D Global Light and parallax background layers.
/// Delete or supersede when Phase 6 real environment art is added.
/// </summary>
public static class SceneSetupTool
{
    private const string BgArtPath  = "Assets/Art/Backgrounds";
    private const string FarBgPath  = "Assets/Art/Backgrounds/FarBg.png";
    private const string MidBgPath  = "Assets/Art/Backgrounds/MidBg.png";
    private const string NearBgPath = "Assets/Art/Backgrounds/NearBg.png";

    [MenuItem("Tools/Setup Scene Lighting & Backgrounds (DEV-11)")]
    public static void SetupScene()
    {
        // Tasks 2–4 will fill this in
    }
}
#endif
```

- [ ] **Step 3: Create `Assets/Scripts/Platformer/Platformer.asmdef`**

This makes `ParallaxBackground` and `ParallaxController` reachable by name from test assembly definitions.

```json
{
    "name": "Axiom.Platformer",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Create skeleton `Assets/Scripts/Platformer/ParallaxBackground.cs`**

```csharp
/// <summary>
/// Calculates the world-space X offset for a parallax background layer.
/// parallaxFactor 0 = pinned to world (no parallax); 1 = infinitely distant.
/// </summary>
public class ParallaxBackground
{
    public float ParallaxFactor { get; }
    private readonly float _startPositionX;

    public ParallaxBackground(float startPositionX, float parallaxFactor)
    {
        _startPositionX = startPositionX;
        ParallaxFactor  = parallaxFactor;
    }

    public float CalculateOffsetX(float cameraX)
    {
        // Placeholder — Task 5 implements this
        return _startPositionX;
    }
}
```

- [ ] **Step 5: Create skeleton `Assets/Scripts/Platformer/ParallaxController.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Moves this GameObject's X position each frame using parallax offset math.
/// Attach one per background layer. Camera reference resolved via Camera.main.
/// </summary>
public class ParallaxController : MonoBehaviour
{
    [SerializeField] private float parallaxFactor = 0.5f;

    private ParallaxBackground _background;

    private void Start()
    {
        // Initialized in Task 6
    }

    private void Update()
    {
        // Implemented in Task 6
    }
}
```

- [ ] **Step 6: Create `Assets/Tests/Editor/SceneSetupTests.asmdef`**

Test runner assemblies use `optionalUnityReferences` (not `references`) — this matches `LevelBuilderTests.asmdef` and avoids "assembly reference not found" errors in Unity 6. `Axiom.Platformer` is listed so `ParallaxBackground` is resolvable in `ParallaxBackgroundTests`.

```json
{
    "name": "SceneSetupTests",
    "references": [
        "Unity.RenderPipelines.Universal.Runtime",
        "SceneSetupTool",
        "Axiom.Platformer"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
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

- [ ] **Step 7: Create skeleton `Assets/Tests/Editor/SceneSetupToolTests.cs`**

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SceneSetupToolTests
{
    [SetUp]
    public void SetUp() => SceneSetupTool.SetupScene();

    [TearDown]
    public void TearDown()
    {
        // Destroy only scene objects created by the tool; sorting layers are project-level and persist intentionally
        var light = Object.FindFirstObjectByType<Light2D>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var bgLayers = GameObject.Find("BackgroundLayers");
        if (bgLayers != null) Object.DestroyImmediate(bgLayers);
    }

    // Tests added in Tasks 2–4
}
```

- [ ] **Step 8: Create skeleton `Assets/Tests/Editor/ParallaxBackgroundTests.cs`**

```csharp
using NUnit.Framework;

public class ParallaxBackgroundTests
{
    // Tests added in Task 5
}
```

- [ ] **Step 9: Verify zero compile errors**

Unity Editor Console — confirm no errors after reimport.

- [ ] **Step 10: Check in (UVCS)**

Unity Version Control → Pending Changes → select all new files under `Assets/Editor/`, `Assets/Scripts/Platformer/`, `Assets/Tests/Editor/` → Summary: `scaffold: add SceneSetupTool, Platformer asmdef, ParallaxBackground, ParallaxController skeletons for DEV-11` → **Check in**

---

## Task 2: Sorting Layers

**Files:**
- Modify: `Assets/Editor/SceneSetupTool.cs`
- Modify: `Assets/Tests/Editor/SceneSetupToolTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `SceneSetupToolTests`:

```csharp
[Test]
public void SetupScene_CreatesBackgroundSortingLayer()
{
    bool found = false;
    foreach (var layer in UnityEngine.SortingLayer.layers)
        if (layer.name == "Background") { found = true; break; }
    Assert.IsTrue(found, "Sorting layer 'Background' must exist after SetupScene");
}

[Test]
public void SetupScene_CreatesMidgroundSortingLayer()
{
    bool found = false;
    foreach (var layer in UnityEngine.SortingLayer.layers)
        if (layer.name == "Midground") { found = true; break; }
    Assert.IsTrue(found, "Sorting layer 'Midground' must exist after SetupScene");
}

[Test]
public void SetupScene_CreatesForegroundSortingLayer()
{
    bool found = false;
    foreach (var layer in UnityEngine.SortingLayer.layers)
        if (layer.name == "Foreground") { found = true; break; }
    Assert.IsTrue(found, "Sorting layer 'Foreground' must exist after SetupScene");
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Unity Editor → Window → General → Test Runner → EditMode → run all three new tests.
Expected: **FAIL** — "Sorting layer 'X' must exist after SetupScene"

- [ ] **Step 3: Implement `EnsureSortingLayers()` in `SceneSetupTool.cs`**

Add the method and call it from `SetupScene()`:

```csharp
[MenuItem("Tools/Setup Scene Lighting & Backgrounds (DEV-11)")]
public static void SetupScene()
{
    EnsureSortingLayers();
}

// Stable unique IDs for sorting layers — small fixed integers, collision-safe and deterministic.
// Unity's built-in Default layer uses 0; these start at 1001 to avoid conflicts.
private static readonly (string name, int uniqueId)[] RequiredSortingLayers =
{
    ("Background", 1001),
    ("Midground",  1002),
    ("Foreground", 1003),
};

private static void EnsureSortingLayers()
{
    var tagManager        = new SerializedObject(
        AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
    var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");

    foreach (var (layerName, uniqueId) in RequiredSortingLayers)
    {
        bool exists = false;
        for (int i = 0; i < sortingLayersProp.arraySize; i++)
        {
            if (sortingLayersProp.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("name").stringValue == layerName)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            sortingLayersProp.InsertArrayElementAtIndex(sortingLayersProp.arraySize);
            var newLayer = sortingLayersProp.GetArrayElementAtIndex(
                sortingLayersProp.arraySize - 1);
            newLayer.FindPropertyRelative("name").stringValue  = layerName;
            newLayer.FindPropertyRelative("uniqueID").intValue = uniqueId;
        }
    }

    tagManager.ApplyModifiedProperties();
    AssetDatabase.SaveAssets();
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: **PASS** for all three sorting layer tests.
Note: sorting layers persist in `ProjectSettings/TagManager.asset` — this is intentional.

- [ ] **Step 5: Check in (UVCS)**

→ select `Assets/Editor/SceneSetupTool.cs`, `Assets/Tests/Editor/SceneSetupToolTests.cs`, `ProjectSettings/TagManager.asset` → Summary: `feat: create Background/Midground/Foreground sorting layers for DEV-11` → **Check in**

---

## Task 3: Global Light 2D

**Files:**
- Modify: `Assets/Editor/SceneSetupTool.cs`
- Modify: `Assets/Tests/Editor/SceneSetupToolTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `SceneSetupToolTests`:

```csharp
[Test]
public void SetupScene_CreatesGlobalLight2D()
{
    var light = Object.FindFirstObjectByType<Light2D>();
    Assert.IsNotNull(light, "A Light2D component must exist in the scene after SetupScene");
}

[Test]
public void SetupScene_GlobalLight2DIsTypeGlobal()
{
    var light = Object.FindFirstObjectByType<Light2D>();
    Assert.AreEqual(Light2D.LightType.Global, light.lightType,
        "Light must be type Global to illuminate the entire scene");
}

[Test]
public void SetupScene_GlobalLightIntensityIsHalfForDarkFantasy()
{
    var light = Object.FindFirstObjectByType<Light2D>();
    Assert.AreEqual(0.5f, light.intensity, 0.01f,
        "Intensity 0.5 gives a dim, dark-fantasy baseline; Phase 7 can tune this");
}

[Test]
public void SetupScene_GlobalLightHasWarmBrokenSunTint()
{
    var light = Object.FindFirstObjectByType<Light2D>();
    Assert.AreEqual(1f,    light.color.r, 0.01f, "Red channel must be 1.0");
    Assert.AreEqual(0.95f, light.color.g, 0.01f, "Green channel must be 0.95");
    Assert.AreEqual(0.85f, light.color.b, 0.01f, "Blue channel must be 0.85 — warm broken-sun tint");
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Expected: **FAIL** — "A Light2D component must exist in the scene after SetupScene"

- [ ] **Step 3: Implement `CreateGlobalLight()` in `SceneSetupTool.cs`**

```csharp
[MenuItem("Tools/Setup Scene Lighting & Backgrounds (DEV-11)")]
public static void SetupScene()
{
    EnsureSortingLayers();
    CreateGlobalLight();
}

private static void CreateGlobalLight()
{
    // Remove stale instance to stay idempotent
    var existing = Object.FindFirstObjectByType<Light2D>();
    if (existing != null) Object.DestroyImmediate(existing.gameObject);

    var lightGO = new GameObject("Global Light 2D");
    var light   = lightGO.AddComponent<Light2D>();

    light.lightType = Light2D.LightType.Global;
    light.intensity = 0.5f;
    light.color     = new Color(1f, 0.95f, 0.85f); // warm broken-sun tint
    // MarkSceneDirty is called once by SetupScene() after all objects are created
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: all four light tests **PASS**.

- [ ] **Step 5: Check in (UVCS)**

→ select `Assets/Editor/SceneSetupTool.cs`, `Assets/Tests/Editor/SceneSetupToolTests.cs` → Summary: `feat: create Global Light 2D with dark-fantasy intensity and broken-sun tint for DEV-11` → **Check in**

---

## Task 4: Background Layer Hierarchy + Placeholder Sprites

**Files:**
- Modify: `Assets/Editor/SceneSetupTool.cs`
- Modify: `Assets/Tests/Editor/SceneSetupToolTests.cs`

Layer specification:

| Child Name | Sorting Layer | Order | Parallax Factor | Color (RGBA) | World Y |
|---|---|---|---|---|---|
| `FarBackground` | Background | 0 | 0.9 | `(0.08, 0.06, 0.12, 1)` — deep indigo night | 0 |
| `MidBackground` | Midground | 0 | 0.7 | `(0.14, 0.10, 0.18, 1)` — dark purple dusk | 0 |
| `NearBackground` | Midground | 1 | 0.5 | `(0.18, 0.15, 0.22, 1)` — muted violet haze | 0 |

All layers are scaled to 200 × 20 world units so they fill any reasonable camera framing. Phase 6 swaps these out for real art.

- [ ] **Step 1: Write failing tests**

Add to `SceneSetupToolTests`:

```csharp
[Test]
public void SetupScene_CreatesBackgroundLayersParent()
{
    var parent = GameObject.Find("BackgroundLayers");
    Assert.IsNotNull(parent, "BackgroundLayers parent GameObject must exist after SetupScene");
    Assert.AreEqual(3, parent.transform.childCount,
        "BackgroundLayers must have exactly 3 children (Far, Mid, Near)");
}

[Test]
public void SetupScene_FarBackgroundHasCorrectSortingLayer()
{
    var far = GameObject.Find("BackgroundLayers").transform.Find("FarBackground");
    Assert.IsNotNull(far, "FarBackground child must exist");
    var renderer = far.GetComponent<SpriteRenderer>();
    Assert.IsNotNull(renderer, "FarBackground must have a SpriteRenderer");
    Assert.AreEqual("Background", renderer.sortingLayerName,
        "FarBackground must be on sorting layer 'Background'");
}

[Test]
public void SetupScene_MidBackgroundHasMidgroundSortingLayer()
{
    var mid = GameObject.Find("BackgroundLayers").transform.Find("MidBackground");
    var renderer = mid.GetComponent<SpriteRenderer>();
    Assert.AreEqual("Midground", renderer.sortingLayerName,
        "MidBackground must be on sorting layer 'Midground'");
}

[Test]
public void SetupScene_NearBackgroundHasMidgroundSortingLayer()
{
    var near = GameObject.Find("BackgroundLayers").transform.Find("NearBackground");
    var renderer = near.GetComponent<SpriteRenderer>();
    Assert.AreEqual("Midground", renderer.sortingLayerName,
        "NearBackground must be on sorting layer 'Midground', order 1 (in front of Mid)");
}

[Test]
public void SetupScene_AllBackgroundsHaveSprites()
{
    var parent = GameObject.Find("BackgroundLayers");
    foreach (Transform child in parent.transform)
    {
        var renderer = child.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(renderer.sprite,
            $"{child.name} SpriteRenderer must have a sprite assigned");
    }
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Expected: **FAIL** — "BackgroundLayers parent GameObject must exist after SetupScene"

- [ ] **Step 3: Implement `CreateBackgroundLayers()` in `SceneSetupTool.cs`**

```csharp
[MenuItem("Tools/Setup Scene Lighting & Backgrounds (DEV-11)")]
public static void SetupScene()
{
    EnsureSortingLayers();
    CreateGlobalLight();
    CreateBackgroundLayers();
    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
}

private static readonly (string name, string sortingLayer, int sortingOrder, float parallaxFactor, Color color)[] BackgroundLayerDefs =
{
    ("FarBackground",  "Background", 0, 0.9f, new Color(0.08f, 0.06f, 0.12f)),
    ("MidBackground",  "Midground",  0, 0.7f, new Color(0.14f, 0.10f, 0.18f)),
    ("NearBackground", "Midground",  1, 0.5f, new Color(0.18f, 0.15f, 0.22f)),
};

private static void CreateBackgroundLayers()
{
    // Remove stale instance
    var existing = GameObject.Find("BackgroundLayers");
    if (existing != null) Object.DestroyImmediate(existing);

    System.IO.Directory.CreateDirectory(BgArtPath);

    var parent = new GameObject("BackgroundLayers");

    foreach (var (layerName, sortingLayer, sortingOrder, parallaxFactor, color) in BackgroundLayerDefs)
    {
        var spritePath = $"{BgArtPath}/{layerName}.png";
        EnsurePlaceholderSprite(spritePath, color);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        var go       = new GameObject(layerName);
        go.transform.SetParent(parent.transform, false);
        // Scale to fill a wide area — Phase 6 swaps real art in
        go.transform.localScale = new Vector3(200f, 20f, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite           = sprite;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder     = sortingOrder;

        var controller = go.AddComponent<ParallaxController>();
        // Set the serialized field via SerializedObject so it's saved to the scene
        var so = new SerializedObject(controller);
        so.FindProperty("parallaxFactor").floatValue = parallaxFactor;
        so.ApplyModifiedProperties();
    }
}

private static void EnsurePlaceholderSprite(string path, Color color)
{
    if (!System.IO.File.Exists(path))
    {
        var texture = new Texture2D(64, 64);
        var pixels  = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        texture.SetPixels(pixels);
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
    }

    // Import unconditionally — ensures the asset exists in the AssetDatabase before GetAtPath
    AssetDatabase.ImportAsset(path);

    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
    if (importer == null)
    {
        Debug.LogError($"[SceneSetupTool] Failed to get TextureImporter for {path}");
        return;
    }

    importer.textureType         = TextureImporterType.Sprite;
    importer.spriteImportMode    = SpriteImportMode.Single;
    importer.spritePixelsPerUnit = 16;
    importer.filterMode          = FilterMode.Point;
    importer.textureCompression  = TextureImporterCompression.Uncompressed;
    importer.wrapMode            = TextureWrapMode.Clamp;
    importer.SaveAndReimport();
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: all five background tests **PASS**.

- [ ] **Step 5: Check in (UVCS)**

→ select `Assets/Editor/SceneSetupTool.cs`, `Assets/Tests/Editor/SceneSetupToolTests.cs`, `Assets/Art/Backgrounds/` → Summary: `feat: create BackgroundLayers hierarchy with placeholder sprites and sorting layers for DEV-11` → **Check in**

---

## Task 5: ParallaxBackground — pure offset logic (TDD)

**Files:**
- Modify: `Assets/Scripts/Platformer/ParallaxBackground.cs`
- Modify: `Assets/Tests/Editor/ParallaxBackgroundTests.cs`

- [ ] **Step 1: Write failing tests**

Replace `ParallaxBackgroundTests.cs` with:

```csharp
using NUnit.Framework;

public class ParallaxBackgroundTests
{
    [Test]
    public void CalculateOffsetX_ReturnsStartPosition_WhenCameraAtZero()
    {
        var layer = new ParallaxBackground(startPositionX: 5f, parallaxFactor: 0.5f);
        Assert.AreEqual(5f, layer.CalculateOffsetX(cameraX: 0f), 0.001f,
            "At camera X=0 the layer must sit at its start position");
    }

    [Test]
    public void CalculateOffsetX_LayerMovesAtHalfCameraSpeed_ForFactor0point5()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.5f);
        // travel = cameraX * (1 - 0.5) = 10 * 0.5 = 5
        Assert.AreEqual(5f, layer.CalculateOffsetX(cameraX: 10f), 0.001f,
            "Factor 0.5: layer should move at half the camera speed");
    }

    [Test]
    public void CalculateOffsetX_LayerDoesNotMove_WhenFactorIsOne()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 1.0f);
        // travel = cameraX * (1 - 1) = 0 — layer pinned in world space (infinitely far)
        Assert.AreEqual(0f, layer.CalculateOffsetX(cameraX: 100f), 0.001f,
            "Factor 1.0: layer must stay at startPositionX regardless of camera movement");
    }

    [Test]
    public void CalculateOffsetX_LayerMovesWithCamera_WhenFactorIsZero()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.0f);
        // travel = cameraX * (1 - 0) = cameraX — layer tracks camera exactly (no parallax)
        Assert.AreEqual(10f, layer.CalculateOffsetX(cameraX: 10f), 0.001f,
            "Factor 0: layer moves at full camera speed (pinned to world)");
    }

    [Test]
    public void CalculateOffsetX_WorksNegativeCameraDirection()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.7f);
        // travel = -10 * (1 - 0.7) = -3
        Assert.AreEqual(-3f, layer.CalculateOffsetX(cameraX: -10f), 0.001f,
            "Parallax must work when camera moves left (negative X)");
    }

    [Test]
    public void ParallaxFactor_IsPreservedFromConstructor()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.9f);
        Assert.AreEqual(0.9f, layer.ParallaxFactor, 0.001f);
    }
}
```

- [ ] **Step 2: Run tests — verify all FAIL**

Unity Test Runner → EditMode → run all six `ParallaxBackgroundTests`.
Expected: **FAIL** — skeleton returns `_startPositionX` for all cases.

- [ ] **Step 3: Implement `CalculateOffsetX` in `ParallaxBackground.cs`**

```csharp
/// <summary>
/// Calculates the world-space X offset for a parallax background layer.
/// parallaxFactor 0 = pinned to world (moves with camera); 1 = infinitely distant (stays put).
/// </summary>
public class ParallaxBackground
{
    public float ParallaxFactor { get; }
    private readonly float _startPositionX;

    public ParallaxBackground(float startPositionX, float parallaxFactor)
    {
        _startPositionX = startPositionX;
        ParallaxFactor  = parallaxFactor;
    }

    /// <param name="cameraX">Camera's current world-space X position.</param>
    /// <returns>World-space X position for this background layer.</returns>
    public float CalculateOffsetX(float cameraX)
    {
        return _startPositionX + cameraX * (1f - ParallaxFactor);
    }
}
```

- [ ] **Step 4: Run tests — verify all PASS**

Expected: all six tests **PASS**.

- [ ] **Step 5: Check in (UVCS)**

→ select `Assets/Scripts/Platformer/ParallaxBackground.cs`, `Assets/Tests/Editor/ParallaxBackgroundTests.cs` → Summary: `feat: implement ParallaxBackground offset formula with full test coverage for DEV-11` → **Check in**

---

## Task 6: ParallaxController MonoBehaviour

**Files:**
- Modify: `Assets/Scripts/Platformer/ParallaxController.cs`

No unit tests for `ParallaxController` — it only wires `Camera.main` and `ParallaxBackground` together. It is verified in the play-mode playtest (Task 7).

- [ ] **Step 1: Implement `ParallaxController.cs`**

Replace the skeleton with:

```csharp
using UnityEngine;

/// <summary>
/// Drives this background layer's X position each frame using parallax offset math.
/// Attach one instance per background layer. Camera resolved automatically via Camera.main.
/// </summary>
public class ParallaxController : MonoBehaviour
{
    [SerializeField] private float parallaxFactor = 0.5f;

    private ParallaxBackground _background;
    private Transform          _cameraTransform;

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.LogError("[ParallaxController] Camera.main not found — parallax disabled.");
            enabled = false;
            return;
        }
        _cameraTransform = Camera.main.transform;
        _background      = new ParallaxBackground(transform.position.x, parallaxFactor);
    }

    private void Update()
    {
        float newX = _background.CalculateOffsetX(_cameraTransform.position.x);
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
    }
}
```

- [ ] **Step 2: Verify zero compile errors**

Unity Editor Console — no errors after reimport.

- [ ] **Step 3: Check in (UVCS)**

→ select `Assets/Scripts/Platformer/ParallaxController.cs` → Summary: `feat: implement ParallaxController MonoBehaviour for per-layer parallax scrolling (DEV-11)` → **Check in**

---

## Task 7: Run the tool, playtest, and close DEV-11

**Files:**
- Modify: `Assets/Scenes/Platformer.unity` (via Unity Editor)

- [ ] **Step 1: Open `Platformer.unity` in Unity Editor**

File → Open Scene → `Assets/Scenes/Platformer.unity`

- [ ] **Step 2: Run the setup tool**

Unity Editor → Tools → **Setup Scene Lighting & Backgrounds (DEV-11)**

Verify in the Hierarchy panel:
- `Global Light 2D` GameObject exists at root
- `BackgroundLayers` GameObject exists with three children: `FarBackground`, `MidBackground`, `NearBackground`

- [ ] **Step 3: Verify sorting layers in Tags & Layers**

Edit → Project Settings → Tags and Layers → Sorting Layers.
Expected: `Background`, `Midground`, `Default`, `Foreground` all present.

- [ ] **Step 4: Verify Light 2D Inspector**

Select `Global Light 2D` in the Hierarchy. Inspector must show:
- Light Type: **Global**
- Intensity: **0.5**
- Color: warm off-white (1, 0.95, 0.85)

- [ ] **Step 5: Verify background layers in Scene view**

Select each child of `BackgroundLayers` in the Hierarchy. Each must show:
- A `SpriteRenderer` with the correct placeholder sprite (dark indigo / purple / violet)
- Correct `Sorting Layer` and `Order in Layer` values per the Task 4 table
- A `ParallaxController` component with the expected `Parallax Factor` value
- Transform Scale: (200, 20, 1)

- [ ] **Step 6: Enter Play Mode — verify parallax scrolling**

Press Play. Move the player left and right across the level.

Manually verify:
- [ ] Scene is lit — tiles and player sprite visible (not black/unlit)
- [ ] All three background layers are visible behind the tilemap
- [ ] `FarBackground` moves the least as the camera follows the player (factor 0.9 — nearly pinned)
- [ ] `MidBackground` moves at a medium rate (factor 0.7)
- [ ] `NearBackground` moves the most (factor 0.5)
- [ ] No console errors from `ParallaxController` (Camera.main resolves correctly)
- [ ] No frame-rate impact — parallax is a single Vector3 assignment per layer per frame

- [ ] **Step 7: Exit Play Mode and save scene (Cmd+S / Ctrl+S)**

- [ ] **Step 8: Final check in (UVCS)**

→ select `Assets/Scenes/Platformer.unity`, `Assets/Art/Backgrounds/` → Summary: `feat: complete DEV-11 — Global Light 2D, sorting layers, parallax background layers, verified in Play Mode` → **Check in**

- [ ] **Step 9: Transition DEV-11 to Done in Jira**

Via Atlassian MCP: transition DEV-11 status to Done.
