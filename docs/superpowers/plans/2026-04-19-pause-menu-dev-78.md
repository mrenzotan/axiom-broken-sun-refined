# Pause Menu Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement a pause menu accessible via Escape key or on-screen button that freezes game time and provides Resume, Save, Settings, and Quit options — shareable across both Battle and Platformer scenes.

**Architecture:** A `PauseMenuController` plain C# class manages pause state and `Time.timeScale`. A `PauseMenuUI` MonoBehaviour handles Unity lifecycle (input polling, button wiring, panel toggle). A `VolumeSettingsUI` MonoBehaviour wires three sliders to `AudioManager`. All three live in `Assets/Scripts/Core/` on the GameManager prefab, making the pause menu persistent across scene loads via DontDestroyOnLoad. A new `PauseMenuLogic` plain C# class isolates the editable/testable state machine for main-menu ↔ settings navigation.

**Tech Stack:** Unity 6 LTS, URP 2D, New Input System (polled directly, no action map enable/disable), TextMeshPro for labels, Unity UI Canvas (Screen Space Overlay)

**Jira:** DEV-78 — Create pause menu for Battle and Platformer scenes
**Labels:** `phase-4-bridge`, `phase-5-data`, `unity`

---

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Where does pause menu live? | `Assets/Scripts/Core/` on GameManager prefab | GameManager is DontDestroyOnLoad — pause menu persists across scenes naturally |
| Input method for toggle | Raw device polling (Escape / gamepad Start) | Matches ConfirmNewGameDialogUI pattern; avoids action-map enable/disable complexity |
| Time freeze mechanism | `Time.timeScale = 0f / 1f` | Stated in DEV-78 technical notes; global, reliable, freezes all WaitForSeconds coroutines |
| Master volume control | `AudioListener.volume` | Built-in Unity master; simpler than adding another AudioMixer exposed parameter |
| Music/Ambient/SFX controls | `AudioManager.SetMusicVolume()`, `.SetSfxVolume()` | Existing API; ambient mirrors music by default per AudioSettingsStore |
| Assembly | `Axiom.Core` (existing) | Pause menu crosses both Battle and Platformer — belongs in shared Core |
| Save button | Calls `GameManager.Instance.PersistToDisk()` | Already implemented in Phase 4–5 |
| Quit button | `GameManager.Instance.SceneTransition.BeginTransition("MainMenu")` with `SceneManager.LoadScene` fallback | Matches BattleController's scene-transition pattern |

---

## Task 1: PauseMenuLogic — Pure C# State Machine

**Files:**
- Create: `Assets/Scripts/Core/PauseMenuLogic.cs`
- Test: `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs`

**Step 1: Write the failing tests**

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: Unity Test Runner → Edit Mode → CoreTests → PauseMenuLogicTests  
Expected: All tests FAIL — `PauseMenuLogic` and `PauseMenuPanel` don't exist yet.

**Step 3: Write PauseMenuPanel enum and PauseMenuLogic implementation**

```csharp
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
```

**Step 4: Run tests to verify they pass**

Run: Unity Test Runner → Edit Mode → CoreTests → PauseMenuLogicTests  
Expected: All 12 tests PASS.

**Step 5: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add PauseMenuLogic state machine with Edit Mode tests`
  - `Assets/Scripts/Core/PauseMenuLogic.cs`
  - `Assets/Scripts/Core/PauseMenuLogic.cs.meta`
  - `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs`
  - `Assets/Tests/Editor/Core/PauseMenuLogicTests.cs.meta`

---

## Task 2: AudioSettingsStore — Add Master Volume Persistence

**Files:**
- Modify: `Assets/Scripts/Core/AudioSettingsStore.cs`

**Step 1: Add master volume methods to AudioSettingsStore**

Add the following to `AudioSettingsStore.cs`:

```csharp
public const string PlayerPrefsKeyMaster = "axiom.audio.master";

public float GetMasterVolumeNormalized() =>
    Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKeyMaster, 1f));

public void SetMasterVolume(float linear01)
{
    PlayerPrefs.SetFloat(PlayerPrefsKeyMaster, Mathf.Clamp01(linear01));
    PlayerPrefs.Save();
}
```

Insert the `PlayerPrefsKeyMaster` constant after the existing `PlayerPrefsKeySfx` line. Insert the two methods after `SetSfxVolume`.

**Step 2: Verify existing AudioSettingsStore tests still pass**

Run: Unity Test Runner → Edit Mode → CoreTests → AudioSettingsStore tests  
Expected: All pass (no behavior change to existing methods).

**Step 3: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add master volume persistence to AudioSettingsStore`
  - `Assets/Scripts/Core/AudioSettingsStore.cs`

---

## Task 3: AudioManager — Add Master Volume API

**Files:**
- Modify: `Assets/Scripts/Core/AudioManager.cs`

**Step 1: Add master volume methods to AudioManager**

Add the following methods to `AudioManager.cs` after the existing `SetSfxVolume` method:

```csharp
public void SetMasterVolume(float linear01)
{
    AudioListener.volume = Mathf.Clamp01(linear01);
    if (_store != null)
        _store.SetMasterVolume(Mathf.Clamp01(linear01));
}

public float GetMasterVolumeNormalized() =>
    _store != null ? _store.GetMasterVolumeNormalized() : 1f;
```

Initialize `AudioListener.volume` in `Start()` after the existing `_service.ApplyPersistedVolumesToMixer()` call:

```csharp
AudioListener.volume = _store != null ? _store.GetMasterVolumeNormalized() : 1f;
```

**Step 2: Verify the project compiles**

Open Unity → check Console for compilation errors.  
Expected: No errors.

**Step 3: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add master volume API to AudioManager`
  - `Assets/Scripts/Core/AudioManager.cs`

---

## Task 4: VolumeSettingsUI — Settings Sliders MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Core/VolumeSettingsUI.cs`

**Step 1: Write VolumeSettingsUI**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour shell for the pause menu's Settings panel volume sliders.
    /// Wires three sliders (Master, Music, SFX) to <see cref="AudioManager"/>.
    /// All logic lives in the plain C# <see cref="VolumeSettingsController"/>; this
    /// class handles Unity lifecycle only.
    /// </summary>
    public class VolumeSettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;

        private VolumeSettingsController _controller;

        private void Start()
        {
            var audioManager = FindAnyObjectByType<AudioManager>();
            _controller = new VolumeSettingsController(audioManager);

            if (_masterSlider != null)
            {
                _masterSlider.onValueChanged.AddListener(OnMasterChanged);
                _masterSlider.value = _controller.GetMasterVolume();
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.AddListener(OnMusicChanged);
                _musicSlider.value = _controller.GetMusicVolume();
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
                _sfxSlider.value = _controller.GetSfxVolume();
            }
        }

        private void OnDestroy()
        {
            if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        }

        private void OnMasterChanged(float value) => _controller?.SetMasterVolume(value);
        private void OnMusicChanged(float value) => _controller?.SetMusicVolume(value);
        private void OnSfxChanged(float value) => _controller?.SetSfxVolume(value);
    }
}
```

**Step 2: Write VolumeSettingsController (plain C#)**

```csharp
namespace Axiom.Core
{
    /// <summary>
    /// Pure business logic for volume slider operations. No Unity dependencies beyond
    /// the <see cref="AudioManager"/> reference passed in at construction.
    /// </summary>
    public sealed class VolumeSettingsController
    {
        private readonly AudioManager _audioManager;

        public VolumeSettingsController(AudioManager audioManager)
        {
            _audioManager = audioManager;
        }

        public float GetMasterVolume() =>
            _audioManager != null ? _audioManager.GetMasterVolumeNormalized() : 1f;

        public float GetMusicVolume() =>
            _audioManager != null ? _audioManager.GetMusicVolumeNormalized() : 1f;

        public float GetSfxVolume() =>
            _audioManager != null ? _audioManager.GetSfxVolumeNormalized() : 1f;

        public void SetMasterVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetMasterVolume(linear01);
        }

        public void SetMusicVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetMusicVolume(linear01);
        }

        public void SetSfxVolume(float linear01)
        {
            if (_audioManager == null) return;
            _audioManager.SetSfxVolume(linear01);
        }
    }
}
```

**Step 3: Verify the project compiles**

Open Unity → check Console for compilation errors.  
Expected: No errors. No runtime test yet — MonoBehaviour requires scene setup; Edit Mode tests for the controller are in Task 7.

**Step 4: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add VolumeSettingsUI and VolumeSettingsController for pause menu settings`
  - `Assets/Scripts/Core/VolumeSettingsUI.cs`
  - `Assets/Scripts/Core/VolumeSettingsUI.cs.meta`
  - `Assets/Scripts/Core/VolumeSettingsController.cs`
  - `Assets/Scripts/Core/VolumeSettingsController.cs.meta`

---

## Task 5: PauseMenuUI — Main MonoBehaviour + Input Polling

**Files:**
- Create: `Assets/Scripts/Core/PauseMenuUI.cs`

**Step 1: Write PauseMenuUI**

```csharp
using TMPro;
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
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Settings Sub-panel")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _settingsBackButton;

        [Header("First selected button on pause open")]
        [SerializeField] private GameObject _firstSelectedOnPause;

        [Header("First selected button on settings open")]
        [SerializeField] private GameObject _firstSelectedOnSettings;

        [Header("Save feedback")]
        [SerializeField] private TMP_Text _saveFeedbackText;
        [SerializeField] private float _saveFeedbackDuration = 1.5f;

        private PauseMenuLogic _logic;

        private void Awake()
        {
            _logic = new PauseMenuLogic();
            ApplyPanelState();
        }

        private void Start()
        {
            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeClicked);
            if (_saveButton != null) _saveButton.onClick.AddListener(OnSaveClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
            if (_settingsBackButton != null) _settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (_saveButton != null) _saveButton.onClick.RemoveListener(OnSaveClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
            if (_settingsBackButton != null) _settingsBackButton.onClick.RemoveListener(OnSettingsBackClicked);
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseButtonClicked);

            if (_logic != null && _logic.IsPaused)
                Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            if (_logic != null && _logic.IsPaused)
                Time.timeScale = 1f;
        }

        private void Update()
        {
            bool toggleRequested =
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonStart.wasPressedThisFrame);

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

        // ── Button callbacks ─────────────────────────────────────────────────

        private void OnPauseButtonClicked()
        {
            _logic.Pause();
            ApplyPanelState();
        }

        private void OnResumeClicked()
        {
            _logic.Resume();
            ApplyPanelState();
        }

        private void OnSaveClicked()
        {
            GameManager.Instance?.PersistToDisk();
            StartCoroutine(ShowSaveFeedback());
        }

        private System.Collections.IEnumerator ShowSaveFeedback()
        {
            if (_saveFeedbackText != null)
            {
                _saveFeedbackText.gameObject.SetActive(true);
                _saveFeedbackText.text = "Saved.";
                yield return new WaitForSecondsRealtime(_saveFeedbackDuration);
                _saveFeedbackText.gameObject.SetActive(false);
            }
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

        // ── Panel state ──────────────────────────────────────────────────────

        private void ApplyPanelState()
        {
            Time.timeScale = _logic.IsPaused ? 0f : 1f;

            bool paused = _logic.IsPaused;

            if (_pausePanel != null)
                _pausePanel.SetActive(paused);

            if (_pauseButton != null)
                _pauseButton.gameObject.SetActive(!paused);

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
```

**Step 2: Verify the project compiles**

Open Unity → check Console for compilation errors.  
Expected: No errors.

**Step 3: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add PauseMenuUI MonoBehaviour with input polling and panel management`
  - `Assets/Scripts/Core/PauseMenuUI.cs`
  - `Assets/Scripts/Core/PauseMenuUI.cs.meta`

---

## Task 6: Edit Mode Tests for VolumeSettingsController

**Files:**
- Create: `Assets/Tests/Editor/Core/VolumeSettingsControllerTests.cs`

**Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Axiom.Core;

namespace CoreTests
{
    [TestFixture]
    public class VolumeSettingsControllerTests
    {
        [Test]
        public void GetMasterVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetMasterVolume());
        }

        [Test]
        public void GetMusicVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetMusicVolume());
        }

        [Test]
        public void GetSfxVolume_NullManager_Returns1()
        {
            var controller = new VolumeSettingsController(null);
            Assert.AreEqual(1f, controller.GetSfxVolume());
        }

        [Test]
        public void SetMasterVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetMasterVolume(0.5f));
        }

        [Test]
        public void SetMusicVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetMusicVolume(0.5f));
        }

        [Test]
        public void SetSfxVolume_NullManager_DoesNotThrow()
        {
            var controller = new VolumeSettingsController(null);
            Assert.DoesNotThrow(() => controller.SetSfxVolume(0.5f));
        }
    }
}
```

**Step 2: Run tests to verify they pass (null-manager fallback is built in)**

Run: Unity Test Runner → Edit Mode → CoreTests → VolumeSettingsControllerTests  
Expected: All 6 tests PASS (null AudioManager graceful fallback tests).

**Step 3: Check in via UVCS**

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `test(DEV-78): add VolumeSettingsController null-manager Edit Mode tests`
  - `Assets/Tests/Editor/Core/VolumeSettingsControllerTests.cs`
  - `Assets/Tests/Editor/Core/VolumeSettingsControllerTests.cs.meta`

---

## Task 7: Unity Editor — Scene and Prefab Setup

> **Unity Editor task (user):** This entire task is performed in the Unity Editor. No code changes.

### 7a. Create the Pause Menu Canvas on the GameManager prefab

1. Open the GameManager prefab (in the Project window, double-click it to enter Prefab Mode).
2. Right-click the GameManager root → UI → Canvas.
   - Rename to `PauseMenuCanvas`.
   - Canvas component: Render Mode = **Screen Space - Overlay**.
   - Canvas Scaler: UI Scale Mode = **Scale With Screen Size**, Reference Resolution = **1920 × 1080**, Screen Match Mode = **Match Width or Height**, Match = **0.5**.
3. Add a **Canvas Group** component to `PauseMenuCanvas`. Set `Alpha = 1`, uncheck `Interactable` and `Blocks Raycasts` (will be controlled by PauseMenuUI).
4. Add a child **Panel** under `PauseMenuCanvas` → rename to `PausePanel`.
   - Set the Panel's Image to a semi-transparent dark color (e.g., RGBA `0, 0, 0, 180`).
   - Set anchors to **stretch-stretch** (full screen).
   - Deactivate `PausePanel` in the Inspector (it should be hidden at start).
5. Under `PausePanel`, create the **Main buttons container** (a vertical or grid Layout Group):
   - `ResumeButton` (Button + TMP text "Resume")
   - `SaveButton` (Button + TMP text "Save")
   - `SettingsButton` (Button + TMP text "Settings")
   - `QuitButton` (Button + TMP text "Quit to Main Menu")
6. Under `PausePanel`, create **SettingsPanel** (deactivated by default):
   - `MasterSlider` (Slider + TMP label "Master")
   - `MusicSlider` (Slider + TMP label "Music")
   - `SFXSlider` (Slider + TMP label "SFX")
   - `BackButton` (Button + TMP text "Back")
7. Add a **Button** as a sibling of `PausePanel` (not a child) — rename to `PauseButton`.
   - Position it in the top-right corner of the Canvas.
   - Set its text/icon to a pause icon (⚙ or ⏸ for now, placeholder).
8. Add the **PauseMenuUI** component to `PauseMenuCanvas`.
   - Drag all the child references (PauseButton, PausePanel, resume/save/settings/quit buttons, settingsPanel, settingsBackButton, firstSelectedOnPause, firstSelectedOnSettings) into the Inspector fields.
   - Add a small **TMP_Text** child under `PausePanel` named `SaveFeedbackText`. Set text empty, font size ~24, centered alignment. Drag it into the `_saveFeedbackText` slot on `PauseMenuUI`.
9. Add the **VolumeSettingsUI** component to `SettingsPanel` (or a child GameObject).
   - Drag the three Slider references into the Inspector fields.

### 7b. Verify in both scenes

1. Open `Assets/Scenes/Platformer.unity`. Confirm the GameManager prefab is in the scene and the PauseMenuCanvas appears in the Hierarchy.
2. Open `Assets/Scenes/Battle.unity`. Confirm the same.
3. Enter Play Mode. Press Escape. Confirm:
   - PausePanel appears, PauseButton hides.
   - `Time.timeScale` sets to 0 (visible in Debugger or Console if you add a Debug.Log).
   - Clicking Resume hides PausePanel and restores `Time.timeScale = 1`.
   - Settings button opens SettingsPanel; Back button returns to Main panel.
   - Quit button transitions to MainMenu scene (or loads it if SceneTransitionController is not present).
   - Master/Music/SFX sliders change volume.
4. Repeat in the Battle scene.

### 7c. Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-78): add pause menu Canvas prefab on GameManager with all UI bindings`
  - `Assets/Prefabs/` (ifGameManager prefab modified)
  - `Assets/Scenes/Platformer.unity`
  - `Assets/Scenes/Platformer.unity.meta`
  - `Assets/Scenes/Battle.unity`
  - `Assets/Scenes/Battle.unity.meta`
  - All newly generated `.meta` files for new Canvas/UI GameObjects

---

## Task 8: Verify Save Feedback in Unity Editor

> **Note:** The save feedback fields (`_saveFeedbackText`, `_saveFeedbackDuration`) and the `ShowSaveFeedback()` coroutine are already included in Task 5's PauseMenuUI code. They use `WaitForSecondsRealtime` (unscaled time) so the "Saved." text appears and fades correctly even while `Time.timeScale = 0`.

The `TMP_Text _saveFeedbackText` field must be wired in the Unity Editor during Task 7 (prefab setup). The user should add a small TMP_Text element under `PausePanel` for the save feedback and drag it into the `_saveFeedbackText` slot on the `PauseMenuUI` component.

**Files:** None — code is already in Task 5's PauseMenuUI.cs

**Step 1: Verify the save feedback compiles and works**

1. In Unity Editor, after completing Task 7's prefab setup, add a `TMP_Text` child under `PausePanel` named `SaveFeedbackText`.
2. Set its text to empty string, font size to ~24, and alignment to center.
3. Drag `SaveFeedbackText` into the `_saveFeedbackText` slot on `PauseMenuUI`.
4. Enter Play Mode, pause, and click Save. Confirm "Saved." appears for ~1.5 seconds then disappears.
5. Confirm this works with `Time.timeScale = 0` (the text should still appear and fade on unscaled time).

No UVCS check-in needed for this verification — the prefab changes are part of Task 7's check-in.

---

## Task 9: Verify Time.timeScale Safety Net

The `OnDestroy` and `OnDisable` safety nets for `Time.timeScale` are already included in Task 5's PauseMenuUI code. This task verifies they work correctly.

**Files:** None (verification only)

**Step 1: Verify the timeScale reset logic exists in PauseMenuUI.cs**

Open `Assets/Scripts/Core/PauseMenuUI.cs` and confirm both methods are present:

```csharp
// Inside OnDestroy, after button listener cleanup:
if (_logic != null && _logic.IsPaused)
    Time.timeScale = 1f;

// As a separate method:
private void OnDisable()
{
    if (_logic != null && _logic.IsPaused)
        Time.timeScale = 1f;
}
```

**Step 2: Manual verification**

In Unity Play Mode:
1. Pause the game with Escape.
2. Stop Play Mode while paused.
3. Enter Play Mode again. Confirm `Time.timeScale = 1` (game doesn't start frozen).
4. Pause, then transition to another scene (Quit to Main Menu). Confirm gameplay runs at normal speed in the new scene.

No UVCS check-in needed for this task — the code was included in Task 5.

---

## Task 10: Integration Test — Manual Verification Checklist

> **This is a manual QA task, not code.** Run through these in both scenes.

### Platformer Scene

- [ ] Press Escape → pause menu opens; game freezes (player stops moving, enemies stop)
- [ ] Click on-screen Pause button (⚙/⏸ corner) → pause menu opens
- [ ] Press Escape again → pause menu closes; game resumes
- [ ] Click Resume → pause menu closes; game resumes
- [ ] Click Settings → settings panel with 3 sliders appears
- [ ] Drag Master slider → all audio scales
- [ ] Drag Music slider → music/ambient volume changes
- [ ] Drag SFX slider → SFX volume changes
- [ ] Click Back → returns to main pause panel
- [ ] Click Save → "Saved." text appears briefly
- [ ] Click Quit to Main Menu → saves and transitions to MainMenu scene; `Time.timeScale` is 1

### Battle Scene

- [ ] Press Escape during player turn → pause menu opens; battle freezes (no turn timer advancement)
- [ ] Press Escape during enemy turn → pause menu opens
- [ ] Resume → battle continues from where it left off
- [ ] Settings → same slider behavior as Platformer
- [ ] Save → "Saved." appears
- [ ] Quit → transitions to MainMenu; `Time.timeScale` is 1

### Edge Cases

- [ ] Pause while SpellInputUI PTT is active → PTT input stops; resume → PTT input works again
- [ ] Alt+Tab while paused → game stays paused on return
- [ ] Quit from pause menu, then start Continue → game state is restored from save
- [ ] No NullReferenceException when GameManager is absent (standalone testing)
