using System.Collections.Generic;

namespace Axiom.Platformer
{
    public enum PlayerDeathOutcome
    {
        None,
        RespawnAtLastCheckpoint,
        GameOver,
    }

    public static class PlayerDeathResolver
    {
        public static PlayerDeathOutcome Resolve(
            int currentHp,
            IReadOnlyList<string> activatedCheckpointIds)
        {
            if (currentHp > 0)
                return PlayerDeathOutcome.None;

            if (activatedCheckpointIds == null || activatedCheckpointIds.Count == 0)
                return PlayerDeathOutcome.GameOver;

            return PlayerDeathOutcome.RespawnAtLastCheckpoint;
        }
    }
}
