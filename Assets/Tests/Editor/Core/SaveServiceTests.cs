using System;
using System.IO;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoreTests
{
    public class SaveServiceTests
    {
        private string _tempDirectory;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _saveService = new SaveService(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }

        [Test]
        public void HasSave_False_WhenNoFile()
        {
            Assert.IsFalse(_saveService.HasSave());
        }

        [Test]
        public void Save_Then_TryLoad_RestoresPayload()
        {
            var original = new SaveData
            {
                playerLevel = 4,
                playerXp = 1200,
                currentHp = 37,
                currentMp = 12,
                maxHp = 100,
                maxMp = 50,
                unlockedSpellIds = new[] { "spell_a", "spell_b" },
                inventory = new[]
                {
                    new InventorySaveEntry { itemId = "potion_hp", quantity = 3 }
                },
                worldPositionX = 12.5f,
                worldPositionY = -3.25f,
                activeSceneName = "Platformer"
            };

            _saveService.Save(original);

            Assert.IsTrue(_saveService.TryLoad(out SaveData restored));
            Assert.AreEqual(original.playerLevel, restored.playerLevel);
            Assert.AreEqual(original.playerXp, restored.playerXp);
            Assert.AreEqual(original.currentHp, restored.currentHp);
            Assert.AreEqual(original.currentMp, restored.currentMp);
            Assert.AreEqual(original.maxHp, restored.maxHp);
            Assert.AreEqual(original.maxMp, restored.maxMp);
            Assert.AreEqual(original.unlockedSpellIds.Length, restored.unlockedSpellIds.Length);
            Assert.AreEqual("spell_a", restored.unlockedSpellIds[0]);
            Assert.AreEqual(1, restored.inventory.Length);
            Assert.AreEqual("potion_hp", restored.inventory[0].itemId);
            Assert.AreEqual(3, restored.inventory[0].quantity);
            Assert.AreEqual(original.worldPositionX, restored.worldPositionX);
            Assert.AreEqual(original.worldPositionY, restored.worldPositionY);
            Assert.AreEqual(original.activeSceneName, restored.activeSceneName);
        }

        [Test]
        public void TryLoad_ReturnsFalse_OnCorruptJson()
        {
            Directory.CreateDirectory(_tempDirectory);
            string savePath = Path.Combine(_tempDirectory, SaveService.DefaultFileName);
            File.WriteAllText(savePath, "{ not valid json");

            bool loaded = _saveService.TryLoad(out SaveData restored);

            Assert.IsFalse(loaded);
            Assert.IsNull(restored);
        }

        [Test]
        public void TryLoad_DoesNotThrow_OnCorruptJson()
        {
            Directory.CreateDirectory(_tempDirectory);
            string savePath = Path.Combine(_tempDirectory, SaveService.DefaultFileName);
            File.WriteAllText(savePath, "{ not valid json");

            Assert.DoesNotThrow(() => _saveService.TryLoad(out _));
        }

        [Test]
        public void TryLoad_RecoversFromBackup_WhenPrimaryCorrupt()
        {
            var first = new SaveData { playerLevel = 3, playerXp = 10, maxHp = 100, maxMp = 50 };
            var second = new SaveData { playerLevel = 9, playerXp = 99, maxHp = 100, maxMp = 50 };

            _saveService.Save(first);
            _saveService.Save(second);

            string primaryPath = Path.Combine(_tempDirectory, SaveService.DefaultFileName);
            File.WriteAllText(primaryPath, "{ not valid json");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Primary save unreadable"));

            Assert.IsTrue(_saveService.TryLoad(out SaveData restored));
            Assert.AreEqual(3, restored.playerLevel);
            Assert.AreEqual(10, restored.playerXp);
        }

        [Test]
        public void TryLoad_ReturnsFalse_WhenPrimaryAndBackupCorrupt()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(Path.Combine(_tempDirectory, SaveService.DefaultFileName), "{ bad");
            File.WriteAllText(Path.Combine(_tempDirectory, SaveService.DefaultBackupFileName), "{ worse");

            Assert.IsFalse(_saveService.TryLoad(out SaveData restored));
            Assert.IsNull(restored);
        }

        [Test]
        public void TryLoad_IgnoresCorruptStagingFile_AndLoadsPrimary()
        {
            var original = new SaveData { playerLevel = 5, maxHp = 100, maxMp = 50 };
            _saveService.Save(original);

            string stagingPath = Path.Combine(_tempDirectory, SaveService.DefaultFileName + ".tmp");
            File.WriteAllText(stagingPath, "{ truncated write");

            Assert.IsTrue(_saveService.TryLoad(out SaveData restored));
            Assert.AreEqual(5, restored.playerLevel);
        }

        [Test]
        public void Save_SecondWrite_MovesPreviousPrimaryIntoBackup()
        {
            var first = new SaveData { playerLevel = 1, maxHp = 100, maxMp = 50 };
            var second = new SaveData { playerLevel = 2, maxHp = 100, maxMp = 50 };

            _saveService.Save(first);
            _saveService.Save(second);

            string backupPath = Path.Combine(_tempDirectory, SaveService.DefaultBackupFileName);
            Assert.IsTrue(File.Exists(backupPath));

            string backupJson = File.ReadAllText(backupPath);
            SaveData fromBackup = JsonUtility.FromJson<SaveData>(backupJson);
            Assert.AreEqual(1, fromBackup.playerLevel);
        }

        [Test]
        public void HasSave_IsTrue_WhenOnlyBackupExists()
        {
            Directory.CreateDirectory(_tempDirectory);
            var payload = new SaveData { playerLevel = 7, maxHp = 100, maxMp = 50 };
            string json = JsonUtility.ToJson(payload, prettyPrint: true);
            File.WriteAllText(Path.Combine(_tempDirectory, SaveService.DefaultBackupFileName), json);

            Assert.IsTrue(_saveService.HasSave());
            Assert.IsTrue(_saveService.TryLoad(out SaveData loaded));
            Assert.AreEqual(7, loaded.playerLevel);
        }
    }
}
