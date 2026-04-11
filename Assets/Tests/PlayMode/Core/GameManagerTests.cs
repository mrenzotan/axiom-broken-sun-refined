using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Axiom.Core;

namespace CoreTests.PlayMode
{
    public class GameManagerTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
                Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Awake_SetsSingletonInstance()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null; // wait one frame for Awake

            Assert.AreEqual(gm, GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator Awake_InitializesPlayerState()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            yield return null;

            Assert.IsNotNull(GameManager.Instance.PlayerState);
            Assert.Greater(GameManager.Instance.PlayerState.MaxHp, 0);
            Assert.Greater(GameManager.Instance.PlayerState.MaxMp, 0);
        }

        [UnityTest]
        public IEnumerator Awake_DestroysDuplicateInstance_KeepsFirst()
        {
            var go1 = new GameObject("GameManager1");
            var first = go1.AddComponent<GameManager>();
            yield return null;

            var go2 = new GameObject("GameManager2");
            go2.AddComponent<GameManager>();
            yield return null;

            // First instance must still be the singleton
            Assert.AreEqual(first, GameManager.Instance);
            // First GameObject must not have been destroyed
            Assert.IsTrue(go1 != null);
        }

        [UnityTest]
        public IEnumerator OnDestroy_ClearsInstance()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            yield return null;

            Object.Destroy(go);
            yield return null;

            Assert.IsNull(GameManager.Instance);
        }
    }
}
