using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle.UI
{
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

        [SerializeField]
        [Tooltip("XpBarUI component that drives the XP fill bar and label.")]
        private XpBarUI _xpBar;

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

        public void Show(PostBattleResult result, XpProgress xpBefore, XpProgress xpAfter, int levelsGained = 0)
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

            ShowPanel();

            if (_xpBar != null)
            {
                if (xpAfter.IsAtLevelCap)
                    _xpBar.ShowLevelCap();
                else if (levelsGained > 0)
                    StartCoroutine(_xpBar.AnimateLevelUpFlow(xpBefore, xpAfter));
                else
                    _xpBar.AnimateTo(xpAfter.CurrentXp, xpAfter.XpForNextLevel, xpBefore.Progress01);
            }
        }

        private string ResolveDisplayName(string itemId)
        {
            if (_itemCatalog != null && _itemCatalog.TryGetItem(itemId, out ItemData data))
                return string.IsNullOrEmpty(data.displayName) ? itemId : data.displayName;
            return itemId;
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            OnDismissed?.Invoke();
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }
    }
}
