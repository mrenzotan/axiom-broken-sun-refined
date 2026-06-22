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
        public void SetPhase(int phase) => SetIntegerIfExists(PhaseHash, phase);
        public void TriggerFormChange() => SetTriggerIfExists(PhaseChangeHash);
        public void SetAttackIndex(int index) => SetIntegerIfExists(AttackIndexHash, index);

        private void Awake()
        {
            _originalLocalPosition = transform.localPosition;
        }

        public void TriggerAttack()  => StartCoroutine(MoveAndAttackSequence());
        public void TriggerHurt()    => SetTriggerIfExists(HurtHash);
        public void TriggerDefeat()  => SetTriggerIfExists(DefeatHash);

        private System.Collections.IEnumerator MoveAndAttackSequence()
        {
            // ── Leg 1: Run toward player ─────────────────────────────────────
            // MoveRight = true on a localScale.x = -1 sprite plays the RunRight clip
            // which visually runs LEFT (toward the player). Position lerps left via _attackPositionX.
            SetBoolIfExists(MoveRightHash, true);
            SetBoolIfExists(IsRunningHash, true);
            // Pick a random attack animation for this attack
            SetIntegerIfExists(AttackIndexHash, UnityEngine.Random.Range(0, 2));

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
            SetTriggerIfExists(AttackHash);
            SetBoolIfExists(IsRunningHash, false);

            yield return new WaitForSeconds(_attackDuration);

            // ── Leg 2: Run back to origin ────────────────────────────────────
            // MoveRight = false plays RunLeft clip which on a flipped sprite visually runs RIGHT.
            SetBoolIfExists(MoveRightHash, false);
            SetBoolIfExists(IsRunningHash, true);

            elapsed = 0f;
            while (elapsed < _moveDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(_attackPositionX, _originalLocalPosition.x, elapsed / _moveDuration);
                transform.localPosition = new Vector3(x, _originalLocalPosition.y, _originalLocalPosition.z);
                yield return null;
            }
            transform.localPosition = _originalLocalPosition;
            SetBoolIfExists(IsRunningHash, false);

            OnAttackSequenceComplete?.Invoke();
        }

        private void SetBoolIfExists(int parameterHash, bool value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Bool))
                _animator.SetBool(parameterHash, value);
        }

        private void SetIntegerIfExists(int parameterHash, int value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Int))
                _animator.SetInteger(parameterHash, value);
        }

        private void SetTriggerIfExists(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
                _animator.SetTrigger(parameterHash);
        }

        private bool HasParameter(int parameterHash, AnimatorControllerParameterType expectedType)
        {
            if (_animator == null) return false;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == parameterHash && parameter.type == expectedType)
                    return true;
            }

            return false;
        }
    }
}
