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
        private readonly MicrophoneBufferPool _bufferPool;
        private readonly ConcurrentQueue<short[]> _inputQueue;

        public MicrophoneCapture(ConcurrentQueue<short[]> inputQueue, MicrophoneBufferPool bufferPool)
        {
            _inputQueue = inputQueue ?? throw new ArgumentNullException(nameof(inputQueue));
            _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
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

            ProcessSamples(floatSamples, floatSamples.Length);
        }

        /// <summary>
        /// Converts the first <paramref name="count"/> elements of <paramref name="floatSamples"/>
        /// to PCM16 and enqueues the result.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="floatSamples"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> exceeds <paramref name="floatSamples"/>.Length.</exception>
        public void ProcessSamples(float[] floatSamples, int count)
        {
            if (floatSamples == null) throw new ArgumentNullException(nameof(floatSamples));
            if (count == 0) return;
            if (count > floatSamples.Length)
                throw new ArgumentOutOfRangeException(nameof(count),
                    $"count ({count}) cannot exceed floatSamples.Length ({floatSamples.Length}).");

            short[] pcm = _bufferPool.RentShort(count);
            for (int i = 0; i < count; i++)
            {
                float clamped = floatSamples[i] < -1f ? -1f
                              : floatSamples[i] >  1f ?  1f
                              : floatSamples[i];
                pcm[i] = (short)(clamped * 32767f);
            }
            _inputQueue.Enqueue(pcm);
        }

        /// <summary>
        /// Enqueues a <c>null</c> sentinel into the input queue. The background
        /// recognition loop uses this to trigger <c>FinalResult()</c> and reset
        /// recognizer state between push-to-talk sessions.
        /// </summary>
        public void EnqueueSentinel()
        {
            _inputQueue.Enqueue(null);
        }
    }
}
