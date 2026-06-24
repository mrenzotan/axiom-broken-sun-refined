#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReplaceManaRootFont
{
    private const string OldFontPath = "Assets/TextMesh Pro/Fonts/Tiny RPG - Mana Root SDF.asset";
    private const string NewFontPath = "Assets/TextMesh Pro/Fonts/m5x7 Bitmap.asset";

    [MenuItem("Tools/UI/Replace Mana Root Font With M5x7")]
    private static void ReplaceFont()
    {
        TMP_FontAsset oldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldFontPath);
        TMP_FontAsset newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontPath);

        if (oldFont == null)
        {
            Debug.LogError($"[ReplaceManaRootFont] Could not load old font at path: {OldFontPath}");
            return;
        }

        if (newFont == null)
        {
            Debug.LogError($"[ReplaceManaRootFont] Could not load new font at path: {NewFontPath}");
            return;
        }

        if (oldFont == newFont)
        {
            Debug.LogWarning("[ReplaceManaRootFont] Old font and new font are the same asset. Nothing changed.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        int changed = 0;

        try
        {
            changed += ReplaceInPrefabs(oldFont, newFont);
            changed += ReplaceInScenes(oldFont, newFont);
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (originalSceneSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ReplaceManaRootFont] Replaced {changed} TextMeshPro font reference(s).");
    }

    private static int ReplaceInPrefabs(TMP_FontAsset oldFont, TMP_FontAsset newFont)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int changed = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar(
                "Replacing TMP Fonts",
                $"Checking prefab {i + 1} of {prefabGuids.Length}: {path}",
                prefabGuids.Length == 0 ? 1f : (float)i / prefabGuids.Length
            );

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            int prefabChanges = ReplaceInChildren(prefabRoot, oldFont, newFont);

            if (prefabChanges > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                changed += prefabChanges;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return changed;
    }

    private static int ReplaceInScenes(TMP_FontAsset oldFont, TMP_FontAsset newFont)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int changed = 0;

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar(
                "Replacing TMP Fonts",
                $"Checking scene {i + 1} of {sceneGuids.Length}: {path}",
                sceneGuids.Length == 0 ? 1f : (float)i / sceneGuids.Length
            );

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int sceneChanges = ReplaceInOpenScene(oldFont, newFont);

            if (sceneChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed += sceneChanges;
            }
        }

        return changed;
    }

    private static int ReplaceInOpenScene(TMP_FontAsset oldFont, TMP_FontAsset newFont)
    {
        TMP_Text[] textComponents = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);

        int changed = 0;

        foreach (TMP_Text text in textComponents)
        {
            if (text.font != oldFont)
                continue;

            text.font = newFont;
            EditorUtility.SetDirty(text);
            changed++;
        }

        return changed;
    }

    private static int ReplaceInChildren(GameObject root, TMP_FontAsset oldFont, TMP_FontAsset newFont)
    {
        TMP_Text[] textComponents = root.GetComponentsInChildren<TMP_Text>(true);
        int changed = 0;

        foreach (TMP_Text text in textComponents)
        {
            if (text.font != oldFont)
                continue;

            text.font = newFont;
            EditorUtility.SetDirty(text);
            changed++;
        }

        return changed;
    }
}
#endif
