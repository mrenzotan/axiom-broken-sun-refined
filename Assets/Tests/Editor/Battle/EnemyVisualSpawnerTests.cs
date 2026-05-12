using NUnit.Framework;
using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle.Tests
{
    public class EnemyVisualSpawnerTests
    {
        private EnemyVisualSpawner _spawner;
        private GameObject _anchorGo;
        private Transform _anchor;
        private GameObject _fakePrefab;
        private GameObject _fallbackGo;
        private EnemyBattleAnimator _fallback;
        private EnemyData _data;

        [SetUp]
        public void SetUp()
        {
            _spawner = new EnemyVisualSpawner();

            _anchorGo = new GameObject("EnemySpawnAnchor");
            _anchor = _anchorGo.transform;

            // "Prefab template" — a deactivated GameObject with EnemyBattleAnimator
            // attached. Object.Instantiate clones runtime GameObjects in EditMode tests.
            _fakePrefab = new GameObject("FakeBattleVisualPrefab");
            _fakePrefab.AddComponent<EnemyBattleAnimator>();
            _fakePrefab.SetActive(false);

            _fallbackGo = new GameObject("FallbackEnemyAnimator");
            _fallback = _fallbackGo.AddComponent<EnemyBattleAnimator>();

            _data = ScriptableObject.CreateInstance<EnemyData>();
            _data.enemyName = "TestEnemy";
            _data.battleVisualPrefab = _fakePrefab;
        }

        [TearDown]
        public void TearDown()
        {
            if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
            if (_fakePrefab != null) Object.DestroyImmediate(_fakePrefab);
            if (_fallbackGo != null) Object.DestroyImmediate(_fallbackGo);
            if (_data != null) Object.DestroyImmediate(_data);
        }

        [Test]
        public void Spawn_DataNull_ReturnsFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(null, _anchor, _fallback);
            Assert.AreSame(_fallback, result);
            Assert.AreEqual(0, _anchor.childCount,
                "Nothing should be instantiated when EnemyData is null.");
        }

        [Test]
        public void Spawn_PrefabNull_ReturnsFallback()
        {
            _data.battleVisualPrefab = null;
            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);
            Assert.AreSame(_fallback, result);
            Assert.AreEqual(0, _anchor.childCount);
        }

        [Test]
        public void Spawn_AnchorNull_ReturnsFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(_data, null, _fallback);
            Assert.AreSame(_fallback, result);
        }

        [Test]
        public void Spawn_PrefabHasNoEnemyBattleAnimator_ReturnsFallback()
        {
            var noAnimatorPrefab = new GameObject("NoAnimatorPrefab");
            noAnimatorPrefab.SetActive(false);
            _data.battleVisualPrefab = noAnimatorPrefab;

            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);

            Assert.AreSame(_fallback, result,
                "Spawner must return the fallback when the prefab lacks an EnemyBattleAnimator.");

            Object.DestroyImmediate(noAnimatorPrefab);
        }

        [Test]
        public void Spawn_Valid_ParentsInstanceUnderAnchor()
        {
            _spawner.Spawn(_data, _anchor, _fallback);
            Assert.AreEqual(1, _anchor.childCount,
                "Spawned visual prefab should be parented directly under the anchor.");
        }

        [Test]
        public void Spawn_Valid_ReturnsSpawnedAnimator_NotFallback()
        {
            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);
            Assert.IsNotNull(result);
            Assert.AreNotSame(_fallback, result,
                "Should return the spawned instance's animator, not the fallback.");
            Assert.IsTrue(result.transform.IsChildOf(_anchor),
                "Spawned animator should live inside the anchor's hierarchy.");
        }

        [Test]
        public void Spawn_Valid_ResetsLocalPositionToZero()
        {
            // Even if the prefab template has a non-zero local position, the spawned
            // instance must sit exactly at the anchor.
            _fakePrefab.transform.localPosition = new Vector3(5f, 6f, 7f);

            _spawner.Spawn(_data, _anchor, _fallback);

            Transform spawned = _anchor.GetChild(0);
            Assert.AreEqual(Vector3.zero, spawned.localPosition,
                "Spawned instance localPosition should be (0,0,0) so the anchor defines spawn position.");
        }

        [Test]
        public void Spawn_AnimatorOnChildOfPrefab_StillResolved()
        {
            // Mirrors the project's Enemy → Visual (child) sprite-flipping pattern from
            // GAME_PLAN.md §6: the EnemyBattleAnimator lives on a child, not the root.
            var rootPrefab = new GameObject("RootOnlyPrefab");
            rootPrefab.SetActive(false);
            var visualChild = new GameObject("Visual");
            visualChild.transform.SetParent(rootPrefab.transform, worldPositionStays: false);
            visualChild.AddComponent<EnemyBattleAnimator>();

            _data.battleVisualPrefab = rootPrefab;

            EnemyBattleAnimator result = _spawner.Spawn(_data, _anchor, _fallback);

            Assert.IsNotNull(result);
            Assert.AreNotSame(_fallback, result);
            Assert.IsTrue(result.transform.IsChildOf(_anchor));

            Object.DestroyImmediate(rootPrefab);
        }
    }
}
