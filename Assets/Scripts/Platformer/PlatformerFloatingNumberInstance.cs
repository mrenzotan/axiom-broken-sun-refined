using System;
using TMPro;
using UnityEngine;

namespace Axiom.Platformer
{
    /// <summary>
    /// A single pooled floating-number instance. Animates upward and fades, then recycles itself.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class PlatformerFloatingNumberInstance : MonoBehaviour
    {
        [SerializeField] private float _riseSpeed = 1.5f;
        [SerializeField] private float _duration = 1.0f;

        private TextMeshPro _tmp;
        private float _elapsed;
        private bool _playing;
        private Color _startColor;
        private Action<PlatformerFloatingNumberInstance> _onComplete;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
        }

        public void Play(string text, Color color, Action<PlatformerFloatingNumberInstance> onComplete)
        {
            _tmp.text = text;
            _tmp.color = color;
            _startColor = color;
            _elapsed = 0f;
            _playing = true;
            _onComplete = onComplete;
        }

        private void Update()
        {
            if (!_playing)
                return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);

            Color c = _startColor;
            c.a = 1f - t;
            _tmp.color = c;

            if (_elapsed >= _duration)
            {
                _playing = false;
                _onComplete?.Invoke(this);
            }
        }
    }
}
