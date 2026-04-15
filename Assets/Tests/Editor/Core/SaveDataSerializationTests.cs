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

        [Test]
        public void SaveData_JsonUtility_RoundTrip_PreservesDefeatedEnemyIds()
        {
            var original = new SaveData
            {
                defeatedEnemyIds = new[] { "enemy_slime_01", "enemy_bat_02" }
            };

            string json = JsonUtility.ToJson(original, prettyPrint: true);
            SaveData copy = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(2, copy.defeatedEnemyIds.Length);
            Assert.AreEqual("enemy_slime_01", copy.defeatedEnemyIds[0]);
            Assert.AreEqual("enemy_bat_02", copy.defeatedEnemyIds[1]);
        }

        [Test]
        public void SaveData_DefaultDefeatedEnemyIds_IsEmpty()
        {
            var data = new SaveData();
            Assert.AreEqual(0, data.defeatedEnemyIds.Length);
        }

        [Test]
        public void SaveData_JsonUtility_BackwardCompat_MissingDefeatedEnemyIds_DeserializesAsEmpty()
        {
            // Legacy JSON written before DEV-61 — has no defeatedEnemyIds field.
            string legacyJson = "{\"saveVersion\":1,\"playerLevel\":3,\"playerXp\":0,\"currentHp\":50," +
                                "\"currentMp\":20,\"maxHp\":100,\"maxMp\":50," +
                                "\"unlockedSpellIds\":[],\"inventory\":[]," +
                                "\"worldPositionX\":0.0,\"worldPositionY\":0.0," +
                                "\"activeSceneName\":\"Platformer\",\"activatedCheckpointIds\":[]}";

            SaveData copy = JsonUtility.FromJson<SaveData>(legacyJson);

            Assert.IsNotNull(copy.defeatedEnemyIds);
            Assert.AreEqual(0, copy.defeatedEnemyIds.Length);
            Assert.AreEqual(3, copy.playerLevel);
        }
    }
}
