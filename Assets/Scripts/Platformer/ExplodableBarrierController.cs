using System.Collections;
using Axiom.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Axiom.Platformer
{
    /// <summary>
    /// A rubble/boulder barrier cleared only by a steam-vent explosion (the player
    /// cannot ignite it directly). Composed of one or more child block SpriteRenderers
    /// (e.g. tiled explodable-blocks) that fade out together as a single unit on
    /// detonation, behind one shared collider and one puzzleId. Implements
    /// IExplosionDestructible.
    /// </summary>
    public class ExplodableBarrierController : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField]
        [Tooltip("The visual child blocks that make up this barrier. Leave empty to auto-collect every child SpriteRenderer on Awake.")]
        private SpriteRenderer[] _blockRenderers;

        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.4f;

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the cleared state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;

        public string PuzzleId => _puzzleId;

        [Header("Destruction cue")]
        [SerializeField] private ParticleSystem _debrisVfx;
        [SerializeField] private AudioClip _destroySfx;
        [SerializeField] private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Fired when the barrier is detonated by a vent blast. Wire to a CinemachineImpulseSource.GenerateImpulse (camera shake) or any other scene reaction. Keeps this asmdef free of a Cinemachine reference.")]
        private UnityEvent _onDetonated;

        private bool _isCleared;
        public bool IsCleared => _isCleared;

        private void Awake()
        {
            if (_blockRenderers == null || _blockRenderers.Length == 0)
                _blockRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }
        }

        public void Detonate()
        {
            if (_isCleared) return;
            _isCleared = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            if (_debrisVfx != null) _debrisVfx.Play();
            if (_audioSource != null && _destroySfx != null) _audioSource.PlayOneShot(_destroySfx);
            _onDetonated?.Invoke();

            if (_solidCollider != null) _solidCollider.enabled = false;
            StartCoroutine(FadeOutCoroutine());
        }

        public void ApplySolvedImmediate()
        {
            _isCleared = true;
            if (_solidCollider != null) _solidCollider.enabled = false;
            DisableAllBlocks();
        }

        private IEnumerator FadeOutCoroutine()
        {
            if (_blockRenderers == null || _blockRenderers.Length == 0)
                yield break;

            var startColors = new Color[_blockRenderers.Length];
            for (int i = 0; i < _blockRenderers.Length; i++)
                startColors[i] = _blockRenderers[i] != null ? _blockRenderers[i].color : Color.clear;

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                for (int i = 0; i < _blockRenderers.Length; i++)
                {
                    if (_blockRenderers[i] == null) continue;
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    _blockRenderers[i].color = c;
                }
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.85f, t);
                yield return null;
            }

            DisableAllBlocks();
        }

        private void DisableAllBlocks()
        {
            if (_blockRenderers == null) return;
            for (int i = 0; i < _blockRenderers.Length; i++)
                if (_blockRenderers[i] != null) _blockRenderers[i].enabled = false;
        }
    }
}
