using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class BurnableObstacleTests
    {
        [Test]
        public void CanIgnite_NullSpellId_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite(null, igniteSpellIds));
        }

        [Test]
        public void CanIgnite_EmptySpellId_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite(string.Empty, igniteSpellIds));
        }

        [Test]
        public void CanIgnite_NullList_ReturnsFalse()
        {
            Assert.IsFalse(BurnableObstacle.CanIgnite("combust", null));
        }

        [Test]
        public void CanIgnite_SpellInList_ReturnsTrue()
        {
            var igniteSpellIds = new List<string> { "combust", "ancient burn" };
            Assert.IsTrue(BurnableObstacle.CanIgnite("combust", igniteSpellIds));
        }

        [Test]
        public void CanIgnite_SpellNotInList_ReturnsFalse()
        {
            var igniteSpellIds = new List<string> { "combust" };
            Assert.IsFalse(BurnableObstacle.CanIgnite("freeze", igniteSpellIds));
        }
    }
}
