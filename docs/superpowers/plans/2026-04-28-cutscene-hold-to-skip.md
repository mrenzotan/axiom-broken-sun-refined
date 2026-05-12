# Cutscene Hold-Enter-to-Skip Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add two Enter-key behaviors to cutscenes: tap Enter (&#8804;0.2s) advances the slide (or finishes typewriter), hold Enter (&#8805;3s) skips the entire cutscene. Visual feedback via a radial progress ring while holding.

**Architecture:** A new plain C# class `CutsceneInputHandler` holds the tap/hold/skip state machine. `CutsceneUI` (MonoBehaviour) reads `Keyboard.current`, passes booleans to the handler, acts on results, and drives the radial ring UI. Existing Space/click/A input is unchanged.

**Tech Stack:** Unity 6 LTS, New Input System, UGUI (Canvas Image with Filled/Radial360), TextMeshPro

**Design Decisions (from grill-me session):**
| Decision | Value |
|---|---|
| Hold-to-skip duration | 3 seconds |
| Tap threshold (tap vs hold boundary) | 0.2 seconds |
| Visual feedback | Radial ring next to "Hold Enter to skip" text prompt |
| Release before 3s | Cancel entirely (no advance) |
| Which keys hold-to-skip | Enter only (Space/click/A = advance only, unchanged) |
| Skip destination | Same nextSceneName as normal completion |
| Skip transition | Cut immediately (no extra flash/fade) |
| Enter tap while typewriter revealing | Finish typewriter only (matches Space behavior) |

---

### Task 1: CutsceneInputHandler — plain C# state machine

**Files:**
- Create: `Assets/Scripts/Core/CutsceneInputHandler.cs`
- Create: `Assets/Tests/Editor/Core/CutsceneInputHandlerTests.cs`

**Step 1: Write the failing tests**

```csharp
// Assets/Tests/Editor/Core/CutsceneInputHandlerTests.cs
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class CutsceneInputHandlerTests
    {
        [Test]
        public void NoInput_ReturnsNone()
        {
            var handler = new CutsceneInputHandler();
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.None, result);
        }

        [Test]
        public void TapEnter_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0.0f);
            // Release within tap threshold (0.2s)
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void SameFramePressAndRelease_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            var result = handler.ProcessEnterInput(enterPressed: true, enterReleased: true, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void HoldEnterForFullDuration_ReturnsSkip()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 1f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.1f);
            Assert.AreEqual(CutsceneInputResult.Skip, result);
        }

        [Test]
        public void HoldEnter_ReleaseBeforeThreshold_ReturnsNone()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            // Hold for 1s then release (less than 3s skip threshold)
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.016f);
            Assert.AreEqual(CutsceneInputResult.None, result);
        }

        [Test]
        public void HoldEnter_ReleaseWithinTapThreshold_ReturnsAdvance()
        {
            var handler = new CutsceneInputHandler { TapThreshold = 0.2f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.15f);
            Assert.AreEqual(CutsceneInputResult.Advance, result);
        }

        [Test]
        public void SkipProgress_ReflectsHoldDuration()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 3f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.5f);
            Assert.That(handler.SkipProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void IsHoldingEnter_TrueWhilePressing_FalseAfterRelease()
        {
            var handler = new CutsceneInputHandler();
            Assert.IsFalse(handler.IsHoldingEnter);
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            Assert.IsTrue(handler.IsHoldingEnter);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.IsFalse(handler.IsHoldingEnter);
        }

        [Test]
        public void IsHoldingEnter_FalseAfterSkip()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 0.5f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.6f);
            Assert.IsFalse(handler.IsHoldingEnter);
        }

        [Test]
        public void Reset_ClearsHoldState()
        {
            var handler = new CutsceneInputHandler();
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 1.0f);
            handler.Reset();
            Assert.IsFalse(handler.IsHoldingEnter);
            Assert.AreEqual(0f, handler.SkipProgress);
        }

        [Test]
        public void MultipleSequentialTaps_EachReturnsAdvance()
        {
            var handler = new CutsceneInputHandler();
            // First tap
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result1 = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result1);
            // Second tap
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            var result2 = handler.ProcessEnterInput(enterPressed: false, enterReleased: true, deltaTime: 0.1f);
            Assert.AreEqual(CutsceneInputResult.Advance, result2);
        }

        [Test]
        public void HoldToSkip_ReturnsNoneEachFrame_BeforeThreshold()
        {
            var handler = new CutsceneInputHandler { HoldToSkipDuration = 3f };
            handler.ProcessEnterInput(enterPressed: true, enterReleased: false, deltaTime: 0f);
            for (int i = 0; i < 10; i++)
            {
                var result = handler.ProcessEnterInput(enterPressed: false, enterReleased: false, deltaTime: 0.2f);
                Assert.AreEqual(CutsceneInputResult.None, result);
            }
        }
    }
}
```

**Step 2: Write `CutsceneInputHandler`**

```csharp
// Assets/Scripts/Core/CutsceneInputHandler.cs
namespace Axiom.Core
{
    public enum CutsceneInputResult
    {
        None,
        Advance,
        Skip
    }

    public sealed class CutsceneInputHandler
    {
        private bool _isHoldingEnter;
        private float _holdDuration;

        public float HoldToSkipDuration { get; set; } = 3f;
        public float TapThreshold { get; set; } = 0.2f;

        public bool IsHoldingEnter => _isHoldingEnter;

        public float SkipProgress
        {
            get
            {
                if (HoldToSkipDuration <= 0f) return 0f;
                return UnityEngine.Mathf.Clamp01(_holdDuration / HoldToSkipDuration);
            }
        }

        public CutsceneInputResult ProcessEnterInput(bool enterPressed, bool enterReleased, float deltaTime)
        {
            // Same-frame press+release (very fast tap) — treat as advance
            if (enterPressed && enterReleased)
            {
                _isHoldingEnter = false;
                _holdDuration = 0f;
                return CutsceneInputResult.Advance;
            }

            if (enterPressed)
            {
                _isHoldingEnter = true;
                _holdDuration = 0f;
                return CutsceneInputResult.None;
            }

            if (!_isHoldingEnter)
                return CutsceneInputResult.None;

            _holdDuration += deltaTime;

            if (_holdDuration >= HoldToSkipDuration)
            {
                _isHoldingEnter = false;
                _holdDuration = 0f;
                return CutsceneInputResult.Skip;
            }

            if (enterReleased)
            {
                _isHoldingEnter = false;
                bool wasTap = _holdDuration < TapThreshold;
                _holdDuration = 0f;
                return wasTap ? CutsceneInputResult.Advance : CutsceneInputResult.None;
            }

            return CutsceneInputResult.None;
        }

        public void Reset()
        {
            _isHoldingEnter = false;
            _holdDuration = 0f;
        }
    }
}
```

**Step 3: Verify tests pass**

Run: Unity Editor → Window → General → Test Runner → EditMode → CoreTests → CutsceneInputHandlerTests → Run All
Expected: All 12 tests PASS

**Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add CutsceneInputHandler with tap/hold/skip state machine`
- `Assets/Scripts/Core/CutsceneInputHandler.cs`
- `Assets/Scripts/Core/CutsceneInputHandler.cs.meta`
- `Assets/Tests/Editor/Core/CutsceneInputHandlerTests.cs`
- `Assets/Tests/Editor/Core/CutsceneInputHandlerTests.cs.meta`

---

### Task 2: Wire CutsceneInputHandler into CutsceneUI

**Files:**
- Modify: `Assets/Scripts/Core/CutsceneUI.cs`

**Step 1: Refactor CutsceneUI to use CutsceneInputHandler**

Replace `CutsceneUI.cs` with the following (only `HandleInput` and the relevant fields change; `HandleInput` is replaced and radial ring UI fields + update logic are added):

Changes relative to current file:
1. Add fields: `_inputHandler`, `_holdToSkipDuration`, `_tapThreshold`, UI references for radial ring
2. Instantiate `_inputHandler` in `Start()`
3. Replace `HandleInput()` with new version that delegates Enter to handler, keeps Space/click/A unchanged
4. Add `UpdateSkipRingUI()` method

Full file:

```csharp
using Axiom.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Core
{
    public class CutsceneUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Full-screen Image that displays each slide's sprite.")]
        private Image _slideImage;

        [SerializeField]
        [Tooltip("TextMeshPro text box for the typewriter effect.")]
        private TMP_Text _textBox;

        [SerializeField]
        [Tooltip("Optional: assign directly to override GameManager-driven data.")]
        private CutsceneData _cutsceneData;

        [SerializeField]
        [Tooltip("Characters revealed per second. Default: 40.")]
        [Min(1f)]
        private float _charsPerSecond = 40f;

        [SerializeField]
        [Tooltip("Transition style used when loading the next scene.")]
        private TransitionStyle _exitTransitionStyle = TransitionStyle.BlackFade;

        [Header("Hold-to-Skip")]
        [SerializeField]
        [Tooltip("Seconds to hold Enter before skipping the entire cutscene.")]
        [Min(0.1f)]
        private float _holdToSkipDuration = 3f;

        [SerializeField]
        [Tooltip("Max hold duration (seconds) considered a tap. Releases below this threshold advance the slide.")]
        [Min(0.01f)]
        private float _tapThreshold = 0.2f;

        [SerializeField]
        [Tooltip("Image component for the radial progress ring (Image Type: Filled, Fill Method: Radial 360). Shown while holding Enter.")]
        private Image _skipRingImage;

        [SerializeField]
        [Tooltip("Fill portion of the radial ring. Must be a child of _skipRingImage or share same rect.")]
        private Image _skipRingFill;

        [SerializeField]
        [Tooltip("TMP_Text label shown alongside the ring (e.g. 'Hold Enter to skip').")]
        private TMP_Text _skipPromptText;

        private CutscenePlayer _player;
        private TypewriterEffect _typewriter;
        private CutsceneInputHandler _inputHandler;
        private float _autoAdvanceTimer;

        public bool IsPlaying => _player != null && !_player.IsComplete;

        private void Awake()
        {
            if (Camera.main == null && Camera.allCamerasCount == 0)
            {
                var camGo = new GameObject("CutsceneCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                Debug.Log("[CutsceneUI] No camera found in scene — created fallback MainCamera.", this);
            }
        }

        private void Start()
        {
            _player = new CutscenePlayer();
            _typewriter = new TypewriterEffect();
            _inputHandler = new CutsceneInputHandler
            {
                HoldToSkipDuration = _holdToSkipDuration,
                TapThreshold = _tapThreshold
            };

            if (_cutsceneData == null)
            {
                Debug.LogWarning("[CutsceneUI] No CutsceneData assigned. Cutscene will complete immediately.", this);
            }

            if (_slideImage == null)
                Debug.LogError("[CutsceneUI] _slideImage (Image) is not assigned in the Inspector. Slide images will not display.", this);

            if (_textBox == null)
                Debug.LogError("[CutsceneUI] _textBox (TMP_Text) is not assigned in the Inspector. Text will not display.", this);

            EnsureSlideImageReady();
            HideSkipRingUI();

            _player.Start(_cutsceneData);

            if (!_player.IsComplete)
            {
                PlayCutsceneMusic();
                RenderCurrentSlide();
            }
        }

        private void Update()
        {
            if (_player == null || _player.IsComplete)
            {
                HandleCompletion();
                return;
            }

            HandleInput();
            UpdateSkipRingUI();

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.Update(Time.deltaTime);
                if (_textBox != null)
                    _textBox.text = _typewriter.VisibleText;

                if (_typewriter.IsComplete)
                {
                    float delay = _player.CurrentSlide?.autoAdvanceDelay ?? 3f;
                    _autoAdvanceTimer = delay >= 0f ? delay : 0f;
                }
            }

            if (_typewriter != null && _typewriter.IsComplete && _autoAdvanceTimer > 0f)
            {
                _autoAdvanceTimer -= Time.deltaTime;
                if (_autoAdvanceTimer <= 0f)
                    AdvanceSlide();
            }
        }

        private void HandleInput()
        {
            if (_player == null || _player.IsComplete) return;

            Keyboard kb = Keyboard.current;

            // --- Enter key: tap = advance, hold = skip (via CutsceneInputHandler) ---
            if (kb != null)
            {
                var result = _inputHandler.ProcessEnterInput(
                    kb.enterKey.wasPressedThisFrame,
                    kb.enterKey.wasReleasedThisFrame,
                    Time.deltaTime
                );

                switch (result)
                {
                    case CutsceneInputResult.Skip:
                        Debug.Log("[CutsceneUI] Hold-to-skip triggered.");
                        _player.Skip();
                        return;
                    case CutsceneInputResult.Advance:
                        TryAdvanceOrFinishTypewriter();
                        return;
                }
            }
            else if (_inputHandler.IsHoldingEnter)
            {
                // Keyboard disconnected mid-hold — reset to prevent stuck UI
                _inputHandler.Reset();
            }

            // --- Space, mouse click, or gamepad A: advance (unchanged) ---
            bool advancePressed =
                (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
                advancePressed = true;

            if (advancePressed)
                TryAdvanceOrFinishTypewriter();
        }

        private void TryAdvanceOrFinishTypewriter()
        {
            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.SkipToEnd();
                if (_textBox != null)
                    _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = _player.CurrentSlide?.autoAdvanceDelay ?? 3f;
            }
            else
            {
                AdvanceSlide();
            }
        }

        private void UpdateSkipRingUI()
        {
            if (_inputHandler == null) return;

            bool show = _inputHandler.IsHoldingEnter;

            if (_skipRingImage != null)
                _skipRingImage.gameObject.SetActive(show);

            if (_skipRingFill != null)
            {
                _skipRingFill.gameObject.SetActive(show);
                if (show)
                    _skipRingFill.fillAmount = _inputHandler.SkipProgress;
            }

            if (_skipPromptText != null)
                _skipPromptText.gameObject.SetActive(show);
        }

        private void HideSkipRingUI()
        {
            if (_skipRingImage != null)
                _skipRingImage.gameObject.SetActive(false);
            if (_skipRingFill != null)
                _skipRingFill.gameObject.SetActive(false);
            if (_skipPromptText != null)
                _skipPromptText.gameObject.SetActive(false);
        }

        private void AdvanceSlide()
        {
            if (_player == null || _player.IsComplete) return;

            _player.Advance();

            if (!_player.IsComplete)
                RenderCurrentSlide();
            else
                HandleCompletion();
        }

        private void RenderCurrentSlide()
        {
            CutsceneSlide slide = _player.CurrentSlide;

            if (_slideImage != null)
            {
                if (slide?.image != null)
                {
                    _slideImage.sprite = slide.image;
                    _slideImage.enabled = true;
                    if (_slideImage.color.a < 0.01f)
                        _slideImage.color = Color.white;
                }
                else
                {
                    _slideImage.enabled = false;
                    Debug.Log($"[CutsceneUI] Slide {_player.CurrentSlideIndex + 1} has no image assigned.", this);
                }
            }

            if (_textBox != null && _typewriter != null && slide != null)
            {
                _typewriter.Start(slide.text ?? "", _charsPerSecond);
                _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = 0f;
            }
        }

        private void EnsureSlideImageReady()
        {
            if (_slideImage == null) return;

            RectTransform rt = _slideImage.rectTransform;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            _slideImage.color = Color.white;
            _slideImage.enabled = true;
            _slideImage.raycastTarget = false;
        }

        private void PlayCutsceneMusic()
        {
            AudioClip clip = _player?.CutsceneMusic;
            if (clip == null)
            {
                Debug.Log("[CutsceneUI] No cutsceneMusic assigned on CutsceneData — no music will play.", this);
                return;
            }

            AudioManager audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("[CutsceneUI] Cannot play cutscene music — GameManager.Instance or AudioManager not found.", this);
                return;
            }

            Debug.Log($"[CutsceneUI] Playing cutscene music: {clip.name}", this);
            audioManager.PlayBgm(clip, 1f);
        }

        private void HandleCompletion()
        {
            if (_player == null || !_player.IsComplete) return;

            string nextScene = _player.NextSceneName;
            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogWarning("[CutsceneUI] Cutscene complete but no nextSceneName set.", this);
                return;
            }

            SceneTransitionController transition = GetSceneTransition();
            if (transition != null && !transition.IsTransitioning)
            {
                transition.BeginTransition(nextScene, _exitTransitionStyle);
            }
            else if (transition == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }

            _player = null;
        }

        private SceneTransitionController GetSceneTransition()
        {
            GameManager gm = GameManager.Instance;
            return gm?.SceneTransition;
        }

        private AudioManager GetAudioManager()
        {
            GameManager gm = GameManager.Instance;
            return gm?.AudioManager;
        }
    }
}
```

**Step 2: Verify compilation**

Open Unity Editor → ensure no compile errors in Assets/Scripts/Core/ (watch for `Assets/Scripts/Core/CutsceneUI.cs` and `Assets/Scripts/Core/CutsceneInputHandler.cs`).

**Step 3: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): wire CutsceneInputHandler into CutsceneUI with hold-to-skip`
- `Assets/Scripts/Core/CutsceneUI.cs`
- `Assets/Scripts/Core/CutsceneUI.cs.meta`

---

### Task 3: Add radial ring UI to Cutscene scene

> **Unity Editor task (user):** Open Cutscene.unity and add the hold-to-skip UI elements.

**Step 1: Create the skip UI hierarchy**

In `Assets/Scenes/Cutscene.unity`, under the existing Canvas:

1. Right-click Canvas → Create Empty. Name it `SkipPrompt`.
2. Set `SkipPrompt` RectTransform:
   - Anchor: Bottom-Center (hold Alt+Shift while selecting the anchor preset)
   - Pos Y: 80
   - Width: 400, Height: 60

3. Right-click `SkipPrompt` → UI → Text - TextMeshPro. Name it `SkipLabel`.
   - Text: "Hold Enter to skip"
   - Font Size: 24
   - Alignment: Center
   - Color: White (with alpha ~200)
   - RectTransform: Stretch to fill parent (anchor min 0,0; anchor max 1,1; offsets 0)

4. Right-click `SkipPrompt` → UI → Image. Name it `SkipRingBackground`.
   - Anchor: Right side of label text, or position manually to the right of the text
   - Width: 40, Height: 40
   - Source Image: Built-in "UISprite" or "Knob" (any circle sprite)
   - Color: White with alpha ~80 (semi-transparent)
   - RectTransform anchor: Center-Right of parent (or manual positioning)

5. Right-click `SkipRingBackground` → UI → Image. Name it `SkipRingFill`.
   - Source Image: Same circle sprite as background
   - Image Type: Filled
   - Fill Method: Radial 360
   - Fill Origin: Top
   - Fill Amount: 0
   - Color: White (full opacity, this draws on top of the semi-transparent background)
   - RectTransform: Stretch to fill parent (anchor min 0,0; anchor max 1,1; offsets 0)

**Step 2: Wire references in CutsceneUI Inspector**

1. Select the GameObject that has `CutsceneUI` component.
2. Drag `SkipRingBackground` (the Image) into `CutsceneUI._skipRingImage`.
3. Drag `SkipRingFill` (the Image child) into `CutsceneUI._skipRingFill`.
4. Drag `SkipLabel` (the TMP_Text) into `CutsceneUI._skipPromptText`.

**Step 3: Verify in Play Mode**

1. Enter Play Mode with Cutscene.unity active.
2. Tap Enter — should advance slide (or finish typewriter first).
3. Hold Enter — radial ring should fill, "Hold Enter to skip" should appear.
4. Release before full ring — ring should disappear, nothing happens.
5. Hold Enter for 3 full seconds — cutscene should skip to Level_1-1.
6. Space / mouse click should still advance as before.

**Step 4: Check in via UVCS**

Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add hold-to-skip radial ring UI to Cutscene scene`
- `Assets/Scenes/Cutscene.unity`
- `Assets/Scenes/Cutscene.unity.meta`

---

### Task 4: Existing tests regression check

**Step 1: Run all existing Core tests**

Unity Editor → Test Runner → EditMode → CoreTests → Run All

Expected: All 29 existing tests (CutscenePlayerTests, TypewriterEffectTests, etc.) still pass. The CutsceneInputHandler tests (12 new) also pass.

**Step 2: No further commits needed** — test suite should pass with zero changes to existing test files.
