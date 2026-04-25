using NUnit.Framework;
using Axiom.Platformer.UI;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class PlatformerHpHudFormatterTests
    {
        [Test]
        public void Format_FullHp_ReturnsCurrentSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(100, 100);
            Assert.AreEqual("HP 100/100", result);
        }

        [Test]
        public void Format_PartialHp_ReturnsCurrentSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(37, 100);
            Assert.AreEqual("HP 37/100", result);
        }

        [Test]
        public void Format_ZeroHp_ReturnsZeroSlashMax()
        {
            string result = PlatformerHpHudFormatter.Format(0, 100);
            Assert.AreEqual("HP 0/100", result);
        }

        [Test]
        public void Format_MaxHpZero_ThrowsArgumentOutOfRange()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                PlatformerHpHudFormatter.Format(0, 0));
        }
    }
}
