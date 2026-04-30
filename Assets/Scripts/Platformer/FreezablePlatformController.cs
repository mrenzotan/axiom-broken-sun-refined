using System.Collections;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;

namespace Axiom.Platformer
{
    public class FreezablePlatformController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private BoxCollider2D _solidCollider;
        [SerializeField] private Sprite _waterSprite;
        [SerializeField] private Sprite _iceSprite;
        [SerializeField] private List<SpellData> _freezeSpells = new();
        [SerializeField, Min(1f)] private float _freezeDuration = 5f;
        [SerializeField, Min(0.1f)] private float _warningWindow = 1.5f;
        [SerializeField] private float _warningFlashStartHz = 4f;
        [SerializeField] private float _warningFlashEndHz = 12f;

        private bool _isFrozen;
        private bool _isPlayerInRange;

        public bool IsFrozen => _isFrozen;
        public bool IsPlayerInRange => _isPlayerInRange;

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool TryFreeze(string spellId)
        {
            if (_isFrozen) return false;
            if (!_isPlayerInRange) return false;

            var freezeSpellIds = new List<string>(_freezeSpells.Count);
            for (int i = 0; i < _freezeSpells.Count; i++)
            {
                SpellData spell = _freezeSpells[i];
                if (spell != null) freezeSpellIds.Add(spell.spellName);
            }

            if (!FreezablePlatform.CanFreeze(spellId, freezeSpellIds)) return false;

            _isFrozen = true;
            StartCoroutine(FreezeCoroutine());
            return true;
        }

        private IEnumerator FreezeCoroutine()
        {
            SetVisualState(frozen: true);

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
            SetVisualState(frozen: false);
            _isFrozen = false;
        }

        private void SetVisualState(bool frozen)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.sprite = frozen ? _iceSprite : _waterSprite;
            if (_solidCollider != null)
                _solidCollider.enabled = frozen;
        }
    }
}
