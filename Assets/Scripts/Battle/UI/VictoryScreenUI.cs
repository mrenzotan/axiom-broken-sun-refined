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

        /// <summary>Fires exactly once when the player clicks the Confirm button.</summary>
        public event Action OnDismissed;

        public bool IsShowing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            HidePanel();
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
        /// Reveals the panel and renders <paramref name="result"/>. Call once per battle.
        /// </summary>
        public void Show(PostBattleResult result)
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
        }

        private string ResolveDisplayName(string itemId)
        {
            if (_itemCatalog != null && _itemCatalog.TryGetItem(itemId, out ItemData data))
                return string.IsNullOrEmpty(data.displayName) ? itemId : data.displayName;
            return itemId;
        }

        private void OnConfirmClicked()
        {
            HidePanel();
            OnDismissed?.Invoke();
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
