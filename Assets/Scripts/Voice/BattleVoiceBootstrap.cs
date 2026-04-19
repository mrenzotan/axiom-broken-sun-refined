using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Axiom.Battle;
using Axiom.Core;
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
        private SpellUnlockService _spellUnlockService;
        private List<SpellData> _activeSpells;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (!ValidateRequiredReferences(out string missingRef))
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Required reference '{missingRef}' is not assigned " +
                    "in the Inspector. Voice pipeline will not start; Spell action disabled.", this);
                DisableSpell();
                enabled = false;
                yield break;
            }

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

            // Prefer the runtime-owned SpellUnlockService so the recognizer stays in sync
            // with story unlocks + level-up grants. Fall back to the Inspector array
            // when running the Battle scene in isolation (no GameManager, or GameManager
            // present but SpellUnlockService has no spells yet — e.g. no save loaded).
            _spellUnlockService = GameManager.Instance != null
                ? GameManager.Instance.SpellUnlockService
                : null;

            bool serviceHasSpells = _spellUnlockService != null
                && _spellUnlockService.UnlockedSpells.Count > 0;

            _activeSpells = serviceHasSpells
                ? new List<SpellData>(_spellUnlockService.UnlockedSpells)
                : new List<SpellData>(_unlockedSpells ?? Array.Empty<SpellData>());

            SpellData[] spells = _activeSpells.ToArray();

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

            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked += HandleSpellUnlocked;
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

        private void HandleSpellUnlocked(SpellData newSpell)
        {
            if (newSpell == null) return;
            if (_voskModel == null) return;            // pipeline never initialized — ignore
            if (_recognizerService == null) return;    // no active recognizer to swap

            _activeSpells.Add(newSpell);

            StartCoroutine(RebuildRecognizer(_activeSpells.ToArray()));
        }

        private IEnumerator RebuildRecognizer(SpellData[] spells)
        {
            Task<VoskRecognizer> rebuildTask =
                SpellVocabularyManager.RebuildRecognizerAsync(_voskModel, _sampleRate, spells);

            yield return new WaitUntil(() => rebuildTask.IsCompleted);

            if (rebuildTask.IsFaulted)
            {
                Debug.LogError(
                    $"[BattleVoiceBootstrap] Failed to rebuild Vosk recognizer on spell unlock: " +
                    $"{rebuildTask.Exception?.InnerException?.Message}", this);
                yield break;
            }

            VoskRecognizer newRecognizer = rebuildTask.Result;
            if (newRecognizer == null) yield break;  // empty set — should not happen post-init

            // Atomic-enough swap: stop old service (drains queues), hand the new recognizer
            // to a fresh VoskRecognizerService reusing the existing shared queues.
            ConcurrentQueue<short[]> inputQueue = _recognizerService.InputQueue;
            ConcurrentQueue<string>  resultQueue = _recognizerService.ResultQueue;

            _recognizerService.Dispose();

            _recognizerService = new VoskRecognizerService(newRecognizer, inputQueue, resultQueue);
            _recognizerService.Start();

            _microphoneInputHandler.Inject(inputQueue, _recognizerService);
            _spellCastController.Inject(resultQueue, spells);

            Debug.Log($"[BattleVoiceBootstrap] Vosk recognizer rebuilt with {spells.Length} spells after unlock.");
        }

        /// <summary>
        /// Validates that all required Inspector references are assigned.
        /// Returns <c>true</c> when all required refs are present. On failure,
        /// returns <c>false</c> and sets <paramref name="missingRefName"/> to
        /// the name of the first missing field (in declaration order).
        ///
        /// <para>
        /// Called at the top of <see cref="Start"/> before any async Vosk work
        /// so that missing refs fail fast with a clear diagnostic instead of
        /// throwing <see cref="System.NullReferenceException"/> deep in the
        /// coroutine after the 50MB Vosk model has been loaded.
        /// </para>
        ///
        /// <para>
        /// Public to allow Edit Mode tests to exercise the guard paths without
        /// running the coroutine. <c>_actionMenuUI</c> is intentionally excluded
        /// from this check: <see cref="DisableSpell"/> already handles its own
        /// null case with a logged error and is still useful to call even when
        /// the UI reference is missing (nothing crashes).
        /// </para>
        /// </summary>
        public bool ValidateRequiredReferences(out string missingRefName)
        {
            if (_microphoneInputHandler == null)
            {
                missingRefName = nameof(_microphoneInputHandler);
                return false;
            }

            if (_spellCastController == null)
            {
                missingRefName = nameof(_spellCastController);
                return false;
            }

            missingRefName = null;
            return true;
        }

        private void OnDestroy()
        {
            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked -= HandleSpellUnlocked;

            _recognizerService?.Dispose();
            _recognizerService = null;
            _voskModel?.Dispose();
            _voskModel = null;
        }
    }
}
