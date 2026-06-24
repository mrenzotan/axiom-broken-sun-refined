using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using Axiom.Platformer;
using UnityEngine;

namespace Axiom.Voice
{
    public class PlatformerVoiceSpellController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional explicit meltable obstacles. Leave empty to find all active MeltableObstacleController instances in the scene.")]
        private MeltableObstacleController[] _meltableObstacles;

        [SerializeField]
        [Tooltip("Optional explicit freezable platforms. Leave empty to find all active FreezablePlatformController instances in the scene.")]
        private FreezablePlatformController[] _freezablePlatforms;

        [SerializeField]
        [Tooltip("Optional explicit burnable obstacles. Leave empty to find all active BurnableObstacleController instances in the scene.")]
        private BurnableObstacleController[] _burnableObstacles;

        [SerializeField]
        [Tooltip("Optional explicit steam vents. Leave empty to find all active SteamVentController instances in the scene.")]
        private SteamVentController[] _steamVents;

        [SerializeField]
        [Tooltip("Optional explicit acid puddles. Leave empty to find all active AcidPuddleController instances in the scene.")]
        private AcidPuddleController[] _acidPuddles;

        [SerializeField]
        [Tooltip("Player in this scene. Drives the cast animation and raises the fire-frame event.")]
        private PlayerController _player;

        [SerializeField]
        [Tooltip("Player aura cue. Suppressed while a cast animation plays.")]
        private PlayerAuraCue _auraCue;

        [SerializeField]
        [Tooltip("World-space floating-number spawner. Shows '-N MP' on a cast and 'Not enough MP' when broke. Assign the scene's PlatformerFloatingNumberSpawner.")]
        private PlatformerFloatingNumberSpawner _floatingNumbers;

        [SerializeField, Min(0.1f)]
        [Tooltip("Fallback seconds before resolving a cast if the animation fire-frame event never fires.")]
        private float _spellFireTimeout = 1f;

        private PlatformerCastSequencer _castSequencer;
        private Coroutine _fireTimeout;

        private ConcurrentQueue<string> _resultQueue;
        private IReadOnlyList<SpellData> _unlockedSpells;
        private PlayerState _playerState;
        private readonly List<MeltableObstacleController> _sceneMeltableObstacles = new();
        private readonly List<FreezablePlatformController> _sceneFreezablePlatforms = new();
        private readonly List<BurnableObstacleController> _sceneBurnableObstacles = new();
        private readonly List<SteamVentController> _sceneSteamVents = new();
        private readonly List<AcidPuddleController> _sceneAcidPuddles = new();

        public void Inject(ConcurrentQueue<string> resultQueue, IReadOnlyList<SpellData> unlockedSpells, PlayerState playerState = null)
        {
            _resultQueue = resultQueue;
            _unlockedSpells = unlockedSpells;
            _playerState = playerState;
        }

        private void OnEnable()
        {
            if (_player != null) _player.SpellCastFired += OnPlayerSpellFireFrame;
        }

        private void OnDisable()
        {
            if (_player != null) _player.SpellCastFired -= OnPlayerSpellFireFrame;
            StopFireTimeout();
        }

        private void Start()
        {
            _resultQueue ??= new ConcurrentQueue<string>();
            _unlockedSpells ??= Array.Empty<SpellData>();
        }

        private void Update()
        {
            _castSequencer ??= new PlatformerCastSequencer(BeginCastAction, ResolveAction, EndCastAction);

            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null) continue;

                PlayerState playerState = _playerState ?? GameManager.Instance?.PlayerState;
                if (playerState == null) continue;

                CastEvaluation evaluation = PlatformerSpellWorldCaster.EvaluateCast(
                    matched,
                    playerState.CurrentMp,
                    ResolveMeltableObstacles(),
                    ResolveFreezablePlatforms(),
                    ResolveBurnableObstacles(),
                    ResolveSteamVents(),
                    ResolveAcidPuddles());

                switch (evaluation)
                {
                    case CastEvaluation.Castable:
                        _castSequencer.RequestCast(matched);
                        break;
                    case CastEvaluation.InsufficientMana:
                        if (_floatingNumbers != null && _player != null)
                            _floatingNumbers.SpawnInsufficientMana(_player.transform.position);
                        break;
                    // CastEvaluation.NoTarget: no nearby resolvable puzzle — stay silent.
                }
            }
        }

        private void BeginCastAction(SpellData spell)
        {
            if (_player != null) _player.BeginCast();
            if (_auraCue != null) _auraCue.Suppress(true);
            StartFireTimeout();
        }

        private void ResolveAction(SpellData spell)
        {
            PlayerState playerState = _playerState ?? GameManager.Instance?.PlayerState;
            int mpBefore = playerState?.CurrentMp ?? 0;

            PlatformerSpellWorldCaster.TryCast(
                spell,
                ResolveMeltableObstacles(),
                ResolveFreezablePlatforms(),
                ResolveBurnableObstacles(),
                ResolveSteamVents(),
                ResolveAcidPuddles(),
                playerState);

            if (playerState != null && _floatingNumbers != null && _player != null)
            {
                int spent = mpBefore - playerState.CurrentMp;
                if (spent > 0)
                    _floatingNumbers.SpawnManaSpent(_player.transform.position, spent);
            }
        }

        private void EndCastAction()
        {
            if (_auraCue != null) _auraCue.Suppress(false);
            StopFireTimeout();
        }

        private void OnPlayerSpellFireFrame() => _castSequencer?.NotifyFireFrame();

        private void StartFireTimeout()
        {
            // Edit-mode safe: only arm the coroutine fallback at runtime. EditMode tests drive the
            // fire-frame by invoking OnPlayerSpellFireFrame directly, so they must not hit StartCoroutine.
            if (!Application.isPlaying) return;
            StopFireTimeout();
            _fireTimeout = StartCoroutine(FireTimeoutCoroutine());
        }

        private void StopFireTimeout()
        {
            if (_fireTimeout != null)
            {
                StopCoroutine(_fireTimeout);
                _fireTimeout = null;
            }
        }

        private IEnumerator FireTimeoutCoroutine()
        {
            yield return new WaitForSeconds(_spellFireTimeout);
            _fireTimeout = null;
            _castSequencer.NotifyFireFrame();
        }

        private IReadOnlyList<MeltableObstacleController> ResolveMeltableObstacles()
        {
            if (_meltableObstacles != null && _meltableObstacles.Length > 0)
                return _meltableObstacles;

            _sceneMeltableObstacles.Clear();
            _sceneMeltableObstacles.AddRange(FindObjectsByType<MeltableObstacleController>());
            return _sceneMeltableObstacles;
        }

        private IReadOnlyList<FreezablePlatformController> ResolveFreezablePlatforms()
        {
            if (_freezablePlatforms != null && _freezablePlatforms.Length > 0)
                return _freezablePlatforms;

            _sceneFreezablePlatforms.Clear();
            _sceneFreezablePlatforms.AddRange(FindObjectsByType<FreezablePlatformController>());
            return _sceneFreezablePlatforms;
        }

        private IReadOnlyList<BurnableObstacleController> ResolveBurnableObstacles()
        {
            if (_burnableObstacles != null && _burnableObstacles.Length > 0)
                return _burnableObstacles;

            _sceneBurnableObstacles.Clear();
            _sceneBurnableObstacles.AddRange(FindObjectsByType<BurnableObstacleController>());
            return _sceneBurnableObstacles;
        }

        private IReadOnlyList<SteamVentController> ResolveSteamVents()
        {
            if (_steamVents != null && _steamVents.Length > 0)
                return _steamVents;

            _sceneSteamVents.Clear();
            _sceneSteamVents.AddRange(FindObjectsByType<SteamVentController>());
            return _sceneSteamVents;
        }

        private IReadOnlyList<AcidPuddleController> ResolveAcidPuddles()
        {
            if (_acidPuddles != null && _acidPuddles.Length > 0)
                return _acidPuddles;

            _sceneAcidPuddles.Clear();
            _sceneAcidPuddles.AddRange(FindObjectsByType<AcidPuddleController>());
            return _sceneAcidPuddles;
        }
    }
}
