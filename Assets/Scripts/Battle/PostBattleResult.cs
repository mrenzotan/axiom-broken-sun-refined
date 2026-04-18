using System;
using System.Collections.Generic;

namespace Axiom.Battle
{
    /// <summary>
    /// Immutable output of <see cref="PostBattleOutcomeService.ResolveVictory"/>.
    /// Populated by the service from <see cref="Axiom.Data.EnemyData"/>; consumed by
    /// <see cref="PostBattleFlowController"/> to apply XP and items and drive the
    /// Victory screen.
    /// </summary>
    public readonly struct PostBattleResult
    {
        public int Xp { get; }
        public IReadOnlyList<ItemGrant> Items { get; }

        public PostBattleResult(int xp, IReadOnlyList<ItemGrant> items)
        {
            Xp    = xp;
            Items = items ?? Array.Empty<ItemGrant>();
        }
    }
}
