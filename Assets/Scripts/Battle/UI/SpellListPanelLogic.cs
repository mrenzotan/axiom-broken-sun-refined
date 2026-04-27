using System;
using System.Collections.Generic;
using System.Globalization;
using Axiom.Core;
using Axiom.Data;

namespace Axiom.Battle
{
    public class SpellListPanelLogic
    {
        private readonly List<SpellData> _spells;

        public IReadOnlyList<SpellData> Spells => _spells;
        public bool IsEmpty => _spells.Count == 0;

        public IReadOnlyList<string> SpellNames
        {
            get
            {
                var names = new string[_spells.Count];
                for (int i = 0; i < _spells.Count; i++)
                    names[i] = ToDisplayCase(_spells[i].spellName);
                return names;
            }
        }

        public string EmptyMessage => "No spells available";

        public SpellListPanelLogic(List<SpellData> spells)
        {
            _spells = spells != null && spells.Count > 0
                ? new List<SpellData>(spells)
                : new List<SpellData>();
        }

        public static SpellListPanelLogic BuildFromSpellUnlockService(SpellUnlockService service)
        {
            if (service == null) return null;
            var unlocked = service.UnlockedSpells;
            var list = unlocked != null
                ? new List<SpellData>(unlocked)
                : new List<SpellData>();
            return new SpellListPanelLogic(list);
        }

        private static string ToDisplayCase(string spellName)
        {
            if (string.IsNullOrWhiteSpace(spellName)) return string.Empty;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spellName);
        }
    }
}
