using UnityEngine;

namespace Axiom.Core
{
    public enum CutsceneInputResult
    {
        None,
        Advance,
        Skip
    }

    public sealed class CutsceneInputHandler
    {
        private bool _isHoldingEnter;
        private float _holdDuration;

        public float HoldToSkipDuration { get; set; } = 3f;
        public float TapThreshold { get; set; } = 0.2f;

        public bool IsHoldingEnter => _isHoldingEnter;

        public float SkipProgress
        {
            get
            {
                if (HoldToSkipDuration <= 0f) return 0f;
                return Mathf.Clamp01(_holdDuration / HoldToSkipDuration);
            }
        }

        public CutsceneInputResult ProcessEnterInput(bool enterPressed, bool enterReleased, float deltaTime)
        {
            if (enterPressed && enterReleased)
            {
                _isHoldingEnter = false;
                _holdDuration = 0f;
                return CutsceneInputResult.Advance;
            }

            if (enterPressed)
            {
                _isHoldingEnter = true;
                _holdDuration = 0f;
                return CutsceneInputResult.None;
            }

            if (!_isHoldingEnter)
                return CutsceneInputResult.None;

            _holdDuration += deltaTime;

            if (_holdDuration >= HoldToSkipDuration)
            {
                _isHoldingEnter = false;
                _holdDuration = 0f;
                return CutsceneInputResult.Skip;
            }

            if (enterReleased)
            {
                _isHoldingEnter = false;
                bool wasTap = _holdDuration < TapThreshold;
                _holdDuration = 0f;
                return wasTap ? CutsceneInputResult.Advance : CutsceneInputResult.None;
            }

            return CutsceneInputResult.None;
        }

        public void Reset()
        {
            _isHoldingEnter = false;
            _holdDuration = 0f;
        }
    }
}
