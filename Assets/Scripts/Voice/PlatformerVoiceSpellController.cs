using System;
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

        private ConcurrentQueue<string> _resultQueue;
        private IReadOnlyList<SpellData> _unlockedSpells;
        private PlayerState _playerState;
        private readonly List<MeltableObstacleController> _sceneMeltableObstacles = new();
        private readonly List<FreezablePlatformController> _sceneFreezablePlatforms = new();

        public void Inject(ConcurrentQueue<string> resultQueue, IReadOnlyList<SpellData> unlockedSpells, PlayerState playerState = null)
        {
            _resultQueue = resultQueue;
            _unlockedSpells = unlockedSpells;
            _playerState = playerState;
        }

        private void Start()
        {
            _resultQueue ??= new ConcurrentQueue<string>();
            _unlockedSpells ??= Array.Empty<SpellData>();
        }

        private void Update()
        {
            while (_resultQueue.TryDequeue(out string voskJson))
            {
                SpellData matched = SpellResultMatcher.Match(voskJson, _unlockedSpells);
                if (matched == null)
                    continue;

                PlatformerSpellWorldCaster.TryCast(
                    matched,
                    ResolveMeltableObstacles(),
                    ResolveFreezablePlatforms(),
                    _playerState ?? GameManager.Instance?.PlayerState);
            }
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
    }
}
