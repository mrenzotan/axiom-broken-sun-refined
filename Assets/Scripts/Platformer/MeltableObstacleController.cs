using System.Collections;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Axiom.Platformer
{
    public class MeltableObstacleController : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private TilemapCollider2D _solidCollider;
        [SerializeField] private List<SpellData> _meltSpells = new();
        [SerializeField, Min(0.05f)] private float _fadeDuration = 0.7f;

        private static readonly Color FlashTint = new(0xBF / 255f, 0xE9 / 255f, 1f, 1f);
        private const float FlashDuration = 0.15f;
        private const float SinkScaleY = 0.6f;

        private bool _isMelted;
        private bool _isPlayerInRange;

        public bool IsMelted => _isMelted;

        public void SetPlayerInRange(bool inRange)
        {
            _isPlayerInRange = inRange;
        }

        public bool TryMelt(string spellId)
        {
            if (_isMelted) return false;
            if (!_isPlayerInRange) return false;

            var meltSpellIds = new List<string>(_meltSpells.Count);
            for (int i = 0; i < _meltSpells.Count; i++)
            {
                SpellData spell = _meltSpells[i];
                if (spell != null) meltSpellIds.Add(spell.spellName);
            }

            if (!MeltableObstacle.CanMelt(spellId, meltSpellIds)) return false;

            _isMelted = true;
            StartCoroutine(MeltCoroutine());
            return true;
        }

        private IEnumerator MeltCoroutine()
        {
            yield return FlashCoroutine();

            if (_solidCollider != null)
                _solidCollider.enabled = false;

            yield return FadeAndSinkCoroutine();

            if (_tilemap != null)
                _tilemap.gameObject.SetActive(false);
        }

        private IEnumerator FlashCoroutine()
        {
            if (_tilemap == null) yield break;

            float halfFlash = FlashDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _tilemap.color = Color.Lerp(Color.white, FlashTint, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < halfFlash)
            {
                elapsed += Time.deltaTime;
                _tilemap.color = Color.Lerp(FlashTint, Color.white, Mathf.Clamp01(elapsed / halfFlash));
                yield return null;
            }
            _tilemap.color = Color.white;
        }

        private IEnumerator FadeAndSinkCoroutine()
        {
            if (_tilemap == null) yield break;

            float fadeWindow = Mathf.Max(0.01f, _fadeDuration - FlashDuration);
            Transform tilemapTransform = _tilemap.transform;
            Vector3 startScale = tilemapTransform.localScale;
            Vector3 endScale = new(startScale.x, startScale.y * SinkScaleY, startScale.z);

            float elapsed = 0f;
            while (elapsed < fadeWindow)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeWindow);
                float eased = EaseOutQuad(progress);

                Color color = _tilemap.color;
                color.a = 1f - eased;
                _tilemap.color = color;

                tilemapTransform.localScale = Vector3.Lerp(startScale, endScale, progress);
                yield return null;
            }

            Color finalColor = _tilemap.color;
            finalColor.a = 0f;
            _tilemap.color = finalColor;
            tilemapTransform.localScale = endScale;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
