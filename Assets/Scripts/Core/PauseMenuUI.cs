using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour shell for the pause menu. Handles Unity lifecycle (input polling,
    /// panel toggle, button wiring) and delegates all state decisions to
    /// <see cref="PauseMenuLogic"/>. Placed on the GameManager prefab so it persists
    /// across scene loads via DontDestroyOnLoad.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Pause Button (corner, always visible during play)")]
        [SerializeField] private Button _pauseButton;

        [Header("Overlay Panel (shown when paused)")]
        [SerializeField] private GameObject _pausePanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Settings Sub-panel")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _settingsBackButton;

        [Header("First selected button on pause open")]
        [SerializeField] private GameObject _firstSelectedOnPause;

        [Header("First selected button on settings open")]
        [SerializeField] private GameObject _firstSelectedOnSettings;

        private PauseMenuLogic _logic;

        private static bool CanPause => SceneManager.GetActiveScene().name != "MainMenu";

        private void Awake()
        {
            EnsureCanvasIsRaycastable();
            EnsureCanvasRendersOnTop();
            _logic = new PauseMenuLogic();
            ApplyPanelState();
        }

        /// <summary>
        /// Prefab mistakes (scale 0, or CanvasGroup blocking raycasts) make every button dead.
        /// Normalizes this canvas so UI input works without hand-fixing the prefab every time.
        /// </summary>
        private void EnsureCanvasIsRaycastable()
        {
            var rect = transform as RectTransform;
            if (rect != null && rect.localScale.sqrMagnitude < 1e-6f)
                rect.localScale = Vector3.one;

            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            var group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        private void EnsureCanvasRendersOnTop()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.sortingOrder < 1000)
                canvas.sortingOrder = 1000;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_logic != null && _logic.IsPaused)
                Time.timeScale = 1f;
        }

        private void Start()
        {
            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
            if (_settingsBackButton != null) _settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
            if (_settingsBackButton != null) _settingsBackButton.onClick.RemoveListener(OnSettingsBackClicked);
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseButtonClicked);

            if (_logic != null && _logic.IsPaused)
                Time.timeScale = 1f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyPanelState();
        }

        private void Update()
        {
            if (!CanPause)
            {
                if (_logic.IsPaused)
                {
                    _logic.Resume();
                    ApplyPanelState();
                }
                return;
            }

            bool toggleRequested =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

            if (toggleRequested)
            {
                if (_logic.ActivePanel == PauseMenuPanel.Settings)
                {
                    _logic.CloseSettings();
                }
                else
                {
                    _logic.TogglePause();
                }
                ApplyPanelState();
            }
        }

        private void OnPauseButtonClicked()
        {
            if (!CanPause) return;
            _logic.Pause();
            ApplyPanelState();
        }

        private void OnResumeClicked()
        {
            _logic.Resume();
            ApplyPanelState();
        }

        private void OnSettingsClicked()
        {
            _logic.OpenSettings();
            ApplyPanelState();
        }

        private void OnSettingsBackClicked()
        {
            _logic.CloseSettings();
            ApplyPanelState();
        }

        private void OnQuitClicked()
        {
            GameManager.Instance?.PersistToDisk();
            _logic.Resume();
            ApplyPanelState();

            var gm = GameManager.Instance;
            if (gm != null && gm.SceneTransition != null)
                gm.SceneTransition.BeginTransition("MainMenu", TransitionStyle.BlackFade);
            else
                SceneManager.LoadScene("MainMenu");
        }

        private void ApplyPanelState()
        {
            Time.timeScale = _logic.IsPaused ? 0f : 1f;

            bool paused = _logic.IsPaused;

            if (_pausePanel != null)
                _pausePanel.SetActive(paused);

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(!paused && CanPause);

            bool showSettings = _logic.ActivePanel == PauseMenuPanel.Settings;

            if (_settingsPanel != null)
                _settingsPanel.SetActive(showSettings);

            if (_pausePanel != null && _pausePanel.activeSelf)
            {
                GameObject mainButtons = _resumeButton?.transform.parent?.gameObject;
                if (mainButtons != null)
                    mainButtons.SetActive(!showSettings);
            }

            if (paused)
            {
                GameObject select = showSettings
                    ? _firstSelectedOnSettings
                    : _firstSelectedOnPause;

                if (select != null)
                    EventSystem.current?.SetSelectedGameObject(select);
            }
        }
    }
}
