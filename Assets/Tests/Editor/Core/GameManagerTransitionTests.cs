using NUnit.Framework;
using UnityEngine;
using Axiom.Core;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerTransitionTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            // Destroy any stale Instance from an interrupted previous run so the
            // singleton guard in Awake never fires unexpectedly.
            if (GameManager.Instance != null)
                Object.DestroyImmediate(GameManager.Instance.gameObject);

            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void SceneTransition_IsNull_WhenNoChildController()
        {
            // GameManager has no SceneTransitionController child — should be null.
            Assert.IsNull(_gm.SceneTransition);
        }

        [Test]
        public void OnSceneReady_IsRaised_ByRaiseSceneReady()
        {
            bool fired = false;
            _gm.OnSceneReady += () => fired = true;

            _gm.RaiseSceneReady();

            Assert.IsTrue(fired);
        }

        [Test]
        public void RaiseSceneReady_DoesNotThrow_WhenNoSubscribers()
        {
            Assert.DoesNotThrow(() => _gm.RaiseSceneReady());
        }

        [Test]
        public void SceneTransition_IsAssigned_WhenChildControllerExists()
        {
            // Destroy and recreate so Awake runs with the child present.
            Object.DestroyImmediate(_go);
            _go = new GameObject("GameManager");
            var childGo = new GameObject("Child");
            childGo.transform.SetParent(_go.transform);
            childGo.AddComponent<SceneTransitionController>();
            _gm = _go.AddComponent<GameManager>();

            Assert.IsNotNull(_gm.SceneTransition);
        }
    }
}
