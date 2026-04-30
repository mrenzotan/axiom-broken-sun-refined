using NUnit.Framework;
using Axiom.Core;

namespace Axiom.Core.Tests
{
    public class PlayerStateTutorialFlagsTests
    {
        private static PlayerState NewState() =>
            new PlayerState(maxHp: 40, maxMp: 33, attack: 5, defense: 3, speed: 4);

        [Test]
        public void NewPlayerState_AllTutorialFlags_DefaultToFalse()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(ps.HasSeenFirstDeath);
            Assert.IsFalse(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkFirstDeathSeen_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsTrue(ps.HasSeenFirstDeath);
            Assert.IsFalse(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkFirstSpikeHitSeen_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstSpikeHitSeen();
            Assert.IsTrue(ps.HasSeenFirstSpikeHit);
            Assert.IsFalse(ps.HasSeenFirstDeath);
        }

        [Test]
        public void MarkFirstBattleTutorialCompleted_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkFirstBattleTutorialCompleted();
            Assert.IsTrue(ps.HasCompletedFirstBattleTutorial);
            Assert.IsFalse(ps.HasCompletedSpellTutorialBattle);
        }

        [Test]
        public void MarkSpellTutorialBattleCompleted_FlipsOnlyThatFlag()
        {
            PlayerState ps = NewState();
            ps.MarkSpellTutorialBattleCompleted();
            Assert.IsTrue(ps.HasCompletedSpellTutorialBattle);
            Assert.IsFalse(ps.HasCompletedFirstBattleTutorial);
        }

        [Test]
        public void RestoreTutorialFlags_AppliesAllFour()
        {
            PlayerState ps = NewState();
            ps.RestoreTutorialFlags(
                hasSeenFirstDeath: true,
                hasSeenFirstSpikeHit: true,
                hasCompletedFirstBattleTutorial: true,
                hasCompletedSpellTutorialBattle: true);
            Assert.IsTrue(ps.HasSeenFirstDeath);
            Assert.IsTrue(ps.HasSeenFirstSpikeHit);
            Assert.IsTrue(ps.HasCompletedFirstBattleTutorial);
            Assert.IsTrue(ps.HasCompletedSpellTutorialBattle);
        }
    }
}
