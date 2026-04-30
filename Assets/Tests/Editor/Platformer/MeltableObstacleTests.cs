using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class MeltableObstacleTests
    {
        [Test]
        public void CanMelt_NullSpellId_ReturnsFalse()
        {
            var meltSpellIds = new List<string> { "combust" };

            bool result = MeltableObstacle.CanMelt(null, meltSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_EmptySpellId_ReturnsFalse()
        {
            var meltSpellIds = new List<string> { "combust" };

            bool result = MeltableObstacle.CanMelt(string.Empty, meltSpellIds);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanMelt_SpellInList_ReturnsTrue()
        {
            var meltSpellIds = new List<string> { "combust", "ignite" };

            bool result = MeltableObstacle.CanMelt("combust", meltSpellIds);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanMelt_SpellNotInList_ReturnsFalse()
        {
            var meltSpellIds = new List<string> { "combust" };

            bool result = MeltableObstacle.CanMelt("freeze", meltSpellIds);

            Assert.IsFalse(result);
        }
    }
}
