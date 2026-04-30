namespace Axiom.Core
{
    public enum PauseMenuPanel
    {
        Closed,
        Main,
        Settings
    }

    public sealed class PauseMenuLogic
    {
        public bool IsPaused { get; private set; }
        public PauseMenuPanel ActivePanel { get; private set; }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            ActivePanel = PauseMenuPanel.Main;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            ActivePanel = PauseMenuPanel.Closed;
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void OpenSettings()
        {
            if (!IsPaused) return;
            ActivePanel = PauseMenuPanel.Settings;
        }

        public void CloseSettings()
        {
            if (ActivePanel != PauseMenuPanel.Settings) return;
            ActivePanel = PauseMenuPanel.Main;
        }
    }
}
