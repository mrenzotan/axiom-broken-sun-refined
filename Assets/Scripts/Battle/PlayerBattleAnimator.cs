using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour adapter for the player's battle Animator.
    /// Lifecycle only — exposes trigger methods injected into BattleAnimationService as Actions.
    /// </summary>
    public class PlayerBattleAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [SerializeField]
        [Tooltip("Local-space X position to move to before attacking (toward the enemy).")]
        private float _attackPositionX = 0f;

        [SerializeField]
        [Tooltip("Seconds to travel each leg of the move-attack-return sequence.")]
        private float _moveDuration = 0.3f;

        [SerializeField]
        [Tooltip("Seconds to wait after triggering the attack before running back. Set to match the attack clip length.")]
        private float _attackDuration = 0.5f;

        private static readonly int AttackHash    = Animator.StringToHash("Attack");
        private static readonly int HurtHash      = Animator.StringToHash("Hurt");
        private static readonly int DefeatHash    = Animator.StringToHash("Defeat");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int MoveRightHash  = Animator.StringToHash("MoveRight");
        private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
        private static readonly int CastHash       = Animator.StringToHash("Cast");

        private Vector3 _originalLocalPosition;

        /// <summary>
        /// Fired by Unity Animation Event on the hit frame of the attack clip.
        /// BattleController subscribes to trigger damage visual feedback at the right moment.
        /// </summary>
        public event System.Action OnHitFrame;

        /// <summary>
        /// Fired when the full move → attack → return sequence is complete.
        /// BattleController subscribes to advance the turn at the right moment.
        /// </summary>
        public event System.Action OnAttackSequenceComplete;

        /// <summary>
        /// Fired by Unity Animation Event on the fire frame of the cast clip.
        /// BattleController subscribes to spawn VFX and resolve spell damage at the right moment.
        /// </summary>
        public event System.Action OnSpellFireFrame;

        /// <summary>
        /// Called by Unity Animation Event on the attack clip's hit frame.
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnHit() => OnHitFrame?.Invoke();

        private void Awake()
        {
            _originalLocalPosition = transform.localPosition;
        }

        public void TriggerAttack()  => StartCoroutine(MoveAndAttackSequence());
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);
        public void TriggerCharge()  => _animator.SetBool(IsChargingHash, true);
        public void TriggerCast()    { _animator.SetBool(IsChargingHash, false); _animator.SetTrigger(CastHash); }

        /// <summary>
        /// Called by Unity Animation Event on the cast clip's fire frame.
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnSpellFire() => OnSpellFireFrame?.Invoke();

        private System.Collections.IEnumerator MoveAndAttackSequence()
        {
            // ── Leg 1: Run toward enemy ──────────────────────────────────────
            _animator.SetBool(MoveRightHash, true);
            _animator.SetBool(IsRunningHash, true);

            float elapsed = 0f;
            float startX  = _originalLocalPosition.x;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(startX, _attackPositionX, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = new Vector3(_attackPositionX, _originalLocalPosition.y, _originalLocalPosition.z);

            // ── Attack: direct run → attack transition (no idle gap) ─────────
            _animator.SetTrigger(AttackHash);
            _animator.SetBool(IsRunningHash, false);

            yield return new WaitForSeconds(_attackDuration);

            // ── Leg 2: Run back to origin ────────────────────────────────────
            _animator.SetBool(MoveRightHash, false);
            _animator.SetBool(IsRunningHash, true);

            elapsed = 0f;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(_attackPositionX, _originalLocalPosition.x, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = _originalLocalPosition;
            _animator.SetBool(IsRunningHash, false);

            OnAttackSequenceComplete?.Invoke();
        }
    }
}
