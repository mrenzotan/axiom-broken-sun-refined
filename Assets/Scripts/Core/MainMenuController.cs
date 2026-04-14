using System;

namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for the main menu. No Unity dependencies — fully testable in Edit Mode.
    /// Instantiated by <see cref="MainMenuUI"/> with real GameManager delegates in Start().
    /// Test code injects stubs via the constructor.
    /// </summary>
    public sealed class MainMenuController
    {
        private readonly Func<bool> _hasSaveFile;
        private readonly Action _startNewGame;
        private readonly Action _continueGame;
        private readonly Action _quit;

        /// <param name="hasSaveFile">Returns true when a valid save file exists on disk.</param>
        /// <param name="startNewGame">Resets state and loads Platformer for a fresh playthrough.</param>
        /// <param name="continueGame">Loads save data then loads the saved scene.</param>
        /// <param name="quit">Optional — exits the application when the Quit button is used.</param>
        public MainMenuController(Func<bool> hasSaveFile, Action startNewGame, Action continueGame, Action quit = null)
        {
            _hasSaveFile  = hasSaveFile  ?? throw new ArgumentNullException(nameof(hasSaveFile));
            _startNewGame = startNewGame ?? throw new ArgumentNullException(nameof(startNewGame));
            _continueGame = continueGame ?? throw new ArgumentNullException(nameof(continueGame));
            _quit = quit;
        }

        /// <summary>Returns true when a valid save file exists on disk.</summary>
        public bool CanContinue() => _hasSaveFile();

        /// <summary>Starts a fresh playthrough. Always callable regardless of save state.</summary>
        public void OnNewGameClicked() => _startNewGame();

        /// <summary>Resumes from save. No-op when <see cref="CanContinue"/> is false.</summary>
        public void OnContinueClicked()
        {
            if (!CanContinue()) return;
            _continueGame();
        }

        /// <summary>Exits the game. No-op when no quit delegate was supplied.</summary>
        public void OnQuitClicked() => _quit?.Invoke();
    }
}
