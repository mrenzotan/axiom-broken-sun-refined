using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vosk;

namespace Axiom.Voice
{
    public class PlatformerVoiceBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("MicrophoneInputHandler component in this scene.")]
        private MicrophoneInputHandler _microphoneInputHandler;

        [SerializeField]
        [Tooltip("PlatformerVoiceSpellController component in this scene.")]
        private PlatformerVoiceSpellController _spellController;

        [SerializeField]
        [Tooltip("Spells available for platformer voice casting when no runtime SpellUnlockService is available.")]
        private SpellData[] _unlockedSpells;

        [SerializeField]
        [Tooltip("Sample rate in Hz passed to the Vosk recognizer. Must match MicrophoneInputHandler._sampleRate.")]
        private int _sampleRate = 16000;

        private static readonly string ModelRelativePath =
            Path.Combine("VoskModels", "vosk-model-en-us-0.22-lgraph");

        private VoskRecognizerService _recognizerService;
        private Model _voskModel;
        private SpellUnlockService _spellUnlockService;
        private List<SpellData> _activeSpells;
        private MicrophoneBufferPool _bufferPool;
        private BootstrapResourceGuard _resourceGuard = new BootstrapResourceGuard();
        private bool _isPipelineReady;

        private IEnumerator Start()
        {
            _resourceGuard = new BootstrapResourceGuard();

            if (!IsPlatformerScene(SceneManager.GetActiveScene().name))
            {
                SetVoiceComponentsEnabled(false);
                enabled = false;
                yield break;
            }

            if (_microphoneInputHandler == null || _spellController == null)
            {
                Debug.LogError("[PlatformerVoiceBootstrap] Required voice references are not assigned.", this);
                enabled = false;
                yield break;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[PlatformerVoiceBootstrap] No microphone device detected; platformer voice casting disabled.", this);
                yield break;
            }

            string modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);
            if (!Directory.Exists(modelPath))
            {
                Debug.LogError(
                    $"[PlatformerVoiceBootstrap] Vosk model not found at: {modelPath}\n" +
                    "Place vosk-model-en-us-0.22-lgraph inside StreamingAssets/VoskModels/.", this);
                yield break;
            }

            Task<Model> modelTask = Task.Run(() => new Model(modelPath));
            yield return new WaitUntil(() => modelTask.IsCompleted);

            if (modelTask.IsFaulted)
            {
                Debug.LogError(
                    $"[PlatformerVoiceBootstrap] Failed to load Vosk model: " +
                    $"{modelTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            _voskModel = _resourceGuard.TakeOrDispose(modelTask.Result);
            if (_voskModel == null)
                yield break;

            _spellUnlockService = GameManager.Instance != null
                ? GameManager.Instance.SpellUnlockService
                : null;

            bool serviceHasSpells = _spellUnlockService != null
                && _spellUnlockService.UnlockedSpells.Count > 0;

            _activeSpells = serviceHasSpells
                ? new List<SpellData>(_spellUnlockService.UnlockedSpells)
                : new List<SpellData>(_unlockedSpells ?? Array.Empty<SpellData>());

            yield return BuildAndStartRecognizer(_activeSpells.ToArray());

            if (!_isPipelineReady)
            {
                yield break;
            }

            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked += HandleSpellUnlocked;
        }

        private IEnumerator BuildAndStartRecognizer(SpellData[] spells)
        {
            Task<VoskRecognizer> recognizerTask =
                SpellVocabularyManager.RebuildRecognizerAsync(_voskModel, _sampleRate, spells);
            yield return new WaitUntil(() => recognizerTask.IsCompleted);

            if (recognizerTask.IsFaulted)
            {
                Debug.LogError(
                    $"[PlatformerVoiceBootstrap] Failed to build Vosk recognizer: " +
                    $"{recognizerTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            VoskRecognizer recognizer = _resourceGuard.TakeOrDispose(recognizerTask.Result);
            if (recognizer == null)
            {
                if (!_resourceGuard.TeardownRequested)
                    Debug.LogWarning("[PlatformerVoiceBootstrap] No platformer voice spells available.", this);
                yield break;
            }

            var inputQueue = new ConcurrentQueue<short[]>();
            var resultQueue = new ConcurrentQueue<string>();
            _bufferPool = new MicrophoneBufferPool();

            _recognizerService = new VoskRecognizerService(recognizer, inputQueue, resultQueue, _bufferPool);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService, _bufferPool);
            _spellController.Inject(resultQueue, spells);
            SetVoiceComponentsEnabled(true);
            _isPipelineReady = true;

            Debug.Log("[PlatformerVoiceBootstrap] Platformer voice casting ready.");
        }

        private void HandleSpellUnlocked(SpellData newSpell)
        {
            if (newSpell == null) return;
            if (_activeSpells == null) return;

            _activeSpells.Add(newSpell);
            Debug.Log("[PlatformerVoiceBootstrap] Platformer voice grammar will include new spells on next scene load.");
        }

        private void StopVoicePipeline()
        {
            _resourceGuard.RequestTeardown();

            _isPipelineReady = false;
            SetVoiceComponentsEnabled(false);

            if (_spellUnlockService != null)
            {
                _spellUnlockService.OnSpellUnlocked -= HandleSpellUnlocked;
                _spellUnlockService = null;
            }

            _recognizerService?.Dispose();
            _recognizerService = null;
            _voskModel?.Dispose();
            _voskModel = null;
            _bufferPool = null;
            _activeSpells = null;
        }

        private void SetVoiceComponentsEnabled(bool enabledForPlatformer)
        {
            if (_spellController != null)
                _spellController.enabled = enabledForPlatformer;
            if (_microphoneInputHandler != null)
                _microphoneInputHandler.enabled = enabledForPlatformer;
        }

        private static bool IsPlatformerScene(string sceneName) =>
            string.Equals(sceneName, "Platformer", StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(sceneName)
                && sceneName.StartsWith("Level_", StringComparison.Ordinal));

        private void OnDestroy()
        {
            StopVoicePipeline();
        }
    }
}
