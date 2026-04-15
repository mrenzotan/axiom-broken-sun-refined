using System;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class LootEntry
    {
        [Tooltip("The item dropped if this entry rolls. Null entries are ignored at runtime.")]
        public ItemData item;

        [Tooltip("Drop chance 0.0–1.0. 1.0 = guaranteed.")]
        [Range(0f, 1f)] public float dropChance = 1f;
    }
}
