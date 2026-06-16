using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Axiom.Platformer
{
    public class MeltableObstacleController : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private TilemapCollider2D _solidCollider;
        [SerializeField] private List<SpellData> _meltSpells = new();
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.7f;

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
        private const float SinkScaleY = 0.6f;

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
            if (_isMelted && _solidCollider == null && (_tilemap == null || !_tilemap.gameObject.activeSelf))
                return; // already in terminal state

            _isMelted = true;
            if (_solidCollider != null)
                _solidCollider.enabled = false;
            if (_tilemap != null)
                _tilemap.gameObject.SetActive(false);
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

            if (_solidCollider != null)
                _solidCollider.enabled = false;

            yield return FadeAndSinkCoroutine();

            if (_tilemap != null)
                _tilemap.gameObject.SetActive(false);
        }

        private IEnumerator FlashCoroutine()
        {
            if (_tilemap == null) yield break;

            float halfFlash = FlashDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _tilemap.color = Color.Lerp(Color.white, FlashTint, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _tilemap.color = Color.Lerp(FlashTint, Color.white, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            _tilemap.color = Color.white;
        }

        private IEnumerator FadeAndSinkCoroutine()
        {
            if (_tilemap == null) yield break;

            float fadeWindow = Mathf.Max(0.01f, _fadeDuration - FlashDuration);
            Transform tilemapTransform = _tilemap.transform;
            Vector3 startScale = tilemapTransform.localScale;
            Vector3 endScale = new(startScale.x, startScale.y * SinkScaleY, startScale.z);

            float elapsed = 0f;
            while (elapsed < fadeWindow)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeWindow);
                float eased = EaseOutQuad(progress);

                Color color = _tilemap.color;
                color.a = 1f - eased;
                _tilemap.color = color;

                tilemapTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
                yield return null;
            }

            Color finalColor = _tilemap.color;
            finalColor.a = 0f;
            _tilemap.color = finalColor;
            tilemapTransform.localScale = endScale;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
