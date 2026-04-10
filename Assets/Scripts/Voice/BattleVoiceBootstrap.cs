using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Axiom.Battle;
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

        [SerializeField]
        [Tooltip("ActionMenuUI in the Battle scene. Assign to disable the Spell button when voice is unavailable.")]
        private ActionMenuUI _actionMenuUI;

        private static readonly string ModelRelativePath =
            Path.Combine("VoskModels", "vosk-model-en-us-0.22-lgraph");

        private VoskRecognizerService _recognizerService;
        private Model _voskModel;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[BattleVoiceBootstrap] No microphone device detected — Spell button disabled.", this);
                DisableSpell();
                yield break;
            }

            string modelPath = Path.Combine(Application.streamingAssetsPath, ModelRelativePath);

            if (!Directory.Exists(modelPath))
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Vosk model not found at: {modelPath}\n" +
                    "Place vosk-model-en-us-0.22-lgraph inside StreamingAssets/VoskModels/.", this);
                DisableSpell();
                yield break;
            }

            Task<Model> modelTask = Task.Run(() => new Model(modelPath));
            yield return new WaitUntil(() => modelTask.IsCompleted);

            if (modelTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to load Vosk model: " +
                    $"{modelTask.Exception?.InnerException?.Message}", this);
                DisableSpell();
                yield break;
            }

            _voskModel = modelTask.Result;
            SpellData[] spells = _unlockedSpells ?? Array.Empty<SpellData>();

            Task<VoskRecognizer> recognizerTask =
                SpellVocabularyManager.RebuildRecognizerAsync(_voskModel, _sampleRate, spells);
            yield return new WaitUntil(() => recognizerTask.IsCompleted);

            if (recognizerTask.IsFaulted)
            {
                _voskModel.Dispose();
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to build Vosk recognizer: " +
                    $"{recognizerTask.Exception?.InnerException?.Message}", this);
                DisableSpell();
                yield break;
            }

            if (recognizerTask.Result == null)
            {
                Debug.LogWarning(
                    "[BattleVoiceBootstrap] Spell list is empty — voice recognition not started.\n" +
                    "Assign at least one SpellData asset to the Unlocked Spells field.", this);
                DisableSpell();
                yield break;
            }

            var inputQueue  = new ConcurrentQueue<short[]>();
            var resultQueue = new ConcurrentQueue<string>();

            _recognizerService = new VoskRecognizerService(recognizerTask.Result, inputQueue, resultQueue);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService);
            _spellCastController.Inject(resultQueue, spells);

            Debug.Log("[BattleVoiceBootstrap] Vosk pipeline ready.");
        }

        private void DisableSpell()
        {
            if (_actionMenuUI == null)
            {
                Debug.LogError("[BattleVoiceBootstrap] ActionMenuUI is not assigned — Spell button cannot be disabled. Assign it in the Inspector.", this);
                return;
            }
            _actionMenuUI.SetSpellInteractable(false);
        }

        private void OnDestroy()
        {
            _recognizerService?.Dispose();
            _recognizerService = null;
            _voskModel?.Dispose();
            _voskModel = null;
        }
    }
}
