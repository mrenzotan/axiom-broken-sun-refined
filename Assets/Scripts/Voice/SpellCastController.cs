using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Axiom.Battle;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Voice
{
    /// <summary>
    /// MonoBehaviour that polls the Vosk result queue each frame, matches recognized text
    /// against the player's unlocked spell list, and dispatches confirmed spells to
    /// <see cref="BattleController"/>. Contains no parsing or matching logic — delegates
    /// entirely to <see cref="SpellResultMatcher"/>.
    ///
    /// Assign <see cref="_battleController"/> in the Inspector.
    /// Call <see cref="Inject"/> with the shared result queue and unlocked spell list
    /// before or during the voice-spell phase. If Inject is never called, Start() creates
    /// a stub empty queue so the Battle scene runs without a Vosk service attached.
    /// Typical Inject caller: a scene-level Vosk bootstrap MonoBehaviour (Phase 3).
    /// </summary>
    public class SpellCastController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Assign the BattleController component from this scene.")]
        private BattleController _battleController;

        private ConcurrentQueue<string>  _resultQueue;
        private IReadOnlyList<SpellData> _unlockedSpells;
        private bool                     _battleControllerWarningLogged;

        // ── Injection ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the shared Vosk result queue and the player's current unlocked spell list.
        /// Call this before or after Start() — the queue is replaced if already stubbed.
        /// </summary>
        public void Inject(
            ConcurrentQueue<string>  resultQueue,
            IReadOnlyList<SpellData> unlockedSpells)
        {
            _resultQueue    = resultQueue;
            _unlockedSpells = unlockedSpells;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            // Stub queue and empty spell list keep Update() safe when Inject() has not been
            // called yet — allows the Battle scene to run in isolation without a Vosk service.
            _resultQueue    = _resultQueue    ?? new ConcurrentQueue<string>();
            _unlockedSpells = _unlockedSpells ?? Array.Empty<SpellData>();
        }

        private void Update()
        {
            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null) continue;

                if (_battleController == null)
                {
                    if (!_battleControllerWarningLogged)
                    {
                        Debug.LogError("[SpellCastController] BattleController is not assigned in the Inspector.", this);
                        _battleControllerWarningLogged = true;
                    }
                    continue;
                }

                _battleController.OnSpellCast(matched);
            }
        }
    }
}
