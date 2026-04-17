using System;
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;
using UnityEngine;

namespace BattleTests
{
    public class ItemEffectResolverTests
    {
        private static CharacterStats MakeStats(int maxHp, int maxMp = 30, int atk = 10, int def = 5)
        {
            var s = new CharacterStats { MaxHP = maxHp, MaxMP = maxMp, ATK = atk, DEF = def, SPD = 5 };
            s.Initialize();
            return s;
        }

        private static ItemData MakeItem(ItemEffectType effect, int power,
            List<ChemicalCondition> cures = null)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = "test_item";
            item.displayName = "Test Item";
            item.effectType = effect;
            item.effectPower = power;
            item.curesConditions = cures ?? new List<ChemicalCondition>();
            return item;
        }

        // ── Null guards ─────────────────────────────────────────────────────

        [Test]
        public void Resolve_NullItem_Throws()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null, target));
        }

        [Test]
        public void Resolve_NullTarget_Throws()
        {
            var resolver = new ItemEffectResolver();
            var item = MakeItem(ItemEffectType.RestoreHP, 50);
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(item, null));
        }

        // ── RestoreHP ───────────────────────────────────────────────────────

        [Test]
        public void Resolve_RestoreHP_HealsTarget()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(40); // HP = 60

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreHP, 30), target);

            Assert.AreEqual(90, target.CurrentHP);
            Assert.AreEqual(ItemEffectType.RestoreHP, result.EffectType);
            Assert.AreEqual(30, result.Amount);
        }

        [Test]
        public void Resolve_RestoreHP_ClampsToMax()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(10); // HP = 90

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreHP, 50), target);

            Assert.AreEqual(100, target.CurrentHP);
            Assert.AreEqual(10, result.Amount); // only 10 HP was actually restored
        }

        // ── RestoreMP ───────────────────────────────────────────────────────

        [Test]
        public void Resolve_RestoreMP_RestoresMP()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.SpendMP(30); // MP = 20

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreMP, 25), target);

            Assert.AreEqual(45, target.CurrentMP);
            Assert.AreEqual(ItemEffectType.RestoreMP, result.EffectType);
            Assert.AreEqual(25, result.Amount);
        }

        [Test]
        public void Resolve_RestoreMP_ClampsToMax()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.SpendMP(5); // MP = 45

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.RestoreMP, 50), target);

            Assert.AreEqual(50, target.CurrentMP);
            Assert.AreEqual(5, result.Amount);
        }

        // ── Revive ──────────────────────────────────────────────────────────

        [Test]
        public void Resolve_Revive_OnDefeatedTarget_RestoresHP()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(100); // HP = 0, defeated
            Assert.IsTrue(target.IsDefeated);

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.Revive, 50), target);

            Assert.AreEqual(50, target.CurrentHP);
            Assert.IsFalse(target.IsDefeated);
            Assert.AreEqual(ItemEffectType.Revive, result.EffectType);
            Assert.AreEqual(50, result.Amount);
        }

        [Test]
        public void Resolve_Revive_OnAliveTarget_IsNoOp()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.TakeDamage(30); // HP = 70, alive

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.Revive, 50), target);

            Assert.AreEqual(70, target.CurrentHP); // unchanged
            Assert.AreEqual(0, result.Amount);
        }

        // ── None ────────────────────────────────────────────────────────────

        [Test]
        public void Resolve_None_DoesNotChangeStats()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100, maxMp: 50);
            target.TakeDamage(20);

            ItemUseResult result = resolver.Resolve(
                MakeItem(ItemEffectType.None, 0), target);

            Assert.AreEqual(80, target.CurrentHP);
            Assert.AreEqual(50, target.CurrentMP);
            Assert.AreEqual(0, result.Amount);
        }

        // ── Condition curing ────────────────────────────────────────────────

        [Test]
        public void Resolve_CuresConditions_RemovesListedConditions()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 3);
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Burning));

            var cures = new List<ChemicalCondition> { ChemicalCondition.Burning };
            resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target);

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Burning));
        }

        [Test]
        public void Resolve_CuresConditions_IgnoresNoneCondition()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);

            var cures = new List<ChemicalCondition> { ChemicalCondition.None };
            Assert.DoesNotThrow(() =>
                resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target));
        }

        [Test]
        public void Resolve_CuresConditions_NotPresent_IsNoOp()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);

            var cures = new List<ChemicalCondition> { ChemicalCondition.Frozen };
            Assert.DoesNotThrow(() =>
                resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target));

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Frozen));
        }

        [Test]
        public void Resolve_CuresMultipleConditions_RemovesAll()
        {
            var resolver = new ItemEffectResolver();
            var target = MakeStats(100);
            target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5, duration: 3);
            target.ApplyStatusCondition(ChemicalCondition.Frozen, baseDamage: 5, duration: 3);
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Burning));
            Assert.IsTrue(target.HasCondition(ChemicalCondition.Frozen));

            var cures = new List<ChemicalCondition>
                { ChemicalCondition.Burning, ChemicalCondition.Frozen };
            resolver.Resolve(MakeItem(ItemEffectType.RestoreHP, 10, cures), target);

            Assert.IsFalse(target.HasCondition(ChemicalCondition.Burning));
            Assert.IsFalse(target.HasCondition(ChemicalCondition.Frozen));
        }
    }
}
