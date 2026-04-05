using System;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes the four player actions during their turn.
    /// Reads stats from CharacterStats instances injected at construction time.
    /// No MonoBehaviour — BattleController creates and holds this.
    ///
    /// Pass a custom randomSource for deterministic testing; omit for production
    /// (defaults to UnityEngine.Random.value).
    /// </summary>
    public class PlayerActionHandler
    {
        private readonly CharacterStats _playerStats;
        private readonly CharacterStats _enemyStats;
        private readonly Func<float> _randomSource;

        private const float CritChance = 0.2f;
        private const float CritMultiplier = 1.5f;

        public PlayerActionHandler(
            CharacterStats playerStats,
            CharacterStats enemyStats,
            Func<float> randomSource = null)
        {
            _playerStats  = playerStats;
            _enemyStats   = enemyStats;
            _randomSource = randomSource ?? (() => UnityEngine.Random.value);
        }

        /// <summary>
        /// Deals damage to the enemy. Returns AttackResult with damage, crit flag,
        /// and whether the enemy was defeated.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            int baseDamage = Math.Max(1, _playerStats.ATK - _enemyStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _enemyStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _enemyStats.IsDefeated
            };
        }

        /// <summary>Placeholder for Phase 3 voice spell system.</summary>
        public string ExecuteSpell() => "No spells yet.";

        /// <summary>Placeholder for Phase 5 inventory system.</summary>
        public string ExecuteItem() => "No items.";

        /// <summary>
        /// No combat logic for Flee — BattleManager transitions to Fled state.
        /// Intentionally empty. BattleController calls BattleManager.OnPlayerFled() directly.
        /// </summary>
        public void ExecuteFlee() { }
    }
}
