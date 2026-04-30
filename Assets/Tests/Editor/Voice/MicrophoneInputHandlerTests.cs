using System.Collections.Concurrent;
using System.Reflection;
using Axiom.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Axiom.Voice.Tests
{
    public class MicrophoneInputHandlerTests
    {
        private static readonly MethodInfo _onEnableMethod =
            typeof(MicrophoneInputHandler).GetMethod("OnEnable",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _onDisableMethod =
            typeof(MicrophoneInputHandler).GetMethod("OnDisable",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _onPushToTalkStartedMethod =
            typeof(MicrophoneInputHandler).GetMethod("OnPushToTalkStarted",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private const string NullActionErrorMsg =
            "[MicrophoneInputHandler] Push-to-talk InputActionReference is not assigned " +
            "or its action is null. Disabling component to prevent NullReferenceException.";
        private const string InjectNotCalledErrorMsg =
            "[MicrophoneInputHandler] Inject() was not called before PTT press.";

        private GameObject _gameObject;
        private MicrophoneInputHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestMicHandler");
            _handler = _gameObject.AddComponent<MicrophoneInputHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        // ── OnEnable / OnDisable with null action reference ─────────────────────────

        [Test]
        public void OnEnable_NullPushToTalkAction_DoesNotThrow()
        {
            // Edit Mode tests don't invoke Unity lifecycle methods automatically,
            // so call OnEnable directly via reflection.
            LogAssert.Expect(LogType.Error, NullActionErrorMsg);
            Assert.DoesNotThrow(() =>
            {
                _onEnableMethod.Invoke(_handler, null);
            });
        }

        [Test]
        public void OnDisable_NullPushToTalkAction_DoesNotThrow()
        {
            // Call OnEnable first (which self-disables), then OnDisable — both with null action.
            LogAssert.Expect(LogType.Error, NullActionErrorMsg);
            _onEnableMethod.Invoke(_handler, null);
            Assert.DoesNotThrow(() =>
            {
                _onDisableMethod.Invoke(_handler, null);
            });
        }

        [Test]
        public void OnEnable_NullActionReference_DisablesComponent()
        {
            // OnEnable should log an error and set enabled = false.
            LogAssert.Expect(LogType.Error, NullActionErrorMsg);
            _onEnableMethod.Invoke(_handler, null);

            Assert.IsFalse(_handler.enabled,
                "MicrophoneInputHandler should disable itself when pushToTalkAction is null.");
        }

        // ── PTT callback with null recognizer service ─────────────────────────────────

        [Test]
        public void Inject_NullRecognizerService_DoesNotThrow()
        {
            var inputQueue = new ConcurrentQueue<short[]>();
            var bufferPool = new MicrophoneBufferPool();

            Assert.DoesNotThrow(() =>
            {
                _handler.Inject(inputQueue, null, bufferPool);
            });
        }

        // ── PTT press before Inject ──────────────────────────────────────────────────

        [Test]
        public void OnPushToTalkStarted_BeforeInject_DoesNotThrow()
        {
            // OnPushToTalkStarted guards against _capture == null when Inject()
            // was never called. Invoke via reflection since it's a private method.
            LogAssert.Expect(LogType.Error, InjectNotCalledErrorMsg);
            Assert.DoesNotThrow(() =>
            {
                _onPushToTalkStartedMethod.Invoke(
                    _handler, new object[] { default(InputAction.CallbackContext) });
            });
        }
    }
}