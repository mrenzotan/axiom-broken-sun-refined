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

        [Test]
        public void TickDamage_DrainsHpByPercent()
        {
            // Spec: a 10% tick on a 100/100 player should leave them at 90 HP.
            var result = HazardDamageResolver.Resolve(
                currentHp: 100,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 10);

            Assert.AreEqual(90, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }

        [Test]
        public void TickDamage_ClampsToZero()
        {
            // Spec: a tick that would overshoot zero clamps and is fatal.
            var result = HazardDamageResolver.Resolve(
                currentHp: 5,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 10);

            Assert.AreEqual(0, result.NewHp);
            Assert.IsTrue(result.IsFatal);
        }

        [Test]
        public void FirstHitPlusOneTick_KillsAtThreshold()
        {
            // Spec: 30 HP player takes a 20% first hit (→10 HP) then a 10% tick (→0 HP, fatal).
            var afterFirstHit = HazardDamageResolver.Resolve(
                currentHp: 30,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 20);

            Assert.AreEqual(10, afterFirstHit.NewHp);
            Assert.IsFalse(afterFirstHit.IsFatal);

            var afterTick = HazardDamageResolver.Resolve(
                currentHp: afterFirstHit.NewHp,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 10);

            Assert.AreEqual(0, afterTick.NewHp);
            Assert.IsTrue(afterTick.IsFatal);
        }

        [Test]
        public void ZeroPercentTick_IsNoOp()
        {
            // Spec: a 0% tick must not damage. Supports the "first-hit-only" preset
            // (where _damagePerTickPercent = 0) and the "pure attrition" preset
            // (where _firstHitDamagePercent = 0).
            var result = HazardDamageResolver.Resolve(
                currentHp: 50,
                maxHp: 100,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: 0);

            Assert.AreEqual(50, result.NewHp);
            Assert.IsFalse(result.IsFatal);
        }
    }
}
