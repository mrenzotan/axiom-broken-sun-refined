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
            Assert.AreEqual(2, _gameManager.PlayerState.Inventory.GetQuantity("potion_hp"));
            Assert.AreEqual(1, _gameManager.PlayerState.Inventory.GetQuantity("ether_mp"));
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
            _gameManager.PlayerState.Inventory.Add("potion_hp", 2);
            _gameManager.PlayerState.Inventory.Add("ether_mp", 1);
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
        public void SaveData_RoundTripsLastCheckpoint()
        {
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                activatedCheckpointIds = new[] { "cp_level1-1_01" },
                lastCheckpointPositionX = 12.25f,
                lastCheckpointPositionY = -3.5f,
                lastCheckpointSceneName = "Level_1-1"
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(12.25f, _gameManager.PlayerState.LastCheckpointPositionX);
            Assert.AreEqual(-3.5f, _gameManager.PlayerState.LastCheckpointPositionY);
            Assert.AreEqual("Level_1-1", _gameManager.PlayerState.LastCheckpointSceneName);

            SaveData rebuilt = _gameManager.BuildSaveData();
            Assert.AreEqual(12.25f, rebuilt.lastCheckpointPositionX);
            Assert.AreEqual(-3.5f, rebuilt.lastCheckpointPositionY);
            Assert.AreEqual("Level_1-1", rebuilt.lastCheckpointSceneName);
        }

        [Test]
        public void SaveData_RoundTripsCheckpointProgression()
        {
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                checkpointLevel   = 3,
                checkpointXp      = 220,
                checkpointMaxHp   = 130,
                checkpointMaxMp   = 60,
                checkpointAttack  = 14,
                checkpointDefense = 8,
                checkpointSpeed   = 11,
                checkpointUnlockedSpellIds = new[] { "spell_firebolt", "spell_icewall" }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(3,   _gameManager.PlayerState.CheckpointLevel);
            Assert.AreEqual(220, _gameManager.PlayerState.CheckpointXp);
            Assert.AreEqual(130, _gameManager.PlayerState.CheckpointMaxHp);
            Assert.AreEqual(2,   _gameManager.PlayerState.CheckpointUnlockedSpellIds.Count);
            Assert.IsTrue(_gameManager.PlayerState.HasCheckpointProgression);

            SaveData rebuilt = _gameManager.BuildSaveData();
            Assert.AreEqual(3,   rebuilt.checkpointLevel);
            Assert.AreEqual(14,  rebuilt.checkpointAttack);
            Assert.AreEqual(2,   rebuilt.checkpointUnlockedSpellIds.Length);
        }

        [Test]
        public void CaptureCheckpointProgression_SnapshotsCurrentLevelAndStats()
        {
            _gameManager.PlayerState.ApplyProgression(level: 4, xp: 50);
            _gameManager.PlayerState.ApplyVitals(maxHp: 130, maxMp: 60, currentHp: 100, currentMp: 30);
            _gameManager.PlayerState.ApplyStats(attack: 15, defense: 9, speed: 12);

            _gameManager.PlayerState.CaptureCheckpointProgression();

            Assert.AreEqual(4,   _gameManager.PlayerState.CheckpointLevel);
            Assert.AreEqual(50,  _gameManager.PlayerState.CheckpointXp);
            Assert.AreEqual(130, _gameManager.PlayerState.CheckpointMaxHp);
            Assert.AreEqual(15,  _gameManager.PlayerState.CheckpointAttack);
        }

        [Test]
        public void RestoreCheckpointProgression_RollsBackLevelAndStats()
        {
            _gameManager.PlayerState.ApplyProgression(level: 3, xp: 10);
            _gameManager.PlayerState.ApplyVitals(maxHp: 110, maxMp: 50, currentHp: 110, currentMp: 50);
            _gameManager.PlayerState.ApplyStats(attack: 11, defense: 6, speed: 9);
            _gameManager.PlayerState.CaptureCheckpointProgression();

            // Simulate post-checkpoint progression: gain a level, level-up grows stats.
            _gameManager.PlayerState.GrowStats(deltaMaxHp: 20, deltaMaxMp: 10, deltaAttack: 4, deltaDefense: 2, deltaSpeed: 3);
            _gameManager.PlayerState.ApplyProgression(level: 4, xp: 0);

            _gameManager.PlayerState.RestoreCheckpointProgression();

            Assert.AreEqual(3,   _gameManager.PlayerState.Level);
            Assert.AreEqual(10,  _gameManager.PlayerState.Xp);
            Assert.AreEqual(110, _gameManager.PlayerState.MaxHp);
            Assert.AreEqual(11,  _gameManager.PlayerState.Attack);
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
        public void DefeatedEnemyIdsInScene_IsEmpty_ByDefault()
        {
            using var enumerator = _gameManager.DefeatedEnemyIdsInScene("any_scene").GetEnumerator();
            Assert.IsFalse(enumerator.MoveNext());
        }

        [Test]
        public void MarkEnemyDefeated_RecordsUnderActiveScene()
        {
            _gameManager.PlayerState.SetActiveScene("Level_1-1");
            _gameManager.MarkEnemyDefeated("enemy_a");
            _gameManager.MarkEnemyDefeated("enemy_b");

            CollectionAssert.AreEquivalent(
                new[] { "enemy_a", "enemy_b" },
                new List<string>(_gameManager.DefeatedEnemyIdsInScene("Level_1-1")));
            // Other scenes unaffected.
            using var other = _gameManager.DefeatedEnemyIdsInScene("Level_1-2").GetEnumerator();
            Assert.IsFalse(other.MoveNext());
        }

        [Test]
        public void MarkEnemyDefeatedInScene_IsScopedToThatScene()
        {
            _gameManager.MarkEnemyDefeatedInScene("Level_1-1", "slime_01");
            _gameManager.MarkEnemyDefeatedInScene("Level_1-2", "bat_02");

            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "slime_01"));
            Assert.IsFalse(_gameManager.IsEnemyDefeatedInScene("Level_1-2", "slime_01"));
            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-2", "bat_02"));
            Assert.IsFalse(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "bat_02"));
        }

        [Test]
        public void ClearDefeatedEnemiesInScene_LeavesOtherScenesIntact()
        {
            _gameManager.MarkEnemyDefeatedInScene("Level_1-1", "slime_01");
            _gameManager.MarkEnemyDefeatedInScene("Level_1-2", "bat_02");

            _gameManager.ClearDefeatedEnemiesInScene("Level_1-1");

            Assert.IsFalse(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "slime_01"));
            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-2", "bat_02"));
        }

        [Test]
        public void BuildSaveData_IncludesDefeatedEnemiesPerScene()
        {
            _gameManager.MarkEnemyDefeatedInScene("Level_1-1", "enemy_slime_01");
            _gameManager.MarkEnemyDefeatedInScene("Level_1-2", "enemy_bat_02");

            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.defeatedEnemiesPerScene);
            Assert.AreEqual(2, data.defeatedEnemiesPerScene.Length);

            var lookup = new System.Collections.Generic.Dictionary<string, string[]>();
            foreach (var entry in data.defeatedEnemiesPerScene)
                lookup[entry.sceneName] = entry.enemyIds;

            CollectionAssert.AreEquivalent(new[] { "enemy_slime_01" }, lookup["Level_1-1"]);
            CollectionAssert.AreEquivalent(new[] { "enemy_bat_02" }, lookup["Level_1-2"]);
        }

        [Test]
        public void BuildSaveData_DefeatedEnemiesPerScene_IsEmptyArray_WhenNoneDefeated()
        {
            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.defeatedEnemiesPerScene);
            Assert.AreEqual(0, data.defeatedEnemiesPerScene.Length);
        }

        [Test]
        public void ApplySaveData_RestoresDefeatedEnemiesPerScene()
        {
            _gameManager.MarkEnemyDefeatedInScene("Level_1-1", "stale_enemy");

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                defeatedEnemiesPerScene = new[]
                {
                    new DefeatedEnemiesSceneEntry
                    {
                        sceneName = "Level_1-2",
                        enemyIds = new[] { "enemy_a", "enemy_b" }
                    }
                }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsFalse(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "stale_enemy"));
            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-2", "enemy_a"));
            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-2", "enemy_b"));
        }

        [Test]
        public void ApplySaveData_MigratesLegacyDefeatedEnemyIds_UnderActiveSceneName()
        {
            var saveData = new SaveData
            {
                saveVersion = 1,
                maxHp = 100,
                maxMp = 50,
                activeSceneName = "Level_1-1",
                defeatedEnemyIds = new[] { "enemy_a", "enemy_b" }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "enemy_a"));
            Assert.IsTrue(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "enemy_b"));
        }

        [Test]
        public void ApplySaveData_NullDefeatedEnemiesPerScene_ClearsAllScenes()
        {
            _gameManager.MarkEnemyDefeatedInScene("Level_1-1", "stale_enemy");

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                defeatedEnemiesPerScene = null
            };

            _gameManager.ApplySaveData(saveData);

            Assert.IsFalse(_gameManager.IsEnemyDefeatedInScene("Level_1-1", "stale_enemy"));
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

        // ---- Damaged Enemy HP ----

        [Test]
        public void GetDamagedEnemyHp_ReturnsNegativeOne_WhenNotTracked()
        {
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("unknown_enemy"));
        }

        [Test]
        public void GetDamagedEnemyHp_ReturnsNegativeOne_ForNullId()
        {
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(null));
        }

        [Test]
        public void GetDamagedEnemyHp_ReturnsNegativeOne_ForEmptyId()
        {
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(string.Empty));
        }

        [Test]
        public void SetDamagedEnemyHp_ThenGet_ReturnsStoredValue()
        {
            _gameManager.SetDamagedEnemyHp("enemy_slime", 35);
            Assert.AreEqual(35, _gameManager.GetDamagedEnemyHp("enemy_slime"));
        }

        [Test]
        public void SetDamagedEnemyHp_OverwritesPreviousValue()
        {
            _gameManager.SetDamagedEnemyHp("enemy_slime", 35);
            _gameManager.SetDamagedEnemyHp("enemy_slime", 10);
            Assert.AreEqual(10, _gameManager.GetDamagedEnemyHp("enemy_slime"));
        }

        [Test]
        public void SetDamagedEnemyHp_NullId_IsNoOp()
        {
            _gameManager.SetDamagedEnemyHp(null, 50);
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp(null));
        }

        [Test]
        public void ClearDamagedEnemyHp_RemovesEntry()
        {
            _gameManager.SetDamagedEnemyHp("enemy_bat", 20);
            _gameManager.ClearDamagedEnemyHp("enemy_bat");
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_bat"));
        }

        [Test]
        public void ClearDamagedEnemyHp_NullId_IsNoOp()
        {
            _gameManager.SetDamagedEnemyHp("enemy_bat", 20);
            _gameManager.ClearDamagedEnemyHp(null);
            Assert.AreEqual(20, _gameManager.GetDamagedEnemyHp("enemy_bat"));
        }

        [Test]
        public void ClearAllDamagedEnemyHp_RemovesAllEntries()
        {
            _gameManager.SetDamagedEnemyHp("enemy_a", 10);
            _gameManager.SetDamagedEnemyHp("enemy_b", 20);
            _gameManager.ClearAllDamagedEnemyHp();
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_a"));
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_b"));
        }

        [Test]
        public void BuildSaveData_IncludesDamagedEnemyHpPerScene()
        {
            _gameManager.SetDamagedEnemyHpInScene("Level_1-1", "enemy_slime_01", 25);
            _gameManager.SetDamagedEnemyHpInScene("Level_1-2", "enemy_bat_02", 40);

            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.damagedEnemyHpPerScene);
            Assert.AreEqual(2, data.damagedEnemyHpPerScene.Length);

            var lookup = new System.Collections.Generic.Dictionary<string, EnemyHpSaveEntry[]>();
            foreach (var entry in data.damagedEnemyHpPerScene)
                lookup[entry.sceneName] = entry.entries;

            Assert.AreEqual(1, lookup["Level_1-1"].Length);
            Assert.AreEqual("enemy_slime_01", lookup["Level_1-1"][0].enemyId);
            Assert.AreEqual(25, lookup["Level_1-1"][0].currentHp);

            Assert.AreEqual(1, lookup["Level_1-2"].Length);
            Assert.AreEqual("enemy_bat_02", lookup["Level_1-2"][0].enemyId);
            Assert.AreEqual(40, lookup["Level_1-2"][0].currentHp);
        }

        [Test]
        public void BuildSaveData_DamagedEnemyHpPerScene_IsEmptyArray_WhenNoneDamaged()
        {
            SaveData data = _gameManager.BuildSaveData();

            Assert.IsNotNull(data.damagedEnemyHpPerScene);
            Assert.AreEqual(0, data.damagedEnemyHpPerScene.Length);
        }

        [Test]
        public void ApplySaveData_RestoresDamagedEnemyHpPerScene()
        {
            _gameManager.SetDamagedEnemyHpInScene("Level_1-1", "stale_enemy", 99);

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                damagedEnemyHpPerScene = new[]
                {
                    new DamagedEnemyHpSceneEntry
                    {
                        sceneName = "Level_1-2",
                        entries = new[]
                        {
                            new EnemyHpSaveEntry { enemyId = "enemy_a", currentHp = 30 },
                            new EnemyHpSaveEntry { enemyId = "enemy_b", currentHp = 15 }
                        }
                    }
                }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHpInScene("Level_1-1", "stale_enemy"));
            Assert.AreEqual(30, _gameManager.GetDamagedEnemyHpInScene("Level_1-2", "enemy_a"));
            Assert.AreEqual(15, _gameManager.GetDamagedEnemyHpInScene("Level_1-2", "enemy_b"));
        }

        [Test]
        public void ApplySaveData_MigratesLegacyDamagedEnemyHp_UnderActiveSceneName()
        {
            var saveData = new SaveData
            {
                saveVersion = 1,
                maxHp = 100,
                maxMp = 50,
                activeSceneName = "Level_1-1",
                damagedEnemyHp = new[]
                {
                    new EnemyHpSaveEntry { enemyId = "enemy_a", currentHp = 30 }
                }
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(30, _gameManager.GetDamagedEnemyHpInScene("Level_1-1", "enemy_a"));
        }

        [Test]
        public void ApplySaveData_NullDamagedEnemyHpPerScene_ClearsDictionary()
        {
            _gameManager.SetDamagedEnemyHpInScene("Level_1-1", "stale", 50);

            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                damagedEnemyHpPerScene = null
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHpInScene("Level_1-1", "stale"));
        }

        [Test]
        public void PersistAndLoad_RoundTrip_RestoresDamagedEnemyHp()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            _gameManager.SetDamagedEnemyHp("enemy_slime_01", 25);
            _gameManager.SetDamagedEnemyHp("enemy_bat_02", 40);
            _gameManager.PersistToDisk();

            _gameManager.ClearAllDamagedEnemyHp();
            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_slime_01"));

            bool loaded = _gameManager.TryLoadFromDiskIntoGame();

            Assert.IsTrue(loaded);
            Assert.AreEqual(25, _gameManager.GetDamagedEnemyHp("enemy_slime_01"));
            Assert.AreEqual(40, _gameManager.GetDamagedEnemyHp("enemy_bat_02"));
        }

        [Test]
        public void ApplySaveData_MissingDamagedEnemyHpPerScene_DefaultsToEmpty()
        {
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                damagedEnemyHpPerScene = null
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHpInScene("Level_1-1", "any_enemy"));
        }

        [Test]
        public void StartNewGame_ClearsDamagedEnemyHp()
        {
            _gameManager.SetDamagedEnemyHp("enemy_a", 30);

            _gameManager.StartNewGame();

            Assert.AreEqual(-1, _gameManager.GetDamagedEnemyHp("enemy_a"));
        }

        // ── ATK/DEF/SPD persistence (DEV-65) ─────────────────────────────────

        [Test]
        public void BuildSaveData_IncludesGrownAttackDefenseSpeed()
        {
            // PlayerState is seeded with CD base stats (atk=10, def=5, spd=8).
            _gameManager.PlayerState.ApplyStats(attack: 25, defense: 14, speed: 19);

            SaveData data = _gameManager.BuildSaveData();

            Assert.AreEqual(25, data.attack);
            Assert.AreEqual(14, data.defense);
            Assert.AreEqual(19, data.speed);
        }

        [Test]
        public void ApplySaveData_RestoresAttackDefenseSpeed()
        {
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                attack  = 30,
                defense = 18,
                speed   = 22
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(30, _gameManager.PlayerState.Attack);
            Assert.AreEqual(18, _gameManager.PlayerState.Defense);
            Assert.AreEqual(22, _gameManager.PlayerState.Speed);
        }

        [Test]
        public void ApplySaveData_LegacySaveWithZeroStats_KeepsCharacterDataBaseStats()
        {
            // A pre-DEV-65 save has attack/defense/speed == 0 (JsonUtility default).
            // Expected behavior: fall back to PlayerState base values from CharacterData.
            // Test CharacterData: atk=10, def=5, spd=8 (see CreateTestCharacterData).
            var saveData = new SaveData
            {
                maxHp = 100,
                maxMp = 50,
                attack  = 0,
                defense = 0,
                speed   = 0
            };

            _gameManager.ApplySaveData(saveData);

            Assert.AreEqual(10, _gameManager.PlayerState.Attack);
            Assert.AreEqual(5,  _gameManager.PlayerState.Defense);
            Assert.AreEqual(8,  _gameManager.PlayerState.Speed);
        }

        [Test]
        public void PersistAndLoad_RoundTrip_RestoresGrownStats()
        {
            SaveService tempSaveService = CreateTempSaveService();
            _gameManager.SetSaveServiceForTests(tempSaveService);

            // Simulate level-up stat growth.
            _gameManager.PlayerState.ApplyStats(attack: 42, defense: 27, speed: 33);
            _gameManager.PersistToDisk();

            // Reset to base stats to prove the load (not the in-memory state) restores them.
            _gameManager.PlayerState.ApplyStats(attack: 10, defense: 5, speed: 8);

            bool loaded = _gameManager.TryLoadFromDiskIntoGame();

            Assert.IsTrue(loaded);
            Assert.AreEqual(42, _gameManager.PlayerState.Attack);
            Assert.AreEqual(27, _gameManager.PlayerState.Defense);
            Assert.AreEqual(33, _gameManager.PlayerState.Speed);
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

        private SaveService CreateTempSaveService()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _tempDirectories.Add(tempDirectory);
            return new SaveService(tempDirectory);
        }
    }
}
