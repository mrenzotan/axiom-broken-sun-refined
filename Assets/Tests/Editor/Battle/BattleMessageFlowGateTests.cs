using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Axiom.Battle.Tests
{
    public class BattleMessageFlowGateTests
    {
        [Test]
        public void ContinueWhenReady_WhileUnblocked_ExecutesImmediately()
        {
            var gate = new BattleMessageFlowGate();
            bool called = false;

            gate.ContinueWhenReady(() => called = true);

            Assert.IsTrue(called);
        }

        [Test]
        public void ContinueWhenReady_NullContinuation_ThrowsArgumentNullException()
        {
            var gate = new BattleMessageFlowGate();

            Assert.Throws<ArgumentNullException>(() => gate.ContinueWhenReady(null));
        }

        [Test]
        public void ContinueWhenReady_WhileBlocked_DefersUntilUnblocked()
        {
            var gate = new BattleMessageFlowGate();
            bool called = false;
            gate.SetBlocked(true);

            gate.ContinueWhenReady(() => called = true);

            Assert.IsFalse(called);
            gate.SetBlocked(false);
            Assert.IsTrue(called);
        }

        [Test]
        public void SetBlocked_False_ReleasesContinuationsInFifoOrder()
        {
            var gate = new BattleMessageFlowGate();
            var calls = new List<int>();
            gate.SetBlocked(true);
            gate.ContinueWhenReady(() => calls.Add(1));
            gate.ContinueWhenReady(() => calls.Add(2));

            gate.SetBlocked(false);

            CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
        }

        [Test]
        public void SetBlocked_RepeatedTrue_DoesNotReleaseContinuations()
        {
            var gate = new BattleMessageFlowGate();
            bool called = false;
            gate.SetBlocked(true);
            gate.ContinueWhenReady(() => called = true);

            gate.SetBlocked(true);

            Assert.IsFalse(called);
        }

        [Test]
        public void SetBlocked_False_StopsDrainingWhenContinuationReblocks()
        {
            var gate = new BattleMessageFlowGate();
            var calls = new List<int>();
            gate.SetBlocked(true);
            gate.ContinueWhenReady(() =>
            {
                calls.Add(1);
                gate.SetBlocked(true);
            });
            gate.ContinueWhenReady(() => calls.Add(2));

            gate.SetBlocked(false);
            CollectionAssert.AreEqual(new[] { 1 }, calls);
            gate.SetBlocked(false);
            CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
        }
    }
}
