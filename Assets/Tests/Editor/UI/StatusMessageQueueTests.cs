using System;
using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Tests.UI
{
    public class StatusMessageQueueTests
    {
        [Test]
        public void Post_FirstMessage_BecomesBusyOnce()
        {
            var queue = new StatusMessageQueue();
            int busyCount = 0;
            queue.BusyStateChanged += isBusy =>
            {
                if (isBusy) busyCount++;
            };

            queue.Post("First");
            queue.Post("Second");

            Assert.IsTrue(queue.IsBusy);
            Assert.AreEqual(1, busyCount);
        }

        [Test]
        public void Continue_WhileRevealing_CompletesCurrentWithoutAdvancing()
        {
            var queue = new StatusMessageQueue(charsPerSecond: 1f);
            queue.Post("First");
            queue.Post("Second");

            queue.Continue();

            Assert.AreEqual("First", queue.VisibleText);
            Assert.IsTrue(queue.IsCurrentMessageComplete);
            Assert.AreEqual(2, queue.PendingCount);
        }

        [Test]
        public void Continue_AfterReveal_AdvancesInFifoOrder()
        {
            var queue = new StatusMessageQueue(charsPerSecond: 30f);
            queue.Post("First");
            queue.Post("Second");
            queue.Continue();
            queue.Continue();

            Assert.AreEqual("Second", queue.CurrentMessage);
            Assert.AreEqual(string.Empty, queue.VisibleText);
            Assert.AreEqual(1, queue.PendingCount);
        }

        [Test]
        public void Continue_FinalMessage_BecomesIdleOnce()
        {
            var queue = new StatusMessageQueue();
            int idleCount = 0;
            queue.BusyStateChanged += isBusy =>
            {
                if (!isBusy) idleCount++;
            };
            queue.Post("Only");

            queue.Continue();
            queue.Continue();
            queue.Continue();

            Assert.IsFalse(queue.IsBusy);
            Assert.AreEqual(0, queue.PendingCount);
            Assert.AreEqual(1, idleCount);
        }

        [Test]
        public void Update_RevealsCurrentMessageThroughTypewriter()
        {
            var queue = new StatusMessageQueue(charsPerSecond: 10f);
            queue.Post("Hello");

            queue.Update(0.2f);

            Assert.AreEqual("He", queue.VisibleText);
            Assert.IsFalse(queue.IsCurrentMessageComplete);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Post_BlankMessage_ThrowsArgumentException(string message)
        {
            var queue = new StatusMessageQueue();

            Assert.Throws<ArgumentException>(() => queue.Post(message));
        }

        [Test]
        public void NewlyDisplayedQueuedMessage_StartsUnrevealed()
        {
            var queue = new StatusMessageQueue();
            queue.Post("First");
            queue.Post("Second");
            queue.Continue();
            queue.Continue();

            Assert.AreEqual("Second", queue.CurrentMessage);
            Assert.AreEqual(string.Empty, queue.VisibleText);
            Assert.IsFalse(queue.IsCurrentMessageComplete);
        }
    }
}
