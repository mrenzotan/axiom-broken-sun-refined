using NUnit.Framework;
using Axiom.Battle;

namespace Axiom.Tests.UI
{
    public class StatusMessageQueueTests
    {
        [Test]
        public void Post_FirstMessage_DisplaysOnOneLine()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Enemy attacks!");
            Assert.AreEqual("Enemy attacks!", queue.GetDisplay());
        }

        [Test]
        public void Post_SecondMessage_DisplaysBothLines()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Enemy attacks!");
            queue.Post("Kael takes 8 damage.");
            Assert.AreEqual("Enemy attacks!\nKael takes 8 damage.", queue.GetDisplay());
        }

        [Test]
        public void Post_ThirdMessage_OldestLineDropped()
        {
            var queue = new StatusMessageQueue();
            queue.Post("Line one.");
            queue.Post("Line two.");
            queue.Post("Line three.");
            Assert.AreEqual("Line two.\nLine three.", queue.GetDisplay());
        }

        [Test]
        public void GetDisplay_BeforeAnyPost_ReturnsEmpty()
        {
            var queue = new StatusMessageQueue();
            Assert.AreEqual(string.Empty, queue.GetDisplay());
        }
    }
}
