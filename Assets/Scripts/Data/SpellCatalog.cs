using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Registry of every <see cref="SpellData"/> in the game. Resolves string IDs
    /// (spellName) to <see cref="SpellData"/> assets for save/load round-tripping,
    /// and filters spells by <see cref="SpellUnlockCondition.requiredLevel"/> for level-up grants.
    ///
    /// One asset per project, referenced by <c>GameManager</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellCatalog", menuName = "Axiom/Data/Spell Catalog")]
    public class SpellCatalog : ScriptableObject
    {
        [Tooltip("All SpellData assets in the game. Order irrelevant. Spell names must be unique.")]
        [SerializeField] private SpellData[] _spells = System.Array.Empty<SpellData>();

        public IReadOnlyList<SpellData> AllSpells => _spells;

        /// <summary>
        /// Looks up a spell by its <see cref="SpellData.spellName"/> (lowercase).
        /// Returns false and null when <paramref name="spellName"/> is null, empty,
        /// or not present in the catalog.
        /// </summary>
        public bool TryGetByName(string spellName, out SpellData spell)
        {
            spell = null;
            if (string.IsNullOrWhiteSpace(spellName)) return false;
            if (_spells == null) return false;

            for (int i = 0; i < _spells.Length; i++)
            {
                SpellData candidate = _spells[i];
                if (candidate == null) continue;
                if (candidate.spellName == spellName)
                {
                    spell = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns every spell with <c>unlockCondition.requiredLevel &gt; 0</c> and
        /// <c>unlockCondition.requiredLevel &lt;= level</c>.
        /// Story-only spells (<c>requiredLevel == 0</c>) are excluded — they must be granted via
        /// <c>SpellUnlockService.Unlock(SpellData)</c>.
        /// Note: this filters by level only. Prerequisite checks are handled by
        /// <c>SpellUnlockService.NotifyPlayerLevel</c> using <c>IsUnlockedFor()</c>.
        /// </summary>
        public IEnumerable<SpellData> GetUnlocksAtOrBelowLevel(int level)
        {
            if (_spells == null) yield break;
            for (int i = 0; i < _spells.Length; i++)
            {
                SpellData candidate = _spells[i];
                if (candidate == null) continue;
                int reqLevel = candidate.unlockCondition != null
                    ? candidate.unlockCondition.requiredLevel
                    : 1;
                if (reqLevel <= 0) continue;
                if (reqLevel > level) continue;
                yield return candidate;
            }
        }

        /// <summary>
        /// Test-only hook — lets Edit Mode tests populate the catalog without asset serialization.
        /// </summary>
        internal void SetSpellsForTests(SpellData[] spells) => _spells = spells ?? System.Array.Empty<SpellData>();
    }
}
