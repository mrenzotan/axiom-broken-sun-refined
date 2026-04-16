using System;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class SpellUnlockServiceTests
    {
        private static SpellData MakeSpell(string name, int requiredLevel = 0, SpellData prerequisite = null)
        {
            SpellData spell = ScriptableObject.CreateInstance<SpellData>();
            spell.spellName = name;
            spell.unlockCondition = new SpellUnlockCondition
            {
                requiredLevel = requiredLevel,
                prerequisiteSpell = prerequisite
            };
            return spell;
        }

        private static SpellCatalog MakeCatalog(params SpellData[] spells)
        {
            SpellCatalog catalog = ScriptableObject.CreateInstance<SpellCatalog>();
            catalog.SetSpellsForTests(spells);
            return catalog;
        }

        [Test]
        public void NewService_HasEmptyUnlockedList()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.AreEqual(0, service.UnlockedSpells.Count);
        }

        [Test]
        public void Unlock_AddsSpell_AndFiresEvent()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            bool result = service.Unlock(freeze);

            Assert.IsTrue(result);
            CollectionAssert.Contains(service.UnlockedSpells, freeze);
            CollectionAssert.AreEqual(new[] { freeze }, fired);
        }

        [Test]
        public void Unlock_DuplicateCall_IsSilentlyIgnored()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));

            int fireCount = 0;
            service.OnSpellUnlocked += _ => fireCount++;

            service.Unlock(freeze);
            bool secondResult = service.Unlock(freeze);

            Assert.IsFalse(secondResult);
            Assert.AreEqual(1, service.UnlockedSpells.Count);
            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void Unlock_NullSpell_ThrowsArgumentNullException()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.Throws<ArgumentNullException>(() => service.Unlock(null));
        }

        [Test]
        public void Contains_ReturnsTrueForUnlockedSpell()
        {
            SpellData freeze = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(freeze));
            service.Unlock(freeze);

            Assert.IsTrue(service.Contains(freeze));
        }

        [Test]
        public void Contains_NullReturnsFalse()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog());

            Assert.IsFalse(service.Contains(null));
        }

        [Test]
        public void NotifyPlayerLevel_GrantsEligibleSpells_AndFiresEventPerNewSpell()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellData lategame = MakeSpell("crystal spike", 5);
            SpellData storyOnly = MakeSpell("ancient burn", 0);

            SpellUnlockService service = new SpellUnlockService(
                MakeCatalog(starter, midgame, lategame, storyOnly));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            service.NotifyPlayerLevel(3);

            CollectionAssert.AreEquivalent(new[] { starter, midgame }, service.UnlockedSpells);
            CollectionAssert.AreEquivalent(new[] { starter, midgame }, fired);
        }

        [Test]
        public void NotifyPlayerLevel_DoesNotRefireForAlreadyUnlockedSpells()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellData midgame = MakeSpell("combust", 3);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(starter, midgame));

            service.NotifyPlayerLevel(1); // grants starter

            var firedAfterL1 = new List<SpellData>();
            service.OnSpellUnlocked += firedAfterL1.Add;

            service.NotifyPlayerLevel(3); // should only grant midgame

            CollectionAssert.AreEqual(new[] { midgame }, firedAfterL1);
            CollectionAssert.AreEquivalent(new[] { starter, midgame }, service.UnlockedSpells);
        }

        [Test]
        public void NotifyPlayerLevel_RespectsPrerequisiteChains()
        {
            // Spell A (level 3, no prereq) → Spell B (level 3, requires A)
            // Both become level-eligible at L3, but B can only unlock after A.
            // A single NotifyPlayerLevel(3) call should grant both in order.
            SpellData spellA = MakeSpell("freeze", 3);
            SpellData spellB = MakeSpell("shatter", 3, prerequisite: spellA);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(spellA, spellB));

            var fired = new List<SpellData>();
            service.OnSpellUnlocked += fired.Add;

            service.NotifyPlayerLevel(3);

            CollectionAssert.AreEquivalent(new[] { spellA, spellB }, service.UnlockedSpells);
            CollectionAssert.AreEquivalent(new[] { spellA, spellB }, fired);
        }

        [Test]
        public void NotifyPlayerLevel_DoesNotGrantSpellWhenPrerequisiteNotMet()
        {
            SpellData spellA = MakeSpell("freeze", 5); // higher level than B
            SpellData spellB = MakeSpell("shatter", 3, prerequisite: spellA);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(spellA, spellB));

            service.NotifyPlayerLevel(3);

            // B's level is met but prereq (A, level 5) is not unlocked yet
            CollectionAssert.IsEmpty(service.UnlockedSpells);
        }

        [Test]
        public void Constructor_NullCatalog_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SpellUnlockService(null));
        }

        [Test]
        public void RestoreFromIds_PopulatesUnlockedSpellsWithoutFiringEvent()
        {
            SpellData starter = MakeSpell("freeze", 1);
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(starter));

            int fireCount = 0;
            service.OnSpellUnlocked += _ => fireCount++;

            service.RestoreFromIds(new[] { "freeze" });

            CollectionAssert.AreEqual(new[] { starter }, service.UnlockedSpells);
            Assert.AreEqual(0, fireCount, "Save-load restore must not fire unlock events.");
        }

        [Test]
        public void RestoreFromIds_IgnoresUnknownIds()
        {
            SpellUnlockService service = new SpellUnlockService(MakeCatalog(MakeSpell("freeze", 1)));

            service.RestoreFromIds(new[] { "freeze", "nonexistent", null, string.Empty });

            Assert.AreEqual(1, service.UnlockedSpells.Count);
        }
    }
}
