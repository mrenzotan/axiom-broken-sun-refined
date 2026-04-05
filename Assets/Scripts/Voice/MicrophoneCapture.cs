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
