using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Axiom.Data
{
    [Serializable]
    public class SpellUnlockCondition
    {
        [Tooltip("Minimum player level required to unlock this spell. 0 = story-only (never auto-granted by level). 1 = starter spell (granted at level 1 or above).")]
        [Min(0)] public int requiredLevel = 1;

        [Tooltip("Optional spell that must be unlocked first. Null = no prerequisite.")]
        public SpellData prerequisiteSpell;

        /// <summary>
        /// True when the player's level and unlocked-spell set both satisfy this condition.
        /// A null prerequisite is treated as satisfied.
        /// </summary>
        public bool IsUnlockedFor(int playerLevel, IReadOnlyCollection<string> unlockedSpellNames)
        {
            if (playerLevel < requiredLevel) return false;
            if (prerequisiteSpell == null) return true;
            if (unlockedSpellNames == null) return false;
            return unlockedSpellNames.Contains(prerequisiteSpell.spellName);
        }
    }
}
