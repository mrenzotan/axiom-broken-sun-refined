using System;
using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# service that computes post-battle rewards for a Victory outcome.
    /// Rolls <see cref="EnemyData.loot"/> entries independently using the supplied
    /// <see cref="System.Random"/> so Edit Mode tests stay deterministic.
    /// </summary>
    public sealed class PostBattleOutcomeService
    {
        /// <summary>
        /// Builds a <see cref="PostBattleResult"/> from the enemy's XP reward and loot table.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="enemy"/> or <paramref name="random"/> is null.</exception>
        public PostBattleResult ResolveVictory(EnemyData enemy, System.Random random)
        {
            if (enemy == null)  throw new ArgumentNullException(nameof(enemy));
            if (random == null) throw new ArgumentNullException(nameof(random));

            int xp = enemy.xpReward;

            var items = new List<ItemGrant>();
            List<LootEntry> loot = enemy.loot;
            if (loot == null)
                return new PostBattleResult(xp, items);

            for (int i = 0; i < loot.Count; i++)
            {
                LootEntry entry = loot[i];
                if (entry == null) continue;
                if (entry.item == null) continue;
                if (string.IsNullOrWhiteSpace(entry.item.itemId)) continue;
                if (entry.dropChance <= 0f) continue;

                if (random.NextDouble() < entry.dropChance)
                    items.Add(new ItemGrant(entry.item.itemId, 1));
            }

            return new PostBattleResult(xp, items);
        }
    }
}
