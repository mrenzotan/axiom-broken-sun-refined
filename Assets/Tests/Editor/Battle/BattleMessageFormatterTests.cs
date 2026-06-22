using System;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Battle.Tests
{
    public class BattleMessageFormatterTests
    {
        [Test]
        public void ConditionApplied_Frozen_ExplainsNextActionSkip()
        {
            Assert.AreEqual(
                "Void Wraith was Frozen! It will skip its next action.",
                BattleMessageFormatter.ConditionApplied("Void Wraith", ChemicalCondition.Frozen));
        }

        [Test]
        public void ConditionApplied_NonFrozenCondition_NamesCondition()
        {
            Assert.AreEqual(
                "Void Wraith was Burning!",
                BattleMessageFormatter.ConditionApplied("Void Wraith", ChemicalCondition.Burning));
        }

        [Test]
        public void ConditionDamage_NamesConditionAndAmount()
        {
            Assert.AreEqual(
                "Void Wraith takes 5 damage from Burning.",
                BattleMessageFormatter.ConditionDamage("Void Wraith", ChemicalCondition.Burning, 5));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConditionApplied_BlankCharacterName_ThrowsArgumentException(string characterName)
        {
            Assert.Throws<ArgumentException>(() =>
                BattleMessageFormatter.ConditionApplied(characterName, ChemicalCondition.Burning));
        }

        [Test]
        public void ConditionApplied_None_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                BattleMessageFormatter.ConditionApplied("Void Wraith", ChemicalCondition.None));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ConditionDamage_NonPositiveDamage_ThrowsArgumentOutOfRangeException(int damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleMessageFormatter.ConditionDamage("Void Wraith", ChemicalCondition.Burning, damage));
        }
    }
}
