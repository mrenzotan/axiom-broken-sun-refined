using NUnit.Framework;

namespace PlatformerTests
{
    public class AggroAlertGateTests
    {
        [Test]
        public void RegisterDetection_NotDetected_ReturnsFalse()
        {
            var gate = new AggroAlertGate();
            Assert.IsFalse(gate.RegisterDetection(false));
        }

        [Test]
        public void RegisterDetection_RisingEdge_ReturnsTrue()
        {
            var gate = new AggroAlertGate();
            Assert.IsTrue(gate.RegisterDetection(true));
        }

        [Test]
        public void RegisterDetection_StaysDetected_FiresOnlyOnce()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);
            Assert.IsFalse(
                gate.RegisterDetection(true),
                "Indicator must fire once on detection, not every frame the player stays in range.");
        }

        [Test]
        public void RegisterDetection_FallingEdge_ReturnsFalse()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);
            Assert.IsFalse(gate.RegisterDetection(false));
        }

        [Test]
        public void RegisterDetection_LosesThenRegainsPlayer_FiresAgain()
        {
            var gate = new AggroAlertGate();
            gate.RegisterDetection(true);   // first detection
            gate.RegisterDetection(false);  // player leaves radius
            Assert.IsTrue(
                gate.RegisterDetection(true),
                "Re-entering the aggro radius is a new detection and must fire again.");
        }
    }
}
