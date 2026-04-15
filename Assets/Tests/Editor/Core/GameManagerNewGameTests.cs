using System;
using System.Collections.Generic;
using System.IO;
using Axiom.Core;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Editor.Core
{
    public class GameManagerNewGameTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;
        private readonly List<string> _tempDirectories = new List<string>();

        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);

            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerObject != null)
                UnityEngine.Object.DestroyImmediate(_gameManagerObject);

            foreach (string tempDirectory in _tempDirectories)
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
            }

            _tempDirectories.Clear();
        }

        private SaveService CreateTempSaveService()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _tempDirectories.Add(tempDirectory);
            return new SaveService(tempDirectory);
        }

        [Test]
        public void StartNewGame_ResetsPlayerLevelToOne()
        {
            _gameManager.PlayerState.ApplyProgression(level: 7, xp: 9000);

            _gameManager.StartNewGame();

            Assert.AreEqual(1, _gameManager.PlayerState.Level);
        }

        [Test]
        public void StartNewGame_ResetsXpToZero()
        {
            _gameManager.PlayerState.ApplyProgression(level: 3, xp: 500);

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.Xp);
        }

        [Test]
        public void StartNewGame_ResetsCurrentHpToMaxHp()
        {
            _gameManager.PlayerState.SetCurrentHp(10);

            _gameManager.StartNewGame();

            Assert.AreEqual(_gameManager.PlayerState.MaxHp, _gameManager.PlayerState.CurrentHp);
        }

        [Test]
        public void StartNewGame_ResetsCurrentMpToMaxMp()
        {
            _gameManager.PlayerState.SetCurrentMp(0);

            _gameManager.StartNewGame();

            Assert.AreEqual(_gameManager.PlayerState.MaxMp, _gameManager.PlayerState.CurrentMp);
        }

        [Test]
        public void StartNewGame_ClearsUnlockedSpells()
        {
            _gameManager.PlayerState.SetUnlockedSpellIds(new[] { "spell_a", "spell_b" });

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.UnlockedSpellIds.Count);
        }

        [Test]
        public void StartNewGame_ClearsInventory()
        {
            _gameManager.PlayerState.SetInventoryItemIds(new[] { "potion_hp", "potion_hp" });

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.InventoryItemIds.Count);
        }

        [Test]
        public void StartNewGame_ReturnsMaxHpOf100()
        {
            _gameManager.StartNewGame();

            Assert.AreEqual(100, _gameManager.PlayerState.MaxHp);
        }

        [Test]
        public void StartNewGame_ClearsDefeatedEnemies()
        {
            _gameManager.MarkEnemyDefeated("enemy_a");
            _gameManager.MarkEnemyDefeated("enemy_b");

            _gameManager.StartNewGame();

            Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_a"));
            Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_b"));
        }

        [Test]
        public void StartNewGame_ClearsWorldSnapshot()
        {
            _gameManager.SetWorldSnapshot(new WorldSnapshot());

            _gameManager.StartNewGame();

            Assert.IsNull(_gameManager.CurrentWorldSnapshot);
        }

        [Test]
        public void StartNewGame_DeletesExistingSaveFile()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.MarkEnemyDefeated("enemy_a");
            _gameManager.PersistToDisk();
            Assert.IsTrue(tempSaveService.HasSave());

            _gameManager.StartNewGame();

            Assert.IsFalse(tempSaveService.HasSave());
        }
    }
}
