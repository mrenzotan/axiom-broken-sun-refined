using System;
using System.Collections.Concurrent;

namespace Axiom.Voice
{
    /// <summary>
    /// Thread-safe pool of reusable <c>float[]</c> and <c>short[]</c> buffers
    /// for the voice capture hot path. Eliminates per-frame heap allocation.
    /// Buffers are keyed by exact length so consumers can rely on
    /// <c>array.Length</c> as the valid data count.
    /// </summary>
    public sealed class MicrophoneBufferPool
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<float[]>> _floatPools = new ConcurrentDictionary<int, ConcurrentQueue<float[]>>();
        private readonly ConcurrentDictionary<int, ConcurrentQueue<short[]>> _shortPools = new ConcurrentDictionary<int, ConcurrentQueue<short[]>>();

        /// <summary>
        /// Rent a <c>float[]</c> buffer of exactly <paramref name="size"/> elements.
        /// </summary>
        public float[] RentFloat(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");

            ConcurrentQueue<float[]> pool = _floatPools.GetOrAdd(size, _ => new ConcurrentQueue<float[]>());
            if (pool.TryDequeue(out float[] buffer))
                return buffer;

            return new float[size];
        }

        /// <summary>
        /// Return a <c>float[]</c> buffer to the pool for reuse.
        /// Mis-sized buffers are silently discarded.
        /// </summary>
        public void ReturnFloat(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;

            ConcurrentQueue<float[]> pool = _floatPools.GetOrAdd(buffer.Length, _ => new ConcurrentQueue<float[]>());
            pool.Enqueue(buffer);
        }

        /// <summary>
        /// Rent a <c>short[]</c> buffer of exactly <paramref name="size"/> elements.
        /// </summary>
        public short[] RentShort(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");

            ConcurrentQueue<short[]> pool = _shortPools.GetOrAdd(size, _ => new ConcurrentQueue<short[]>());
            if (pool.TryDequeue(out short[] buffer))
                return buffer;

            return new short[size];
        }

        /// <summary>
        /// Return a <c>short[]</c> buffer to the pool for reuse.
        /// Mis-sized buffers are silently discarded.
        /// </summary>
        public void ReturnShort(short[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;

            ConcurrentQueue<short[]> pool = _shortPools.GetOrAdd(buffer.Length, _ => new ConcurrentQueue<short[]>());
            pool.Enqueue(buffer);
        }
    }
}
