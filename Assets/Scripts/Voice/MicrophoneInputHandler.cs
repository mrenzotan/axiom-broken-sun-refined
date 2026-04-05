using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Axiom.Voice
{
    /// <summary>
    /// MonoBehaviour responsible solely for Unity microphone lifecycle and
    /// push-to-talk input wiring. Calls <see cref="MicrophoneCapture.ProcessSamples"/>
    /// each frame while recording; calls <see cref="VoskRecognizerService.RequestFinalResult"/>
    /// on PTT release. Contains no recognition logic.
    ///
    /// Call <see cref="Inject"/> with the shared queue and service before this
    /// component is enabled. Typical caller: a scene-level bootstrap MonoBehaviour
    /// or <c>BattleController</c> when entering the voice-spell phase.
    /// </summary>
    public class MicrophoneInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionReference _pushToTalkAction;

        /// <summary>
        /// Sample rate in Hz passed to <c>Microphone.Start</c>.
        /// Must match the rate used when constructing the <see cref="Vosk.VoskRecognizer"/>.
        /// </summary>
        [SerializeField] private int _sampleRate = 16000;

        private MicrophoneCapture     _capture;
        private VoskRecognizerService _recognizerService;

        private AudioClip _clip;
        private int       _lastSamplePos;
        private bool      _isCapturing;

        // null → Unity picks the default microphone device.
        // Expose via a public setter if a device-selection UI is added later.
        private string _deviceName;

        // ── Injection ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the shared audio-input queue and the recognizer service.
        /// Must be called before the GameObject is enabled.
        /// </summary>
        public void Inject(
            ConcurrentQueue<short[]> inputQueue,
            VoskRecognizerService    recognizerService)
        {
            _capture           = new MicrophoneCapture(inputQueue);
            _recognizerService = recognizerService;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            _pushToTalkAction.action.started  += OnPushToTalkStarted;
            _pushToTalkAction.action.canceled += OnPushToTalkCanceled;
            _pushToTalkAction.action.Enable();
        }

        private void OnDisable()
        {
            _pushToTalkAction.action.started  -= OnPushToTalkStarted;
            _pushToTalkAction.action.canceled -= OnPushToTalkCanceled;
            _pushToTalkAction.action.Disable();
            StopCapture();
        }

        private void Update()
        {
            if (!_isCapturing || _clip == null || _capture == null) return;

            int currentPos = Microphone.GetPosition(_deviceName);
            if (currentPos == _lastSamplePos) return;

            int newSamples = currentPos - _lastSamplePos;
            if (newSamples < 0) newSamples += _clip.samples; // ring-buffer wrap

            float[] buffer = new float[newSamples];
            _clip.GetData(buffer, _lastSamplePos % _clip.samples);
            _capture.ProcessSamples(buffer);
            _lastSamplePos = currentPos;
        }

        // ── PTT callbacks ─────────────────────────────────────────────────────────

        private void OnPushToTalkStarted(InputAction.CallbackContext _)
        {
            if (_capture == null)
            {
                Debug.LogError("[MicrophoneInputHandler] Inject() was not called before PTT press.", this);
                return;
            }
            if (_isCapturing) return;

            _clip = Microphone.Start(_deviceName, loop: true, lengthSec: 1, frequency: _sampleRate);
            if (_clip == null)
            {
                Debug.LogWarning("[MicrophoneInputHandler] No microphone device found — PTT ignored.", this);
                return;
            }

            _lastSamplePos = 0;
            _isCapturing   = true;
        }

        private void OnPushToTalkCanceled(InputAction.CallbackContext _) => StopCapture();

        // ── Internal ──────────────────────────────────────────────────────────────

        private void StopCapture()
        {
            if (!_isCapturing) return;
            _isCapturing = false;
            Microphone.End(_deviceName);
            _clip = null;
            _recognizerService?.RequestFinalResult();
        }
    }
}
