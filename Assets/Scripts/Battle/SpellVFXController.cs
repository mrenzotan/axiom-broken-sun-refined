using System.Collections;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour that plays a per-spell sprite animation and SFX when a spell is cast.
    /// Wired into BattleController via the _spellVfxController serialized field.
    ///
    /// Unity Editor setup required on the SpellVFX GameObject in the Battle scene:
    ///   - SpriteRenderer    — starts disabled; this controller enables it for the clip duration.
    ///   - Animator          — must use SpellVFXAnimator controller (one state named "SpellVFX"
    ///                         using a placeholder clip named exactly "SpellVFXBase").
    ///   - AudioSource       — Play On Awake: off, Loop: off, Spatial Blend: 0 (fully 2D).
    ///                         At runtime, output is routed to the SFX mixer bus when <see cref="AudioManager"/> is present.
    /// </summary>
    public class SpellVFXController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animator on this GameObject. Must use the SpellVFX base AnimatorController.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("SpriteRenderer on this GameObject. Keep disabled at start; this controller manages visibility.")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        [Tooltip("AudioSource on this GameObject for one-shot SFX playback.")]
        private AudioSource _audioSource;

        // Must match the state name in the SpellVFXAnimator controller.
        private const string VfxStateName = "SpellVFX";

        // Must match the placeholder clip name in the SpellVFXAnimator controller.
        private const string BaseClipName = "SpellVFXBase";

        private AnimatorOverrideController _overrideController;

        private void Awake()
        {
            if (_animator == null) return;
            _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _overrideController;

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private void Start()
        {
            if (_audioSource == null) return;

            AudioManager audio =
                GameManager.Instance != null
                    ? GameManager.Instance.GetComponentInChildren<AudioManager>()
                    : null;
            if (audio == null)
                audio = FindAnyObjectByType<AudioManager>();

            audio?.RouteSourceThroughSfxBus(_audioSource);
        }

        /// <summary>
        /// Plays the VFX clip and/or SFX from the given SpellData at the specified world position.
        /// Fields are optional — null castVfxClip or null castSfx are silently skipped.
        /// If called while a previous effect is playing, it is interrupted immediately.
        /// No-op if spell is null.
        /// </summary>
        public void Play(SpellData spell, Vector3 position)
        {
            if (spell == null) return;
            StopAllCoroutines();
            StartCoroutine(PlaySequence(spell, position));
        }

        private IEnumerator PlaySequence(SpellData spell, Vector3 position)
        {
            // Reposition to the target character before any visual appears.
            transform.position = position;

            // SFX: pick a random variant and fire immediately at cast time.
            // Using an array of 1-5 variants prevents the same sound from playing every cast.
            if (spell.castSfxVariants != null && spell.castSfxVariants.Length > 0 && _audioSource != null)
            {
                var clip = spell.castSfxVariants[UnityEngine.Random.Range(0, spell.castSfxVariants.Length)];
                if (clip != null)
                    _audioSource.PlayOneShot(clip);
            }

            // VFX shows for the exact duration of the animation clip.
            if (spell.castVfxClip != null && _animator != null && _spriteRenderer != null)
            {
                _overrideController[BaseClipName] = spell.castVfxClip;
                _animator.Play(VfxStateName, 0, 0f);
                _spriteRenderer.enabled = true;

                yield return new WaitForSeconds(spell.castVfxClip.length);

                _spriteRenderer.enabled = false;
            }
        }
    }
}
