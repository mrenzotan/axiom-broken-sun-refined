using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class PlayerDeathResolverTests
    {
        [Test]
        public void Resolve_HpAboveZero_ReturnsNone()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 10,
                activatedCheckpointIds: new List<string>());

            Assert.AreEqual(PlayerDeathOutcome.None, outcome);
        }

        [Test]
        public void Resolve_HpZeroAndNoActivatedCheckpoints_ReturnsGameOver()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: new List<string>());

            Assert.AreEqual(PlayerDeathOutcome.GameOver, outcome);
        }

        [Test]
        public void Resolve_HpZeroAndCheckpointActivated_ReturnsRespawn()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: new List<string> { "CP_Level_1_1_Start" });

            Assert.AreEqual(PlayerDeathOutcome.RespawnAtLastCheckpoint, outcome);
        }

        [Test]
        public void Resolve_NullCheckpointList_TreatedAsEmpty()
        {
            var outcome = PlayerDeathResolver.Resolve(
                currentHp: 0,
                activatedCheckpointIds: null);

            Assert.AreEqual(PlayerDeathOutcome.GameOver, outcome);
        }
    }
}
