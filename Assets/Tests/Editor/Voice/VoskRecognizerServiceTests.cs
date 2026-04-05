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
