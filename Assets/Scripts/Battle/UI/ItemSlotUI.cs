using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemSlotUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private Button _button;

        private ItemData _item;
        private Action<ItemData> _onSelect;

        public void Setup(ItemData item, int quantity, Action<ItemData> onSelect)
        {
            _item = item;
            _onSelect = onSelect;
            _nameText.text = item.displayName;
            _quantityText.text = $"x{quantity}";
            _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick() => _onSelect?.Invoke(_item);

        private void OnDestroy() => _button.onClick.RemoveAllListeners();
    }
}
