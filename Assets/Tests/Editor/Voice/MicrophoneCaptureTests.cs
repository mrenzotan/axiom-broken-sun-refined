using System.Collections.Concurrent;
using NUnit.Framework;

namespace Axiom.Voice.Tests
{
    public class MicrophoneCaptureTests
    {
        private MicrophoneBufferPool _bufferPool;

        [SetUp]
        public void SetUp()
        {
            _bufferPool = new MicrophoneBufferPool();
        }

        // ── Constructor ──────────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullQueue_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MicrophoneCapture(null, _bufferPool));
        }

        [Test]
        public void Constructor_NullBufferPool_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MicrophoneCapture(new System.Collections.Concurrent.ConcurrentQueue<short[]>(), null));
        }

        // ── ProcessSamples: guard clauses ─────────────────────────────────────────

        [Test]
        public void ProcessSamples_NullArray_ThrowsArgumentNullException()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            Assert.Throws<System.ArgumentNullException>(() =>
                capture.ProcessSamples(null));
        }

        [Test]
        public void ProcessSamples_EmptyArray_DoesNotEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[0]);

            Assert.AreEqual(0, queue.Count);
        }

        // ── ProcessSamples: conversion and enqueue ───────────────────────────────

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuesExactlyOneChunk()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 0.5f, -0.5f });

            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuedChunkMatchesInputLength()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 0.1f, 0.2f, 0.3f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(3, result.Length);
        }

        [Test]
        public void ProcessSamples_PlusOneSample_EnqueuesMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MinusOneSample_EnqueuesMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { -1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_ZeroSample_EnqueuesZero()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 0.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(0, result[0]);
        }

        [Test]
        public void ProcessSamples_AboveClampValue_ClampsToMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_BelowClampValue_ClampsToMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { -2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MultipleCallsEnqueueMultipleChunks()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue, _bufferPool);

            capture.ProcessSamples(new float[] { 0.1f });
            capture.ProcessSamples(new float[] { 0.2f });
            capture.ProcessSamples(new float[] { 0.3f });

            Assert.AreEqual(3, queue.Count);
        }

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
    }
}
