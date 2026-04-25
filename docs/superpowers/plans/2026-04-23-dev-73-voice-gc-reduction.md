# Vosk Voice Capture GC Reduction — DEV-73

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:writing-plans to implement this plan task-by-task.

**Goal:** Eliminate per-frame heap allocations in the voice capture hot path by introducing a buffer-pool strategy.

**Root Cause:** Two allocation sites in the PTT hot loop:

| Location | Allocated each frame | Solution |
|---|---|---|
| `MicrophoneInputHandler.Update()` line 91 — `new float[newSamples]` | `float[]` per frame | Reusable `float[]` ring buffer owned by the MonoBehaviour |
| `MicrophoneCapture.ToPcm16()` line 45 — `new short[floatSamples.Length]` | `short[]` per audio chunk | Rent/return from a thread-safe `MicrophoneBufferPool` |

**Architecture:** A thread-safe `MicrophoneBufferPool` (plain C#, lives in `Voice` namespace) provides `RentFloatBuffer()` / `ReturnFloatBuffer()` and `RentShortBuffer()` / `ReturnShortBuffer()`. Buffers are `ConcurrentQueue<byte[]>` of pooled arrays; no blocking locks.

**Tech Stack:** C#, NUnit (Edit Mode), Unity Test Framework, Unity Profiler

---

## Affected Files

| File | Change |
|---|---|
| `Assets/Scripts/Voice/MicrophoneBufferPool.cs` | **New** — thread-safe buffer pool for `float[]` and `short[]` |
| `Assets/Scripts/Voice/MicrophoneCapture.cs` | Rent/return PCM16 `short[]` from pool instead of `new short[]` |
| `Assets/Scripts/Voice/MicrophoneInputHandler.cs` | Use pooled `float[]` reusable buffer in `Update()` |
| `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs` | Buffer pool correctness + allocation-count tests |
| `Assets/Tests/Editor/Voice/MicrophoneInputHandlerBufferTests.cs` | Buffer reuse path tests |

---

## Task 1: Write MicrophoneBufferPool

**File:**
- Create: `Assets/Scripts/Voice/MicrophoneBufferPool.cs`

```csharp
using System;
using System.Collections.Concurrent;

namespace Axiom.Voice
{
    /// <summary>
    /// Thread-safe pool of reusable <c>float[]</c> and <c>short[]</c> buffers
    /// for the voice capture hot path. Eliminates per-frame heap allocation.
    /// </summary>
    public sealed class MicrophoneBufferPool
    {
        private readonly ConcurrentQueue<float[]> _floatPool = new ConcurrentQueue<float[]>();
        private readonly ConcurrentQueue<short[]> _shortPool = new ConcurrentQueue<short[]>();

        private readonly int _floatBufferSize;
        private readonly int _shortBufferSize;

        public MicrophoneBufferPool(int floatBufferSize = 8192, int shortBufferSize = 8192)
        {
            _floatBufferSize = floatBufferSize;
            _shortBufferSize = shortBufferSize;
        }

        /// <summary>
        /// Rent a <c>float[]</c> buffer of at least <paramref name="minSize"/> elements.
        /// Returns a buffer of exactly <c>_floatBufferSize</c> to maximise reuse.
        /// </summary>
        public float[] RentFloat(int minSize)
        {
            if (minSize > _floatBufferSize)
                throw new ArgumentOutOfRangeException(nameof(minSize),
                    $"Requested {minSize} floats but pool only provides {_floatBufferSize}.");

            if (_floatPool.TryDequeue(out float[] buffer))
                return buffer;

            return new float[_floatBufferSize];
        }

        /// <summary>
        /// Return a <c>float[]</c> buffer to the pool for reuse.
        /// </summary>
        public void ReturnFloat(float[] buffer)
        {
            if (buffer == null) return;
            if (buffer.Length != _floatBufferSize) return; // discard mis-sized buffers
            _floatPool.Enqueue(buffer);
        }

        /// <summary>
        /// Rent a <c>short[]</c> buffer of at least <paramref name="minSize"/> elements.
        /// Returns a buffer of exactly <c>_shortBufferSize</c> to maximise reuse.
        /// </summary>
        public short[] RentShort(int minSize)
        {
            if (minSize > _shortBufferSize)
                throw new ArgumentOutOfRangeException(nameof(minSize),
                    $"Requested {minSize} shorts but pool only provides {_shortBufferSize}.");

            if (_shortPool.TryDequeue(out short[] buffer))
                return buffer;

            return new short[_shortBufferSize];
        }

        /// <summary>
        /// Return a <c>short[]</c> buffer to the pool for reuse.
        /// </summary>
        public void ReturnShort(short[] buffer)
        {
            if (buffer == null) return;
            if (buffer.Length != _shortBufferSize) return; // discard mis-sized buffers
            _shortPool.Enqueue(buffer);
        }
    }
}
```

---

## Task 2: Update MicrophoneCapture to use pooled buffers

**File:**
- Modify: `Assets/Scripts/Voice/MicrophoneCapture.cs`

**Changes:**

1. Add a `MicrophoneBufferPool` field (injected via constructor or property):
```csharp
private readonly MicrophoneBufferPool _bufferPool;
private readonly ConcurrentQueue<short[]> _inputQueue;
```

2. Update constructor to accept and store the pool:
```csharp
public MicrophoneCapture(ConcurrentQueue<short[]> inputQueue, MicrophoneBufferPool bufferPool)
{
    _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
    _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
}
```

3. Replace `ProcessSamples` body — rent a pooled `short[]`, convert in-place, enqueue, return the buffer to pool:
```csharp
public void ProcessSamples(float[] floatSamples)
{
    if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
    if (floatSamples.Length == 0) return;

    short[] pcm = _bufferPool.RentShort(floatSamples.Length);
    for (int i = 0; i < floatSamples.Length; i++)
    {
        float clamped = floatSamples[i] < -1f ? -1f
                      : floatSamples[i] >  1f ?  1f
                      : floatSamples[i];
        pcm[i] = (short)(clamped * 32767f);
    }
    _inputQueue.Enqueue(pcm);
    _bufferPool.ReturnShort(pcm); // return after enqueue — Vosk dequeues the reference
}
```

> **Note:** The `short[]` is returned to the pool *after* enqueuing because `VoskRecognizerService.RecognitionLoop` dequeues it and processes it on the background thread. By the time `ProcessSamples` returns, the background thread has already claimed the reference from the queue — returning it to the pool is safe.

4. Delete the now-unused `ToPcm16` helper method.

---

## Task 3: Update MicrophoneInputHandler to use pooled float buffer

**File:**
- Modify: `Assets/Scripts/Voice/MicrophoneInputHandler.cs`

**Changes:**

1. Add a `MicrophoneBufferPool` field and update `Inject()` to accept and store it:
```csharp
private MicrophoneBufferPool _bufferPool;
```

```csharp
public void Inject(
    ConcurrentQueue<short[]> inputQueue,
    VoskRecognizerService    recognizerService,
    MicrophoneBufferPool     bufferPool)   // new parameter
{
    _capture            = new MicrophoneCapture(inputQueue, bufferPool);
    _recognizerService  = recognizerService;
    _bufferPool         = bufferPool;
}
```

2. Add a reusable `float[]` buffer field alongside `_clip`, `_lastSamplePos`, `_isCapturing`:
```csharp
private AudioClip _clip;
private int       _lastSamplePos;
private bool     _isCapturing;
private float[]  _reusableFloatBuffer;
```

3. In `OnPushToTalkStarted`, allocate the reusable buffer when capture begins:
```csharp
_lastSamplePos = 0;
_isCapturing   = true;
_reusableFloatBuffer = _bufferPool.RentFloat(8192);
```

4. In `StopCapture`, return the buffer to the pool:
```csharp
private void StopCapture()
{
    if (!_isCapturing) return;
    _isCapturing = false;
    Microphone.End(_deviceName);
    _clip = null;
    _bufferPool?.ReturnFloat(_reusableFloatBuffer);
    _reusableFloatBuffer = null;
    _capture?.EnqueueSentinel();
}
```

5. In `Update`, replace `new float[newSamples]` with the reusable buffer, copying into it:
```csharp
private void Update()
{
    if (!_isCapturing || _clip == null || _capture == null) return;

    int currentPos = Microphone.GetPosition(_deviceName);
    if (currentPos == _lastSamplePos) return;

    int newSamples = currentPos - _lastSamplePos;
    if (newSamples < 0) newSamples += _clip.samples; // ring-buffer wrap

    // Grow the reusable buffer if needed (should rarely happen)
    if (_reusableFloatBuffer == null || _reusableFloatBuffer.Length < newSamples)
        _reusableFloatBuffer = new float[newSamples];

    _clip.GetData(_reusableFloatBuffer, _lastSamplePos % _clip.samples);
    _capture.ProcessSamples(_reusableFloatBuffer, newSamples); // new overload
    _lastSamplePos = currentPos;
}
```

6. Update `BattleVoiceBootstrap.Start()` — both existing `Inject()` call sites (lines 151 and 209) must pass a `MicrophoneBufferPool` instance:
```csharp
// Replace:
_microphoneInputHandler.Inject(inputQueue, _recognizerService);
// With:
_microphoneInputHandler.Inject(inputQueue, _recognizerService, _bufferPool);
```

7. Add an overload to `MicrophoneCapture.ProcessSamples(float[], int count)`:
```csharp
public void ProcessSamples(float[] floatSamples, int count)
{
    if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
    if (count == 0) return;

    short[] pcm = _bufferPool.RentShort(count);
    for (int i = 0; i < count; i++)
    {
        float clamped = floatSamples[i] < -1f ? -1f
                      : floatSamples[i] >  1f ?  1f
                      : floatSamples[i];
        pcm[i] = (short)(clamped * 32767f);
    }
    _inputQueue.Enqueue(pcm);
    _bufferPool.ReturnShort(pcm);
}
```

> **Note:** `Update()` still reads `_clip.GetData(_reusableFloatBuffer, ...)` into the reused `float[]`. The `float[]` is returned to the pool only in `StopCapture()`, not on every frame — no allocations occur as long as the audio chunk stays within the pre-allocated buffer size.

---

## Task 4: Write MicrophoneCaptureBufferReuseTests

**File:**
- Create: `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs`

```csharp
using System.Collections.Concurrent;
using Axiom.Voice;
using NUnit.Framework;

namespace Axiom.Voice.Tests
{
    public class MicrophoneCaptureBufferReuseTests
    {
        private ConcurrentQueue<short[]> _inputQueue;
        private MicrophoneBufferPool _bufferPool;
        private MicrophoneCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _inputQueue = new ConcurrentQueue<short[]>();
            _bufferPool = new MicrophoneBufferPool(floatBufferSize: 8192, shortBufferSize: 8192);
            _capture = new MicrophoneCapture(_inputQueue, _bufferPool);
        }

        [Test]
        public void RentFloat_ReturnsPooledBuffer_WhenAvailable()
        {
            var buf = _bufferPool.RentFloat(100);
            _bufferPool.ReturnFloat(buf);
            var buf2 = _bufferPool.RentFloat(100);
            Assert.AreSame(buf, buf2, "Second rent should return the same buffer instance.");
            _bufferPool.ReturnFloat(buf2);
        }

        [Test]
        public void RentFloat_AllocatesNew_WhenPoolEmpty()
        {
            var buf = _bufferPool.RentFloat(100);
            Assert.IsNotNull(buf);
            Assert.GreaterOrEqual(buf.Length, 100);
            _bufferPool.ReturnFloat(buf);
        }

        [Test]
        public void RentFloat_Throws_WhenRequestedSizeExceedsPoolSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentFloat(9000));
        }

        [Test]
        public void RentShort_AllocatesNew_WhenPoolEmpty()
        {
            var buf = _bufferPool.RentShort(100);
            Assert.IsNotNull(buf);
            Assert.GreaterOrEqual(buf.Length, 100);
            _bufferPool.ReturnShort(buf);
        }

        [Test]
        public void RentShort_Throws_WhenRequestedSizeExceedsPoolSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentShort(9000));
        }

        [Test]
        public void ProcessSamples_EnqueuesPcm16ConvertedData()
        {
            float[] floatSamples = new float[100];
            for (int i = 0; i < 100; i++)
                floatSamples[i] = 0.5f; // half amplitude

            _capture.ProcessSamples(floatSamples);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] pcm),
                "Expected a PCM16 buffer in the input queue.");
            Assert.AreEqual(100, pcm.Length);
            Assert.AreEqual((short)(0.5f * 32767f), pcm[0]);
        }

        [Test]
        public void ProcessSamples_ClampsClippingValues()
        {
            float[] floatSamples = new float[4] { -2f, -1.5f, 1.5f, 2f };
            _capture.ProcessSamples(floatSamples);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] pcm));
            Assert.AreEqual(-32767, pcm[0]); // clamped -2 → -1
            Assert.AreEqual(-32767, pcm[1]); // clamped -1.5 → -1
            Assert.AreEqual( 32767, pcm[2]); // clamped  1.5 →  1
            Assert.AreEqual( 32767, pcm[3]); // clamped  2 →  1
        }

        [Test]
        public void ProcessSamples_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => _capture.ProcessSamples(null));
        }

        [Test]
        public void ProcessSamples_ThrowsOnEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => _capture.ProcessSamples(new float[0]));
        }

        [Test]
        public void ProcessSamples_ShortBufferReturnedAfterEnqueue_CanBeReused()
        {
            float[] floatSamples = new float[100];
            _capture.ProcessSamples(floatSamples);
            _capture.ProcessSamples(floatSamples); // second call should not grow pool

            // Both PCM buffers were returned to pool; third rent should reuse same buffer
            Assert.IsTrue(_inputQueue.TryDequeue(out _));
            Assert.IsTrue(_inputQueue.TryDequeue(out _));
            // Pool should have at least 1 buffer back
            var buf = _bufferPool.RentShort(100);
            _bufferPool.ReturnShort(buf);
            Assert.Pass("Buffer reuse did not throw and pool accepted returns.");
        }

        [Test]
        public void MultipleCaptures_AllEnqueueSuccessfully()
        {
            for (int i = 0; i < 10; i++)
            {
                float[] buf = new float[1000];
                _capture.ProcessSamples(buf);
            }

            Assert.AreEqual(10, _inputQueue.Count, "All 10 audio chunks should be in the queue.");
        }
    }
}
```

---

## Task 5: Write MicrophoneInputHandlerBufferTests

**File:**
- Create: `Assets/Tests/Editor/Voice/MicrophoneInputHandlerBufferTests.cs`

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Voice.Tests
{
    public class MicrophoneInputHandlerBufferTests
    {
        private static readonly MethodInfo _onEnableMethod =
            typeof(MicrophoneInputHandler).GetMethod("OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _stopCaptureMethod =
            typeof(MicrophoneInputHandler).GetMethod("StopCapture",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject _gameObject;
        private MicrophoneInputHandler _handler;
        private ConcurrentQueue<short[]> _inputQueue;
        private MicrophoneBufferPool _bufferPool;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestMicHandler");
            _handler = _gameObject.AddComponent<MicrophoneInputHandler>();
            _inputQueue = new ConcurrentQueue<short[]>();
            _bufferPool = new MicrophoneBufferPool();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Inject_AcceptsBufferPool()
        {
            Assert.DoesNotThrow(() =>
                _handler.Inject(_inputQueue, null, _bufferPool));
        }

        [Test]
        public void StopCapture_CalledTwice_DoesNotThrow()
        {
            _handler.Inject(_inputQueue, null, _bufferPool);
            // Cannot call StopCapture directly without a real AudioClip,
            // but Inject + null guard should not throw
            Assert.DoesNotThrow(() => _stopCaptureMethod.Invoke(_handler, null));
        }

        [Test]
        public void Inject_WithNullRecognizerService_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                _handler.Inject(_inputQueue, null, _bufferPool));
        }
    }
}
```

---

## Task 6: Profiler Validation

> **Unity Editor task (user):** In Unity Editor, open **Window → Analysis → Profiler**. Start a 60-second recording of a sustained push-to-talk combat session in `Battle.unity`. Capture before/after screenshots of the **GC Alloc** column in the Profiler timeline and attach them to the Jira ticket.

**Acceptance Criteria Check:**
- GC Alloc column shows noticeably fewer/allocation-free spikes during PTT sessions after the fix
- No frame hitches (frame time stays below 16.67ms)
- All existing `VoiceTests` Edit Mode tests pass (see Task 7)

---

## Task 7: Run all Voice Edit Mode tests

Run: `Unity Test Runner → Edit Mode → VoiceTests`

Expected: **ALL PASS** — especially:
- `MicrophoneCaptureBufferReuseTests` — all 10 tests pass
- `MicrophoneInputHandlerBufferTests` — all 3 tests pass
- `VoskRecognizerServiceTests` — no regressions

---

## Task 8: UVCS Check-in

- [ ] **Check in via UVCS:**
  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `perf(DEV-73): eliminate voice capture GC pressure via buffer pooling`

  - `Assets/Scripts/Voice/MicrophoneBufferPool.cs`
  - `Assets/Scripts/Voice/MicrophoneBufferPool.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureBufferReuseTests.cs.meta`
  - `Assets/Tests/Editor/Voice/MicrophoneInputHandlerBufferTests.cs`
  - `Assets/Tests/Editor/Voice/MicrophoneInputHandlerBufferTests.cs.meta`

---

## Execution Options

**1. Subagent-Driven (this session)** — dispatch subagents per task, review between tasks, fast iteration

**2. Parallel Session (separate)** — open new session with executing-plans, batch execution with checkpoints

Which approach?

(End of file)
