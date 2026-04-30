# Cutscene System + Battle Music Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Pair with the `executing-unity-game-dev-plans` skill for the Unity Editor handoffs and UVCS check-in cadence.

**Goal:** Build a ScriptableObject-driven cutscene system with image slides and typewriter-text text boxes, integrated into the existing transition pipeline (MainMenu → Cutscene → Level_1-1), plus add per-enemy battle music and cutscene music via the existing AudioManager/MusicVol mixer group.

**Architecture:** A `CutsceneData` ScriptableObject holds a list of slides (each with image + text). A plain-C# `CutscenePlayer` drives slide advancement and skip logic. A plain-C# `TypewriterEffect` handles character-by-character text reveal. A `CutsceneUI` MonoBehaviour renders the current slide and wires Unity input/UI. For audio, `AudioPlaybackService` gains a generic `PlayBgm()` method callable by any scene controller; `BattleEnvironmentData` gains an optional `battleMusic` clip; `BattleController` plays it on init. The existing scene transition controller handles all fade-in/out. No AudioMixer changes needed — both new music sources route through the existing BGM `AudioSource` → `MusicVol` bus.

**Tech Stack:** Unity 6.0.4 LTS, URP 2D, TextMeshPro, ScriptableObject data layer, existing `SceneTransitionController` + `AudioManager` + `AudioPlaybackService`, Unity Test Framework (Edit Mode + Play Mode, NUnit).

---

## File Structure

### New runtime scripts

| File | Assembly | Responsibility |
|---|---|---|
| `Assets/Scripts/Data/CutsceneData.cs` | `Axiom.Data` | ScriptableObject: list of `CutsceneSlide` (image + text + auto-advance delay) |
| `Assets/Scripts/Core/CutscenePlayer.cs` | `Axiom.Core` | Plain C#: slide index advancement, completion detection, skip logic |
| `Assets/Scripts/Core/TypewriterEffect.cs` | `Axiom.Core` | Plain C#: character-by-character reveal with skippable instant-reveal |
| `Assets/Scripts/Core/CutsceneUI.cs` | `Axiom.Core` | MonoBehaviour: owns Image + TMP_Text, creates CutscenePlayer + TypewriterEffect, handles skip/advance input, triggers scene transition on completion |

### New tests

| File | Assembly |
|---|---|
| `Assets/Tests/Editor/Core/CutscenePlayerTests.cs` | `CoreTests` (existing) |
| `Assets/Tests/Editor/Core/TypewriterEffectTests.cs` | `CoreTests` (existing) |
| `Assets/Tests/Editor/Data/CutsceneDataTests.cs` | `DataTests` (existing) |
| `Assets/Tests/Editor/Core/AudioPlaybackServiceBgmTests.cs` | `CoreTests` (existing) |

### New Unity Editor assets (user-created)

| Asset | Location |
|---|---|
| `CD_Opening.asset` | `Assets/Data/Cutscenes/` |
| `Assets/Scenes/Cutscene.unity` | Scene with Canvas (Image + TMP_Text) + CutsceneUI component |

### Modified

| File | Change |
|---|---|
| `Assets/Scripts/Data/BattleEnvironmentData.cs` | Add `[SerializeField] private AudioClip _battleMusic` + public property |
| `Assets/Scripts/Core/AudioPlaybackService.cs` | Add `PlayBgm(AudioClip, float)` method; handle "Cutscene" + "Battle" scenes (no-op — controllers drive music) |
| `Assets/Scripts/Core/AudioManager.cs` | Add `PlayBgm(AudioClip, float)` public method delegating to `_service` |
| `Assets/Scripts/Core/GameManager.cs` | Add `AudioManager` public property; change `StartNewGame()` target to `"Cutscene"`; add `[SerializeField]` `_cutsceneSceneName` |
| `Assets/Scripts/Battle/BattleController.cs` | In `InitializeFromTransition()`: play battle music from `PendingBattle.EnvironmentData` via `AudioManager` |

### Unity Editor tasks (user)

| Task |
|---|
| Create folder `Assets/Data/Cutscenes/` |
| Create `CD_Opening.asset` via Create menu → Axiom → Data → Cutscene Data |
| Populate CD_Opening with slides (images + text) |
| Create `Assets/Scenes/Cutscene.unity` with Canvas, Image, TMP_Text, CutsceneUI component |
| Wire CutsceneUI's Image and TMP_Text references in the Inspector |
| Add Cutscene scene to Build Settings |
| On GameManager prefab: assign `_cutsceneSceneName = "Cutscene"` |
| On each ExplorationEnemyCombatTrigger in platformer scenes: assign `battleMusic` clip on each `BattleEnvironmentData` asset |

---

## Task 1: CutsceneData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/CutsceneData.cs`
- Create: `Assets/Scripts/Data/CutsceneData.cs.meta` (auto-generated)

### Step 1: Write CutsceneData.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewCutsceneData", menuName = "Axiom/Data/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [Tooltip("Scene loaded after the cutscene completes. Default: Level_1-1.")]
        public string nextSceneName = "Level_1-1";

        [Tooltip("Optional background music for this cutscene. Played on the BGM bus (MusicVol).")]
        public AudioClip cutsceneMusic;

        [Tooltip("Slides displayed in order. Each slide has an image and text with typewriter effect.")]
        public List<CutsceneSlide> slides = new List<CutsceneSlide>();
    }

    [Serializable]
    public class CutsceneSlide
    {
        [Tooltip("Full-screen image for this slide.")]
        public Sprite image;

        [TextArea(3, 10)]
        [Tooltip("Text revealed character-by-character with typewriter effect.")]
        public string text;

        [Tooltip("Seconds after typewriter completes before auto-advancing. 0 = manual advance only.")]
        [Min(0f)]
        public float autoAdvanceDelay = 3f;
    }
}
```

> **Unity Editor task (user):**
> - Create folder `Assets/Data/Cutscenes/`
> - Create `CD_Opening.asset` via Create menu → Axiom → Data → Cutscene Data
> - Populate slides with opening cutscene images and text
> - Set `nextSceneName = "Level_1-1"`

### Step 2: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add CutsceneData ScriptableObject + CutsceneSlide`
  - `Assets/Scripts/Data/CutsceneData.cs`
  - `Assets/Scripts/Data/CutsceneData.cs.meta`
  - `Assets/Data/Cutscenes/`
  - `Assets/Data/Cutscenes.meta`
  - `Assets/Data/Cutscenes/CD_Opening.asset`
  - `Assets/Data/Cutscenes/CD_Opening.asset.meta`

---

## Task 1b: CutsceneData Edit Mode Tests

**Files:**
- Create: `Assets/Tests/Editor/Data/CutsceneDataTests.cs`

### Step 1b.1: Write CutsceneDataTests.cs

```csharp
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Tests.Data
{
    public class CutsceneDataTests
    {
        [Test]
        public void DefaultNextSceneName_IsLevel1_1()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.AreEqual("Level_1-1", data.nextSceneName);
        }

        [Test]
        public void SlidesList_IsInitializedAndEmpty()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.IsNotNull(data.slides);
            Assert.AreEqual(0, data.slides.Count);
        }

        [Test]
        public void CutsceneMusic_DefaultsToNull()
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            Assert.IsNull(data.cutsceneMusic);
        }

        [Test]
        [CreateAssetMenu(menuName = "Test")]
        public void HasCreateAssetMenuAttribute()
        {
            var attrs = typeof(CutsceneData).GetCustomAttributes(
                typeof(UnityEngine.CreateAssetMenuAttribute), false);
            Assert.AreEqual(1, attrs.Length);
        }
    }
}
```

### Step 1b.2: Run tests to verify they pass

> Expected: 4 tests PASS.

### Step 1b.3: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add CutsceneData edit mode tests`
  - `Assets/Tests/Editor/Data/CutsceneDataTests.cs`
  - `Assets/Tests/Editor/Data/CutsceneDataTests.cs.meta`

---

## Task 2: TypewriterEffect Plain C# Class

**Files:**
- Create: `Assets/Scripts/Core/TypewriterEffect.cs`
- Create: `Assets/Tests/Editor/Core/TypewriterEffectTests.cs`

### Step 2.1: Write TypewriterEffectTests.cs

```csharp
using Axiom.Core;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class TypewriterEffectTests
    {
        [Test]
        public void Start_SetsFullTextAndResetsProgress()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello World", charsPerSecond: 10f);

            Assert.AreEqual("Hello World", effect.FullText);
            Assert.AreEqual("", effect.VisibleText);
            Assert.IsFalse(effect.IsComplete);
        }

        [Test]
        public void Update_RevealsCharactersOverTime()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hi", charsPerSecond: 10f);

            float progress = effect.Update(deltaTime: 0.1f);

            Assert.Greater(effect.VisibleText.Length, 0);
            Assert.LessOrEqual(effect.VisibleText.Length, 2);
            Assert.AreEqual(progress, (float)effect.VisibleText.Length / effect.FullText.Length, 0.001f);
        }

        [Test]
        public void Update_CompletesAfterEnoughTime()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hi", charsPerSecond: 10f);

            float progress = effect.Update(deltaTime: 1.0f);

            Assert.AreEqual("Hi", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void SkipToEnd_RevealsAllTextInstantly()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello World", charsPerSecond: 1f);

            effect.SkipToEnd();

            Assert.AreEqual("Hello World", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Update_AfterSkipToEnd_ReturnsFullText()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello", charsPerSecond: 1f);
            effect.SkipToEnd();

            float progress = effect.Update(deltaTime: 0.5f);

            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void Start_EmptyText_IsCompleteImmediately()
        {
            var effect = new TypewriterEffect();
            effect.Start("", charsPerSecond: 10f);

            Assert.AreEqual("", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Start_NullText_IsCompleteWithEmptyVisible()
        {
            var effect = new TypewriterEffect();
            effect.Start(null, charsPerSecond: 10f);

            Assert.AreEqual("", effect.VisibleText);
            Assert.IsTrue(effect.IsComplete);
        }

        [Test]
        public void Progress_IncreasesLinearly()
        {
            var effect = new TypewriterEffect();
            effect.Start("ABCDEFGHIJ", charsPerSecond: 10f); // 10 chars, needs exactly 1s

            float p1 = effect.Update(0.3f);
            float p2 = effect.Update(0.3f);
            float p3 = effect.Update(0.4f);

            Assert.AreEqual(1f, p3, 0.001f);
            Assert.AreEqual("ABCDEFGHIJ", effect.VisibleText);
        }

        [Test]
        public void Update_ZeroDeltaTime_ReturnsCurrentProgress()
        {
            var effect = new TypewriterEffect();
            effect.Start("Test", charsPerSecond: 10f);
            float initialProgress = effect.Update(0f);

            Assert.AreEqual(0f, initialProgress, 0.001f);
            Assert.AreEqual("", effect.VisibleText);
        }

        [Test]
        public void Reset_ClearsAllState()
        {
            var effect = new TypewriterEffect();
            effect.Start("Hello", charsPerSecond: 10f);
            effect.Update(1f);

            effect.Start("World", charsPerSecond: 5f);

            Assert.AreEqual("World", effect.FullText);
            Assert.AreEqual("", effect.VisibleText);
            Assert.IsFalse(effect.IsComplete);
        }
    }
}
```

### Step 2.2: Run tests to verify they fail

> Expected: 10 tests FAIL — `TypewriterEffect` class not found.

### Step 2.3: Write TypewriterEffect.cs

```csharp
using System;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# character-by-character text reveal with skippable instant-reveal.
    /// Driven by <see cref="CutsceneUI"/> via <see cref="Update"/> each frame.
    /// </summary>
    public sealed class TypewriterEffect
    {
        private string _fullText = "";
        private int _revealedCount;
        private float _accumulator;
        private float _charsPerSecond;

        public string FullText => _fullText;
        public string VisibleText => GetVisibleText();
        public bool IsComplete => _revealedCount >= _fullText.Length;

        /// <summary>
        /// Start revealing a new string. Resets progress.
        /// Null text is treated as empty (immediately complete).
        /// </summary>
        public void Start(string text, float charsPerSecond)
        {
            _fullText = text ?? "";
            _revealedCount = 0;
            _accumulator = 0f;
            _charsPerSecond = Math.Max(0.01f, charsPerSecond);
        }

        /// <summary>
        /// Advance the typewriter by <paramref name="deltaTime"/> seconds.
        /// Returns progress [0, 1] — the fraction of characters revealed.
        /// </summary>
        public float Update(float deltaTime)
        {
            if (IsComplete) return 1f;

            _accumulator += deltaTime;
            int targetCount = (int)(_accumulator * _charsPerSecond);

            if (targetCount > _fullText.Length)
                targetCount = _fullText.Length;

            _revealedCount = targetCount;

            if (_fullText.Length == 0)
                return 1f;

            return (float)_revealedCount / _fullText.Length;
        }

        /// <summary>
        /// Reveal all remaining characters instantly.
        /// </summary>
        public void SkipToEnd()
        {
            _revealedCount = _fullText.Length;
            _accumulator = _fullText.Length / Math.Max(0.01f, _charsPerSecond);
        }

        private string GetVisibleText()
        {
            if (_fullText.Length == 0) return "";
            if (_revealedCount >= _fullText.Length) return _fullText;
            return _fullText.Substring(0, _revealedCount);
        }
    }
}
```

### Step 2.4: Run tests to verify they pass

> Expected: All 10 tests PASS.

### Step 2.5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add TypewriterEffect plain C# class`
  - `Assets/Scripts/Core/TypewriterEffect.cs`
  - `Assets/Scripts/Core/TypewriterEffect.cs.meta`
  - `Assets/Tests/Editor/Core/TypewriterEffectTests.cs`
  - `Assets/Tests/Editor/Core/TypewriterEffectTests.cs.meta`

---

## Task 3: CutscenePlayer Plain C# Class

**Files:**
- Create: `Assets/Scripts/Core/CutscenePlayer.cs`
- Create: `Assets/Tests/Editor/Core/CutscenePlayerTests.cs`

### Step 3.1: Write CutscenePlayerTests.cs

```csharp
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using NUnit.Framework;

namespace Axiom.Tests.Core
{
    public class CutscenePlayerTests
    {
        private CutsceneData MakeData(List<CutsceneSlide> slides, string nextScene = "Level_1-1")
        {
            var data = CutsceneData.CreateInstance<CutsceneData>();
            data.nextSceneName = nextScene;
            data.slides = slides;
            return data;
        }

        private CutsceneSlide MakeSlide(string text = "Slide text", Sprite image = null)
        {
            return new CutsceneSlide { text = text, image = image, autoAdvanceDelay = 0f };
        }

        [Test]
        public void Start_WithSlides_SetsCurrentSlideToFirst()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second")
            });
            player.Start(data);

            Assert.AreEqual(0, player.CurrentSlideIndex);
            Assert.AreEqual("First", player.CurrentSlide.text);
            Assert.IsFalse(player.IsComplete);
        }

        [Test]
        public void Advance_MovesToNextSlide()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second"), MakeSlide("Third")
            });
            player.Start(data);
            player.Advance();

            Assert.AreEqual(1, player.CurrentSlideIndex);
            Assert.AreEqual("Second", player.CurrentSlide.text);
            Assert.IsFalse(player.IsComplete);
        }

        [Test]
        public void Advance_PastLastSlide_MarksComplete()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("Only slide")
            });
            player.Start(data);
            player.Advance();

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Advance_WhenComplete_StaysComplete()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("Only slide")
            });
            player.Start(data);
            player.Advance(); // complete
            player.Advance(); // should stay complete

            Assert.IsTrue(player.IsComplete);
            Assert.GreaterOrEqual(player.CurrentSlideIndex, 0);
        }

        [Test]
        public void Skip_MarksCompleteImmediately()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>
            {
                MakeSlide("First"), MakeSlide("Second"), MakeSlide("Third")
            });
            player.Start(data);
            player.Skip();

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Start_EmptySlides_IsCompleteImmediately()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide>());
            player.Start(data);

            Assert.IsTrue(player.IsComplete);
        }

        [Test]
        public void Start_NullData_IsCompleteWithNullSlide()
        {
            var player = new CutscenePlayer();
            player.Start(null);

            Assert.IsTrue(player.IsComplete);
            Assert.IsNull(player.CurrentSlide);
        }

        [Test]
        public void CurrentSlide_WhenComplete_IsNull()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide("Only") });
            player.Start(data);
            player.Advance();

            Assert.IsNull(player.CurrentSlide);
        }

        [Test]
        public void NextSceneName_ReturnsFromData()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide() }, nextScene: "TestScene");
            player.Start(data);

            Assert.AreEqual("TestScene", player.NextSceneName);
        }

        [Test]
        public void NextSceneName_WithNullData_ReturnsEmptyString()
        {
            var player = new CutscenePlayer();
            player.Start(null);

            Assert.AreEqual("", player.NextSceneName);
        }

        [Test]
        public void CutsceneMusic_ReturnsFromData()
        {
            var player = new CutscenePlayer();
            var data = MakeData(new List<CutsceneSlide> { MakeSlide() });
            data.cutsceneMusic = null; // explicitly null
            player.Start(data);

            Assert.IsNull(player.CutsceneMusic);
        }
    }
}
```

### Step 3.2: Run tests to verify they fail

> Expected: All tests FAIL — `CutscenePlayer` class not found.

### Step 3.3: Write CutscenePlayer.cs

```csharp
using Axiom.Data;
using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# logic for advancing through a <see cref="CutsceneData"/> sequence.
    /// Created and driven by <see cref="CutsceneUI"/>.
    /// </summary>
    public sealed class CutscenePlayer
    {
        private CutsceneData _data;
        private int _index;

        public int CurrentSlideIndex => _index;
        public bool IsComplete { get; private set; }

        public CutsceneSlide CurrentSlide
        {
            get
            {
                if (IsComplete || _data == null || _data.slides == null || _index >= _data.slides.Count)
                    return null;
                return _data.slides[_index];
            }
        }

        public string NextSceneName => _data != null ? _data.nextSceneName ?? "" : "";

        public AudioClip CutsceneMusic => _data?.cutsceneMusic;

        /// <summary>
        /// Begin a cutscene. Null data or empty slides marks immediate completion.
        /// </summary>
        public void Start(CutsceneData data)
        {
            _data = data;
            _index = 0;
            IsComplete = false;

            if (_data == null || _data.slides == null || _data.slides.Count == 0)
                IsComplete = true;
        }

        /// <summary>
        /// Advance to the next slide. Moving past the last slide marks completion.
        /// No-op when already complete.
        /// </summary>
        public void Advance()
        {
            if (IsComplete) return;

            _index++;
            if (_index >= (_data?.slides?.Count ?? 0))
                IsComplete = true;
        }

        /// <summary>
        /// Skip the entire cutscene immediately.
        /// </summary>
        public void Skip() => IsComplete = true;
    }
}
```

### Step 3.4: Run tests to verify they pass

> Expected: All 11 tests PASS.

### Step 3.5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add CutscenePlayer plain C# class`
  - `Assets/Scripts/Core/CutscenePlayer.cs`
  - `Assets/Scripts/Core/CutscenePlayer.cs.meta`
  - `Assets/Tests/Editor/Core/CutscenePlayerTests.cs`
  - `Assets/Tests/Editor/Core/CutscenePlayerTests.cs.meta`

---

## Task 4: CutsceneUI MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Core/CutsceneUI.cs`

### Step 4.1: Write CutsceneUI.cs

```csharp
using Axiom.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Axiom.Core
{
    /// <summary>
    /// MonoBehaviour — renders cutscene slides (image + typewriter text),
    /// handles skip/advance input (New Input System), plays cutscene music via AudioManager,
    /// and triggers the scene transition when the cutscene completes.
    ///
    /// Usage:
    /// 1. Attach to a GameObject in the Cutscene scene.
    /// 2. Assign <see cref="_slideImage"/> (full-screen Image) and <see cref="_textBox"/> (TMP_Text).
    /// 3. Assign <see cref="_cutsceneData"/> directly in the Inspector (or leave null for GameManager-driven).
    /// </summary>
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

        private CutscenePlayer _player;
        private TypewriterEffect _typewriter;
        private float _autoAdvanceTimer;

        public bool IsPlaying => _player != null && !_player.IsComplete;

        private void Start()
        {
            _player = new CutscenePlayer();
            _typewriter = new TypewriterEffect();

            if (_cutsceneData == null)
            {
                Debug.LogWarning("[CutsceneUI] No CutsceneData assigned. Cutscene will complete immediately.", this);
            }

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

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.Update(Time.deltaTime);
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

            // Skip: Escape key or gamepad Start button
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _player.Skip();
                return;
            }

            // Advance: Space, Enter, gamepad A button, or mouse left click
            bool advancePressed =
                (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame)
                advancePressed = true;

            if (!advancePressed) return;

            if (_typewriter != null && !_typewriter.IsComplete)
            {
                _typewriter.SkipToEnd();
                _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = _player.CurrentSlide?.autoAdvanceDelay ?? 3f;
            }
            else
            {
                AdvanceSlide();
            }
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

            if (_slideImage != null && slide?.image != null)
                _slideImage.sprite = slide.image;

            if (_textBox != null && _typewriter != null && slide != null)
            {
                _typewriter.Start(slide.text ?? "", _charsPerSecond);
                _textBox.text = _typewriter.VisibleText;
                _autoAdvanceTimer = 0f;
            }
        }

        private void PlayCutsceneMusic()
        {
            AudioClip clip = _player?.CutsceneMusic;
            if (clip != null)
            {
                AudioManager audioManager = GetAudioManager();
                audioManager?.PlayBgm(clip, 1f);
            }
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

            // Prevent re-entering HandleCompletion next frame
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

> **Unity Editor task (user):**
> - Create new scene `Assets/Scenes/Cutscene.unity`
> - Add Canvas (Screen Space - Overlay, scale with screen size) with:
>   - Full-screen `Image` child (stretch anchors, assign to `CutsceneUI._slideImage`)
>   - Bottom-third `TMP_Text` child (assign to `CutsceneUI._textBox`, set font, size, alignment)
>   - `CutsceneUI` component on the Canvas
> - Wire `_slideImage` and `_textBox` references in the Inspector
> - Optionally assign `_cutsceneData` reference to `CD_Opening` in the Inspector
> - Add the Cutscene scene to File → Build Settings → Scenes in Build

### Step 4.2: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add CutsceneUI MonoBehaviour`
  - `Assets/Scripts/Core/CutsceneUI.cs`
  - `Assets/Scripts/Core/CutsceneUI.cs.meta`
  - `Assets/Scenes/Cutscene.unity`
  - `Assets/Scenes/Cutscene.unity.meta`

---

## Task 5: AudioPlaybackService — Add Generic PlayBgm + Scene Handling

**Files:**
- Modify: `Assets/Scripts/Core/AudioPlaybackService.cs`
- Create: `Assets/Tests/Editor/Core/AudioPlaybackServiceBgmTests.cs`

### Step 5.1: Write AudioPlaybackServiceBgmTests.cs

```csharp
using Axiom.Core;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Core
{
    public class AudioPlaybackServiceBgmTests
    {
        private class TestMixerHook
        {
            public string LastParam;
            public float LastValue;
            public void Set(string p, float v) { LastParam = p; LastValue = v; }
        }

        [Test]
        public void PlayBgm_StopsPreviousAndStartsNewClip()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                var clip = AudioClip.Create("TestClip", 100, 1, 44100, false);
                service.PlayBgm(clip, 0.5f);

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);
                Assert.AreEqual(0.5f, bgm.volume, 0.01f);
                Assert.IsTrue(bgm.loop);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayBgm_NullClip_StopsBgm()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.Play();

            try
            {
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(null, 1f);

                Assert.IsFalse(bgm.isPlaying);
                Assert.IsNull(bgm.clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnSceneBecameActive_CutsceneScene_DoesNotStopAudio()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var clip = AudioClip.Create("TestBgm", 100, 1, 44100, false);
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(clip, 1f);
                service.OnSceneBecameActive("Cutscene");

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnSceneBecameActive_BattleScene_DoesNotStopAudio()
        {
            var store = new AudioSettingsStore();
            var hook = new TestMixerHook();
            var go = new GameObject("TestBgmSource");
            var bgm = go.AddComponent<AudioSource>();
            bgm.playOnAwake = false;

            try
            {
                var clip = AudioClip.Create("TestBgm", 100, 1, 44100, false);
                var service = new AudioPlaybackService(
                    config: null, store, bgmSource: bgm, ambientSource: null, uiSource: null,
                    setMixerParam: hook.Set, musicMixerParameterName: "MusicVol", sfxMixerParameterName: "SfxVol",
                    amplifyUiOneShotWithStoredSfx: false);

                service.PlayBgm(clip, 1f);
                service.OnSceneBecameActive("Battle");

                Assert.IsTrue(bgm.isPlaying);
                Assert.AreEqual(clip, bgm.clip);

                Object.DestroyImmediate(clip);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

### Step 5.2: Run tests to verify they fail

> Expected: 4 tests FAIL — `PlayBgm` method not found.

### Step 5.3: Modify AudioPlaybackService.cs

Add these changes:

**Add two new scene name constants (line ~17):**

```csharp
/// <summary>Cutscene scene name — music is driven by CutsceneUI, not scene name.</summary>
public const string CutsceneSceneName = "Cutscene";

/// <summary>Battle scene name — music is driven by BattleController, not scene name.</summary>
public const string BattleSceneName = "Battle";
```

**Modify `OnSceneBecameActive` (lines 72–85) to use ambient as the default for level scenes:**

Replace the entire method body:

```csharp
public void OnSceneBecameActive(string sceneName)
{
    if (string.IsNullOrEmpty(sceneName)) return;

    if (sceneName == MainMenuSceneName)
        ApplyMainMenuSceneAudio();
    else if (sceneName == CutsceneSceneName || sceneName == BattleSceneName)
    {
        // Music is driven by CutsceneUI / BattleController respectively.
        // Do NOT stop buses — the controller will play the right clip.
    }
    else
    {
        // Default: play exploration ambient loop for all level scenes
        // (Platformer, Level_1-1, Level_2-1, etc.)
        ApplyPlatformerSceneAudio();
    }

    // Re-apply saved levels whenever the active scene changes
    ApplyPersistedVolumesToMixer();
}
```

This ensures that after the cutscene transitions to Level_1-1, the exploration ambient music plays automatically.

**Add `PlayBgm` method (after `PlayUiClick`, line ~121):**

```csharp
/// <summary>
/// Play a specific <see cref="AudioClip"/> on the BGM bus.
/// Clip is looped. Volume is clamped [0,1]. Null clip stops the bus.
/// For cutscene music, battle music, or any scene-driven dynamic BGM.
/// </summary>
public void PlayBgm(AudioClip clip, float volume)
{
    volume = Mathf.Clamp01(volume);

    if (clip == null)
    {
        StopBgmBus();
        return;
    }

    if (_bgmSource == null) return;

    if (_bgmSource.isPlaying && _bgmSource.clip == clip)
    {
        _bgmSource.volume = volume;
        return;
    }

    StopBgmBus();
    _bgmSource.clip = clip;
    _bgmSource.loop = true;
    _bgmSource.volume = volume;
    _bgmSource.Play();
}
```

### Step 5.4: Run tests to verify they pass

> Expected: All 4 new tests PASS. Existing audio tests still pass.

### Step 5.5: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add PlayBgm to AudioPlaybackService, handle Cutscene+ Battle scenes`
  - `Assets/Scripts/Core/AudioPlaybackService.cs`
  - `Assets/Tests/Editor/Core/AudioPlaybackServiceBgmTests.cs`
  - `Assets/Tests/Editor/Core/AudioPlaybackServiceBgmTests.cs.meta`

---

## Task 6: AudioManager — Expose PlayBgm

**Files:**
- Modify: `Assets/Scripts/Core/AudioManager.cs`

### Step 6.1: Modify AudioManager.cs

Add a public `PlayBgm` method (after `PlayUiClick`, around line ~137):

```csharp
/// <summary>
/// Play a looping <see cref="AudioClip"/> on the BGM bus (routed through MusicVol mixer group).
/// Null clip stops the bus. Used by CutsceneUI and BattleController for dynamic music.
/// </summary>
public void PlayBgm(AudioClip clip, float volume) => _service?.PlayBgm(clip, volume);
```

### Step 6.2: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): expose PlayBgm on AudioManager`
  - `Assets/Scripts/Core/AudioManager.cs`

---

## Task 7: GameManager — Add AudioManager Accessor, Route StartNewGame to Cutscene

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`

### Step 7.1: Modify GameManager.cs

**Add `AudioManager` property** (after `SceneTransition` property, around line ~100):

```csharp
private AudioManager _audioManager;
/// <summary>
/// Public accessor for scene-specific controllers (CutsceneUI, BattleController)
/// to play dynamic music on the BGM bus.
/// </summary>
public AudioManager AudioManager =>
    _audioManager ??= GetComponentInChildren<AudioManager>();
```

**Add `_cutsceneSceneName` serialized field** (after other `[SerializeField]` fields, around line ~32):

```csharp
[SerializeField]
[Tooltip("Scene loaded for the opening cutscene when starting a new game.")]
private string _cutsceneSceneName = "Cutscene";
```

**Change `StartNewGame()` target scene** (line ~593):

```csharp
// OLD:
LoadScene("Platformer");

// NEW:
LoadScene(_cutsceneSceneName);
```

Also update the Initial Scene in `Awake()` or wherever the GameManager initially loads the scene.

Update the PlayerState active scene if needed (the `PlatformerWorldRestoreController` sets `ActiveSceneName` when the platformer scene loads, so the cutscene doesn't need to set it):
No change needed — `PlatformerWorldRestoreController` handles this on platformer scene load.

### Step 7.2: Update GameManager tests

The `GameManagerNewGameTests` currently expect `StartNewGame` to load "Platformer". Verify or update the mock to reference the cutscene scene name. If tests use a mock SceneTransitionController, they should still pass since they only test the state reset, not the exact scene name.

### Step 7.3: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add AudioManager accessor, route StartNewGame to Cutscene scene`
  - `Assets/Scripts/Core/GameManager.cs`

> **Unity Editor task (user):**
> - On the GameManager prefab, set `_cutsceneSceneName = "Cutscene"` in the Inspector

---

## Task 8: BattleEnvironmentData — Add Battle Music

**Files:**
- Modify: `Assets/Scripts/Data/BattleEnvironmentData.cs`

### Step 8.1: Modify BattleEnvironmentData.cs

Add a battle music field:

```csharp
[SerializeField]
[Tooltip("Optional looping battle music. Played on the BGM bus (MusicVol mixer group) when battle starts.")]
private AudioClip _battleMusic;

/// <summary>Optional battle music clip. Null = no music during battle.</summary>
public AudioClip BattleMusic => _battleMusic;
```

### Step 8.2: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): add battleMusic field to BattleEnvironmentData`
  - `Assets/Scripts/Data/BattleEnvironmentData.cs`

---

## Task 9: BattleController — Play Battle Music on Init

**Files:**
- Modify: `Assets/Scripts/Battle/BattleController.cs`

### Step 9.1: Modify BattleController.cs

In `InitializeFromTransition()` (or wherever the battle initializes after transition), add battle music playback.

Locate the area where `_environmentService.Apply(pending?.EnvironmentData, _backgroundRenderer)` is called (around lines ~240-250). After it, add:

```csharp
// Play battle music from the per-enemy BattleEnvironmentData (same pattern as dynamic background).
AudioClip battleMusic = pending?.EnvironmentData?.BattleMusic;
if (battleMusic != null)
{
    GameManager gm = GameManager.Instance;
    gm?.AudioManager?.PlayBgm(battleMusic, 1f);
}
```

### Step 9.2: Check in via UVCS

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat(DEV-81): play per-enemy battle music via AudioManager on battle start`
  - `Assets/Scripts/Battle/BattleController.cs`

> **Unity Editor task (user):**
> - For each `BattleEnvironmentData` asset (e.g., `BED_SnowMountain.asset`), assign a `battleMusic` AudioClip from `Assets/Audio/Music/Wav/`

---

## Task 10: End-to-End Verification

### Step 10.1: Run all tests

Open Unity Editor → Window → General → Test Runner → Run All

> Expected: All existing tests pass. All new tests pass.

### Step 10.2: Manual Play-Test Flow

1. Start from **MainMenu.unity**
2. Click **New Game**
3. Verify: BlackFade transition → Cutscene scene loads
4. Verify: First slide image and typewriter text appear
5. Verify: Typewriter reveals characters smoothly
6. Press Space/Enter → text skips to full (if typewriter still going)
7. Press Space/Enter again → advances to next slide
8. After last slide or Escape skip → BlackFade transition → Level_1-1 loads
9. Verify: Level_1-1 world is playable with correct PlayerState

### Step 10.3: Manual Battle Music Test

1. In Level_1-1, approach an enemy to trigger combat
2. Verify: WhiteFlash transition → Battle scene loads
3. Verify: Battle music plays (if assigned on that enemy's BattleEnvironmentData)
4. Verify: Music volume is controlled by the MusicVol slider in settings
5. Complete/flee battle → verify music stops and exploration ambient resumes

### Step 10.4: Test Skip Flow

1. Start from MainMenu → New Game
2. When cutscene starts, press Escape → cutscene skips immediately
3. Verify: Immediate transition to Level_1-1

---

## Architecture Summary

```
┌──────────────────────────────────────────────────────────┐
│  GameManager (persistent singleton)                       │
│  ├── SceneTransition → SceneTransitionController          │
│  ├── AudioManager → AudioPlaybackService                  │
│  ├── StartNewGame() → LoadScene("Cutscene")              │
│  └── [SerializeField] _cutsceneSceneName                  │
├──────────────────────────────────────────────────────────┤
│  Cutscene Scene                                           │
│  ├── CutsceneUI (MonoBehaviour)                           │
│  │     ├── CutscenePlayer (plain C#) → slide progression  │
│  │     ├── TypewriterEffect (plain C#) → text reveal      │
│  │     ├── _slideImage (Image)                            │
│  │     ├── _textBox (TMP_Text)                            │
│  │     └── On complete → SceneTransition.BeginTransition()│
│  └── [Optional] CutsceneData assigned in Inspector        │
├──────────────────────────────────────────────────────────┤
│  CutsceneData (ScriptableObject, Axiom.Data)              │
│  ├── nextSceneName ("Level_1-1")                          │
│  ├── cutsceneMusic (AudioClip, optional)                  │
│  └── slides: List<CutsceneSlide>                          │
│       ├── image (Sprite)                                  │
│       ├── text (string)                                   │
│       └── autoAdvanceDelay (float)                        │
├──────────────────────────────────────────────────────────┤
│  Battle Environment (modified)                            │
│  ├── BattleEnvironmentData.battleMusic (new AudioClip)    │
│  ├── BattleEntry carries it cross-scene                   │
│  └── BattleController plays it via AudioManager.PlayBgm() │
├──────────────────────────────────────────────────────────┤
│  Audio System (modified)                                  │
│  ├── AudioMixer → MusicVol bus (unchanged)                │
│  ├── AudioPlaybackService.PlayBgm(clip, volume) → new     │
│  ├── OnSceneBecameActive: "Cutscene" + "Battle" = no-op   │
│  └── AudioManager.PlayBgm() → delegates to service        │
└──────────────────────────────────────────────────────────┘
```

**Flow: MainMenu → Cutscene → Level_1-1**
```
New Game click
  → GameManager.StartNewGame()
    → Reset all state
    → LoadScene("Cutscene") → BlackFade transition
  → Cutscene scene loads
    → CutsceneUI.Start()
      → CutscenePlayer.Start(data)
      → PlayCutsceneMusic() via AudioManager.PlayBgm()
      → RenderCurrentSlide() → TypewriterEffect.Start()
    → Update() loop:
      → TypewriterEffect.Update(dt) → text reveals char-by-char
      → Input: Space/Enter/Click → skip text or advance slide
      → Input: Escape → CutscenePlayer.Skip()
    → On complete:
      → SceneTransition.BeginTransition(nextSceneName, BlackFade)
  → Level_1-1 scene loads
    → PlatformerWorldRestoreController sets ActiveSceneName
    → AudioManager.PlayBgm(clip) for any assigned music
```
