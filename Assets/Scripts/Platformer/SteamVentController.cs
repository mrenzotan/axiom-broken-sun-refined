using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    /// <summary>
    /// A re-ignitable steam vent: speaking an ignite spell in range erupts it and
    /// detonates its linked/in-radius obstacles. Unlike the one-shot crate/barrier,
    /// the vent is a permanent scene fixture with NO persisted/solved state and NO
    /// puzzleId — it can be re-cast any number of times. Each cast erupts and spends
    /// MP; clearing obstacles is a no-op once they're already gone (each obstacle is
    /// one-shot and persists across a Battle round-trip via its OWN puzzleId).
    /// </summary>
    public class SteamVentController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Looping idle 'puff' frames (e.g. geyser-0,1,2). Plays continuously while idle, and resumes after each eruption settles.")]
        [SerializeField] private Sprite[] _ventFrames;
        [SerializeField, Min(0.1f)] private float _ventFps = 6f;

        [Tooltip("One-shot eruption frames (e.g. geyser-3,4,5) played once when the vent ignites, after which the idle loop resumes. Leave empty to skip the sprite eruption (blast VFX/SFX still fire).")]
        [SerializeField] private Sprite[] _eruptionFrames;
        [SerializeField, Min(0.1f)] private float _eruptionFps = 10f;

        [SerializeField] private List<SpellData> _igniteSpells = new();

        [Header("Explosion targets")]
        [SerializeField]
        [Tooltip("Obstacles cleared when this vent is ignited. Assign BurnableObstacleController / ExplodableBarrierController instances (anything implementing IExplosionDestructible).")]
        private List<MonoBehaviour> _linkedTargets = new();

        [SerializeField, Min(0f)]
        [Tooltip("Optional. If > 0, also clears any IExplosionDestructible within this radius at ignite time.")]
        private float _blastRadius = 0f;

        [SerializeField]
        [Tooltip("Layers searched by the optional blast radius overlap.")]
        private LayerMask _blastMask = ~0;

        [Header("Blast cue")]
        [SerializeField] private ParticleSystem _blastVfx;
        [SerializeField] private AudioClip _blastSfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the vent ignites. Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onIgnited;

        private bool _isPlayerInRange;
        private Coroutine _ventLoopCoroutine;

        private void Start()
        {
            StartVentLoop();

            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool CanIgniteWith(string spellId)
        {
            if (!_isPlayerInRange) return false;

            return BurnableObstacle.CanIgnite(spellId, BuildIgniteSpellIds());
        }

        public bool TryIgnite(string spellId)
        {
            if (!CanIgniteWith(spellId)) return false;

            PlayBlastCue();
            PlayEruption();
            DetonateTargets();
            return true;
        }

        private void PlayBlastCue()
        {
            if (_blastVfx != null) _blastVfx.Play();
            if (_audioSource != null && _blastSfx != null) _audioSource.PlayOneShot(_blastSfx);
            _onIgnited?.Invoke();
        }

        private void DetonateTargets()
        {
            var seen = new HashSet<IExplosionDestructible>();

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                if (_linkedTargets[i] is IExplosionDestructible target && seen.Add(target))
                    target.Detonate();
            }

            if (_blastRadius > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _blastRadius, _blastMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    var target = hits[i].GetComponentInParent<IExplosionDestructible>();
                    if (target != null && seen.Add(target))
                        target.Detonate();
                }
            }
        }

        private List<string> BuildIgniteSpellIds()
        {
            var ids = new List<string>(_igniteSpells.Count);
            for (int i = 0; i < _igniteSpells.Count; i++)
            {
                SpellData spell = _igniteSpells[i];
                if (spell != null) ids.Add(spell.spellName);
            }

            return ids;
        }

        private void StartVentLoop()
        {
            StopVentLoop();
            _ventLoopCoroutine = StartCoroutine(VentLoopCoroutine());
        }

        // Plays the one-shot eruption frames, then settles back into the idle puff loop.
        private void PlayEruption()
        {
            StopVentLoop();
            _ventLoopCoroutine = StartCoroutine(EruptThenResumeLoopCoroutine());
        }

        private void StopVentLoop()
        {
            if (_ventLoopCoroutine != null)
            {
                StopCoroutine(_ventLoopCoroutine);
                _ventLoopCoroutine = null;
            }
        }

        private IEnumerator EruptThenResumeLoopCoroutine()
        {
            yield return PlayEruptionFramesOnce();
            // Eruption settles: resume the idle puff loop (runs until the object is destroyed).
            yield return VentLoopCoroutine();
        }

        private IEnumerator PlayEruptionFramesOnce()
        {
            if (_spriteRenderer == null || _eruptionFrames == null || _eruptionFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _eruptionFps);
            for (int i = 0; i < _eruptionFrames.Length; i++)
            {
                _spriteRenderer.sprite = _eruptionFrames[i];
                yield return frameWait;
            }
        }

        private IEnumerator VentLoopCoroutine()
        {
            if (_spriteRenderer == null || _ventFrames == null || _ventFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _ventFps);
            int frame = 0;
            while (true)
            {
                _spriteRenderer.sprite = _ventFrames[frame];
                frame = (frame + 1) % _ventFrames.Length;
                yield return frameWait;
            }
        }
    }
}
