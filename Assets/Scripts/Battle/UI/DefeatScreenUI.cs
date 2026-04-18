using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene panel shown on Defeat. Displays a "Defeated…" message and a
    /// Continue button. Fires <see cref="OnContinueClicked"/> when the player accepts.
    /// <see cref="PostBattleFlowController"/> routes the click to
    /// <see cref="Axiom.Core.GameManager.TryContinueGame"/>.
    /// </summary>
    public class DefeatScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _continueButton;

        /// <summary>Fires exactly once when the player clicks Continue.</summary>
        public event Action OnContinueClicked;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            HidePanel();
        }

        private void OnEnable()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClickedInternal);
        }

        private void OnDisable()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(OnContinueClickedInternal);
        }

        /// <summary>
        /// Reveals the panel. Call once per battle on Defeat.
        /// </summary>
        public void Show()
        {
            if (_titleText != null)
                _titleText.text = "DEFEATED";
            ShowPanel();
        }

        private void OnContinueClickedInternal()
        {
            HidePanel();
            OnContinueClicked?.Invoke();
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
