using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class FreezablePlatformTests
    {
        [Test]
        public void CanFreeze_NullSpellId_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze(null, freezeSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanFreeze_EmptySpellId_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze(string.Empty, freezeSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanFreeze_SpellInList_ReturnsTrue()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze("freeze", freezeSpellIds);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanFreeze_SpellNotInList_ReturnsFalse()
        {
            var freezeSpellIds = new List<string> { "freeze" };

            bool result = FreezablePlatform.CanFreeze("combust", freezeSpellIds);

            Assert.IsFalse(result);
        }
    }
}
