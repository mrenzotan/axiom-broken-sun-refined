using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Acid puddle hazard (DEV-94 Level 3). A floor pool that:
    ///   - loops its 6-frame sprite animation forever, desynced per instance (random
    ///     speed + random start frame), like the Level 2 animated lava tile;
    ///   - deals ESCALATING damage-over-time while the player overlaps it, resetting the
    ///     escalation when they step out (modeled on HazardTrigger's enter/tick/exit);
    ///   - DISSOLVES (alpha fade + particle VFX) when the player casts a neutralize spell
    ///     from within the proximity zone, persisting the cleared state across a Battle
    ///     round-trip (modeled on BurnableObstacleController).
    ///
    /// MonoBehaviour holds lifecycle + Unity refs only. Pure logic lives in the static
    /// helpers AcidPuddle (spell match) and AcidPuddleDamage (escalation curve); damage
    /// math reuses HazardDamageResolver; player feedback reuses PlayerHurtFeedback.
    ///
    /// PlayerDeathHandler observes PlayerState.CurrentHp and dispatches death/respawn —
    /// this component never knows about death.
    ///
    /// Spec: docs/superpowers/specs/2026-06-22-dev-94-level-3-acid-puddle-design.md
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AcidPuddleController : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite[] _acidFrames;
        [SerializeField, Min(0.1f)] private float _minSpeed = 5f;
        [SerializeField, Min(0.1f)] private float _maxSpeed = 7f;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Trigger sized to the visible acid. Disabled when the puddle dissolves.")]
        private Collider2D _damageCollider;
        [SerializeField, Range(0, 100)] private int _baseTickPercent = 3;
        [SerializeField, Min(1f)] private float _growthFactor = 1.6f;
        [SerializeField, Range(0, 100)] private int _maxTickPercent = 25;
        [SerializeField, Range(0.1f, 3f)] private float _tickIntervalSeconds = 0.5f;

        [Header("Neutralize")]
        [SerializeField] private List<SpellData> _neutralizeSpells = new();
        [SerializeField, Min(0f)] private float _fadeDuration = 0.6f;

        [SerializeField]
        [Tooltip("Stable, scene-unique ID used to persist the dissolved state across a Battle round-trip. Leave blank to opt out of persistence.")]
        private string _puzzleId;
        public string PuzzleId => _puzzleId;

        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;

        private bool _isNeutralized;
        private bool _isPlayerInRange;
        private int _tickIndex;
        private PlayerHurtFeedback _feedback;
        private Coroutine _tickCoroutine;
        private Coroutine _animateCoroutine;

        public bool IsNeutralized => _isNeutralized;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void Start()
        {
            if (_audioSource != null && GameManager.Instance != null
                && GameManager.Instance.AudioManager != null)
            {
                GameManager.Instance.AudioManager.RouteSourceThroughSfxBus(_audioSource);
            }

            // Guard against the restore controller (Script Execution Order -10) having
            // already marked this puddle solved before our Start runs — don't re-animate
            // a dissolved puddle.
            if (!_isNeutralized)
                _animateCoroutine = StartCoroutine(AnimateLoop());
        }

        // ── Animation ───────────────────────────────────────────────
        private IEnumerator AnimateLoop()
        {
            if (_spriteRenderer == null || _acidFrames == null || _acidFrames.Length == 0)
                yield break;

            float speed = Random.Range(_minSpeed, _maxSpeed);
            var wait = new WaitForSeconds(1f / speed);
            int frame = Random.Range(0, _acidFrames.Length);
            while (true)
            {
                _spriteRenderer.sprite = _acidFrames[frame];
                frame = (frame + 1) % _acidFrames.Length;
                yield return wait;
            }
        }

        // ── Escalating DoT ──────────────────────────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isNeutralized) return;
            if (!other.CompareTag("Player")) return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[AcidPuddleController] GameManager not found — acid ignored.", this);
                return;
            }

            _feedback = other.GetComponentInParent<PlayerHurtFeedback>();
            _tickIndex = 0;
            ApplyTickDamage();                 // immediate mild first tick (tick 0 = base)
            _feedback?.PlayHurtAnimation();
            _feedback?.BeginPainOverlap();

            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);
            _tickCoroutine = StartCoroutine(TickWhileOverlapping());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
            _tickIndex = 0;                    // reset escalation on exit
        }

        private void OnDisable()
        {
            // Disabling/destroying mid-overlap (e.g. level unload) must not leave the
            // player tinted or a coroutine running on a dead object.
            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
        }

        private IEnumerator TickWhileOverlapping()
        {
            var wait = new WaitForSeconds(_tickIntervalSeconds);
            while (true)
            {
                yield return wait;
                if (GameManager.Instance == null)
                    continue;
                _tickIndex++;
                ApplyTickDamage();
                _feedback?.FlashOnTick();
            }
        }

        private void ApplyTickDamage()
        {
            int percent = AcidPuddleDamage.PercentForTick(
                _tickIndex, _baseTickPercent, _growthFactor, _maxTickPercent);

            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: HazardMode.PercentMaxHpDamage,
                percentMaxHpDamage: percent);
            state.SetCurrentHp(result.NewHp);
        }

        private void StopTicking()
        {
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        // ── Neutralize + removal ────────────────────────────────────
        public void SetPlayerInRange(bool inRange) => _isPlayerInRange = inRange;

        public bool CanNeutralizeWith(string spellId)
        {
            if (_isNeutralized) return false;
            if (!_isPlayerInRange) return false;
            return AcidPuddle.CanNeutralize(spellId, BuildNeutralizeSpellIds());
        }

        public bool TryNeutralize(string spellId)
        {
            if (!CanNeutralizeWith(spellId)) return false;
            Neutralize();
            return true;
        }

        private void Neutralize()
        {
            _isNeutralized = true;

            if (!string.IsNullOrWhiteSpace(_puzzleId) && GameManager.Instance != null)
                GameManager.Instance.MarkPuzzleSolved(_puzzleId);

            // Stop hurting the player immediately — including when they neutralize while
            // standing in the acid.
            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
            _tickIndex = 0;
            if (_damageCollider != null)
                _damageCollider.enabled = false;

            StopAnimating();
            PlaySuccessCue();
            StartCoroutine(FadeOut());
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }

        private IEnumerator FadeOut()
        {
            if (_spriteRenderer == null)
                yield break;

            Color baseColor = _spriteRenderer.color;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / _fadeDuration));
                _spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }
            _spriteRenderer.enabled = false;
        }

        /// <summary>
        /// Forces the dissolved state with no VFX, no fade, no animation. Called on scene
        /// load by PlatformerWorldRestoreController when this puddle was already neutralized
        /// earlier in the session.
        /// </summary>
        public void ApplySolvedImmediate()
        {
            _isNeutralized = true;
            StopAnimating();
            if (_damageCollider != null)
                _damageCollider.enabled = false;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private void StopAnimating()
        {
            if (_animateCoroutine != null)
            {
                StopCoroutine(_animateCoroutine);
                _animateCoroutine = null;
            }
        }

        private List<string> BuildNeutralizeSpellIds()
        {
            var ids = new List<string>(_neutralizeSpells.Count);
            for (int i = 0; i < _neutralizeSpells.Count; i++)
            {
                SpellData spell = _neutralizeSpells[i];
                if (spell != null) ids.Add(spell.spellName);
            }

            return ids;
        }
    }
}
