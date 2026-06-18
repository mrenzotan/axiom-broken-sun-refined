using NUnit.Framework;
using Axiom.Platformer;
using UnityEngine;

namespace Axiom.Platformer.Tests
{
    [TestFixture]
    public class FallingIcicleLogicTests
    {
        [Test]
        public void TryStartWarning_WhenPlayerIsInPath_EntersWarningOnce()
        {
            var logic = new FallingIcicleLogic();

            bool startedWarning = logic.TryStartWarning(playerInPath: true);
            bool startedAgain = logic.TryStartWarning(playerInPath: true);

            Assert.IsTrue(startedWarning);
            Assert.IsFalse(startedAgain);
            Assert.AreEqual(FallingIcicleState.Warning, logic.State);
        }

        [Test]
        public void TryStartWarning_WhenPlayerIsOutsidePath_RemainsIdle()
        {
            var logic = new FallingIcicleLogic();

            bool startedWarning = logic.TryStartWarning(playerInPath: false);

            Assert.IsFalse(startedWarning);
            Assert.AreEqual(FallingIcicleState.Idle, logic.State);
        }

        [Test]
        public void TickWarning_BeforeDelay_RemainsWarning()
        {
            var logic = new FallingIcicleLogic();
            logic.TryStartWarning(playerInPath: true);

            bool shouldFall = logic.TickWarning(deltaTime: 0.25f, warningSeconds: 0.5f);

            Assert.IsFalse(shouldFall);
            Assert.AreEqual(FallingIcicleState.Warning, logic.State);
        }

        [Test]
        public void TickWarning_AfterDelay_StartsFalling()
        {
            var logic = new FallingIcicleLogic();
            logic.TryStartWarning(playerInPath: true);

            bool shouldFall = logic.TickWarning(deltaTime: 0.5f, warningSeconds: 0.5f);

            Assert.IsTrue(shouldFall);
            Assert.AreEqual(FallingIcicleState.Falling, logic.State);
        }

        [Test]
        public void MarkDisappeared_AfterFalling_MakesIcicleOneShot()
        {
            var logic = new FallingIcicleLogic();
            logic.TryStartWarning(playerInPath: true);
            logic.TickWarning(deltaTime: 0.5f, warningSeconds: 0.5f);

            logic.MarkDisappeared();
            bool restarted = logic.TryStartWarning(playerInPath: true);

            Assert.AreEqual(FallingIcicleState.Disappeared, logic.State);
            Assert.IsFalse(restarted);
        }

        [Test]
        public void CountAssignedIcicles_NullList_ReturnsZero()
        {
            int count = FallingIcicleAssignment.CountAssignedIcicles(null);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void CountAssignedIcicles_IgnoresNullEntries()
        {
            var assignedIcicles = new SpriteRenderer[]
            {
                null,
                null,
            };

            int count = FallingIcicleAssignment.CountAssignedIcicles(assignedIcicles);

            Assert.AreEqual(0, count);
        }
    }
}
