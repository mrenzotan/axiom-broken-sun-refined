using System.Collections.Generic;

namespace Axiom.Platformer
{
    public static class FreezablePlatform
    {
        public static bool CanFreeze(string spellId, IReadOnlyList<string> freezeSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (freezeSpellIds == null) return false;

            for (int i = 0; i < freezeSpellIds.Count; i++)
            {
                if (freezeSpellIds[i] == spellId) return true;
            }

            return false;
        }
    }
}
