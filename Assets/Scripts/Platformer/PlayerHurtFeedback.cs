using System.Collections;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// Lives on the Player root. Plays the Hurt animator trigger on demand,
    /// and applies a sustained sprite tint while one or more spike hazards
    /// overlap the player. The tint is counter-based so that overlapping two
    /// hazards then exiting one does not flicker the tint off — it stays on
    /// until the player has left every overlapping hazard.
    ///
    /// FlashOnTick pulses the sprite back to its resting color for a brief
    /// moment on each hazard damage tick, making the pain feedback read
    /// as a rhythmic flash synced to incoming damage.
    /// </summary>
    public class PlayerHurtFeedback : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animator on the player whose 'Hurt' trigger plays the playerHurt clip.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("Sprite renderer that gets tinted while overlapping a spike hazard.")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        [Tooltip("Color applied to the sprite while overlapping at least one spike hazard.")]
        private Color _painTint = new Color(1f, 0.6f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Briefly applied on each DoT tick as a visible per-tick cue — typically more saturated than the sustained pain tint.")]
        private Color _tickFlashTint = new Color(1f, 0.3f, 0.3f, 1f);

        [SerializeField, Range(0.02f, 0.5f)]
        [Tooltip("Duration in seconds of the per-tick color flash.")]
        private float _tickFlashSeconds = 0.08f;

        [SerializeField]
        [Tooltip("Animator trigger parameter name fired by spike contact. Must match the parameter you add to Player.controller.")]
        private string _hurtTriggerName = "Hurt";

        [SerializeField, Range(0.02f, 0.3f)]
        [Tooltip("Duration of the flash-to-resting-color pulse per hazard tick.")]
        private float _flashDuration = 0.08f;

        private int _overlapCount;
        private Color _restingColor = Color.white;
        private bool _restingColorCaptured;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            if (_spriteRenderer != null)
            {
                _restingColor = _spriteRenderer.color;
                _restingColorCaptured = true;
            }
        }

        public void PlayHurtAnimation()
        {
            if (_animator == null) return;
            _animator.SetTrigger(_hurtTriggerName);
        }

        public void BeginPainOverlap()
        {
            _overlapCount++;
            if (_overlapCount == 1 && _spriteRenderer != null)
                _spriteRenderer.color = _painTint;
        }

        public void EndPainOverlap()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_overlapCount > 0)
                _overlapCount--;
            if (_overlapCount == 0 && _spriteRenderer != null && _restingColorCaptured)
                _spriteRenderer.color = _restingColor;
        }

        /// <summary>
        /// Brief color pulse fired by HazardTrigger on each DoT tick to give a visible
        /// per-tick cue on top of the sustained pain tint. Restores to the correct
        /// sustained color (pain tint while still overlapping, resting otherwise).
        /// </summary>
        public void FlashOnTick()
        {
            if (_spriteRenderer == null) return;
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashTickRoutine());
        }

        private IEnumerator FlashTickRoutine()
        {
            _spriteRenderer.color = _tickFlashTint;
            yield return new WaitForSeconds(_tickFlashSeconds);
            if (_overlapCount > 0)
                _spriteRenderer.color = _painTint;
            else if (_restingColorCaptured)
                _spriteRenderer.color = _restingColor;
            _flashCoroutine = null;
        }

        private IEnumerator FlashRoutine()
        {
            if (_restingColorCaptured)
                _spriteRenderer.color = _restingColor;
            yield return new WaitForSeconds(_flashDuration);
            if (_overlapCount > 0)
                _spriteRenderer.color = _painTint;
            _flashCoroutine = null;
        }
    }
}
