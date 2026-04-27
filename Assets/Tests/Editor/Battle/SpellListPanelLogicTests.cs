using System.Collections.Generic;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Battle.Tests
{
    [TestFixture]
    public class SpellListPanelLogicTests
    {
        private SpellData StubSpell(string name)
        {
            var s = ScriptableObject.CreateInstance<SpellData>();
            s.spellName = name.ToLower();
            return s;
        }

        [Test]
        public void Constructor_NullSpells_ReturnsEmptyList()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.Spells, Is.Empty);
        }

        [Test]
        public void Constructor_EmptySpells_ReturnsEmptyList()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.Spells, Is.Empty);
        }

        [Test]
        public void Constructor_PopulatedSpells_StoresCopy()
        {
            var spells = new List<SpellData> { StubSpell("fireball"), StubSpell("iceshard") };
            var logic = new SpellListPanelLogic(spells);
            Assert.That(logic.Spells.Count, Is.EqualTo(2));
            Assert.That(logic.Spells[0].spellName, Is.EqualTo("fireball"));
            Assert.That(logic.Spells[1].spellName, Is.EqualTo("iceshard"));
        }

        [Test]
        public void IsEmpty_NullSpells_ReturnsTrue()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.IsEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_EmptySpells_ReturnsTrue()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.IsEmpty, Is.True);
        }

        [Test]
        public void IsEmpty_PopulatedSpells_ReturnsFalse()
        {
            var logic = new SpellListPanelLogic(new List<SpellData> { StubSpell("fireball") });
            Assert.That(logic.IsEmpty, Is.False);
        }

        [Test]
        public void SpellNames_ReturnsDisplayCaseNames()
        {
            var spells = new List<SpellData> { StubSpell("fireball"), StubSpell("ice shard") };
            var logic = new SpellListPanelLogic(spells);
            var names = logic.SpellNames;
            Assert.That(names.Count, Is.EqualTo(2));
            Assert.That(names[0], Is.EqualTo("Fireball"));
            Assert.That(names[1], Is.EqualTo("Ice Shard"));
        }

        [Test]
        public void SpellNames_EmptyList_ReturnsEmpty()
        {
            var logic = new SpellListPanelLogic(new List<SpellData>());
            Assert.That(logic.SpellNames, Is.Empty);
        }

        [Test]
        public void EmptyMessage_ReturnsConstant()
        {
            var logic = new SpellListPanelLogic(null);
            Assert.That(logic.EmptyMessage, Is.EqualTo("No spells available"));
        }

        [Test]
        public void BuildFromSpellUnlockService_NullService_ReturnsNull()
        {
            var result = SpellListPanelLogic.BuildFromSpellUnlockService(null);
            Assert.That(result, Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in Object.FindObjectsByType<SpellData>())
                Object.DestroyImmediate(obj);
        }
    }
}
