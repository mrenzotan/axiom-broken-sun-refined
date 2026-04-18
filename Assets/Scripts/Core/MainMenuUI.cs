using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour wrapper for <see cref="MainMenuController"/>.
    /// Handles Unity lifecycle only: creates the controller in Start(),
    /// wires button listeners, drives Continue interactability, routes menu audio,
    /// and toggles the settings view (music / ambient / SFX sliders + back) versus primary menu buttons.
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

        [SerializeField]
        [Tooltip("Modal dialog shown before StartNewGame when a save file exists. " +
                 "Optional — when null, New Game runs immediately with no confirmation (legacy behaviour).")]
        private ConfirmNewGameDialogUI _confirmNewGameDialog;

        [SerializeField]
        [Tooltip("Centralized menu / UI audio. Optional until assigned on the persistent bootstrap object.")]
        private AudioManager _audioManager;

        [SerializeField]
        [Tooltip("Opens settings: hides New Game / Continue / Quit / Settings and shows the settings view.")]
        private Button _settingsButton;

        [SerializeField]
        [Tooltip("Existing back or close control on the settings surface (reuse from your hierarchy).")]
        private Button _backFromSettingsButton;

        [SerializeField]
        [Tooltip("Root object that contains the volume sliders and back button. Hidden until Settings is pressed.")]
        private GameObject _settingsViewRoot;

        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _ambientVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        private MainMenuController _controller;

        private void Start()
        {
            if (_audioManager == null && GameManager.Instance != null)
                _audioManager = GameManager.Instance.GetComponentInChildren<AudioManager>();

            if (_audioManager == null)
                _audioManager = FindFirstObjectByType<AudioManager>();

            TryAutoBindSettingsReferences();
            LogSettingsWiringIssues();

            _controller = new MainMenuController(
                hasSaveFile: () => GameManager.Instance?.HasSaveFile() ?? false,
                startNewGame: () => GameManager.Instance?.StartNewGame(),
                continueGame: () => GameManager.Instance?.TryContinueGame(),
                requestNewGameConfirmation: BuildConfirmationDelegate(),
                quit: QuitApplication);

            _continueButton.interactable = _controller.CanContinue();

            WireButtonWithOptionalUiClick(_newGameButton, _controller.OnNewGameClicked);
            WireButtonWithOptionalUiClick(_continueButton, _controller.OnContinueClicked);
            if (_quitButton != null)
                WireButtonWithOptionalUiClick(_quitButton, _controller.OnQuitClicked);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsButtonClicked);

            if (_backFromSettingsButton != null)
                _backFromSettingsButton.onClick.AddListener(OnBackFromSettingsClicked);

            WireVolumeSliders();
            LogSliderWiringIssues();

            if (_settingsViewRoot != null)
                _settingsViewRoot.SetActive(false);
        }

        private void WireButtonWithOptionalUiClick(Button button, UnityAction handler)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                if (button.interactable)
                    _audioManager?.PlayUiClick();

                handler.Invoke();
            });
        }

        private void OnSettingsButtonClicked()
        {
            if (_settingsButton != null && !_settingsButton.interactable) return;

            if (_settingsViewRoot == null)
            {
                Debug.LogWarning(
                    "[MainMenuUI] Settings was pressed but _settingsViewRoot is not assigned. " +
                    "Assign the settings panel root in the Inspector.",
                    this);
                return;
            }

            _audioManager?.PlayUiClick();
            ShowSettingsPanel();
        }

        private void OnBackFromSettingsClicked()
        {
            if (_backFromSettingsButton != null && !_backFromSettingsButton.interactable) return;

            _audioManager?.PlayUiClick();
            HideSettingsPanel();
        }

        private void ShowSettingsPanel()
        {
            SetPrimaryMenuButtonsVisible(false);
            _settingsViewRoot.SetActive(true);
            RefreshSliderValuesWithoutNotify();
        }

        private void HideSettingsPanel()
        {
            if (_settingsViewRoot != null)
                _settingsViewRoot.SetActive(false);

            SetPrimaryMenuButtonsVisible(true);
        }

        private void SetPrimaryMenuButtonsVisible(bool visible)
        {
            if (_newGameButton != null) _newGameButton.gameObject.SetActive(visible);
            if (_continueButton != null) _continueButton.gameObject.SetActive(visible);
            if (_quitButton != null) _quitButton.gameObject.SetActive(visible);
            if (_settingsButton != null) _settingsButton.gameObject.SetActive(visible);
        }

        private void LogSliderWiringIssues()
        {
            bool anySlider = _musicVolumeSlider != null || _ambientVolumeSlider != null ||
                             _sfxVolumeSlider != null;
            if (!anySlider || _audioManager != null)
                return;

            Debug.LogWarning(
                "[MainMenuUI] Volume sliders are assigned but **Audio Manager** was not found. " +
                "Sliders will not change volume until you add **AudioManager** to the **GameManager** object " +
                "(with Menu Audio Config + Audio Mixer) or drag **Audio Manager** into this **Main Menu UI** component.",
                this);
        }

        private void WireVolumeSliders()
        {
            if (_audioManager == null) return;

            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.SetValueWithoutNotify(_audioManager.GetMusicVolumeNormalized());
                _musicVolumeSlider.onValueChanged.AddListener(_audioManager.SetMusicVolume);
            }

            if (_ambientVolumeSlider != null)
            {
                _ambientVolumeSlider.SetValueWithoutNotify(_audioManager.GetAmbientVolumeNormalized());
                _ambientVolumeSlider.onValueChanged.AddListener(_audioManager.SetAmbientVolume);
            }

            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(_audioManager.GetSfxVolumeNormalized());
                _sfxVolumeSlider.onValueChanged.AddListener(_audioManager.SetSfxVolume);
            }
        }

        private void RefreshSliderValuesWithoutNotify()
        {
            if (_audioManager == null) return;

            if (_musicVolumeSlider != null)
                _musicVolumeSlider.SetValueWithoutNotify(_audioManager.GetMusicVolumeNormalized());

            if (_ambientVolumeSlider != null)
                _ambientVolumeSlider.SetValueWithoutNotify(_audioManager.GetAmbientVolumeNormalized());

            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.SetValueWithoutNotify(_audioManager.GetSfxVolumeNormalized());
        }

        private System.Action BuildConfirmationDelegate()
        {
            if (_confirmNewGameDialog == null)
                return null;

            return () => _confirmNewGameDialog.Show(
                onConfirm: () => GameManager.Instance?.StartNewGame());
        }

        private void OnDestroy()
        {
            _newGameButton?.onClick.RemoveAllListeners();
            _continueButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();
            _settingsButton?.onClick.RemoveAllListeners();
            _backFromSettingsButton?.onClick.RemoveAllListeners();
            _musicVolumeSlider?.onValueChanged.RemoveAllListeners();
            _ambientVolumeSlider?.onValueChanged.RemoveAllListeners();
            _sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Fills missing Settings references when the Canvas uses common names, so Play Mode works
        /// even if Inspector slots were left empty after adding new fields.
        /// </summary>
        private void TryAutoBindSettingsReferences()
        {
            Transform uiRoot = transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                uiRoot = canvas.transform;

            if (_settingsButton == null)
                TryFindSettingsButton(uiRoot);

            if (_settingsViewRoot == null)
                TryFindSettingsViewRoot(uiRoot);
        }

        private void TryFindSettingsButton(Transform uiRoot)
        {
            Button[] buttons = uiRoot.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string n = btn.gameObject.name;
                if (string.Equals(n, "Settings", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "SettingsButton", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "Btn_Settings", StringComparison.OrdinalIgnoreCase))
                {
                    _settingsButton = btn;
                    Debug.Log($"[MainMenuUI] Auto-bound Settings button: '{n}'.", this);
                    return;
                }
            }

            foreach (Button btn in buttons)
            {
                if (btn.gameObject.name.IndexOf("settings", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _settingsButton = btn;
                    Debug.Log($"[MainMenuUI] Auto-bound Settings button (name contains 'Settings'): '{btn.gameObject.name}'.", this);
                    return;
                }
            }
        }

        private void TryFindSettingsViewRoot(Transform uiRoot)
        {
            string[] candidates =
            {
                "SettingsPanel", "SettingsView", "Panel_Settings", "Settings_Menu", "OptionsPanel",
                "Settings", "VolumePanel", "AudioSettings"
            };

            foreach (string name in candidates)
            {
                Transform t = FindDeepChild(uiRoot, name);
                if (t != null)
                {
                    _settingsViewRoot = t.gameObject;
                    Debug.Log($"[MainMenuUI] Auto-bound settings view root: '{name}'.", this);
                    return;
                }
            }

            Slider settingsRefSlider = _musicVolumeSlider != null ? _musicVolumeSlider : _ambientVolumeSlider;
            if (settingsRefSlider != null && _backFromSettingsButton != null)
            {
                Transform canvasTr = settingsRefSlider.GetComponentInParent<Canvas>()?.transform;
                Transform lca = LowestCommonAncestor(
                    settingsRefSlider.transform,
                    _backFromSettingsButton.transform);

                if (lca != null && canvasTr != null && lca != canvasTr)
                {
                    _settingsViewRoot = lca.gameObject;
                    Debug.Log(
                        $"[MainMenuUI] Auto-bound settings view root from common parent of sliders/back: '{_settingsViewRoot.name}'.",
                        this);
                }
            }
        }

        private void LogSettingsWiringIssues()
        {
            if (_settingsButton == null)
            {
                Debug.LogWarning(
                    "[MainMenuUI] Settings does nothing: assign **Settings Button** on this component, " +
                    "or rename your UI Button object to include \"Settings\" (e.g. `Settings`, `SettingsButton`). " +
                    "Other menu buttons work because they are already assigned.",
                    this);
                return;
            }

            if (_settingsViewRoot == null)
            {
                Debug.LogWarning(
                    "[MainMenuUI] Settings button is wired but **Settings View Root** is missing. " +
                    "Assign the GameObject that wraps your volume sliders + Back button, " +
                    "or name that object `SettingsPanel` / `SettingsView` / `AudioSettings` for auto-detect.",
                    this);
            }

            if (_backFromSettingsButton == null && _settingsViewRoot != null)
            {
                Debug.LogWarning(
                    "[MainMenuUI] Assign **Back From Settings Button** so the player can leave settings.",
                    this);
            }
        }

        private static Transform FindDeepChild(Transform parent, string exactName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, exactName, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform nested = FindDeepChild(child, exactName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static Transform LowestCommonAncestor(Transform a, Transform b)
        {
            if (a == null || b == null) return null;

            int depthA = GetDepth(a);
            int depthB = GetDepth(b);
            while (depthA > depthB)
            {
                a = a.parent;
                depthA--;
            }

            while (depthB > depthA)
            {
                b = b.parent;
                depthB--;
            }

            while (a != b)
            {
                a = a.parent;
                b = b.parent;
            }

            return a;
        }

        private static int GetDepth(Transform t)
        {
            int d = 0;
            while (t != null && t.parent != null)
            {
                d++;
                t = t.parent;
            }

            return d;
        }
    }
}
