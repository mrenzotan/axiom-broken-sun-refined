using System.Collections.Generic;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure combustion spell-match logic, shared by BurnableObstacleController and
    /// SteamVentController. Mirrors <see cref="MeltableObstacle"/>.CanMelt.
    /// </summary>
    public static class BurnableObstacle
    {
        public static bool CanIgnite(string spellId, IReadOnlyList<string> igniteSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (igniteSpellIds == null) return false;

            for (int i = 0; i < igniteSpellIds.Count; i++)
            {
                if (igniteSpellIds[i] == spellId) return true;
            }

            return false;
        }
    }
}
