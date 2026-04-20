using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    [TestFixture]
    public class PauseMenuLogicTests
    {
        private PauseMenuLogic _logic;

        [SetUp]
        public void SetUp()
        {
            _logic = new PauseMenuLogic();
        }

        [Test]
        public void InitialState_IsNotPaused()
        {
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }

        [Test]
        public void Pause_SetsIsPausedTrue_AndActivatesMainPanel()
        {
            _logic.Pause();
            Assert.IsTrue(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
        }

        [Test]
        public void Resume_SetsIsPausedFalse_AndClosesPanel()
        {
            _logic.Pause();
            _logic.Resume();
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }

        [Test]
        public void TogglePause_ClosedToMain_TogglesPaused()
        {
            _logic.TogglePause();
            Assert.IsTrue(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
        }

        [Test]
        public void TogglePause_MainToClosed_Resumes()
        {
            _logic.Pause();
            _logic.TogglePause();
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }

        [Test]
        public void OpenSettings_FromMain_SwitchesPanel()
        {
            _logic.Pause();
            _logic.OpenSettings();
            Assert.IsTrue(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Settings, _logic.ActivePanel);
        }

        [Test]
        public void CloseSettings_ReturnsToMain()
        {
            _logic.Pause();
            _logic.OpenSettings();
            _logic.CloseSettings();
            Assert.IsTrue(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
        }

        [Test]
        public void OpenSettings_WhenNotPaused_IsNoOp()
        {
            _logic.OpenSettings();
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }

        [Test]
        public void CloseSettings_WhenOnMain_IsNoOp()
        {
            _logic.Pause();
            _logic.CloseSettings();
            Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
        }

        [Test]
        public void Resume_FromSettings_ResumesDirectly()
        {
            _logic.Pause();
            _logic.OpenSettings();
            _logic.Resume();
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }

        [Test]
        public void Pause_WhenAlreadyPaused_IsNoOp()
        {
            _logic.Pause();
            _logic.Pause();
            Assert.AreEqual(PauseMenuPanel.Main, _logic.ActivePanel);
        }

        [Test]
        public void Resume_WhenNotPaused_IsNoOp()
        {
            _logic.Resume();
            Assert.IsFalse(_logic.IsPaused);
            Assert.AreEqual(PauseMenuPanel.Closed, _logic.ActivePanel);
        }
    }
}
