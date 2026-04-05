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
        /// defeat flag, and immunity flag.
        /// If the enemy is Liquid or Vapor (IsPhysicallyImmune), the attack deals 0 damage.
        /// Uses EffectiveATK to respect the Crystallized halving condition on the player.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            if (_enemyStats.IsPhysicallyImmune)
                return new AttackResult { Damage = 0, IsCrit = false, TargetDefeated = false, IsImmune = true };

            int baseDamage = Math.Max(1, _playerStats.EffectiveATK - _enemyStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _enemyStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _enemyStats.IsDefeated,
                IsImmune       = false
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
