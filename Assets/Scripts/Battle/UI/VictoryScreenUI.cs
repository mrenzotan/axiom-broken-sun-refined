using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Battle-scene panel shown after Victory. Displays XP gained and any items
    /// dropped, then fires <see cref="OnDismissed"/> when the player clicks Confirm.
    /// Driven by <see cref="PostBattleFlowController"/> — this class owns the view only.
    /// </summary>
    public class VictoryScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _xpText;
        [SerializeField] private TextMeshProUGUI _lootText;
        [SerializeField] private Button _confirmButton;

        [SerializeField]
        [Tooltip("Optional: ItemCatalog used to resolve itemId → displayName in the loot list. " +
                 "If unassigned, the raw itemId is shown.")]
        private ItemCatalog _itemCatalog;

        [Header("XP Progress (DEV-76)")]
        [SerializeField]
        [Tooltip("Root GameObject for the XP progress row (label + bar). " +
                 "Stays active in both normal and cap states so the MAX LEVEL label is visible.")]
        private GameObject _xpProgressRoot;

        [SerializeField]
        [Tooltip("TextMeshPro label under _xpProgressRoot. Normal state: '{currentXp} / {xpForNextLevel}'. " +
                 "Cap state: 'MAX LEVEL'.")]
        private TextMeshProUGUI _xpProgressText;

        [SerializeField]
        [Tooltip("Sub-root containing the bar background + fill. Hidden at level cap; " +
                 "visible otherwise so _xpProgressFill can render.")]
        private GameObject _xpProgressBarRoot;

        [SerializeField]
        [Tooltip("Image with Type=Filled (Horizontal) whose fillAmount is driven by XpProgress.Progress01.")]
        private Image _xpProgressFill;

        /// <summary>Fires exactly once when the player clicks the Confirm button.</summary>
        public event Action OnDismissed;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDisable()
        {
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        /// <summary>
        /// Reveals the panel and renders <paramref name="result"/> plus the
        /// post-battle <paramref name="xpProgress"/> snapshot. Call once per battle.
        /// </summary>
        public void Show(PostBattleResult result, XpProgress xpProgress)
        {
            if (_titleText != null)
                _titleText.text = "VICTORY!";

            if (_xpText != null)
                _xpText.text = $"XP  +{result.Xp}";

            if (_lootText != null)
            {
                if (result.Items == null || result.Items.Count == 0)
                {
                    _lootText.text = "No items dropped.";
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Items:");
                    foreach (ItemGrant grant in result.Items)
                    {
                        string display = ResolveDisplayName(grant.ItemId);
                        sb.AppendLine($"  {display} x{grant.Quantity}");
                    }
                    _lootText.text = sb.ToString().TrimEnd();
                }
            }

            RenderXpProgress(xpProgress);

            ShowPanel();
        }

        private void RenderXpProgress(XpProgress xpProgress)
        {
            if (_xpProgressRoot != null) _xpProgressRoot.SetActive(true);

            if (xpProgress.IsAtLevelCap)
            {
                if (_xpProgressText != null) _xpProgressText.text = "MAX LEVEL";
                if (_xpProgressBarRoot != null) _xpProgressBarRoot.SetActive(false);
                return;
            }

            if (_xpProgressBarRoot != null) _xpProgressBarRoot.SetActive(true);
            if (_xpProgressText != null)
                _xpProgressText.text = $"{xpProgress.CurrentXp} / {xpProgress.XpForNextLevel}";
            if (_xpProgressFill != null)
                _xpProgressFill.fillAmount = xpProgress.Progress01;
        }

        private string ResolveDisplayName(string itemId)
        {
            if (_itemCatalog != null && _itemCatalog.TryGetItem(itemId, out ItemData data))
                return string.IsNullOrEmpty(data.displayName) ? itemId : data.displayName;
            return itemId;
        }

        /// <summary>
        /// Deactivates the panel GameObject. The orchestrator
        /// (<see cref="Axiom.Battle.PostBattleFlowController"/>) calls this after it
        /// finishes fading the CanvasGroup out, so the fade stays visible.
        /// </summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            // Do NOT deactivate the panel here — the controller runs a fade on the
            // CanvasGroup first, then calls Hide() when the fade finishes. Hiding
            // here would make the fade invisible.
            OnDismissed?.Invoke();
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }
    }
}
