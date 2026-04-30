using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class BossVictoryCheckerTests
    {
        [Test]
        public void IsVictorious_BossIdInDefeatedSet_ReturnsTrue()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "frost_melt_sentinel_01" },
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsTrue(result);
        }

        [Test]
        public void IsVictorious_BossIdNotInDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "meltspawn_01", "meltspawn_02" },
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_EmptyDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string>(),
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_NullBossId_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: new List<string> { "frost_melt_sentinel_01" },
                bossEnemyId: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsVictorious_NullDefeatedSet_ReturnsFalse()
        {
            bool result = BossVictoryChecker.IsVictorious(
                defeatedEnemyIds: null,
                bossEnemyId: "frost_melt_sentinel_01");

            Assert.IsFalse(result);
        }
    }
}
