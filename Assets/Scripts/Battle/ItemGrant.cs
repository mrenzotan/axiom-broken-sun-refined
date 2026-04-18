namespace Axiom.Battle
{
    /// <summary>
    /// A single item awarded by a post-battle loot roll.
    /// Quantity is currently fixed at 1 per rolled <see cref="Axiom.Data.LootEntry"/>;
    /// the field exists so <see cref="Axiom.Core.Inventory.Add(string, int)"/> can be
    /// called directly without a cast.
    /// </summary>
    public readonly struct ItemGrant
    {
        public string ItemId { get; }
        public int    Quantity { get; }

        public ItemGrant(string itemId, int quantity)
        {
            ItemId   = itemId;
            Quantity = quantity;
        }
    }
}
