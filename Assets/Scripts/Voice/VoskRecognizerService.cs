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

        public ConcurrentQueue<short[]> InputQueue => _inputQueue;
        public ConcurrentQueue<string>  ResultQueue => _resultQueue;

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
