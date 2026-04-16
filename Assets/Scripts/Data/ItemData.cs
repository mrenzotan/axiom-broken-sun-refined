using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Axiom/Data/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Stable string ID used by saves and inventory. Keep unique and immutable once assigned.")]
        public string itemId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [Tooltip("Tooltip / flavor text shown when inspecting the item.")]
        [TextArea(2, 4)] public string description;

        [Tooltip("Category of item — drives which UIs and systems can use it.")]
        public ItemType itemType;

        [Tooltip("Gameplay effect when consumed. None for KeyItem / Equipment placeholders.")]
        public ItemEffectType effectType;

        [Tooltip("Magnitude of the effect (HP restored, MP restored, etc.). Ignored for effectType == None.")]
        public int effectPower;

        [Tooltip("Chemical conditions this item cures on use (e.g. a Salt Bomb curing Frozen, Burning, etc.). Empty for most consumables.")]
        public List<ChemicalCondition> curesConditions = new List<ChemicalCondition>();
    }
}
