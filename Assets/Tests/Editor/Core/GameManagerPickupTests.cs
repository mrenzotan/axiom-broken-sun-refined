using System;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerPickupTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                UnityEngine.Object.DestroyImmediate(_gameManagerObject);
        }

        [Test]
        public void IsPickupCollected_ReturnsFalse_ByDefault()
        {
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_01"));
        }

        [Test]
        public void IsPickupCollected_ReturnsFalse_ForNullOrEmpty()
        {
            Assert.IsFalse(_gameManager.IsPickupCollected(null));
            Assert.IsFalse(_gameManager.IsPickupCollected(string.Empty));
        }

        [Test]
        public void MarkPickupCollected_ThenIsPickupCollected_ReturnsTrue()
        {
            _gameManager.MarkPickupCollected("pickup_01");
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_01"));
        }

        [Test]
        public void MarkPickupCollected_NullOrEmpty_IsNoOp()
        {
            _gameManager.MarkPickupCollected(null);
            _gameManager.MarkPickupCollected(string.Empty);
            Assert.IsFalse(_gameManager.IsPickupCollected("any"));
        }

        [Test]
        public void CollectedPickupIds_IsEmpty_ByDefault()
        {
            using var enumerator = _gameManager.CollectedPickupIds.GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());
        }

        [Test]
        public void CollectedPickupIds_ReflectsMarkPickupCollected()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.MarkPickupCollected("pickup_b");
            var ids = new List<string>(_gameManager.CollectedPickupIds);
            CollectionAssert.AreEquivalent(new[] { "pickup_a", "pickup_b" }, ids);
        }

        [Test]
        public void RestoreCollectedPickups_ReplacesExistingSet()
        {
            _gameManager.MarkPickupCollected("stale");
            _gameManager.RestoreCollectedPickups(new[] { "pickup_x", "pickup_y" });
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_x"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_y"));
        }

        [Test]
        public void RestoreCollectedPickups_WithNull_ClearsSet()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.RestoreCollectedPickups(null);
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        [Test]
        public void RestoreCollectedPickups_SkipsNullAndEmptyIds()
        {
            _gameManager.RestoreCollectedPickups(new[] { "pickup_a", null, string.Empty, "pickup_b" });
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_a"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_b"));
            var ids = new List<string>(_gameManager.CollectedPickupIds);
            Assert.AreEqual(2, ids.Count);
        }

        [Test]
        public void ClearCollectedPickups_RemovesAll()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.ClearCollectedPickups();
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        [Test]
        public void BuildSaveData_IncludesCollectedPickupIds()
        {
            _gameManager.MarkPickupCollected("pickup_01");
            _gameManager.MarkPickupCollected("pickup_02");
            SaveData data = _gameManager.BuildSaveData();
            Assert.IsNotNull(data.collectedPickupIds);
            Assert.AreEqual(2, data.collectedPickupIds.Length);
            CollectionAssert.AreEquivalent(new[] { "pickup_01", "pickup_02" }, data.collectedPickupIds);
        }

        [Test]
        public void BuildSaveData_CollectedPickupIds_IsEmptyArray_WhenNoneCollected()
        {
            SaveData data = _gameManager.BuildSaveData();
            Assert.IsNotNull(data.collectedPickupIds);
            Assert.AreEqual(0, data.collectedPickupIds.Length);
        }

        [Test]
        public void ApplySaveData_RestoresCollectedPickupIds()
        {
            _gameManager.MarkPickupCollected("stale");
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                collectedPickupIds = new[] { "pickup_a", "pickup_b" }
            };
            _gameManager.ApplySaveData(saveData);
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_a"));
            Assert.IsTrue(_gameManager.IsPickupCollected("pickup_b"));
        }

        [Test]
        public void ApplySaveData_NullCollectedPickupIds_ClearsSet()
        {
            _gameManager.MarkPickupCollected("stale");
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                collectedPickupIds = null
            };
            _gameManager.ApplySaveData(saveData);
            Assert.IsFalse(_gameManager.IsPickupCollected("stale"));
        }

        [Test]
        public void StartNewGame_ClearsCollectedPickups()
        {
            _gameManager.MarkPickupCollected("pickup_a");
            _gameManager.StartNewGame();
            Assert.IsFalse(_gameManager.IsPickupCollected("pickup_a"));
        }

        private CharacterData CreateTestCharacterData(
            int maxHp = 100, int maxMp = 50, int atk = 10, int def = 5, int spd = 8)
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = "TestPlayer";
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK = atk;
            cd.baseDEF = def;
            cd.baseSPD = spd;
            return cd;
        }
    }
}
