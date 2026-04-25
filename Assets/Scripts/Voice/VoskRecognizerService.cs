using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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
    ///   6. Enqueue a <c>null</c> sentinel into <paramref name="inputQueue"/> when push-to-talk key is released.
    ///   7. Call <see cref="Stop"/> or <see cref="Dispose"/> to shut down cleanly.
    /// </summary>
    public class VoskRecognizerService : IDisposable
    {
        private readonly VoskRecognizer _recognizer;
        private readonly ConcurrentQueue<short[]> _inputQueue;
        private readonly ConcurrentQueue<string> _resultQueue;
        private readonly MicrophoneBufferPool _bufferPool;

        public ConcurrentQueue<short[]> InputQueue => _inputQueue;
        public ConcurrentQueue<string> ResultQueue => _resultQueue;

        private CancellationTokenSource _cts;
        private Task _recognitionTask;
        private bool _disposed;

        private const int ShutdownTimeoutMs = 2000;

        public VoskRecognizerService(
            VoskRecognizer recognizer,
            ConcurrentQueue<short[]> inputQueue,
            ConcurrentQueue<string> resultQueue,
            MicrophoneBufferPool bufferPool = null)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
            _resultQueue = resultQueue ?? throw new ArgumentNullException(nameof(resultQueue));
            _bufferPool = bufferPool;
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
        /// Cancels the background task and waits up to <see cref="ShutdownTimeoutMs"/> for it
        /// to exit. Drains any remaining audio and flushes a final result. No-op if not started.
        /// After this call, the service can be restarted by calling <see cref="Start"/>.
        /// </summary>
        public void Stop()
        {
            if (_recognitionTask == null) return;

            _cts.Cancel();

            bool completed = _recognitionTask.Wait(ShutdownTimeoutMs);
            if (!completed)
            {
                Debug.LogWarning(
                    "[VoskRecognizerService] Background recognition task did not exit within " +
                    $"{ShutdownTimeoutMs}ms. Forcibly continuing shutdown.");
            }

            if (_recognitionTask.IsFaulted)
            {
                Debug.LogError(
                    "[VoskRecognizerService] Background recognition task faulted: " +
                    $"{_recognitionTask.Exception?.Flatten().Message}");
            }

            _recognitionTask = null;
            _cts?.Dispose();
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
                    if (samples == null)
                    {
                        // Sentinel: trigger FinalResult() to reset recognizer state
                        // between push-to-talk sessions.
                        _resultQueue.Enqueue(_recognizer.FinalResult());
                    }
                    else
                    {
                        // AcceptWaveform returns true when it detects a complete utterance.
                        if (_recognizer.AcceptWaveform(samples, samples.Length))
                            _resultQueue.Enqueue(_recognizer.Result());
                        _bufferPool?.ReturnShort(samples);
                    }
                }
                else
                {
                    // Yield the thread — prevents a busy-wait spin on an empty queue.
                    Thread.Sleep(1);
                }
            }

            // Drain any audio chunks enqueued after cancellation was requested.
            while (_inputQueue.TryDequeue(out short[] remaining))
            {
                if (remaining == null) continue; // skip sentinels during shutdown drain
                _recognizer.AcceptWaveform(remaining, remaining.Length);
                _bufferPool?.ReturnShort(remaining);
            }

            // Always flush a final result on Stop() so no partial recognition is lost.
            _resultQueue.Enqueue(_recognizer.FinalResult());
        }
    }
}
