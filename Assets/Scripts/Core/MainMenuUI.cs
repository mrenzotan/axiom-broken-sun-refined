using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour wrapper for <see cref="MainMenuController"/>.
    /// Handles Unity lifecycle only: creates the controller in Start(),
    /// wires button listeners, and drives Continue interactability.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The New Game button on the main menu Canvas.")]
        private Button _newGameButton;

        [SerializeField]
        [Tooltip("The Continue button on the main menu Canvas.")]
        private Button _continueButton;

        [SerializeField]
        [Tooltip("The Quit button on the main menu Canvas.")]
        private Button _quitButton;

        private MainMenuController _controller;

        private void Start()
        {
            _controller = new MainMenuController(
                hasSaveFile:  () => GameManager.Instance?.HasSaveFile() ?? false,
                startNewGame: () => GameManager.Instance?.StartNewGame(),
                continueGame: () => GameManager.Instance?.TryContinueGame(),
                quit: QuitApplication);

            _continueButton.interactable = _controller.CanContinue();

            _newGameButton.onClick.AddListener(_controller.OnNewGameClicked);
            _continueButton.onClick.AddListener(_controller.OnContinueClicked);
            if (_quitButton != null)
                _quitButton.onClick.AddListener(_controller.OnQuitClicked);
        }

        private void OnDestroy()
        {
            _newGameButton?.onClick.RemoveAllListeners();
            _continueButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
