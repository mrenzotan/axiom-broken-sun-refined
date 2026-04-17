using System;
using System.Collections.Generic;
using System.IO;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            _gameManager.SetPlayerCharacterDataForTests(CreateTestCharacterData());
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

        private CharacterData CreateTestCharacterData(
            int maxHp = 100,
            int maxMp = 50,
            int atk = 10,
            int def = 5,
            int spd = 8,
            string name = "TestPlayer")
        {
            var cd = ScriptableObject.CreateInstance<CharacterData>();
            cd.characterName = name;
            cd.baseMaxHP = maxHp;
            cd.baseMaxMP = maxMp;
            cd.baseATK   = atk;
            cd.baseDEF   = def;
            cd.baseSPD   = spd;
            return cd;
        }

        [Test]
        public void EnsurePlayerState_SeedsMaxHpFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(maxHp: 77);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            Assert.AreEqual(77, _gameManager.PlayerState.MaxHp);
        }

        [Test]
        public void EnsurePlayerState_SeedsAllBaseStatsFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(
                maxHp: 42, maxMp: 13, atk: 7, def: 3, spd: 11);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            PlayerState ps = _gameManager.PlayerState;
            Assert.AreEqual(42, ps.MaxHp);
            Assert.AreEqual(13, ps.MaxMp);
            Assert.AreEqual(7,  ps.Attack);
            Assert.AreEqual(3,  ps.Defense);
            Assert.AreEqual(11, ps.Speed);
        }

        [Test]
        public void EnsurePlayerState_LogsError_AndReturnsNullState_WhenCharacterDataMissing()
        {
            // Discard the SetUp-injected GameManager and build a fresh one with no CD.
            UnityEngine.Object.DestroyImmediate(_gameManagerObject);
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            PlayerState state = _gameManager.PlayerState;

            Assert.IsNull(state);
        }

        [Test]
        public void StartNewGame_SeedsMaxHpFromCharacterData()
        {
            CharacterData cd = CreateTestCharacterData(maxHp: 30);
            _gameManager.SetPlayerCharacterDataForTests(cd);

            _gameManager.StartNewGame();

            Assert.AreEqual(30, _gameManager.PlayerState.MaxHp);
        }

        [Test]
        public void StartNewGame_LogsError_WhenCharacterDataMissing()
        {
            // Discard the SetUp-injected GameManager and build a fresh one with no CD.
            UnityEngine.Object.DestroyImmediate(_gameManagerObject);
            _gameManagerObject = new GameObject("GameManager");
            _gameManager = _gameManagerObject.AddComponent<GameManager>();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            _gameManager.StartNewGame();

            // Reading PlayerState routes through EnsurePlayerState, which logs its own
            // "playerCharacterData is not assigned" error when the CD is missing.
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("playerCharacterData"));

            Assert.IsNull(_gameManager.PlayerState);
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
            _gameManager.PlayerState.Inventory.Add("potion_hp", 2);

            _gameManager.StartNewGame();

            Assert.AreEqual(0, _gameManager.PlayerState.Inventory.GetAll().Count);
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
