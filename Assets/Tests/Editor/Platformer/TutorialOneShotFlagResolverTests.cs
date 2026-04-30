using NUnit.Framework;
using Axiom.Core;
using Axiom.Platformer;

namespace Axiom.Platformer.Tests
{
    public class TutorialOneShotFlagResolverTests
    {
        private static PlayerState NewState() =>
            new PlayerState(maxHp: 40, maxMp: 33, attack: 5, defense: 3, speed: 4);

        [Test]
        public void None_AlwaysReturnsFalse()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.None));
        }

        [Test]
        public void FirstBattle_ReadsHasCompletedFirstBattleTutorial()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstBattle));
            ps.MarkFirstBattleTutorialCompleted();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstBattle));
        }

        [Test]
        public void SpellTutorialBattle_ReadsHasCompletedSpellTutorialBattle()
        {
            PlayerState ps = NewState();
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.SpellTutorialBattle));
            ps.MarkSpellTutorialBattleCompleted();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.SpellTutorialBattle));
        }

        [Test]
        public void FirstSpikeHit_ReadsHasSeenFirstSpikeHit()
        {
            PlayerState ps = NewState();
            ps.MarkFirstSpikeHitSeen();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstSpikeHit));
        }

        [Test]
        public void FirstDeath_ReadsHasSeenFirstDeath()
        {
            PlayerState ps = NewState();
            ps.MarkFirstDeathSeen();
            Assert.IsTrue(TutorialOneShotFlagResolver.IsFlagSet(ps, OneShotTutorialFlag.FirstDeath));
        }

        [Test]
        public void NullPlayerState_AlwaysReturnsFalse()
        {
            Assert.IsFalse(TutorialOneShotFlagResolver.IsFlagSet(null, OneShotTutorialFlag.FirstDeath));
        }
    }
}
