using System.Collections;
using System.Collections.Generic;
using Axiom.Core;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    public class FreezablePlatformController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite[] _waterLoopFrames;
        [SerializeField] private Sprite[] _freezeFrames;
        [SerializeField, Min(0.1f)] private float _waterLoopFps = 6f;
        [SerializeField, Min(0.1f)] private float _freezeFps = 10f;
        [SerializeField] private List<SpellData> _freezeSpells = new();
        [SerializeField, Min(1f)] private float _freezeDuration = 5f;
        [SerializeField, Min(0.1f)] private float _warningWindow = 1.5f;
        [SerializeField] private float _warningFlashStartHz = 4f;
        [SerializeField] private float _warningFlashEndHz = 12f;

        [Header("Success cue")]
        [SerializeField] private ParticleSystem _successVfx;
        [SerializeField] private AudioClip _successSfx;
        [SerializeField] private AudioSource _audioSource;

        private bool _isFrozen;
        private bool _isPlayerInRange;
        private Coroutine _waterLoopCoroutine;

        public bool IsFrozen => _isFrozen;
        public bool IsPlayerInRange => _isPlayerInRange;

        private void Start()
        {
            StartWaterLoop();

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

        public bool CanFreezeWith(string spellId)
        {
            if (_isFrozen) return false;
            if (!_isPlayerInRange) return false;

            return FreezablePlatform.CanFreeze(spellId, BuildFreezeSpellIds());
        }

        public bool TryFreeze(string spellId)
        {
            if (!CanFreezeWith(spellId)) return false;

            _isFrozen = true;
            PlaySuccessCue();
            StartCoroutine(FreezeCoroutine());
            return true;
        }

        private void PlaySuccessCue()
        {
            if (_successVfx != null)
                _successVfx.Play();
            if (_audioSource != null && _successSfx != null)
                _audioSource.PlayOneShot(_successSfx);
        }

        private List<string> BuildFreezeSpellIds()
        {
            var freezeSpellIds = new List<string>(_freezeSpells.Count);
            for (int i = 0; i < _freezeSpells.Count; i++)
            {
                SpellData spell = _freezeSpells[i];
                if (spell != null) freezeSpellIds.Add(spell.spellName);
            }

            return freezeSpellIds;
        }

        private void StartWaterLoop()
        {
            StopWaterLoop();
            _waterLoopCoroutine = StartCoroutine(WaterLoopCoroutine());
        }

        private void StopWaterLoop()
        {
            if (_waterLoopCoroutine != null)
            {
                StopCoroutine(_waterLoopCoroutine);
                _waterLoopCoroutine = null;
            }
        }

        private IEnumerator WaterLoopCoroutine()
        {
            if (_spriteRenderer == null || _waterLoopFrames == null || _waterLoopFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _waterLoopFps);
            int frame = 0;
            while (true)
            {
                _spriteRenderer.sprite = _waterLoopFrames[frame];
                frame = (frame + 1) % _waterLoopFrames.Length;
                yield return frameWait;
            }
        }

        private IEnumerator FreezeCoroutine()
        {
            StopWaterLoop();
            if (_solidCollider != null) _solidCollider.enabled = true;

            yield return PlayFreezeFrames(reverse: false);

            float solidWindow = Mathf.Max(0f, _freezeDuration - _warningWindow);
            yield return new WaitForSeconds(solidWindow);

            float elapsed = 0f;
            Color color = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
            while (elapsed < _warningWindow)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _warningWindow);
                float hz = Mathf.Lerp(_warningFlashStartHz, _warningFlashEndHz, progress);
                float wave = Mathf.Sin(elapsed * hz * 2f * Mathf.PI);
                color.a = wave > 0f ? 1f : 0.5f;
                if (_spriteRenderer != null) _spriteRenderer.color = color;
                yield return null;
            }

            color.a = 1f;
            if (_spriteRenderer != null) _spriteRenderer.color = color;

            if (_solidCollider != null) _solidCollider.enabled = false;
            yield return PlayFreezeFrames(reverse: true);

            _isFrozen = false;
            StartWaterLoop();
        }

        private IEnumerator PlayFreezeFrames(bool reverse)
        {
            if (_spriteRenderer == null || _freezeFrames == null || _freezeFrames.Length == 0)
                yield break;

            var frameWait = new WaitForSeconds(1f / _freezeFps);
            for (int i = 0; i < _freezeFrames.Length; i++)
            {
                int frame = reverse ? _freezeFrames.Length - 1 - i : i;
                _spriteRenderer.sprite = _freezeFrames[frame];
                yield return frameWait;
            }
        }
    }
}
