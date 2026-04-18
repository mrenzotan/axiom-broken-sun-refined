using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Axiom.Battle;
using Axiom.Data;

namespace BattleTests
{
    public class PostBattleOutcomeServiceTests
    {
        private static EnemyData NewEnemy(int xp, List<LootEntry> loot = null)
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.enemyName = "TestEnemy";
            e.maxHP = 10; e.maxMP = 0; e.atk = 1; e.def = 0; e.spd = 1;
            e.xpReward = xp;
            e.loot = loot ?? new List<LootEntry>();
            return e;
        }

        private static ItemData NewItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.displayName = id;
            return item;
        }

        private static LootEntry Entry(ItemData item, float dropChance) =>
            new LootEntry { item = item, dropChance = dropChance };

        [Test]
        public void ResolveVictory_NullEnemy_Throws()
        {
            var service = new PostBattleOutcomeService();
            Assert.Throws<ArgumentNullException>(
                () => service.ResolveVictory(null, new System.Random(0)));
        }

        [Test]
        public void ResolveVictory_NullRandom_Throws()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10);
            Assert.Throws<ArgumentNullException>(
                () => service.ResolveVictory(enemy, null));
        }

        [Test]
        public void ResolveVictory_PassesThroughXpReward()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 42);

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(42, result.Xp);
        }

        [Test]
        public void ResolveVictory_ZeroXpReward_ReturnsZeroXp()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 0);

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Xp);
        }

        [Test]
        public void ResolveVictory_EmptyLoot_ReturnsEmptyItems()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10, loot: new List<LootEntry>());

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_GuaranteedDrop_ReturnsItem()
        {
            var potion = NewItem("potion");
            var loot   = new List<LootEntry> { Entry(potion, 1f) };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("potion", result.Items[0].ItemId);
            Assert.AreEqual(1, result.Items[0].Quantity);
        }

        [Test]
        public void ResolveVictory_ZeroChanceDrop_ReturnsNothing()
        {
            var potion = NewItem("potion");
            var loot   = new List<LootEntry> { Entry(potion, 0f) };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_NullItemInEntry_IsSkipped()
        {
            var loot  = new List<LootEntry> { Entry(null, 1f) };
            var enemy = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_EntryWithEmptyItemId_IsSkipped()
        {
            var item  = NewItem(id: string.Empty);
            var loot  = new List<LootEntry> { Entry(item, 1f) };
            var enemy = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void ResolveVictory_IsDeterministicForSeed()
        {
            var potion = NewItem("potion");
            var ether  = NewItem("ether");
            var loot   = new List<LootEntry>
            {
                Entry(potion, 0.5f),
                Entry(ether,  0.5f),
            };
            var enemy  = NewEnemy(xp: 10, loot: loot);
            var service = new PostBattleOutcomeService();

            var a = service.ResolveVictory(enemy, new System.Random(1234));
            var b = service.ResolveVictory(enemy, new System.Random(1234));

            Assert.AreEqual(a.Items.Count, b.Items.Count);
            for (int i = 0; i < a.Items.Count; i++)
            {
                Assert.AreEqual(a.Items[i].ItemId, b.Items[i].ItemId);
                Assert.AreEqual(a.Items[i].Quantity, b.Items[i].Quantity);
            }
        }

        [Test]
        public void ResolveVictory_NullLootList_ReturnsEmptyItems()
        {
            var service = new PostBattleOutcomeService();
            var enemy   = NewEnemy(xp: 10);
            enemy.loot  = null;

            var result = service.ResolveVictory(enemy, new System.Random(0));

            Assert.AreEqual(0, result.Items.Count);
        }
    }
}
