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
