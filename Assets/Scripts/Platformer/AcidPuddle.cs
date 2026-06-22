using System;
using System.Collections.Generic;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure spell-match rule for acid puddles: does the spoken spell neutralize this
    /// puddle? Kept separate from the MonoBehaviour so the rule is unit-testable
    /// without a scene. Mirrors <see cref="BurnableObstacle"/>.
    /// </summary>
    public static class AcidPuddle
    {
        public static bool CanNeutralize(string spellId, IReadOnlyList<string> neutralizeSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (neutralizeSpellIds == null) return false;

            for (int i = 0; i < neutralizeSpellIds.Count; i++)
            {
                if (string.Equals(neutralizeSpellIds[i], spellId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
