using System;
using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewItemCatalog", menuName = "Axiom/Data/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemData[] _items = Array.Empty<ItemData>();

        private Dictionary<string, ItemData> _lookup;

        public IReadOnlyList<ItemData> AllItems => _items;

        public bool TryGetItem(string itemId, out ItemData item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            EnsureLookup();
            return _lookup.TryGetValue(itemId, out item);
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            if (_items == null) return;
            foreach (ItemData data in _items)
            {
                if (data != null && !string.IsNullOrWhiteSpace(data.itemId))
                    _lookup[data.itemId] = data;
            }
        }

        private void OnEnable() => _lookup = null;

#if UNITY_INCLUDE_TESTS
        public void SetItemsForTests(ItemData[] items)
        {
            _items = items ?? Array.Empty<ItemData>();
            _lookup = null;
        }
#endif
    }
}
