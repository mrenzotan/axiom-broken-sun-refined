using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Core
{
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> _items =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public void Add(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (quantity <= 0) return;

            if (_items.TryGetValue(itemId, out int existing))
                _items[itemId] = existing + quantity;
            else
                _items[itemId] = quantity;
        }

        public bool Remove(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (quantity <= 0) return false;
            if (!_items.TryGetValue(itemId, out int existing)) return false;
            if (existing < quantity) return false;

            int remaining = existing - quantity;
            if (remaining <= 0)
                _items.Remove(itemId);
            else
                _items[itemId] = remaining;
            return true;
        }

        public int GetQuantity(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return 0;
            return _items.TryGetValue(itemId, out int qty) ? qty : 0;
        }

        public bool HasItem(string itemId) => GetQuantity(itemId) > 0;

        public IReadOnlyDictionary<string, int> GetAll() => _items;

        public void Clear() => _items.Clear();

        public InventorySaveEntry[] ToSaveEntries()
        {
            if (_items.Count == 0) return Array.Empty<InventorySaveEntry>();

            var entries = new InventorySaveEntry[_items.Count];
            int i = 0;
            foreach (KeyValuePair<string, int> kvp in _items)
                entries[i++] = new InventorySaveEntry { itemId = kvp.Key, quantity = kvp.Value };
            return entries;
        }

        public void LoadFromSaveEntries(InventorySaveEntry[] entries)
        {
            _items.Clear();
            if (entries == null) return;

            foreach (InventorySaveEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.itemId)) continue;
                if (entry.quantity <= 0) continue;
                _items[entry.itemId] = entry.quantity;
            }
        }
    }
}
