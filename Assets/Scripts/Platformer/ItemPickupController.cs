using System;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Platformer
{
    /// <summary>
    /// Plain C# logic for granting an item to the player's inventory.
    /// </summary>
    public sealed class ItemPickupController
    {
        private readonly ItemData _itemData;
        private readonly int _quantity;

        public ItemPickupController(ItemData itemData, int quantity)
        {
            _itemData = itemData ?? throw new ArgumentNullException(nameof(itemData));
            _quantity = quantity > 0 ? quantity : 1;
        }

        public string ItemId => _itemData.itemId;
        public string DisplayName => _itemData.displayName;
        public int Quantity => _quantity;

        public void GrantTo(Inventory inventory)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));

            inventory.Add(_itemData.itemId, _quantity);
        }
    }
}
