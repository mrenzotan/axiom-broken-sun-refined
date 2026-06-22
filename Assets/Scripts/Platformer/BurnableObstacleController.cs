using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    public class BurnableObstacleController : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite[] _burnFrames;
        [SerializeField, Min(0.1f)] private float _burnFps = 10f;
        [SerializeField] private List<SpellData> _igniteSpells = new();

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the solved (burned) state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the crate ignites (direct cast or a vent blast). Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onIgnited;

        private static readonly Color FlashTint = new(1f, 0xA5 / 255f, 0x3D / 255f, 1f); // warm orange
        private const float FlashDuration = 0.15f;

        private bool _isBurned;
        private bool _isPlayerInRange;

        public bool IsBurned => _isBurned;

        private void Start()
        {
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
            if (_isBurned) return false;
            if (!_isPlayerInRange) return false;

            return BurnableObstacle.CanIgnite(spellId, BuildIgniteSpellIds());
        }

        public bool TryIgnite(string spellId)
        {
            if (!CanIgniteWith(spellId)) return false;
            Ignite();
            return true;
        }

        // IExplosionDestructible — a vent's blast ignites this crate regardless of
        // player range or spell. Idempotent: a spent crate ignores repeat detonations.
        public void Detonate()
        {
            if (_isBurned) return;
            Ignite();
        }

        private void Ignite()
        {
            _isBurned = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            PlaySuccessCue();
            StartCoroutine(BurnCoroutine());
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
            _onIgnited?.Invoke();
        }

        /// <summary>
        /// Forces the terminal burned state with no animation and no success cue.
        /// Called on scene load by PlatformerWorldRestoreController when this puzzle
        /// was already solved earlier in the session. Leaves the final charred frame
        /// visible as a walkable scorch mark.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            _isBurned = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_spriteRenderer != null && _burnFrames != null && _burnFrames.Length > 0)
                _spriteRenderer.sprite = _burnFrames[_burnFrames.Length - 1];
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

        private IEnumerator BurnCoroutine()
        {
            yield return FlashCoroutine();
            yield return PlayBurnFrames();
            // Charred final frame remains visible (walkable scorch). Renderer stays enabled.
        }

        private IEnumerator FlashCoroutine()
        {
            if (_spriteRenderer == null) yield break;

            float halfFlash = FlashDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _spriteRenderer.color = Color.Lerp(Color.white, FlashTint, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _spriteRenderer.color = Color.Lerp(FlashTint, Color.white, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            _spriteRenderer.color = Color.white;
        }

        private IEnumerator PlayBurnFrames()
        {
            if (_spriteRenderer == null || _burnFrames == null || _burnFrames.Length == 0)
            {
                if (_solidCollider != null) _solidCollider.enabled = false;
                yield break;
            }

            int colliderDisableFrame = _burnFrames.Length / 2;
            var frameWait = new WaitForSeconds(1f / _burnFps);
            for (int i = 0; i < _burnFrames.Length; i++)
            {
                _spriteRenderer.sprite = _burnFrames[i];

                if (i == colliderDisableFrame && _solidCollider != null)
                    _solidCollider.enabled = false;

                yield return frameWait;
            }

            if (_solidCollider != null) _solidCollider.enabled = false;
        }
    }
}
