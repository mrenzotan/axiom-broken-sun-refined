using Axiom.Core;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle.UI
{
    public class XpBarUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image with Type=Filled (Horizontal) for the XP bar fill.")]
        private Image _xpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / needed' XP. Optional.")]
        private TMP_Text _xpText;

        [SerializeField]
        [Tooltip("Speed at which the bar fill lerps toward its target value.")]
        private float _lerpSpeed = 5f;

        [SerializeField]
        [Tooltip("How long the 'LEVEL UP!' text holds during the XP bar animation, in seconds.")]
        private float _levelUpHoldDuration = 0.5f;

        private float _targetXpFill;

        private void Update()
        {
            if (_xpBarImage != null)
                _xpBarImage.fillAmount = Mathf.Lerp(
                    _xpBarImage.fillAmount, _targetXpFill, Time.unscaledDeltaTime * _lerpSpeed);
        }

        public void SetXP(int current, int needed)
        {
            _targetXpFill = needed > 0 ? (float)current / needed : 0f;
            if (_xpText != null)
                _xpText.text = $"{current} / {needed}";
        }

        public void AnimateTo(int current, int needed, float startFill)
        {
            _targetXpFill = needed > 0 ? (float)current / needed : 0f;
            if (_xpBarImage != null)
                _xpBarImage.fillAmount = startFill;
            if (_xpText != null)
                _xpText.text = $"{current} / {needed}";
        }

        public void ShowLevelCap()
        {
            _targetXpFill = 0f;
            if (_xpBarImage != null)
                _xpBarImage.fillAmount = 0f;
            if (_xpText != null)
                _xpText.text = "MAX LEVEL";
        }

        public IEnumerator AnimateLevelUpFlow(XpProgress before, XpProgress after)
        {
            if (_xpText != null)
                _xpText.text = $"{before.CurrentXp} / {before.XpForNextLevel}";

            if (_xpBarImage != null)
            {
                _targetXpFill = 1f;
                while (_xpBarImage != null && _xpBarImage.fillAmount < 0.99f)
                    yield return null;
                _xpBarImage.fillAmount = 1f;
            }

            if (_xpText != null)
                _xpText.text = "LEVEL UP!";
            yield return new WaitForSecondsRealtime(_levelUpHoldDuration);

            if (_xpBarImage != null)
                _xpBarImage.fillAmount = 0f;

            if (after.IsAtLevelCap)
            {
                ShowLevelCap();
                yield break;
            }

            _targetXpFill = after.Progress01;

            if (_xpText != null)
                _xpText.text = $"{after.CurrentXp} / {after.XpForNextLevel}";
        }
    }
}