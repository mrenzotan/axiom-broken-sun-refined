using System;
using System.IO;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;

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
    }
}
