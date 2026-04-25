using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class HazardDamageResolverTests
    {
        [Test]
        public void Resolve_InstantKoMode_ReturnsZeroHp()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 80,
                maxHp: 100,
                mode: HazardMode.InstantKO,
                percentMaxHpDamage: 0);

            Assert.AreEqual(0, result.NewHp);
            Assert.IsTrue(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentDamage_SubtractsPercentOfMax()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 80,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 20);

            Assert.AreEqual(60, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentDamageExceedingCurrentHp_ClampsToZeroAndIsFatal()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 10,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 50);

            Assert.AreEqual(0, result.NewHp);
            Assert.IsTrue(result.IsFatal);
        }

        [Test]
        public void Resolve_PercentRoundsUp_SoOneHpDamageNeverZero()
        {
            var result = HazardDamageResolver.Resolve(
                currentHp: 100,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 1);

            Assert.AreEqual(99, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }

        [Test]
        public void Resolve_MaxHpZero_ThrowsArgumentOutOfRange()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                HazardDamageResolver.Resolve(
                    currentHp: 0,
                    maxHp: 0,
                    mode: HazardMode.PercentMaxHpDamage,
                    percentMaxHpDamage: 20));
        }
    }
}
