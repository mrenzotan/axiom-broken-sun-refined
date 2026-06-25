using Axiom.Platformer;
using NUnit.Framework;

namespace Axiom.Platformer.Tests
{
    public class TutorialPauseGateTests
    {
        [Test]
        public void PauseEveryEntry_AlwaysReturnsTrue()
        {
            var gate = new TutorialPauseGate();
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: false));
        }

        [Test]
        public void PauseOnlyOnce_PausesFirstEntryThenNeverAgain()
        {
            var gate = new TutorialPauseGate();
            Assert.IsTrue(gate.ShouldPause(pauseOnlyOnce: true), "first entry must pause");
            Assert.IsFalse(gate.ShouldPause(pauseOnlyOnce: true), "second entry must not pause");
            Assert.IsFalse(gate.ShouldPause(pauseOnlyOnce: true), "third entry must not pause");
        }

        [Test]
        public void HasPaused_IsFalseUntilFirstPause()
        {
            var gate = new TutorialPauseGate();
            Assert.IsFalse(gate.HasPaused);
            gate.ShouldPause(pauseOnlyOnce: true);
            Assert.IsTrue(gate.HasPaused);
        }
    }
}
