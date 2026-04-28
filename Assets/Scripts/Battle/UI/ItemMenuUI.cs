using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    public class ItemMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _contentParent;
        [SerializeField] private ItemSlotUI _slotPrefab;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _emptyMessageText;

        public event Action<ItemData> OnItemSelected;
        public event Action OnCancelled;

        private readonly List<ItemSlotUI> _activeSlots = new List<ItemSlotUI>();

        private void Awake()
        {
            _backButton.onClick.AddListener(HandleBack);
            _panel.SetActive(false);
        }

        public void Show(IReadOnlyList<(ItemData item, int quantity)> items)
        {
            ClearSlots();

            if (items == null || items.Count == 0)
            {
                if (_emptyMessageText != null)
                {
                    _emptyMessageText.gameObject.SetActive(true);
                    _emptyMessageText.text = "Inventory Empty";
                }
            }
            else
            {
                if (_emptyMessageText != null)
                    _emptyMessageText.gameObject.SetActive(false);

                foreach ((ItemData item, int quantity) in items)
                {
                    ItemSlotUI slot = Instantiate(_slotPrefab, _contentParent);
                    slot.Setup(item, quantity, HandleSlotClicked);
                    _activeSlots.Add(slot);
                }
            }

            _panel.SetActive(true);
        }

        public void Hide()
        {
            _panel.SetActive(false);
            ClearSlots();
        }

        private void HandleSlotClicked(ItemData item) => OnItemSelected?.Invoke(item);

        private void HandleBack() => OnCancelled?.Invoke();

        private void ClearSlots()
        {
            foreach (ItemSlotUI slot in _activeSlots)
                if (slot != null) Destroy(slot.gameObject);
            _activeSlots.Clear();
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveAllListeners();
            ClearSlots();
        }
    }
}
