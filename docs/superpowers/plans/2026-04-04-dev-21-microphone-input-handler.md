# DEV-21: MicrophoneInputHandler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `MicrophoneCapture` (pure C# PCM16 conversion service) and `MicrophoneInputHandler` (MonoBehaviour push-to-talk wrapper), producing PCM16 `short[]` chunks enqueued into the `VoskRecognizerService` input queue with zero recognition logic inside either class.

**Architecture:** `MicrophoneCapture` is a pure C# class — no Unity APIs, fully unit-testable in Edit Mode — that converts Unity float samples to PCM16 `short[]` and enqueues them into a `ConcurrentQueue<short[]>`. `MicrophoneInputHandler` is a MonoBehaviour that owns all Unity Microphone API calls (`Microphone.Start/End/GetPosition`, `AudioClip.GetData`), responds to push-to-talk via an `InputActionReference`, and calls `MicrophoneCapture.ProcessSamples()` each `Update()`. On PTT release it calls `VoskRecognizerService.RequestFinalResult()` to flush partial recognition.

**Tech Stack:** Unity 6 LTS, URP 2D, New Input System (`InputActionReference`), Unity `Microphone` API, `System.Collections.Concurrent`, existing `Axiom.Voice` asmdef + `VoiceTests` asmdef

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Voice/MicrophoneCapture.cs` | Create | float[] → PCM16 short[] conversion + enqueue into ConcurrentQueue |
| `Assets/Scripts/Voice/MicrophoneInputHandler.cs` | Create | MonoBehaviour: PTT input, Unity Microphone API, calls MicrophoneCapture |
| `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs` | Create | Edit Mode NUnit tests for MicrophoneCapture |

**No asmdef changes required.** Both runtime files fall under the existing `Axiom.Voice` asmdef (`Assets/Scripts/Voice/Axiom.Voice.asmdef`). Tests fall under the existing `VoiceTests` asmdef (`Assets/Tests/Editor/Voice/VoiceTests.asmdef`).

---

## Task 1: MicrophoneCapture — float-to-PCM16 service (TDD)

**Files:**
- Create: `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs`
- Create: `Assets/Scripts/Voice/MicrophoneCapture.cs`

### Step 1.1 — Write failing tests for constructor and null/empty guards

- [ ] Create `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs` with the following content:

```csharp
using System.Collections.Concurrent;
using NUnit.Framework;

namespace Axiom.Voice.Tests
{
    public class MicrophoneCaptureTests
    {
        // ── Constructor ──────────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullQueue_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MicrophoneCapture(null));
        }

        // ── ProcessSamples: guard clauses ─────────────────────────────────────────

        [Test]
        public void ProcessSamples_NullArray_ThrowsArgumentNullException()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            Assert.Throws<System.ArgumentNullException>(() =>
                capture.ProcessSamples(null));
        }

        [Test]
        public void ProcessSamples_EmptyArray_DoesNotEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[0]);

            Assert.AreEqual(0, queue.Count);
        }

        // ── ProcessSamples: conversion and enqueue ───────────────────────────────

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuesExactlyOneChunk()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.5f, -0.5f });

            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuedChunkMatchesInputLength()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.1f, 0.2f, 0.3f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(3, result.Length);
        }

        [Test]
        public void ProcessSamples_PlusOneSample_EnqueuesMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MinusOneSample_EnqueuesMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { -1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_ZeroSample_EnqueuesZero()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(0, result[0]);
        }

        [Test]
        public void ProcessSamples_AboveClampValue_ClampsToMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_BelowClampValue_ClampsToMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { -2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MultipleCallsEnqueueMultipleChunks()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.1f });
            capture.ProcessSamples(new float[] { 0.2f });
            capture.ProcessSamples(new float[] { 0.3f });

            Assert.AreEqual(3, queue.Count);
        }
    }
}
```

### Step 1.2 — Run tests to confirm they fail

> **Unity Editor task (user):** Open the Unity Test Runner (Window > General > Test Runner), switch to **Edit Mode**, filter by `MicrophoneCaptureTests`. Click **Run All**. Expected: all tests **fail** with compile errors — `MicrophoneCapture` does not exist yet.

### Step 1.3 — Implement MicrophoneCapture

- [ ] Create `Assets/Scripts/Voice/MicrophoneCapture.cs`:

```csharp
using System;
using System.Collections.Concurrent;

namespace Axiom.Voice
{
    /// <summary>
    /// Converts raw Unity microphone float samples to PCM16 <c>short[]</c> chunks
    /// and enqueues them for consumption by <see cref="VoskRecognizerService"/>.
    /// Pure C# — no Unity APIs, no MonoBehaviour lifecycle.
    /// </summary>
    public class MicrophoneCapture
    {
        private readonly ConcurrentQueue<short[]> _inputQueue;

        public MicrophoneCapture(ConcurrentQueue<short[]> inputQueue)
        {
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
        }

        /// <summary>
        /// Converts <paramref name="floatSamples"/> to PCM16 and enqueues the result.
        /// No-op when the array is empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="floatSamples"/> is null.</exception>
        public void ProcessSamples(float[] floatSamples)
        {
            if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
            if (floatSamples.Length == 0) return;

            _inputQueue.Enqueue(ToPcm16(floatSamples));
        }

        private static short[] ToPcm16(float[] floatSamples)
        {
            short[] pcm = new short[floatSamples.Length];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                float clamped = floatSamples[i] < -1f ? -1f
                              : floatSamples[i] >  1f ?  1f
                              : floatSamples[i];
                pcm[i] = (short)(clamped * 32767f);
            }
            return pcm;
        }
    }
}
```

### Step 1.4 — Run tests to confirm all pass

> **Unity Editor task (user):** In the Test Runner (Edit Mode), run all `MicrophoneCaptureTests`. Expected: all **11 tests pass** (green).

### Step 1.5 — Check in Task 1

> **Unity Version Control task (user):** Open Unity Version Control → Pending Changes. Stage the following files and check in with message `feat(DEV-21): add MicrophoneCapture PCM16 conversion service with Edit Mode tests`:
> - `Assets/Scripts/Voice/MicrophoneCapture.cs`
> - `Assets/Scripts/Voice/MicrophoneCapture.cs.meta`
> - `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs`
> - `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs.meta`

---

## Task 2: MicrophoneInputHandler MonoBehaviour

**Files:**
- Create: `Assets/Scripts/Voice/MicrophoneInputHandler.cs`

### Step 2.1 — Add PushToTalk action to the Input Actions asset

> **Unity Editor task (user):**
> 1. Open `Assets/InputSystem_Actions.inputactions` in the Inspector (double-click).
> 2. In the Action Maps panel (left column), click **+** to add a new map named **Voice**.
> 3. In the Actions panel (middle column), rename the default action to **PushToTalk**. Set Action Type to **Button**.
> 4. Click the **+** next to PushToTalk to add a binding. Choose **Left Shift** (Keyboard) as the key.
> 5. Save the asset (**Ctrl+S** or click **Save Asset** in the top-right of the editor).

### Step 2.2 — Create MicrophoneInputHandler.cs

- [ ] Create `Assets/Scripts/Voice/MicrophoneInputHandler.cs`:

```csharp
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Voice
{
    /// <summary>
    /// MonoBehaviour responsible solely for Unity microphone lifecycle and
    /// push-to-talk input wiring. Calls <see cref="MicrophoneCapture.ProcessSamples"/>
    /// each frame while recording; calls <see cref="VoskRecognizerService.RequestFinalResult"/>
    /// on PTT release. Contains no recognition logic.
    ///
    /// Call <see cref="Inject"/> with the shared queue and service before this
    /// component is enabled. Typical caller: a scene-level bootstrap MonoBehaviour
    /// or <c>BattleController</c> when entering the voice-spell phase.
    /// </summary>
    public class MicrophoneInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionReference _pushToTalkAction;

        /// <summary>
        /// Sample rate in Hz passed to <c>Microphone.Start</c>.
        /// Must match the rate used when constructing the <see cref="Vosk.VoskRecognizer"/>.
        /// </summary>
        [SerializeField] private int _sampleRate = 16000;

        private MicrophoneCapture      _capture;
        private VoskRecognizerService  _recognizerService;

        private AudioClip _clip;
        private int       _lastSamplePos;
        private bool      _isCapturing;

        // null → Unity picks the default microphone device.
        // Expose via a public setter if a device-selection UI is added later.
        private string _deviceName;

        // ── Injection ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the shared audio-input queue and the recognizer service.
        /// Must be called before the GameObject is enabled.
        /// </summary>
        public void Inject(
            ConcurrentQueue<short[]>  inputQueue,
            VoskRecognizerService     recognizerService)
        {
            _capture           = new MicrophoneCapture(inputQueue);
            _recognizerService = recognizerService;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            _pushToTalkAction.action.started  += OnPushToTalkStarted;
            _pushToTalkAction.action.canceled += OnPushToTalkCanceled;
            _pushToTalkAction.action.Enable();
        }

        private void OnDisable()
        {
            _pushToTalkAction.action.started  -= OnPushToTalkStarted;
            _pushToTalkAction.action.canceled -= OnPushToTalkCanceled;
            _pushToTalkAction.action.Disable();
            StopCapture();
        }

        private void Update()
        {
            if (!_isCapturing || _clip == null || _capture == null) return;

            int currentPos = Microphone.GetPosition(_deviceName);
            if (currentPos == _lastSamplePos) return;

            int newSamples = currentPos - _lastSamplePos;
            if (newSamples < 0) newSamples += _clip.samples; // ring-buffer wrap

            float[] buffer = new float[newSamples];
            _clip.GetData(buffer, _lastSamplePos % _clip.samples);
            _capture.ProcessSamples(buffer);
            _lastSamplePos = currentPos;
        }

        // ── PTT callbacks ─────────────────────────────────────────────────────────

        private void OnPushToTalkStarted(InputAction.CallbackContext _)
        {
            if (_capture == null)
            {
                Debug.LogError("[MicrophoneInputHandler] Inject() was not called before PTT press.", this);
                return;
            }
            if (_isCapturing) return;

            _clip = Microphone.Start(_deviceName, loop: true, lengthSec: 1, frequency: _sampleRate);
            if (_clip == null)
            {
                Debug.LogWarning("[MicrophoneInputHandler] No microphone device found — PTT ignored.", this);
                return;
            }

            _lastSamplePos = 0;
            _isCapturing   = true;
        }

        private void OnPushToTalkCanceled(InputAction.CallbackContext _) => StopCapture();

        // ── Internal ──────────────────────────────────────────────────────────────

        private void StopCapture()
        {
            if (!_isCapturing) return;
            _isCapturing = false;
            Microphone.End(_deviceName);
            _clip = null;
            _recognizerService?.RequestFinalResult();
        }
    }
}
```

### Step 2.3 — Add MicrophoneInputHandler to the test scene

> **Unity Editor task (user):**
> 1. Open `Assets/Scenes/SampleScene.unity`.
> 2. Create an empty GameObject named **MicrophoneInputHandler** (right-click in Hierarchy > Create Empty, then rename).
> 3. With that GameObject selected, click **Add Component** in the Inspector and search for `MicrophoneInputHandler`.
> 4. Assign `_pushToTalkAction` in the Inspector: click the circle picker next to the field and select **Voice/PushToTalk** from `InputSystem_Actions`.
> 5. Leave `_sampleRate` at **16000**.
> 6. Save the scene (**Ctrl+S**).

### Step 2.4 — Manual Play Mode verification

> **Unity Editor task (user):** Enter Play Mode and run these checks:
>
> | Check | Expected result |
> |---|---|
> | Hold Left Shift — no microphone connected | `[MicrophoneInputHandler] No microphone device found` warning in Console; no NullReferenceException |
> | Hold Left Shift — microphone connected | No errors in Console; `Microphone.IsRecording(null)` returns `true` (add a temporary `Debug.Log(Microphone.IsRecording(null))` in `Update` to verify, then remove it) |
> | Release Left Shift while recording | No errors; recording stops; `Microphone.IsRecording(null)` returns `false` |
> | Hold PTT without calling `Inject()` (remove the Inject call temporarily) | `[MicrophoneInputHandler] Inject() was not called` error in Console; no crash |

### Step 2.5 — Check in Task 2

> **Unity Version Control task (user):** Open Unity Version Control → Pending Changes. Stage the following files and check in with message `feat(DEV-21): add MicrophoneInputHandler push-to-talk MonoBehaviour`:
> - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
> - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
> - `Assets/InputSystem_Actions.inputactions`
> - `Assets/InputSystem_Actions.inputactions.meta`
> - `Assets/Scenes/SampleScene.unity`
> - `Assets/Scenes/SampleScene.unity.meta` _(if modified)_
