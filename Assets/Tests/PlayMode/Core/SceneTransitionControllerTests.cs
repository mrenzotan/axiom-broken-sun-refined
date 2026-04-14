using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Axiom.Core;

namespace Axiom.Tests.PlayMode.Core
{
    public class SceneTransitionControllerTests
    {
        private GameObject _go;
        private SceneTransitionController _controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("Controller");
            var imageGo = new GameObject("Image");
            imageGo.transform.SetParent(_go.transform);
            var image = imageGo.AddComponent<Image>();

            _controller = _go.AddComponent<SceneTransitionController>();
            yield return null; // Allow Awake to run

            // Wire the overlay image via reflection (it's a [SerializeField] meant for Inspector use).
            typeof(SceneTransitionController)
                .GetField("_overlayImage", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, image);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null)
                Object.Destroy(_go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IsTransitioning_DefaultsFalse()
        {
            Assert.IsFalse(_controller.IsTransitioning);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeginTransition_SetsIsTransitioning_True_Immediately()
        {
            // NOTE: Passing a non-existent scene name causes LoadSceneAsync to log a warning
            // and stall at progress < 0.9 — IsTransitioning stays true while we observe it.
            _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.BlackFade);

            // IsTransitioning should be true before the coroutine's first yield returns.
            // We observe after one frame — coroutine has started but fade is still running.
            yield return null;

            Assert.IsTrue(_controller.IsTransitioning);
        }

        [UnityTest]
        public IEnumerator BeginTransition_IsNoOp_WhenAlreadyTransitioning()
        {
            _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.BlackFade);
            yield return null;

            // Second call while transitioning — should not reset or throw.
            Assert.DoesNotThrow(() =>
                _controller.BeginTransition("__nonexistent_test_scene__", TransitionStyle.WhiteFlash));

            Assert.IsTrue(_controller.IsTransitioning);
        }
    }
}
