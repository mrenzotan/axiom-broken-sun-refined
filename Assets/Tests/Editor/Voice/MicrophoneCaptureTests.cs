using System.Collections.Concurrent;
using NUnit.Framework;

namespace Axiom.Voice.Tests
{
    public class MicrophoneCaptureTests
    {
        // ── Constructor ──────────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullQueue_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new MicrophoneCapture(null));
        }

        // ── ProcessSamples: guard clauses ─────────────────────────────────────────

        [Test]
        public void ProcessSamples_NullArray_ThrowsArgumentNullException()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            Assert.Throws<System.ArgumentNullException>(() =>
                capture.ProcessSamples(null));
        }

        [Test]
        public void ProcessSamples_EmptyArray_DoesNotEnqueue()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[0]);

            Assert.AreEqual(0, queue.Count);
        }

        // ── ProcessSamples: conversion and enqueue ───────────────────────────────

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuesExactlyOneChunk()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.5f, -0.5f });

            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void ProcessSamples_ValidSamples_EnqueuedChunkMatchesInputLength()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.1f, 0.2f, 0.3f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(3, result.Length);
        }

        [Test]
        public void ProcessSamples_PlusOneSample_EnqueuesMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MinusOneSample_EnqueuesMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { -1.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_ZeroSample_EnqueuesZero()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(0, result[0]);
        }

        [Test]
        public void ProcessSamples_AboveClampValue_ClampsToMaxPositiveShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(32767, result[0]);
        }

        [Test]
        public void ProcessSamples_BelowClampValue_ClampsToMinNegativeShort()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { -2.0f });

            queue.TryDequeue(out short[] result);
            Assert.AreEqual(-32767, result[0]);
        }

        [Test]
        public void ProcessSamples_MultipleCallsEnqueueMultipleChunks()
        {
            var queue   = new ConcurrentQueue<short[]>();
            var capture = new MicrophoneCapture(queue);

            capture.ProcessSamples(new float[] { 0.1f });
            capture.ProcessSamples(new float[] { 0.2f });
            capture.ProcessSamples(new float[] { 0.3f });

            Assert.AreEqual(3, queue.Count);
        }
    }
}
