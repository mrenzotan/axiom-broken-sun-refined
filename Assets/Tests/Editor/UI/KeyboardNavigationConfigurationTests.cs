using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UITests
{
    public class KeyboardNavigationConfigurationTests
    {
        [Test]
        public void EnabledBuildScenes_HaveConfiguredKeyboardNavigation()
        {
            string originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                foreach (string path in EnabledScenePaths())
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    EventSystem[] systems = Object.FindObjectsByType<EventSystem>(
                        FindObjectsInactive.Include);
                    Assert.AreEqual(1, systems.Length, $"{path}: expected exactly one EventSystem.");
                    Assert.IsTrue(systems[0].sendNavigationEvents,
                        $"{path}: EventSystem must send navigation events.");

                    InputSystemUIInputModule module = systems[0].GetComponent<InputSystemUIInputModule>();
                    Assert.IsNotNull(module, $"{path}: missing InputSystemUIInputModule.");
                    Assert.IsTrue(module.enabled, $"{path}: InputSystemUIInputModule is disabled.");
                    Assert.IsNotNull(module.move, $"{path}: UI/Navigate is unassigned.");
                    Assert.IsNotNull(module.submit, $"{path}: UI/Submit is unassigned.");
                    Assert.IsNotNull(module.cancel, $"{path}: UI/Cancel is unassigned.");

                    foreach (Selectable selectable in Object.FindObjectsByType<Selectable>(
                                 FindObjectsInactive.Include))
                        AssertNavigationEnabled(selectable, path);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScene))
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }
        }

        [TestCase("Assets/Prefabs/Core/GameManager.prefab")]
        [TestCase("Assets/Prefabs/Dialogue/DialogueCanvas.prefab")]
        public void SharedUiPrefabs_HaveNavigationEnabled(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                    AssertNavigationEnabled(selectable, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static IEnumerable<string> EnabledScenePaths() =>
            EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);

        private static void AssertNavigationEnabled(Selectable selectable, string assetPath)
        {
            Assert.AreNotEqual(
                Navigation.Mode.None,
                selectable.navigation.mode,
                $"{assetPath}: '{GetHierarchyPath(selectable.transform)}' cannot be reached by keyboard navigation.");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }
            return path;
        }
    }
}
