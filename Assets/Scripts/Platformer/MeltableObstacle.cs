using System.Collections.Generic;

namespace Axiom.Platformer
{
    public static class MeltableObstacle
    {
        public static bool CanMelt(string spellId, IReadOnlyList<string> meltSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (meltSpellIds == null) return false;

            for (int i = 0; i < meltSpellIds.Count; i++)
            {
                if (meltSpellIds[i] == spellId) return true;
            }

            return false;
        }
    }
}
