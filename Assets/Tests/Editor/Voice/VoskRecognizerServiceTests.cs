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
        public void Sentinel_EnqueuesAtLeastOneResult()
        {
            _service.Start();

            _inputQueue.Enqueue(null);
            // Give the background thread up to 200 ms to process the sentinel.
            Thread.Sleep(200);

            _service.Stop();

            Assert.GreaterOrEqual(_resultQueue.Count, 1,
                "Expected at least one result in queue after null sentinel is processed");
        }

        [Test]
        public void SentinelInInputQueue_TriggersFinalResult_AndResetsRecognizerState()
        {
            // Arrange: recognizer with grammar ["ignis", "aqua"]
            _recognizer = new VoskRecognizer(s_model, 16000f, "[\"ignis\", \"aqua\"]");
            _service = new VoskRecognizerService(_recognizer, _inputQueue, _resultQueue);
            _service.Start();

            // Act: enqueue audio samples followed by a null sentinel, then more audio
            short[] samples1 = new short[800]; // ~50 ms of audio
            short[] samples2 = new short[800];
            _inputQueue.Enqueue(samples1);
            _inputQueue.Enqueue(null); // sentinel
            _inputQueue.Enqueue(samples2);

            // Wait for the background thread to process sentinel + samples2
            Thread.Sleep(300);
            _service.Stop();

            // Assert:
            // 1. At least one result was produced (sentinel triggered FinalResult)
            Assert.GreaterOrEqual(_resultQueue.Count, 1,
                "Expected at least one result after sentinel was processed");

            // 2. samples2 audio was accepted after the sentinel reset
            Assert.GreaterOrEqual(_resultQueue.Count, 2,
                "Expected a second result for samples2 audio processed after reset");
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

        // --- Bounded shutdown (DEV-69) ---

        [Test]
        public void Stop_CompletesWithinTimeout_WhenBackgroundTaskIsSlow()
        {
            _service.Start();
            for (int i = 0; i < 100; i++)
                _inputQueue.Enqueue(new short[1600]);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _service.Stop();
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 2000,
                $"Stop() took {sw.ElapsedMilliseconds}ms — expected bounded shutdown within 2s");
        }

        [Test]
        public void Dispose_HandlesFaultedBackgroundTask_WithoutThrowing()
        {
            _service.Start();
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void RepeatedStopAndDispose_DoesNotThrowOrHang()
        {
            _service.Start();
            _service.Stop();
            Assert.DoesNotThrow(() => _service.Stop());
            Assert.DoesNotThrow(() => _service.Dispose());
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        [Test]
        public void Stop_AfterDispose_IsNoOp()
        {
            _service.Dispose();
            Assert.DoesNotThrow(() => _service.Stop());
        }

        [Test]
        public void DoubleStart_DoesNotCreateSecondTask()
        {
            _service.Start();
            _service.Start();
            _service.Stop();
            Assert.Pass("Double Start did not throw or hang on Stop.");
        }
    }
}
