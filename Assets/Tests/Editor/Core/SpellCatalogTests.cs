using System.Linq;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class SpellCatalogTests
    {
        private static SpellData MakeSpell(string name, int requiredLevel)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.unlockCondition = new SpellUnlockCondition { requiredLevel = requiredLevel };
            return spell;
        }

        private static SpellCatalog MakeCatalog(params SpellData[] spells)
        {
            SpellCatalog catalog = ScriptableObject.CreateInstance<SpellCatalog>();
            catalog.SetSpellsForTests(spells);
            return catalog;
        }

        [Test]
        public void TryGetByName_ReturnsMatchingSpell()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellCatalog catalog = MakeCatalog(freeze);

            bool found = catalog.TryGetByName("freeze", out SpellData result);

            Assert.IsTrue(found);
            Assert.AreSame(freeze, result);
        }

        [Test]
        public void TryGetByName_ReturnsFalseForMissingSpell()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            bool found = catalog.TryGetByName("combust", out SpellData result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetByName_NullOrEmptyReturnsFalse()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            Assert.IsFalse(catalog.TryGetByName(null, out _));
            Assert.IsFalse(catalog.TryGetByName(string.Empty, out _));
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_IncludesMatchingLevels()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellData lategame = MakeSpell("crystal spike", 5);
            SpellCatalog catalog = MakeCatalog(starter, midgame, lategame);

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(3).ToArray();

            CollectionAssert.AreEquivalent(new[] { starter, midgame }, result);
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_ExcludesStoryOnlySpells()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData storyOnly = MakeSpell("ancient burn", 0);
            SpellCatalog catalog = MakeCatalog(starter, storyOnly);

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(10).ToArray();

            CollectionAssert.AreEquivalent(new[] { starter }, result);
        }

        [Test]
        public void GetUnlocksAtOrBelowLevel_ZeroLevelReturnsEmpty()
        {
            SpellCatalog catalog = MakeCatalog(MakeSpell("freeze", 1));

            SpellData[] result = catalog.GetUnlocksAtOrBelowLevel(0).ToArray();

            CollectionAssert.IsEmpty(result);
        }
    }
}
