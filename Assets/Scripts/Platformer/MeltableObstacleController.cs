using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    public class MeltableObstacleController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite[] _meltFrames;
        [SerializeField, Min(0.1f)] private float _meltFps = 10f;
        [SerializeField] private List<SpellData> _meltSpells = new();

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the solved (melted) state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField]
        [Tooltip("Optional particle burst played once when this obstacle is successfully melted.")]
        private ParticleSystem _successVfx;

        [SerializeField]
        [Tooltip("Optional one-shot played when this obstacle is successfully melted. Routed through the SFX mixer bus.")]
        private AudioClip _successSfx;

        [SerializeField]
        [Tooltip("AudioSource on this prefab used to play the success SFX. Auto-routed through the SFX bus on Start.")]
        private AudioSource _audioSource;

        private static readonly Color FlashTint = new(0xBF / 255f, 0xE9 / 255f, 1f, 1f);
        private const float FlashDuration = 0.15f;

        private bool _isMelted;
        private bool _isPlayerInRange;

        public bool IsMelted => _isMelted;

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

        public bool CanMeltWith(string spellId)
        {
            if (_isMelted) return false;
            if (!_isPlayerInRange) return false;

            return MeltableObstacle.CanMelt(spellId, BuildMeltSpellIds());
        }

        public bool TryMelt(string spellId)
        {
            if (!CanMeltWith(spellId)) return false;

            _isMelted = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            PlaySuccessCue();
            StartCoroutine(MeltCoroutine());
            return true;
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }

        /// <summary>
        /// Forces the terminal melted state with no animation and no success cue.
        /// Called on scene load by PlatformerWorldRestoreController when this puzzle
        /// was already solved earlier in the session.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            if (_isMelted
                && (_solidCollider == null || !_solidCollider.enabled)
                && (_spriteRenderer == null || !_spriteRenderer.enabled))
                return; // already in terminal state

            _isMelted = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private List<string> BuildMeltSpellIds()
        {
            var meltSpellIds = new List<string>(_meltSpells.Count);
            for (int i = 0; i < _meltSpells.Count; i++)
            {
                SpellData spell = _meltSpells[i];
                if (spell != null) meltSpellIds.Add(spell.spellName);
            }

            return meltSpellIds;
        }

        private IEnumerator MeltCoroutine()
        {
            yield return FlashCoroutine();

            yield return PlayMeltFrames();

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
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

        private IEnumerator PlayMeltFrames()
        {
            if (_spriteRenderer == null || _meltFrames == null || _meltFrames.Length == 0)
            {
                if (_solidCollider != null) _solidCollider.enabled = false;
                yield break;
            }

            int colliderDisableFrame = _meltFrames.Length / 2;
            var frameWait = new WaitForSeconds(1f / _meltFps);
            for (int i = 0; i < _meltFrames.Length; i++)
            {
                _spriteRenderer.sprite = _meltFrames[i];

                if (i == colliderDisableFrame && _solidCollider != null)
                    _solidCollider.enabled = false;

                yield return frameWait;
            }

            if (_solidCollider != null) _solidCollider.enabled = false;
        }
    }
}
