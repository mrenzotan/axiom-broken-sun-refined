using System;
using System.Collections.Concurrent;
using Axiom.Voice;
using NUnit.Framework;

namespace Axiom.Voice.Tests
{
    public class MicrophoneCaptureBufferReuseTests
    {
        private ConcurrentQueue<short[]> _inputQueue;
        private MicrophoneBufferPool _bufferPool;
        private MicrophoneCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _inputQueue = new ConcurrentQueue<short[]>();
            _bufferPool = new MicrophoneBufferPool();
            _capture = new MicrophoneCapture(_inputQueue, _bufferPool);
        }

        [Test]
        public void RentFloat_ReturnsPooledBuffer_WhenAvailable()
        {
            var buf = _bufferPool.RentFloat(100);
            _bufferPool.ReturnFloat(buf);
            var buf2 = _bufferPool.RentFloat(100);
            Assert.AreSame(buf, buf2, "Second rent should return the same buffer instance.");
            _bufferPool.ReturnFloat(buf2);
        }

        [Test]
        public void RentFloat_AllocatesNew_WhenPoolEmpty()
        {
            var buf = _bufferPool.RentFloat(100);
            Assert.IsNotNull(buf);
            Assert.GreaterOrEqual(buf.Length, 100);
            _bufferPool.ReturnFloat(buf);
        }

        [Test]
        public void RentFloat_Throws_WhenSizeIsNotPositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentFloat(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentFloat(-1));
        }

        [Test]
        public void RentShort_AllocatesNew_WhenPoolEmpty()
        {
            var buf = _bufferPool.RentShort(100);
            Assert.IsNotNull(buf);
            Assert.GreaterOrEqual(buf.Length, 100);
            _bufferPool.ReturnShort(buf);
        }

        [Test]
        public void RentShort_Throws_WhenSizeIsNotPositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentShort(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _bufferPool.RentShort(-1));
        }

        [Test]
        public void ProcessSamples_EnqueuesPcm16ConvertedData()
        {
            float[] floatSamples = new float[100];
            for (int i = 0; i < 100; i++)
                floatSamples[i] = 0.5f; // half amplitude

            _capture.ProcessSamples(floatSamples);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] pcm),
                "Expected a PCM16 buffer in the input queue.");
            Assert.AreEqual(100, pcm.Length);
            Assert.AreEqual((short)(0.5f * 32767f), pcm[0]);
        }

        [Test]
        public void ProcessSamples_ClampsClippingValues()
        {
            float[] floatSamples = new float[4] { -2f, -1.5f, 1.5f, 2f };
            _capture.ProcessSamples(floatSamples);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] pcm));
            Assert.AreEqual(-32767, pcm[0]); // clamped -2 → -1
            Assert.AreEqual(-32767, pcm[1]); // clamped -1.5 → -1
            Assert.AreEqual( 32767, pcm[2]); // clamped  1.5 →  1
            Assert.AreEqual( 32767, pcm[3]); // clamped  2 →  1
        }

        [Test]
        public void ProcessSamples_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => _capture.ProcessSamples(null));
        }

        [Test]
        public void ProcessSamples_EmptyArray_DoesNotEnqueue()
        {
            _capture.ProcessSamples(new float[0]);
            Assert.AreEqual(0, _inputQueue.Count);
        }

        [Test]
        public void ProcessSamples_EnqueuedBuffersAreUniqueInstances()
        {
            float[] floatSamples = new float[100];
            _capture.ProcessSamples(floatSamples);
            _capture.ProcessSamples(floatSamples);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] first));
            Assert.IsTrue(_inputQueue.TryDequeue(out short[] second));
            Assert.AreNotSame(first, second,
                "Each enqueue should receive a distinct buffer instance.");
        }

        [Test]
        public void ProcessSamples_ShortBufferReusedAfterConsumerReturn()
        {
            float[] floatSamples = new float[100];
            _capture.ProcessSamples(floatSamples);
            _capture.ProcessSamples(floatSamples);

            // Simulate consumer dequeuing and returning buffers to pool
            Assert.IsTrue(_inputQueue.TryDequeue(out short[] first));
            Assert.IsTrue(_inputQueue.TryDequeue(out short[] second));
            _bufferPool.ReturnShort(first);
            _bufferPool.ReturnShort(second);

            // Third rent should reuse one of the returned buffers
            var buf = _bufferPool.RentShort(100);
            Assert.That(buf, Is.SameAs(first).Or.SameAs(second));
            _bufferPool.ReturnShort(buf);
        }

        [Test]
        public void ProcessSamples_WithCount_ConvertsOnlyFirstN()
        {
            float[] floatSamples = new float[100];
            for (int i = 0; i < 100; i++)
                floatSamples[i] = 0.5f;

            _capture.ProcessSamples(floatSamples, 50);

            Assert.IsTrue(_inputQueue.TryDequeue(out short[] pcm));
            Assert.AreEqual(50, pcm.Length);
            Assert.AreEqual((short)(0.5f * 32767f), pcm[0]);
        }

        [Test]
        public void ProcessSamples_WithCount_ExceedsArrayLength_Throws()
        {
            float[] floatSamples = new float[10];
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                _capture.ProcessSamples(floatSamples, 20));
        }

        [Test]
        public void MultipleCaptures_AllEnqueueSuccessfully()
        {
            for (int i = 0; i < 10; i++)
            {
                float[] buf = new float[1000];
                _capture.ProcessSamples(buf);
            }

            Assert.AreEqual(10, _inputQueue.Count, "All 10 audio chunks should be in the queue.");
        }
    }
}
