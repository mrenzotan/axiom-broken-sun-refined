using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace Axiom.Battle
{
    /// <summary>
    /// A single pooled floating damage/heal/crit number.
    /// FloatingNumberSpawner configures and releases it back to the pool.
    /// </summary>
    public class FloatingNumberInstance : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private float _floatDistance = 80f;
        [SerializeField] private float _duration = 0.9f;

        private IObjectPool<FloatingNumberInstance> _pool;
        private float _originalFontSize;

        private void Awake()
        {
            _originalFontSize = _text.fontSize;
        }

        public void Initialize(IObjectPool<FloatingNumberInstance> pool)
        {
            _pool = pool;
        }

        /// <summary>
        /// Sets the text, colour, scale, and starting position, then starts the animation.
        /// Called by FloatingNumberSpawner immediately after retrieving from pool.
        /// </summary>
        public void Play(string label, Color color, float scale, Vector3 canvasPosition)
        {
            _text.text = label;
            _text.color = color;
            _text.fontSize = _originalFontSize * scale;
            transform.position = canvasPosition;
            _canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos   = startPos + Vector3.up * _floatDistance;
            float elapsed    = 0f;

            while (elapsed < _duration)
            {
                float t = elapsed / _duration;
                transform.position  = Vector3.Lerp(startPos, endPos, t);
                _canvasGroup.alpha  = Mathf.Lerp(1f, 0f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _pool.Release(this);
        }
    }
}
