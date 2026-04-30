using System;
using System.Collections;
using Axiom.Core;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Attach to a trigger collider in a level scene. On player contact:
    ///   - InstantKO mode: sets PlayerState.CurrentHp to 0 immediately (pit behavior — unchanged).
    ///   - PercentMaxHpDamage mode: applies _firstHitDamagePercent on entry, then ticks
    ///     _damagePerTickPercent every _tickIntervalSeconds while the player remains overlapping,
    ///     and stops on exit. Notifies the player's PlayerHurtFeedback for animation + tint.
    ///
    /// Ticks run from a coroutine started on enter and stopped on exit — not from
    /// OnTriggerStay2D, which Unity stops firing once the player's Rigidbody2D goes
    /// to sleep at rest.
    ///
    /// PlayerDeathHandler observes PlayerState.CurrentHp and dispatches death/respawn —
    /// this component never knows about death.
    ///
    /// Spec: docs/superpowers/specs/2026-04-26-dev-46-spike-hazard-dot-design.md
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardTrigger : MonoBehaviour
    {
        /// <summary>
        /// Fires once per first-contact spike damage frame. Used by
        /// FirstSpikeHitPromptController to show the "spikes deal DoT" prompt
        /// at most once per save (further gated by PlayerState.HasSeenFirstSpikeHit).
        /// Does NOT fire for InstantKO (pit) hazards or for DoT tick frames.
        /// </summary>
        public static event Action OnPlayerFirstHitFrame;

        [SerializeField]
        [Tooltip("InstantKO for pits; PercentMaxHpDamage for spikes.")]
        private HazardMode _mode = HazardMode.PercentMaxHpDamage;

        [SerializeField, Range(0, 100)]
        [Tooltip("HP percent dealt on contact entry. Set to 0 for pure-DoT spikes. Ignored when mode is InstantKO.")]
        private int _firstHitDamagePercent = 20;

        [SerializeField, Range(0, 100)]
        [Tooltip("HP percent dealt every tick while overlapping. Set to 0 for one-shot-only spikes. Ignored when mode is InstantKO.")]
        private int _damagePerTickPercent = 10;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Seconds between DoT ticks while overlapping. Lower values are more punishing.")]
        private float _tickIntervalSeconds = 0.5f;

        private PlayerHurtFeedback _feedback;
        private Coroutine _tickCoroutine;

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[HazardTrigger] GameManager not found — hazard ignored.", this);
                return;
            }

            if (_mode == HazardMode.InstantKO)
            {
                _feedback = other.GetComponentInParent<PlayerHurtFeedback>();
                ApplyPercentDamage(0, HazardMode.InstantKO);
                _feedback?.PlayHurtAnimation();
                return;
            }

            _feedback = other.GetComponentInParent<PlayerHurtFeedback>();
            ApplyPercentDamage(_firstHitDamagePercent, HazardMode.PercentMaxHpDamage);
            OnPlayerFirstHitFrame?.Invoke();
            _feedback?.PlayHurtAnimation();
            _feedback?.BeginPainOverlap();

            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);
            _tickCoroutine = StartCoroutine(TickWhileOverlapping());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_mode == HazardMode.InstantKO)
                return;
            if (!other.CompareTag("Player"))
                return;

            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
        }

        private void OnDisable()
        {
            // Disabling/destroying the hazard mid-overlap (e.g., level unload) must not
            // leave the player tint stuck on or a coroutine running on a dead object.
            StopTicking();
            _feedback?.EndPainOverlap();
            _feedback = null;
        }

        private IEnumerator TickWhileOverlapping()
        {
            WaitForSeconds wait = new WaitForSeconds(_tickIntervalSeconds);
            while (true)
            {
                yield return wait;
                if (GameManager.Instance == null)
                    continue;
                ApplyPercentDamage(_damagePerTickPercent, HazardMode.PercentMaxHpDamage);
                _feedback?.FlashOnTick();
            }
        }

        private void StopTicking()
        {
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        private void ApplyPercentDamage(int percent, HazardMode mode)
        {
            PlayerState state = GameManager.Instance.PlayerState;
            HazardDamageResult result = HazardDamageResolver.Resolve(
                currentHp: state.CurrentHp,
                maxHp: state.MaxHp,
                mode: mode,
                percentMaxHpDamage: percent);
            state.SetCurrentHp(result.NewHp);
        }
    }
}
