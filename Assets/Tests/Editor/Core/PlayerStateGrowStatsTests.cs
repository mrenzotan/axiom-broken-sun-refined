using System;
using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    public class PlayerStateGrowStatsTests
    {
        private static PlayerState NewState() =>
            new PlayerState(maxHp: 100, maxMp: 30, attack: 12, defense: 6, speed: 8);

        [Test]
        public void GrowStats_IncreasesMaxValuesByDeltas()
        {
            PlayerState state = NewState();

            state.GrowStats(deltaMaxHp: 20, deltaMaxMp: 5, deltaAttack: 3, deltaDefense: 2, deltaSpeed: 1);

            Assert.AreEqual(120, state.MaxHp);
            Assert.AreEqual(35,  state.MaxMp);
            Assert.AreEqual(15,  state.Attack);
            Assert.AreEqual(8,   state.Defense);
            Assert.AreEqual(9,   state.Speed);
        }

        [Test]
        public void GrowStats_HealsCurrentHpAndMpToNewMax()
        {
            PlayerState state = NewState();
            state.SetCurrentHp(40);
            state.SetCurrentMp(10);

            state.GrowStats(deltaMaxHp: 20, deltaMaxMp: 5, deltaAttack: 0, deltaDefense: 0, deltaSpeed: 0);

            Assert.AreEqual(120, state.CurrentHp);
            Assert.AreEqual(35,  state.CurrentMp);
        }

        [Test]
        public void GrowStats_ZeroDeltasLeavesValuesUnchanged()
        {
            PlayerState state = NewState();
            state.SetCurrentHp(60);

            state.GrowStats(0, 0, 0, 0, 0);

            Assert.AreEqual(100, state.MaxHp);
            Assert.AreEqual(60,  state.CurrentHp);
            Assert.AreEqual(12,  state.Attack);
        }

        [Test]
        public void GrowStats_RejectsNegativeDeltas()
        {
            PlayerState state = NewState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.GrowStats(-1, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GrowStats(0, -1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GrowStats(0, 0, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GrowStats(0, 0, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.GrowStats(0, 0, 0, 0, -1));
        }
    }
}
