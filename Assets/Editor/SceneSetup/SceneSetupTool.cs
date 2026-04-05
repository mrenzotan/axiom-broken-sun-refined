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
    private const string BgArtPath = "Assets/Art/Backgrounds";

    [MenuItem("Tools/Setup Scene Lighting & Backgrounds (DEV-11)")]
    public static void SetupScene()
    {
        EnsureSortingLayers();
        CreateGlobalLight();
        CreateBackgroundLayers();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // Stable unique IDs for sorting layers — small fixed integers, collision-safe and deterministic.
    // Unity's built-in Default layer uses 0; these start at 1001 to avoid conflicts.
    private static readonly (string name, int uniqueId)[] RequiredSortingLayers =
    {
        ("Background", 1001),
        ("Midground",  1002),
        ("Foreground", 1003),
    };

    // Desired render order (first = furthest back). Default sits between Midground and Foreground.
    private static readonly string[] SortingLayerOrder =
        { "Background", "Midground", "Default", "Foreground" };

    private static void EnsureSortingLayers()
    {
        var tagManager        = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
        var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");

        // Add any missing layers (appended — order corrected below)
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

        // Reorder so layers render in the correct depth sequence
        EnforceSortingLayerOrder(sortingLayersProp);

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private static void EnforceSortingLayerOrder(SerializedProperty sortingLayersProp)
    {
        int count = sortingLayersProp.arraySize;

        // Snapshot current state
        var names = new string[count];
        var ids   = new int[count];
        for (int i = 0; i < count; i++)
        {
            var e  = sortingLayersProp.GetArrayElementAtIndex(i);
            names[i] = e.FindPropertyRelative("name").stringValue;
            ids[i]   = e.FindPropertyRelative("uniqueID").intValue;
        }

        // Build sorted output: known layers in SortingLayerOrder first, then any others
        var sortedNames = new string[count];
        var sortedIds   = new int[count];
        int outIdx = 0;

        foreach (var desired in SortingLayerOrder)
        {
            for (int i = 0; i < count; i++)
            {
                if (names[i] == desired)
                {
                    sortedNames[outIdx] = names[i];
                    sortedIds[outIdx]   = ids[i];
                    outIdx++;
                    break;
                }
            }
        }

        // Append any layers not covered by SortingLayerOrder
        for (int i = 0; i < count; i++)
        {
            bool inOrder = false;
            foreach (var d in SortingLayerOrder)
                if (names[i] == d) { inOrder = true; break; }
            if (!inOrder)
            {
                sortedNames[outIdx] = names[i];
                sortedIds[outIdx]   = ids[i];
                outIdx++;
            }
        }

        // Write sorted order back
        for (int i = 0; i < count; i++)
        {
            var e = sortingLayersProp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue  = sortedNames[i];
            e.FindPropertyRelative("uniqueID").intValue = sortedIds[i];
        }
    }

    private static void CreateGlobalLight()
    {
        // Remove stale instance to stay idempotent
        var existing = Object.FindAnyObjectByType<Light2D>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var lightGO = new GameObject("Global Light 2D");
        var light   = lightGO.AddComponent<Light2D>();

        light.lightType = Light2D.LightType.Global;
        light.intensity = 0.5f;
        light.color     = new Color(1f, 0.95f, 0.85f); // warm broken-sun tint
        // MarkSceneDirty is called once by SetupScene() after all objects are created
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
            Object.DestroyImmediate(texture);
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
}
#endif
