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
        var light = Object.FindAnyObjectByType<Light2D>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var bgLayers = GameObject.Find("BackgroundLayers");
        if (bgLayers != null) Object.DestroyImmediate(bgLayers);
    }

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

    [Test]
    public void SetupScene_CreatesGlobalLight2D()
    {
        var light = Object.FindAnyObjectByType<Light2D>();
        Assert.IsNotNull(light, "A Light2D component must exist in the scene after SetupScene");
    }

    [Test]
    public void SetupScene_GlobalLight2DIsTypeGlobal()
    {
        var light = Object.FindAnyObjectByType<Light2D>();
        Assert.AreEqual(Light2D.LightType.Global, light.lightType,
            "Light must be type Global to illuminate the entire scene");
    }

    [Test]
    public void SetupScene_GlobalLightIntensityIsHalfForDarkFantasy()
    {
        var light = Object.FindAnyObjectByType<Light2D>();
        Assert.AreEqual(0.5f, light.intensity, 0.01f,
            "Intensity 0.5 gives a dim, dark-fantasy baseline; Phase 7 can tune this");
    }

    [Test]
    public void SetupScene_GlobalLightHasWarmBrokenSunTint()
    {
        var light = Object.FindAnyObjectByType<Light2D>();
        Assert.AreEqual(1f,    light.color.r, 0.01f, "Red channel must be 1.0");
        Assert.AreEqual(0.95f, light.color.g, 0.01f, "Green channel must be 0.95");
        Assert.AreEqual(0.85f, light.color.b, 0.01f, "Blue channel must be 0.85 — warm broken-sun tint");
    }

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
}
