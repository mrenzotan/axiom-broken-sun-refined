using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# service that owns the player's unlocked-spell set at runtime and
    /// fires <see cref="OnSpellUnlocked"/> each time a new spell is granted.
    ///
    /// Ownership:  <c>GameManager</c> constructs and holds the singleton instance.
    /// Threading:  all methods run on the Unity main thread. The event is invoked synchronously.
    /// De-dupe:    duplicate <see cref="Unlock"/> calls are silently ignored — the event
    ///             only fires the first time a given spell is added.
    /// Persistence: <see cref="UnlockedSpellNames"/> returns the ordered list of spell names
    ///             for <c>SaveData.unlockedSpellIds</c>. <see cref="RestoreFromIds"/> repopulates
    ///             state on load WITHOUT firing <see cref="OnSpellUnlocked"/>.
    /// </summary>
    public sealed class SpellUnlockService
    {
        private readonly SpellCatalog _catalog;
        private readonly List<SpellData> _unlocked = new List<SpellData>();
        private readonly HashSet<string> _unlockedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Fires synchronously on each new unlock, receiving the spell just granted.
        /// Does NOT fire for duplicate <see cref="Unlock"/> calls or during <see cref="RestoreFromIds"/>.
        /// </summary>
        public event Action<SpellData> OnSpellUnlocked;

        public SpellUnlockService(SpellCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>Read-only view of the unlocked spells in the order they were granted.</summary>
        public IReadOnlyList<SpellData> UnlockedSpells => _unlocked;

        /// <summary>Ordered list of spellNames — for SaveData.unlockedSpellIds.</summary>
        public IReadOnlyList<string> UnlockedSpellNames
        {
            get
            {
                string[] names = new string[_unlocked.Count];
                for (int i = 0; i < _unlocked.Count; i++) names[i] = _unlocked[i].spellName;
                return names;
            }
        }

        /// <summary>
        /// Grants the spell and fires <see cref="OnSpellUnlocked"/>.
        /// Returns true on first unlock, false if already unlocked.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="spell"/> is null.</exception>
        public bool Unlock(SpellData spell)
        {
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            return UnlockInternal(spell, fireEvent: true);
        }

        /// <summary>Returns true if the spell is already unlocked. Null returns false.</summary>
        public bool Contains(SpellData spell)
        {
            if (spell == null) return false;
            return _unlockedNames.Contains(spell.spellName);
        }

        /// <summary>
        /// Auto-grants every catalog spell whose <c>unlockCondition.IsUnlockedFor()</c> returns
        /// true given the player's level and the current unlocked set. Story-only spells
        /// (<c>requiredLevel == 0</c>) are excluded — they must be granted via <see cref="Unlock"/>.
        ///
        /// Loops until stable to resolve prerequisite chains: if spell A (level 3, no prereq)
        /// unlocks, spell B (level 3, requires A) becomes eligible on the next pass.
        /// Bounded by catalog size — at most N passes for N spells.
        /// </summary>
        public void NotifyPlayerLevel(int playerLevel)
        {
            bool grantedAny;
            do
            {
                grantedAny = false;
                foreach (SpellData candidate in _catalog.GetUnlocksAtOrBelowLevel(playerLevel))
                {
                    if (_unlockedNames.Contains(candidate.spellName)) continue;
                    if (candidate.unlockCondition != null
                        && !candidate.unlockCondition.IsUnlockedFor(playerLevel, _unlockedNames))
                        continue;

                    if (UnlockInternal(candidate, fireEvent: true))
                        grantedAny = true;
                }
            } while (grantedAny);
        }

        /// <summary>
        /// Rebuilds the unlocked set from a list of persisted spell names
        /// (e.g. <c>SaveData.unlockedSpellIds</c>). Does NOT fire <see cref="OnSpellUnlocked"/>.
        /// Unknown IDs (not present in the catalog) and null/whitespace entries are silently skipped.
        /// Replaces the current set entirely.
        /// </summary>
        public void RestoreFromIds(IEnumerable<string> spellIds)
        {
            _unlocked.Clear();
            _unlockedNames.Clear();

            if (spellIds == null) return;

            foreach (string id in spellIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!_catalog.TryGetByName(id, out SpellData spell)) continue;
                UnlockInternal(spell, fireEvent: false);
            }
        }

        private bool UnlockInternal(SpellData spell, bool fireEvent)
        {
            if (!_unlockedNames.Add(spell.spellName)) return false;
            _unlocked.Add(spell);
            if (fireEvent) OnSpellUnlocked?.Invoke(spell);
            return true;
        }
    }
}
