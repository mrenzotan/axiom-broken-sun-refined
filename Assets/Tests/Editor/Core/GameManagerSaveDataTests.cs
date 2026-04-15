using System;
using System.Collections.Generic;
using System.IO;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class GameManagerSaveDataTests
    {
        private GameObject _gameManagerObject;
        private GameManager _gameManager;
        private readonly List<string> _tempDirectories = new List<string>();

        [SetUp]
        public void SetUp()
        {
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

        [Test]
        public void ApplySaveData_UpdatesPlayerState()
        {
            var saveData = new SaveData
            {
                playerLevel = 6,
                playerXp = 3210,
                maxHp = 120,
                currentHp = 85,
                maxMp = 70,
                currentMp = 35,
                unlockedSpellIds = new[] { "spell_firebolt", "spell_icewall" },
                inventory = new[]
                {
                    new InventorySaveEntry { itemId = "potion_hp", quantity = 2 },
                    new InventorySaveEntry { itemId = "ether_mp", quantity = 1 }
                },
                worldPositionX = 13.5f,
                worldPositionY = -4.75f,
                activeSceneName = "Platformer"
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(6, _gameManager.PlayerState.Level);
            Assert.AreEqual(3210, _gameManager.PlayerState.Xp);
            Assert.AreEqual(120, _gameManager.PlayerState.MaxHp);
            Assert.AreEqual(85, _gameManager.PlayerState.CurrentHp);
            Assert.AreEqual(70, _gameManager.PlayerState.MaxMp);
            Assert.AreEqual(35, _gameManager.PlayerState.CurrentMp);
            Assert.AreEqual(2, _gameManager.PlayerState.UnlockedSpellIds.Count);
            Assert.AreEqual("spell_firebolt", _gameManager.PlayerState.UnlockedSpellIds[0]);
            Assert.AreEqual(3, _gameManager.PlayerState.InventoryItemIds.Count);
            Assert.AreEqual("potion_hp", _gameManager.PlayerState.InventoryItemIds[0]);
            Assert.AreEqual("potion_hp", _gameManager.PlayerState.InventoryItemIds[1]);
            Assert.AreEqual("ether_mp", _gameManager.PlayerState.InventoryItemIds[2]);
            Assert.AreEqual(13.5f, _gameManager.PlayerState.WorldPositionX);
            Assert.AreEqual(-4.75f, _gameManager.PlayerState.WorldPositionY);
            Assert.AreEqual("Platformer", _gameManager.PlayerState.ActiveSceneName);
        }

        [Test]
        public void PersistToDisk_WritesLoadableSaveFile()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.PlayerState.ApplyProgression(level: 5, xp: 900);
            _gameManager.PlayerState.ApplyVitals(maxHp: 130, maxMp: 60, currentHp: 101, currentMp: 44);
            _gameManager.PlayerState.SetUnlockedSpellIds(new[] { "spell_a", "spell_b" });
            _gameManager.PlayerState.SetInventoryItemIds(new[] { "potion_hp", "potion_hp", "ether_mp" });
            _gameManager.PlayerState.SetWorldPosition(7.25f, -1.5f);
            _gameManager.PlayerState.SetActiveScene("Platformer");

            _gameManager.PersistToDisk();

            Assert.IsTrue(tempSaveService.TryLoad(out SaveData loaded));
            Assert.AreEqual(5, loaded.playerLevel);
            Assert.AreEqual(900, loaded.playerXp);
            Assert.AreEqual(130, loaded.maxHp);
            Assert.AreEqual(101, loaded.currentHp);
            Assert.AreEqual(60, loaded.maxMp);
            Assert.AreEqual(44, loaded.currentMp);
            Assert.AreEqual(2, loaded.unlockedSpellIds.Length);
            Assert.AreEqual(2, loaded.inventory.Length);
            Assert.AreEqual("potion_hp", loaded.inventory[0].itemId);
            Assert.AreEqual(2, loaded.inventory[0].quantity);
            Assert.AreEqual("ether_mp", loaded.inventory[1].itemId);
            Assert.AreEqual(1, loaded.inventory[1].quantity);
            Assert.AreEqual(7.25f, loaded.worldPositionX);
            Assert.AreEqual(-1.5f, loaded.worldPositionY);
            Assert.AreEqual("Platformer", loaded.activeSceneName);
        }

        [Test]
        public void TryLoadFromDiskIntoGame_ReturnsFalse_WhenNoSaveExists()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            bool loaded = _gameManager.TryLoadFromDiskIntoGame();

            Assert.IsFalse(loaded);
        }

        [Test]
        public void BuildSaveData_IncludesActivatedCheckpointIds()
        {
            _gameManager.PlayerState.MarkCheckpointActivated("cp_a");
            _gameManager.PlayerState.MarkCheckpointActivated("cp_b");

            SaveData data = _gameManager.BuildSaveData();

            Assert.AreEqual(2, data.activatedCheckpointIds.Length);
            Assert.AreEqual("cp_a", data.activatedCheckpointIds[0]);
            Assert.AreEqual("cp_b", data.activatedCheckpointIds[1]);
        }

        [Test]
        public void ApplySaveData_RestoresActivatedCheckpointIds()
        {
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                activatedCheckpointIds = new[] { "cp_a", "cp_b" }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsTrue(_gameManager.PlayerState.HasActivatedCheckpoint("cp_a"));
            Assert.IsTrue(_gameManager.PlayerState.HasActivatedCheckpoint("cp_b"));
        }

        [Test]
        public void TryActivateCheckpointRegen_ReturnsTrueAndHeals_OnFirstActivation()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.PlayerState.ApplyVitals(maxHp: 100, maxMp: 50, currentHp: 40, currentMp: 10);

            bool result = _gameManager.TryActivateCheckpointRegen("cp_test", out int healedHp, out int healedMp);

            Assert.IsTrue(result);
            Assert.Greater(healedHp, 0);
            Assert.Greater(healedMp, 0);
            Assert.AreEqual(_gameManager.PlayerState.MaxHp, _gameManager.PlayerState.CurrentHp);
            Assert.AreEqual(_gameManager.PlayerState.MaxMp, _gameManager.PlayerState.CurrentMp);
        }

        [Test]
        public void TryActivateCheckpointRegen_ReturnsFalseAndZero_OnSecondActivation()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.PlayerState.ApplyVitals(maxHp: 100, maxMp: 50, currentHp: 40, currentMp: 10);
            _gameManager.TryActivateCheckpointRegen("cp_test", out _, out _);

            _gameManager.PlayerState.SetCurrentHp(40);
            bool result = _gameManager.TryActivateCheckpointRegen("cp_test", out int healedHp, out int healedMp);

            Assert.IsFalse(result);
            Assert.AreEqual(0, healedHp);
            Assert.AreEqual(0, healedMp);
        }

        [Test]
        public void TryActivateCheckpointRegen_ReturnsFalse_ForInvalidId()
        {
            bool nullResult = _gameManager.TryActivateCheckpointRegen(null, out int hp1, out int mp1);
            bool emptyResult = _gameManager.TryActivateCheckpointRegen(string.Empty, out int hp2, out int mp2);

            Assert.IsFalse(nullResult);
            Assert.AreEqual(0, hp1);
            Assert.AreEqual(0, mp1);
            Assert.IsFalse(emptyResult);
            Assert.AreEqual(0, hp2);
            Assert.AreEqual(0, mp2);
        }

        [Test]
        public void DefeatedEnemyIds_IsEmpty_ByDefault()
        {
            Assert.IsNotNull(_gameManager.DefeatedEnemyIds);
            using var enumerator = _gameManager.DefeatedEnemyIds.GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());
        }

        [Test]
        public void DefeatedEnemyIds_ReflectsMarkEnemyDefeated()
        {
            _gameManager.MarkEnemyDefeated("enemy_a");
            _gameManager.MarkEnemyDefeated("enemy_b");

            var ids = new List<string>(_gameManager.DefeatedEnemyIds);
            CollectionAssert.AreEquivalent(new[] { "enemy_a", "enemy_b" }, ids);
        }

        [Test]
        public void RestoreDefeatedEnemies_ReplacesExistingSet()
        {
            _gameManager.MarkEnemyDefeated("stale_enemy");

            _gameManager.RestoreDefeatedEnemies(new[] { "enemy_x", "enemy_y" });

            Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_x"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_y"));
        }

        [Test]
        public void RestoreDefeatedEnemies_WithNull_ClearsSet()
        {
            _gameManager.MarkEnemyDefeated("enemy_a");

            _gameManager.RestoreDefeatedEnemies(null);

            Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_a"));
            using var enumerator = _gameManager.DefeatedEnemyIds.GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());
        }

        [Test]
        public void RestoreDefeatedEnemies_SkipsNullAndEmptyIds()
        {
            _gameManager.RestoreDefeatedEnemies(new[] { "enemy_a", null, string.Empty, "enemy_b" });

            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_a"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_b"));
            var ids = new List<string>(_gameManager.DefeatedEnemyIds);
            Assert.AreEqual(2, ids.Count);
        }

        [Test]
        public void BuildSaveData_IncludesDefeatedEnemyIds()
        {
            _gameManager.MarkEnemyDefeated("enemy_slime_01");
            _gameManager.MarkEnemyDefeated("enemy_bat_02");

            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.defeatedEnemyIds);
            Assert.AreEqual(2, data.defeatedEnemyIds.Length);
            CollectionAssert.AreEquivalent(
                new[] { "enemy_slime_01", "enemy_bat_02" },
                data.defeatedEnemyIds);
        }

        [Test]
        public void BuildSaveData_DefeatedEnemyIds_IsEmptyArray_WhenNoneDefeated()
        {
            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.defeatedEnemyIds);
            Assert.AreEqual(0, data.defeatedEnemyIds.Length);
        }

        [Test]
        public void ApplySaveData_RestoresDefeatedEnemyIds()
        {
            _gameManager.MarkEnemyDefeated("stale_enemy");

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                defeatedEnemyIds = new[] { "enemy_a", "enemy_b" }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_a"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_b"));
        }

        [Test]
        public void ApplySaveData_NullDefeatedEnemyIds_ClearsSet()
        {
            _gameManager.MarkEnemyDefeated("stale_enemy");

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                defeatedEnemyIds = null
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsFalse(_gameManager.IsEnemyDefeated("stale_enemy"));
        }

        [Test]
        public void PersistAndLoad_RoundTrip_RestoresDefeatedEnemyIds()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.MarkEnemyDefeated("enemy_slime_01");
            _gameManager.MarkEnemyDefeated("enemy_bat_02");
            _gameManager.PersistToDisk();

            _gameManager.ClearDefeatedEnemies();
            Assert.IsFalse(_gameManager.IsEnemyDefeated("enemy_slime_01"));

            bool loaded = _gameManager.TryLoadFromDiskIntoGame();

            Assert.IsTrue(loaded);
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_slime_01"));
            Assert.IsTrue(_gameManager.IsEnemyDefeated("enemy_bat_02"));
        }

        private SaveService CreateTempSaveService()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _tempDirectories.Add(tempDirectory);
            return new SaveService(tempDirectory);
        }
    }
}
