using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Axiom.Core;
using Axiom.Data;

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

        private static CharacterData CreateTestCharacterData()
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = 100;
            cd.baseMaxMP = 50;
            cd.baseATK   = 10;
            cd.baseDEF   = 5;
            cd.baseSPD   = 8;
            return cd;
        }

        [UnityTest]
        public IEnumerator Awake_SetsSingletonInstance()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            yield return null;

            Assert.AreEqual(gm, GameManager.Instance);
        }

        [UnityTest]
        public IEnumerator PlayerState_IsLazilyInitialized_FromCharacterData()
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>();
            gm.SetPlayerCharacterDataForTests(CreateTestCharacterData());
            yield return null;

            Assert.IsNotNull(gm.PlayerState);
            Assert.Greater(gm.PlayerState.MaxHp, 0);
            Assert.Greater(gm.PlayerState.MaxMp, 0);
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

            Assert.AreEqual(first, GameManager.Instance);
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
