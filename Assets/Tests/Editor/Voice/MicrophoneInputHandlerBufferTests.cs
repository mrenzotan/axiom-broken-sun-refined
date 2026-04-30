using System.Collections.Concurrent;
using System.Reflection;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Voice.Tests
{
    public class MicrophoneInputHandlerBufferTests
    {
        private static readonly MethodInfo _onEnableMethod =
            typeof(MicrophoneInputHandler).GetMethod("OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _stopCaptureMethod =
            typeof(MicrophoneInputHandler).GetMethod("StopCapture",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject _gameObject;
        private MicrophoneInputHandler _handler;
        private ConcurrentQueue<short[]> _inputQueue;
        private MicrophoneBufferPool _bufferPool;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestMicHandler");
            _handler = _gameObject.AddComponent<MicrophoneInputHandler>();
            _inputQueue = new ConcurrentQueue<short[]>();
            _bufferPool = new MicrophoneBufferPool();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Inject_AcceptsBufferPool()
        {
            Assert.DoesNotThrow(() =>
                _handler.Inject(_inputQueue, null, _bufferPool));
        }

        [Test]
        public void StopCapture_CalledTwice_DoesNotThrow()
        {
            _handler.Inject(_inputQueue, null, _bufferPool);
            // Cannot call StopCapture directly without a real AudioClip,
            // but Inject + null guard should not throw
            Assert.DoesNotThrow(() => _stopCaptureMethod.Invoke(_handler, null));
            Assert.DoesNotThrow(() => _stopCaptureMethod.Invoke(_handler, null));
        }
    }
}
