using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour adapter for the enemy's battle Animator.
    /// Lifecycle only — exposes trigger methods injected into BattleAnimationService as Actions.
    /// </summary>
    public class EnemyBattleAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [SerializeField]
        [Tooltip("Local-space X position to move to before attacking (toward the player). Should be less than the enemy's origin X.")]
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
        private static readonly int MoveRightHash = Animator.StringToHash("MoveRight");
        private static readonly int PhaseHash     = Animator.StringToHash("Phase");
        private static readonly int PhaseChangeHash = Animator.StringToHash("PhaseChange");

        private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");

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
        /// Called by Unity Animation Event on the attack clip's hit frame.
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnHit() => OnHitFrame?.Invoke();

        /// <summary>
        /// Fired by a Unity Animation Event on the last frame of each phase-change (morph) clip.
        /// BattleController subscribes so it can wait for the morph to finish before the enemy acts.
        /// </summary>
        public event System.Action OnPhaseChangeComplete;

        /// <summary>
        /// Called by Unity Animation Event on the final frame of the morph clips
        /// (FrostmeltSpawnPhaseChange / FrostmeltSpawnPhaseChange2).
        /// The method name must match exactly what is set in the Animation Event inspector.
        /// </summary>
        public void AnimEvent_OnPhaseChangeComplete() => OnPhaseChangeComplete?.Invoke();

        /// <summary>
        /// Sets the Phase animator parameter to trigger a phase transition.
        /// Called by BattleController when the enemy's HP crosses a phase threshold.
        /// </summary>
        public void SetPhase(int phase) => _animator.SetInteger(PhaseHash, phase);
        public void TriggerFormChange() => _animator.SetTrigger(PhaseChangeHash);
        public void SetAttackIndex(int index) => _animator.SetInteger(AttackIndexHash, index);

        private void Awake()
        {
            _originalLocalPosition = transform.localPosition;
        }

        public void TriggerAttack()  => StartCoroutine(MoveAndAttackSequence());
        public void TriggerHurt()    => _animator.SetTrigger(HurtHash);
        public void TriggerDefeat()  => _animator.SetTrigger(DefeatHash);

        private System.Collections.IEnumerator MoveAndAttackSequence()
        {
            // ── Leg 1: Run toward player ─────────────────────────────────────
            // MoveRight = true on a localScale.x = -1 sprite plays the RunRight clip
            // which visually runs LEFT (toward the player). Position lerps left via _attackPositionX.
            _animator.SetBool(MoveRightHash, true);
            _animator.SetBool(IsRunningHash, true);
            // Pick a random attack animation for this attack
            _animator.SetInteger(AttackIndexHash, UnityEngine.Random.Range(0, 2));

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
            // MoveRight = false plays RunLeft clip which on a flipped sprite visually runs RIGHT.
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
