# Audio Chunk Buffering — Voice Recognition Reliability Fix

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix intermittent voice recognition failures by buffering microphone samples into larger chunks (~0.25s) before sending to Vosk's `AcceptWaveform`, so the acoustic model receives enough continuous audio to reliably detect speech patterns.

**Architecture:** Add an internal `List<float>` accumulator inside `MicrophoneCapture` that collects per-frame audio samples. When the accumulator reaches a configurable threshold (default 4000 samples = 0.25s at 16kHz), convert and enqueue the entire accumulated buffer as one `short[]` chunk. On PTT release, flush any remaining samples before the null sentinel. Existing behavior is preserved via a default `minChunkSize = 1` constructor parameter — tests that don't specify it continue to work as before.

**Tech Stack:** C# (.NET Standard 2.1 / Unity Mono), `System.Collections.Concurrent`, Vosk

**Root Cause:** `MicrophoneInputHandler.Update()` reads ~267 samples per frame (at 60fps) and immediately enqueues each tiny chunk. Vosk's acoustic model struggles to find speech patterns in 16ms audio fragments. Frame-time jitter can exacerbate this, creating effective gaps. Accumulating to ~0.25s gives Vosk enough context for reliable recognition.

---

### Task 1: Add internal audio accumulator to MicrophoneCapture

**Files:**
- Modify: `Assets/Scripts/Voice/MicrophoneCapture.cs`

**Step 1: Add accumulator fields and minChunkSize parameter**

Replace the entire file:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Axiom.Voice
{
    public class MicrophoneCapture
    {
        private readonly MicrophoneBufferPool _bufferPool;
        private readonly ConcurrentQueue<short[]> _inputQueue;
        private readonly List<float> _accumulator;
        private readonly int _minChunkSize;

        public MicrophoneCapture(
            ConcurrentQueue<short[]> inputQueue,
            MicrophoneBufferPool bufferPool,
            int minChunkSize = 1)
        {
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
            _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
            _minChunkSize = minChunkSize > 0
                ? minChunkSize
                : throw new ArgumentOutOfRangeException(nameof(minChunkSize),
                    "minChunkSize must be positive.");
            _accumulator = new List<float>(_minChunkSize);
        }

        public void ProcessSamples(float[] floatSamples)
        {
            if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
            if (floatSamples.Length == 0) return;
            ProcessSamples(floatSamples, floatSamples.Length);
        }

        public void ProcessSamples(float[] floatSamples, int count)
        {
            if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
            if (count == 0) return;
            if (count > floatSamples.Length)
                throw new ArgumentOutOfRangeException(nameof(count),
                    $"count ({count}) cannot exceed floatSamples.Length ({floatSamples.Length}).");

            for (int i = 0; i < count; i++)
                _accumulator.Add(floatSamples[i]);

            if (_accumulator.Count >= _minChunkSize)
                FlushAccumulator();
        }

        public void EnqueueSentinel()
        {
            if (_accumulator.Count > 0)
                FlushAccumulator();
            _inputQueue.Enqueue(null);
        }

        private void FlushAccumulator()
        {
            int count = _accumulator.Count;
            short[] pcm = _bufferPool.RentShort(count);
            for (int i = 0; i < count; i++)
            {
                float clamped = _accumulator[i] < -1f ? -1f
                              : _accumulator[i] >  1f ?  1f
                              : _accumulator[i];
                pcm[i] = (short)(clamped * 32767f);
            }
            _accumulator.Clear();
            _inputQueue.Enqueue(pcm);
        }
    }
}
```

**Step 2: Run existing voice tests to verify backward compatibility**

Run all voice tests in Unity Editor → Test Runner → Edit Mode → Axiom.Voice.Tests. All existing tests must pass because `minChunkSize` defaults to `1` and behavior is identical for small inputs.

---

### Task 2: Wire minChunkSize through MicrophoneInputHandler

**Files:**
- Modify: `Assets/Scripts/Voice/MicrophoneInputHandler.cs`

**Step 1: Add serialized threshold field and pass to MicrophoneCapture**

In `MicrophoneInputHandler.cs`, add a serialized field and update `Inject()`:

Add after line 25 (`[SerializeField] private int _sampleRate = 16000;`):

```csharp
        [SerializeField]
        [Tooltip("Minimum audio samples to accumulate before enqueuing to Vosk. " +
                 "4000 = 0.25s at 16kHz. Increase if recognition is unreliable.")]
        private int _chunkSampleThreshold = 4000;
```

Update `Inject()` (line 51) to pass `_chunkSampleThreshold`:

```csharp
            _capture = new MicrophoneCapture(inputQueue, bufferPool, _chunkSampleThreshold);
```

Full updated `Inject()`:

```csharp
        public void Inject(
            ConcurrentQueue<short[]> inputQueue,
            VoskRecognizerService    recognizerService,
            MicrophoneBufferPool     bufferPool)
        {
            _capture            = new MicrophoneCapture(inputQueue, bufferPool, _chunkSampleThreshold);
            _recognizerService  = recognizerService;
            _bufferPool         = bufferPool;
        }
```

---

### Task 3: Add tests for buffering behavior

**Files:**
- Modify: `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs`

**Step 1: Add new test methods**

Append the following tests to `MicrophoneCaptureTests`, before the closing `}` of the class:

```csharp
        // ── Constructor: minChunkSize guard ───────────────────────────────────────

        [Test]
        public void Constructor_MinChunkSizeZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new MicrophoneCapture(new ConcurrentQueue<short[]>(), _bufferPool, 0));
        }

        [Test]
        public void Constructor_MinChunkSizeNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new MicrophoneCapture(new ConcurrentQueue<short[]>(), _bufferPool, -5));
        }

        // ── Buffering: below threshold → no enqueue ───────────────────────────────

        [Test]
        public void ProcessSamples_BelowMinChunkSize_DoesNotEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.ProcessSamples(new float[2000]);

            Assert.AreEqual(0, queue.Count,
                "Fewer samples than minChunkSize should not trigger an enqueue.");
        }

        [Test]
        public void ProcessSamples_AccumulatesOverMultipleCalls_BeforeEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.ProcessSamples(new float[2000]);
            capture.ProcessSamples(new float[1999]);

            Assert.AreEqual(0, queue.Count,
                "Still below threshold after second call — no enqueue yet.");
        }

        [Test]
        public void ProcessSamples_CrossesMinChunkSize_FiresSingleEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.ProcessSamples(new float[2000]);
            capture.ProcessSamples(new float[2000]); // total 4000 → flush

            Assert.AreEqual(1, queue.Count,
                "Crossing the threshold should produce exactly one enqueued chunk.");
            queue.TryDequeue(out short[] result);
            Assert.AreEqual(4000, result.Length);
        }

        [Test]
        public void ProcessSamples_ExceedsMinChunkSize_EnqueuesAllAccumulated()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.ProcessSamples(new float[5000]);

            Assert.AreEqual(1, queue.Count,
                "5000 samples (>= 4000) should trigger one enqueue.");
            queue.TryDequeue(out short[] result);
            Assert.AreEqual(5000, result.Length,
                "All 5000 accumulated samples should be in the chunk.");
        }

        // ── Sentinel flushes remaining accumulator ────────────────────────────────

        [Test]
        public void EnqueueSentinel_FlushesRemainingSamples_BeforeNull()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.ProcessSamples(new float[1500]);
            capture.EnqueueSentinel();

            Assert.AreEqual(2, queue.Count,
                "Sentinel should enqueue the leftover chunk then the null.");
            Assert.IsTrue(queue.TryDequeue(out short[] leftover));
            Assert.AreEqual(1500, leftover.Length);
            Assert.IsTrue(queue.TryDequeue(out short[] sentinel));
            Assert.IsNull(sentinel);
        }

        [Test]
        public void EnqueueSentinel_EmptyAccumulator_OnlyEnqueuesNull()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool, minChunkSize: 4000);

            capture.EnqueueSentinel();

            Assert.AreEqual(1, queue.Count,
                "With empty accumulator, sentinel should only enqueue null.");
            Assert.IsTrue(queue.TryDequeue(out short[] sentinel));
            Assert.IsNull(sentinel);
        }

        // ── Default minChunkSize (1) preserves immediate-flush behavior ────────────

        [Test]
        public void ProcessSamples_DefaultThreshold_StillEnqueuesImmediately()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 0.1f, 0.2f, 0.3f });

            Assert.AreEqual(1, queue.Count);
            queue.TryDequeue(out short[] result);
            Assert.AreEqual(3, result.Length);
        }
```

**Step 2: Run all voice tests**

Run all tests in `Axiom.Voice.Tests` assembly. All 75 existing tests + 8 new tests = 83 tests should pass.

---

### Task 4: UVCS check-in

> **Unity Editor task (user):** Open Unity → Window → Unity Version Control → Pending Changes, stage all modified files, and check in.

- [ ] **Check in via UVCS:**

  Unity Version Control → Pending Changes → stage the files listed below → Check in with message: `feat: buffer mic audio chunks for Vosk recognition reliability`

  - `Assets/Scripts/Voice/MicrophoneCapture.cs`
  - `Assets/Scripts/Voice/MicrophoneCapture.cs.meta`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs`
  - `Assets/Scripts/Voice/MicrophoneInputHandler.cs.meta`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs`
  - `Assets/Tests/Editor/Voice/MicrophoneCaptureTests.cs.meta`
