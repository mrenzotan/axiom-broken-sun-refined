using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleTests
    {
        [Test]
        public void CanNeutralize_NullSpellId_ReturnsFalse()
        {
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize(null, ids));
        }

        [Test]
        public void CanNeutralize_EmptySpellId_ReturnsFalse()
        {
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize(string.Empty, ids));
        }

        [Test]
        public void CanNeutralize_NullList_ReturnsFalse()
        {
            Assert.IsFalse(AcidPuddle.CanNeutralize("neutralize", null));
        }

        [Test]
        public void CanNeutralize_SpellInList_ReturnsTrue()
        {
            // WHY: only the AcidBase neutralize spell may clear an acid puddle.
            var ids = new List<string> { "neutralize" };
            Assert.IsTrue(AcidPuddle.CanNeutralize("neutralize", ids));
        }

        [Test]
        public void CanNeutralize_SpellNotInList_ReturnsFalse()
        {
            // WHY: a wrong-pillar spell (e.g. combust) must not dissolve acid.
            var ids = new List<string> { "neutralize" };
            Assert.IsFalse(AcidPuddle.CanNeutralize("combust", ids));
        }

        [Test]
        public void CanNeutralize_CaseInsensitive_ReturnsTrue()
        {
            // WHY: spellName is stored lowercase, but matching must not depend on casing.
            var ids = new List<string> { "neutralize" };
            Assert.IsTrue(AcidPuddle.CanNeutralize("Neutralize", ids));
        }
    }
}
