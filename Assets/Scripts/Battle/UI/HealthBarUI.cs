using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle
{
    /// <summary>
    /// Drives a gradient-fill HP bar and optional MP bar for one character slot
    /// (PartyMemberSlot or EnemyPanel).
    ///
    /// SetHP / SetMP record a target fill value; Update() lerps the Image fill
    /// smoothly toward it each frame.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image component used as the HP bar fill. Set Image Type to Filled.")]
        private Image _hpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' HP. Optional.")]
        private TMP_Text _hpText;

        [SerializeField]
        [Tooltip("Image component used as the MP bar fill. Null for enemy slots (no MP bar).")]
        private Image _mpBarImage;

        [SerializeField]
        [Tooltip("TMP label showing 'current / max' MP. Null for enemy slots.")]
        private TMP_Text _mpText;

        [SerializeField]
        [Tooltip("Speed at which the bar fill lerps toward its target value (units per second).")]
        private float _lerpSpeed = 5f;

        private float _targetHPFill;
        private float _targetMPFill;

        private void Update()
        {
            if (_hpBarImage != null)
                _hpBarImage.fillAmount = Mathf.Lerp(
                    _hpBarImage.fillAmount, _targetHPFill, Time.deltaTime * _lerpSpeed);

            if (_mpBarImage != null)
                _mpBarImage.fillAmount = Mathf.Lerp(
                    _mpBarImage.fillAmount, _targetMPFill, Time.deltaTime * _lerpSpeed);
        }

        /// <summary>Updates the HP bar fill target and numeric text label.</summary>
        public void SetHP(int current, int max)
        {
            _targetHPFill = max > 0 ? (float)current / max : 0f;
            if (_hpText != null)
                _hpText.text = $"{current} / {max}";
        }

        /// <summary>
        /// Updates the MP bar fill target and numeric text label.
        /// No-op if this slot has no MP bar (e.g. enemy panel).
        /// </summary>
        public void SetMP(int current, int max)
        {
            if (_mpBarImage == null) return;
            _targetMPFill = max > 0 ? (float)current / max : 0f;
            if (_mpText != null)
                _mpText.text = $"{current} / {max}";
        }
    }
}
