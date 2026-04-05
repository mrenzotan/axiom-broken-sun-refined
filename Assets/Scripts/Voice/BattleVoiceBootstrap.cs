using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Axiom.Data;
using UnityEngine;
using Vosk;

namespace Axiom.Voice
{
    /// <summary>
    /// Scene-level bootstrap that loads the Vosk model on a background thread,
    /// constructs <see cref="VoskRecognizerService"/>, and injects the shared queues
    /// into <see cref="MicrophoneInputHandler"/> and <see cref="SpellCastController"/>.
    ///
    /// Place this component in the Battle scene on the same GameObject as
    /// <see cref="MicrophoneInputHandler"/> and <see cref="SpellCastController"/>.
    /// Assign all three SerializeFields in the Inspector before entering Play Mode.
    ///
    /// <para>
    /// <b>Sample rate:</b> <see cref="_sampleRate"/> must match the value set on
    /// <see cref="MicrophoneInputHandler"/> (both default to 16000 Hz).
    /// </para>
    /// </summary>
    public class BattleVoiceBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("MicrophoneInputHandler component in this scene.")]
        private MicrophoneInputHandler _microphoneInputHandler;

        [SerializeField]
        [Tooltip("SpellCastController component in this scene.")]
        private SpellCastController _spellCastController;

        [SerializeField]
        [Tooltip("Spells the player has unlocked. For Phase 3 testing assign SpellData assets directly here.")]
        private SpellData[] _unlockedSpells;

        [SerializeField]
        [Tooltip("Sample rate in Hz passed to the Vosk recognizer. Must match MicrophoneInputHandler._sampleRate.")]
        private int _sampleRate = 16000; // Widened to float when passed to RebuildRecognizerAsync.

        private static readonly string ModelRelativePath =
            Path.Combine("VoskModels", "vosk-model-en-us-0.22-lgraph");

        private VoskRecognizerService _recognizerService;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);

            if (!Directory.Exists(modelPath))
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Vosk model not found at: {modelPath}\n" +
                    "Place vosk-model-en-us-0.22-lgraph inside StreamingAssets/VoskModels/.", this);
                yield break;
            }

            // Model construction is blocking and slow (~1-2s). Run on a background thread.
            Task<Model> modelTask = Task.Run(() => new Model(modelPath));
            yield return new WaitUntil(() => modelTask.IsCompleted);

            if (modelTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to load Vosk model: " +
                    $"{modelTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            SpellData[] spells = _unlockedSpells ?? Array.Empty<SpellData>();

            // VoskRecognizer construction applies grammar to the model — also off main thread.
            Task<VoskRecognizer> recognizerTask =
                SpellVocabularyManager.RebuildRecognizerAsync(modelTask.Result, _sampleRate, spells);
            yield return new WaitUntil(() => recognizerTask.IsCompleted);

            if (recognizerTask.IsFaulted)
            {
                modelTask.Result.Dispose();
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to build Vosk recognizer: " +
                    $"{recognizerTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            if (recognizerTask.Result == null)
            {
                Debug.LogWarning(
                    "[BattleVoiceBootstrap] Spell list is empty — voice recognition not started.\n" +
                    "Assign at least one SpellData asset to the Unlocked Spells field.", this);
                yield break;
            }

            // Create the shared queues.
            var inputQueue  = new ConcurrentQueue<short[]>();
            var resultQueue = new ConcurrentQueue<string>();

            // Wire the pipeline.
            _recognizerService = new VoskRecognizerService(recognizerTask.Result, inputQueue, resultQueue);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService);
            _spellCastController.Inject(resultQueue, spells);

            Debug.Log("[BattleVoiceBootstrap] Vosk pipeline ready.");
        }

        private void OnDestroy()
        {
            _recognizerService?.Dispose();
            _recognizerService = null;
        }
    }
}
