using System.Collections.Generic;

namespace Axiom.Platformer
{
    public static class BossVictoryChecker
    {
        public static bool IsVictorious(IEnumerable<string> defeatedEnemyIds, string bossEnemyId)
        {
            if (string.IsNullOrWhiteSpace(bossEnemyId))
                return false;

            if (defeatedEnemyIds == null)
                return false;

            foreach (string id in defeatedEnemyIds)
            {
                if (string.Equals(id, bossEnemyId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
