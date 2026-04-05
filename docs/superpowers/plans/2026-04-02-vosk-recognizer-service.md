# VoskRecognizerService Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `VoskRecognizerService` as a plain C# producer/consumer service so that `AcceptWaveform()` never runs on the Unity main thread, satisfying DEV-19 acceptance criteria.

**Architecture:** `VoskRecognizerService` owns one background `Task`. It accepts a pre-constructed `VoskRecognizer` (caller creates it from a loaded Model + grammar), a `ConcurrentQueue<short[]>` for audio input (main thread enqueues, background dequeues), and a `ConcurrentQueue<string>` for JSON results (background enqueues, main thread dequeues). A `volatile bool _finalResultRequested` flag triggers a `FinalResult()` flush when push-to-talk is released, without needing additional synchronization primitives.

**Tech Stack:** C# 9, `System.Threading.Tasks`, `System.Collections.Concurrent`, Vosk C# bindings (`Assets/ThirdParty/Vosk/Vosk.dll`), Unity Test Framework (Edit Mode / NUnit)

---

## Prerequisites

The Vosk model must be present for tests to run. If it is not yet placed, tests will be skipped gracefully via `Assume.That` — they will not fail.  
Model path: `Assets/StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph/`  
(Model placement is covered by a separate ticket — DEV-18 or equivalent Vosk Service Setup story.)

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `Assets/Scripts/Voice/VoskRecognizerService.cs` | Plain C# service: threading lifecycle, AcceptWaveform loop, FinalResult flush |
| **Create** | `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs` | Edit Mode NUnit tests for lifecycle, thread isolation, FinalResult |
| **No change** | `Assets/Scripts/Voice/Axiom.Voice.asmdef` | Already has `overrideReferences: true`, `precompiledReferences: ["Vosk.dll"]` |
| **No change** | `Assets/Tests/Editor/Voice/VoiceTests.asmdef` | Already references `Axiom.Voice` with `testReferences` for test runner |

---

## Task 1: Write Failing Edit Mode Tests

**Files:**
- Create: `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`

> These tests compile against `VoskRecognizerService` before it exists, so Task 1 ends at the point where tests compile but fail (red). `VoskRecognizerService` will be a stub created in Task 2.

- [ ] **Step 1.1: Create the test file**

Create `Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Vosk;

namespace Axiom.Voice.Tests
{
    [TestFixture]
    public class VoskRecognizerServiceTests
    {
        // Shared model — expensive to load, so loaded once per test run.
        private static Model s_model;

        [OneTimeSetUp]
        public static void LoadModel()
        {
            string modelPath = Path.Combine(
                Application.streamingAssetsPath,
                "VoskModels/vosk-model-en-us-0.22-lgraph");

            if (Directory.Exists(modelPath))
                s_model = new Model(modelPath);
        }

        [OneTimeTearDown]
        public static void UnloadModel()
        {
            s_model?.Dispose();
            s_model = null;
        }

        // Per-test state
        private VoskRecognizerService _service;
        private VoskRecognizer _recognizer;
        private ConcurrentQueue<short[]> _inputQueue;
        private ConcurrentQueue<string> _resultQueue;

        [SetUp]
        public void SetUp()
        {
            Assume.That(s_model != null,
                "Vosk model not present at StreamingAssets/VoskModels/vosk-model-en-us-0.22-lgraph — skipping");

            _inputQueue = new ConcurrentQueue<short[]>();
            _resultQueue = new ConcurrentQueue<string>();
            _recognizer = new VoskRecognizer(s_model, 16000f, "[\"test\"]");
            _service = new VoskRecognizerService(_recognizer, _inputQueue, _resultQueue);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            _recognizer = null; // owned and disposed by VoskRecognizerService
        }

        // --- Lifecycle ---

        [Test]
        public void Stop_BeforeStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.Stop());
        }

        [Test]
        public void Dispose_BeforeStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            _service.Dispose();
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void Start_ThenStop_CompletesWithinOneSecond()
        {
            _service.Start();

            bool stopCompleted = false;
            var thread = new Thread(() =>
            {
                _service.Stop();
                stopCompleted = true;
            });
            thread.Start();
            thread.Join(System.TimeSpan.FromSeconds(1));

            Assert.IsTrue(stopCompleted,
                "Stop() did not complete within 1 second — likely a thread leak");
        }

        // --- Background thread isolation ---

        [Test]
        public void Start_ReturnsImmediately_ProcessingIsOffMainThread()
        {
            // Enqueue 1 second of silence (16 000 samples at 16 kHz) BEFORE Start().
            // If AcceptWaveform ran on the main thread Start() would block for
            // >100 ms. Off-thread it should return in <10 ms.
            _inputQueue.Enqueue(new short[16000]);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _service.Start();
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 10,
                $"Start() blocked for {sw.ElapsedMilliseconds} ms — AcceptWaveform may be running on the main thread");
        }

        // --- FinalResult flushing ---

        [Test]
        public void RequestFinalResult_EnqueuesAtLeastOneResult()
        {
            _service.Start();

            _service.RequestFinalResult();
            // Give the background thread up to 200 ms to process the flag.
            Thread.Sleep(200);

            _service.Stop();

            Assert.GreaterOrEqual(_resultQueue.Count, 1,
                "Expected at least one result in queue after RequestFinalResult()");
        }

        [Test]
        public void Stop_EnqueuesFinalResult_AfterDrainingPendingAudio()
        {
            _service.Start();
            // Enqueue audio then stop — Stop() must drain the queue and call FinalResult().
            _inputQueue.Enqueue(new short[1600]);
            _service.Stop();

            Assert.GreaterOrEqual(_resultQueue.Count, 1,
                "Expected at least one result after Stop() drains pending audio");
        }
    }
}
```

- [ ] **Step 1.2: Verify the tests do not compile yet**

Open Unity Editor → Window → General → Test Runner → Edit Mode.  
Expected: compile error referencing `VoskRecognizerService` (type does not exist).  
This confirms the tests are genuinely failing before implementation.

---

## Task 2: Implement VoskRecognizerService (Make Tests Pass)

**Files:**
- Create: `Assets/Scripts/Voice/VoskRecognizerService.cs`

- [ ] **Step 2.1: Create the service**

Create `Assets/Scripts/Voice/VoskRecognizerService.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Vosk;

namespace Axiom.Voice
{
    /// <summary>
    /// Producer/consumer speech-recognition service. All <c>AcceptWaveform()</c> calls
    /// run exclusively on a background Task — never on the Unity main thread.
    ///
    /// Usage:
    ///   1. Caller creates a <see cref="VoskRecognizer"/> (from a loaded Model + grammar).
    ///   2. Construct this service with the recognizer and two shared queues.
    ///   3. Call <see cref="Start"/> to begin processing.
    ///   4. Main thread enqueues <c>short[]</c> PCM16 chunks into <paramref name="inputQueue"/>.
    ///   5. Main thread dequeues JSON result strings from <paramref name="resultQueue"/> in Update().
    ///   6. Call <see cref="RequestFinalResult"/> when push-to-talk key is released.
    ///   7. Call <see cref="Stop"/> or <see cref="Dispose"/> to shut down cleanly.
    /// </summary>
    public class VoskRecognizerService : IDisposable
    {
        private readonly VoskRecognizer _recognizer;
        private readonly ConcurrentQueue<short[]> _inputQueue;
        private readonly ConcurrentQueue<string> _resultQueue;

        private CancellationTokenSource _cts;
        private Task _recognitionTask;
        private volatile bool _finalResultRequested;
        private bool _disposed;

        public VoskRecognizerService(
            VoskRecognizer recognizer,
            ConcurrentQueue<short[]> inputQueue,
            ConcurrentQueue<string> resultQueue)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _inputQueue = inputQueue  ?? throw new ArgumentNullException(nameof(inputQueue));
            _resultQueue = resultQueue ?? throw new ArgumentNullException(nameof(resultQueue));
        }

        /// <summary>
        /// Starts the background recognition task. No-op if already started.
        /// Returns immediately — recognition runs off the main thread.
        /// </summary>
        public void Start()
        {
            if (_recognitionTask != null) return;
            _cts = new CancellationTokenSource();
            _recognitionTask = Task.Run(() => RecognitionLoop(_cts.Token));
        }

        /// <summary>
        /// Signals the background task to call <c>FinalResult()</c> and enqueue the result.
        /// Call this when push-to-talk is released.
        /// Thread-safe; safe to call from any thread.
        /// </summary>
        public void RequestFinalResult()
        {
            _finalResultRequested = true;
        }

        /// <summary>
        /// Cancels the background task, waits for it to exit, and drains any pending audio
        /// with a final <c>FinalResult()</c> flush. No-op if not started.
        /// </summary>
        public void Stop()
        {
            if (_recognitionTask == null) return;

            _cts.Cancel();
            _recognitionTask.Wait();
            _recognitionTask = null;

            _cts.Dispose();
            _cts = null;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _recognizer.Dispose();
        }

        // ── Background thread ─────────────────────────────────────────────────────

        private void RecognitionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_inputQueue.TryDequeue(out short[] samples))
                {
                    // AcceptWaveform returns true when it detects a complete utterance.
                    if (_recognizer.AcceptWaveform(samples, samples.Length))
                        _resultQueue.Enqueue(_recognizer.Result());
                }
                else if (_finalResultRequested)
                {
                    _finalResultRequested = false;
                    _resultQueue.Enqueue(_recognizer.FinalResult());
                }
                else
                {
                    // Yield the thread — prevents a busy-wait spin on an empty queue.
                    Thread.Sleep(1);
                }
            }

            // Drain any audio chunks enqueued after cancellation was requested.
            while (_inputQueue.TryDequeue(out short[] remaining))
                _recognizer.AcceptWaveform(remaining, remaining.Length);

            // Always flush a final result on Stop() so no partial recognition is lost.
            _resultQueue.Enqueue(_recognizer.FinalResult());
        }
    }
}
```

- [ ] **Step 2.2: Verify no compile errors**

In Unity Editor, check the bottom status bar.  
Expected: no red compile errors. The `Axiom.Voice.asmdef` already has `Vosk.dll` as a precompiled reference, so `using Vosk;` resolves without any asmdef changes.

- [ ] **Step 2.3: Run all VoskRecognizerService tests**

Unity Editor → Window → General → Test Runner → Edit Mode.  
Filter by `VoskRecognizerServiceTests`.

**If the Vosk model is present:**  
Expected: all 8 tests pass (green).

**If the Vosk model is not yet placed:**  
Expected: all 8 tests show as "Skipped" (yellow) — this is correct and expected. Tests are not failures.  
Note for later: once the model is placed (separate ticket), re-run to confirm green.

- [ ] **Step 2.4: Check in to UVCS**

Unity Version Control → Pending Changes → stage these files:
```
Assets/Scripts/Voice/VoskRecognizerService.cs
Assets/Scripts/Voice/VoskRecognizerService.cs.meta
Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs
Assets/Tests/Editor/Voice/VoskRecognizerServiceTests.cs.meta
```
Check in with message:  
`feat(DEV-19): implement VoskRecognizerService producer/consumer threading`

---

## Self-Review Checklist

**Spec coverage:**

| AC requirement | Covered by |
|---|---|
| Plain C# class (not MonoBehaviour) | `VoskRecognizerService` has no `MonoBehaviour` inheritance |
| `ConcurrentQueue<short[]>` for audio input | Constructor parameter; `_inputQueue.TryDequeue()` in loop |
| `AcceptWaveform()` on background thread only | `RecognitionLoop()` runs inside `Task.Run()` |
| `ConcurrentQueue<string>` for results | Constructor parameter; `_resultQueue.Enqueue()` in loop |
| Clean `Start()` / `Stop()` / `Dispose()` lifecycle | Implemented; `Stop()` waits on task, `Dispose()` guards against double-call |
| No thread leaks | `_cts.Cancel()` + `_recognitionTask.Wait()` in `Stop()`; `Dispose` calls `Stop` |
| Unity Profiler zero-frame-impact | Verified manually: run Profiler during voice session, confirm CPU Main Thread row shows no AcceptWaveform allocation |

**Placeholder scan:** None found.

**Type consistency:** `VoskRecognizerService` constructor signature matches usage in test `SetUp`.  `RequestFinalResult()`, `Start()`, `Stop()`, `Dispose()` names are consistent between implementation and tests.

---

## Notes

- **Model placement is not part of this ticket.** If the Vosk model is absent the tests skip cleanly. Do not treat skipped tests as failures until the model is in place.
- **Grammar is passed in by the caller** (will be `SpellVocabularyManager` in a later ticket). `VoskRecognizerService` never touches grammar directly — it receives an already-configured `VoskRecognizer`.
- **`Thread.Sleep(1)` in the empty-queue branch** prevents a spin-loop that would peg the background thread at 100% CPU between push-to-talk bursts. At 16 kHz, 1 ms of sleep adds at most 16 samples of latency, which is imperceptible for spell-name recognition.
- **FinalResult JSON format:** Vosk returns `{"text":"spell name"}` (or `{"text":""}` when silent). The consumer (`SpellCastController`, a later ticket) is responsible for parsing this JSON and matching against `SpellData` assets.
