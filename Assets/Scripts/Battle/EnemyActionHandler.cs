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
        /// and whether the player was defeated.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            int baseDamage = Math.Max(1, _enemyStats.ATK - _playerStats.DEF);
            bool isCrit    = _randomSource() < CritChance;
            int damage     = isCrit ? (int)(baseDamage * CritMultiplier) : baseDamage;
            _playerStats.TakeDamage(damage);
            return new AttackResult
            {
                Damage         = damage,
                IsCrit         = isCrit,
                TargetDefeated = _playerStats.IsDefeated
            };
        }
    }
}
