using System;

namespace Axiom.Core
{
    public sealed class TypewriterEffect
    {
        private string _fullText = "";
        private int _revealedCount;
        private float _accumulator;
        private float _charsPerSecond;

        public string FullText => _fullText;
        public string VisibleText => GetVisibleText();
        public bool IsComplete => _revealedCount >= _fullText.Length;

        public void Start(string text, float charsPerSecond)
        {
            _fullText = text ?? "";
            _revealedCount = 0;
            _accumulator = 0f;
            _charsPerSecond = Math.Max(0.01f, charsPerSecond);
        }

        public float Update(float deltaTime)
        {
            if (IsComplete) return 1f;

            _accumulator += deltaTime;
            int targetCount = (int)(_accumulator * _charsPerSecond);

            if (targetCount > _fullText.Length)
                targetCount = _fullText.Length;

            _revealedCount = targetCount;

            if (_fullText.Length == 0)
                return 1f;

            return (float)_revealedCount / _fullText.Length;
        }

        public void SkipToEnd()
        {
            _revealedCount = _fullText.Length;
            _accumulator = _fullText.Length / Math.Max(0.01f, _charsPerSecond);
        }

        private string GetVisibleText()
        {
            if (_fullText.Length == 0) return "";
            if (_revealedCount >= _fullText.Length) return _fullText;
            return _fullText.Substring(0, _revealedCount);
        }
    }
}
