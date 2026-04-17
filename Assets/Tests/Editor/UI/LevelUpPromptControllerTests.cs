using System;
using System.Collections.Generic;
using NUnit.Framework;
using Axiom.Battle.UI;
using Axiom.Core;

namespace UITests
{
    public class LevelUpPromptControllerTests
    {
        private static LevelUpResult Result(int prev, int next) =>
            new LevelUpResult(prev, next, 10, 3, 2, 1, 1);

        [Test]
        public void IsPending_FalseWhenEmpty()
        {
            var controller = new LevelUpPromptController();
            Assert.IsFalse(controller.IsPending);
        }

        [Test]
        public void Enqueue_MakesControllerPending()
        {
            var controller = new LevelUpPromptController();
            controller.Enqueue(Result(1, 2), newSpellNames: Array.Empty<string>());
            Assert.IsTrue(controller.IsPending);
        }

        [Test]
        public void Current_ReturnsFirstEnqueuedItem()
        {
            var controller = new LevelUpPromptController();
            controller.Enqueue(Result(1, 2), new[] { "freeze" });
            controller.Enqueue(Result(2, 3), new[] { "combust" });

            Assert.AreEqual(2, controller.Current.Result.NewLevel);
            CollectionAssert.AreEqual(new[] { "freeze" }, controller.Current.NewSpellNames);
        }

        [Test]
        public void Dismiss_AdvancesToNext()
        {
            var controller = new LevelUpPromptController();
            controller.Enqueue(Result(1, 2), Array.Empty<string>());
            controller.Enqueue(Result(2, 3), Array.Empty<string>());

            controller.Dismiss();

            Assert.IsTrue(controller.IsPending);
            Assert.AreEqual(3, controller.Current.Result.NewLevel);
        }

        [Test]
        public void Dismiss_LastItem_ClearsQueueAndFiresOnDismissed()
        {
            var controller = new LevelUpPromptController();
            controller.Enqueue(Result(1, 2), Array.Empty<string>());
            bool dismissedFired = false;
            controller.OnDismissed += () => dismissedFired = true;

            controller.Dismiss();

            Assert.IsFalse(controller.IsPending);
            Assert.IsTrue(dismissedFired);
        }

        [Test]
        public void Dismiss_WhenEmpty_DoesNothing()
        {
            var controller = new LevelUpPromptController();
            bool dismissedFired = false;
            controller.OnDismissed += () => dismissedFired = true;

            Assert.DoesNotThrow(() => controller.Dismiss());
            Assert.IsFalse(dismissedFired);
        }

        [Test]
        public void Enqueue_RejectsNullNewSpellNames()
        {
            var controller = new LevelUpPromptController();
            Assert.Throws<ArgumentNullException>(
                () => controller.Enqueue(Result(1, 2), null));
        }
    }
}
