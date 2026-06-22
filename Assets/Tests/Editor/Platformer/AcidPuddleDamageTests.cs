using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class AcidPuddleDamageTests
    {
        [Test]
        public void PercentForTick_TickZero_ReturnsBase()
        {
            // WHY: first contact is the mild base tick — acid escalates FROM here.
            Assert.AreEqual(3, AcidPuddleDamage.PercentForTick(0, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_TickOne_ReturnsBaseTimesGrowthRounded()
        {
            // 3 * 1.6 = 4.8 -> 5 (round away from zero).
            Assert.AreEqual(5, AcidPuddleDamage.PercentForTick(1, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_StrictlyEscalatesUntilCap()
        {
            // WHY: the core requirement — damage must INCREASE each tick, not be flat.
            int t0 = AcidPuddleDamage.PercentForTick(0, 3, 1.6f, 25);
            int t1 = AcidPuddleDamage.PercentForTick(1, 3, 1.6f, 25);
            int t2 = AcidPuddleDamage.PercentForTick(2, 3, 1.6f, 25);
            int t3 = AcidPuddleDamage.PercentForTick(3, 3, 1.6f, 25);
            Assert.Less(t0, t1);
            Assert.Less(t1, t2);
            Assert.Less(t2, t3);
        }

        [Test]
        public void PercentForTick_LargeTick_ClampsToMax()
        {
            // WHY: escalation must be BOUNDED — an unbounded curve would one-shot the player.
            Assert.AreEqual(25, AcidPuddleDamage.PercentForTick(20, 3, 1.6f, 25));
        }

        [Test]
        public void PercentForTick_NegativeTick_TreatedAsZero()
        {
            // Defensive: a stray negative index must not produce negative/garbage damage.
            Assert.AreEqual(3, AcidPuddleDamage.PercentForTick(-1, 3, 1.6f, 25));
        }
    }
}
