using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoreTests
{
    public class SaveDataSerializationTests
    {
        [Test]
        public void SaveData_JsonUtility_RoundTrip_PreservesFields()
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

            string json = JsonUtility.ToJson(original, prettyPrint: true);
            SaveData copy = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(original.playerLevel, copy.playerLevel);
            Assert.AreEqual(original.playerXp, copy.playerXp);
            Assert.AreEqual(original.currentHp, copy.currentHp);
            Assert.AreEqual(original.currentMp, copy.currentMp);
            Assert.AreEqual(original.maxHp, copy.maxHp);
            Assert.AreEqual(original.maxMp, copy.maxMp);
            Assert.AreEqual(original.unlockedSpellIds.Length, copy.unlockedSpellIds.Length);
            Assert.AreEqual("spell_a", copy.unlockedSpellIds[0]);
            Assert.AreEqual(1, copy.inventory.Length);
            Assert.AreEqual("potion_hp", copy.inventory[0].itemId);
            Assert.AreEqual(3, copy.inventory[0].quantity);
            Assert.AreEqual(original.worldPositionX, copy.worldPositionX);
            Assert.AreEqual(original.worldPositionY, copy.worldPositionY);
            Assert.AreEqual(original.activeSceneName, copy.activeSceneName);
        }

        [Test]
        public void SaveData_JsonUtility_RoundTrip_PreservesActivatedCheckpointIds()
        {
            var original = new SaveData
            {
                activatedCheckpointIds = new[] { "cp_platformer_01", "cp_platformer_02" }
            };

            string json = JsonUtility.ToJson(original, prettyPrint: true);
            SaveData copy = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(2, copy.activatedCheckpointIds.Length);
            Assert.AreEqual("cp_platformer_01", copy.activatedCheckpointIds[0]);
            Assert.AreEqual("cp_platformer_02", copy.activatedCheckpointIds[1]);
        }

        [Test]
        public void SaveData_DefaultActivatedCheckpointIds_IsEmpty()
        {
            var data = new SaveData();
            Assert.AreEqual(0, data.activatedCheckpointIds.Length);
        }
    }
}
