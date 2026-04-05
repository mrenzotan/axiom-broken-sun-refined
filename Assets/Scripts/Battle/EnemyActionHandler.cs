using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes the enemy's attack action during EnemyTurn.
    /// No MonoBehaviour — BattleController creates and holds this.
    /// </summary>
    public class EnemyActionHandler
    {
        private readonly CharacterStats _enemyStats;
        private readonly CharacterStats _playerStats;
        private readonly Func<float> _randomSource;

        private const float CritChance = 0.1f;     // enemies crit less often
        private const float CritMultiplier = 1.5f;

        public EnemyActionHandler(
            CharacterStats enemyStats,
            CharacterStats playerStats,
            Func<float> randomSource = null)
        {
            _enemyStats   = enemyStats;
            _playerStats  = playerStats;
            _randomSource = randomSource ?? (() => UnityEngine.Random.value);
        }

        /// <summary>
        /// Deals damage to the player. Returns AttackResult with damage, crit flag,
        /// defeat flag, and immunity flag.
        /// If the player is Liquid or Vapor (IsPhysicallyImmune), the attack deals 0 damage.
        /// Uses EffectiveATK to respect Crystallized halving on the enemy.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            if (_playerStats.IsPhysicallyImmune)
                return new AttackResult { Damage = 0, IsCrit = false, TargetDefeated = false, IsImmune = true };

            int baseDamage = Math.Max(1, _enemyStats.EffectiveATK - _playerStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _playerStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _playerStats.IsDefeated,
                IsImmune       = false
            };
        }
    }
}
