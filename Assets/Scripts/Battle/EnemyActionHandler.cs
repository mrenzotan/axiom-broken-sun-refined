using System;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# class that executes enemy actions during their turn.
    /// Mirrors PlayerActionHandler — no MonoBehaviour, created and held by BattleController.
    ///
    /// On-hit condition proc:
    ///   After dealing damage, if EnemyData.onHitCondition != None and the roll passes,
    ///   the condition is applied to the player (duplicate guard via HasCondition).
    ///   DoT base damage is sourced from SpellEffectResolver.DoTDamageFor() so the values
    ///   stay in one place and are never duplicated.
    ///
    /// Pass a custom randomSource for deterministic testing; omit for production
    /// (defaults to UnityEngine.Random.value).
    /// </summary>
    public class EnemyActionHandler
    {
        private readonly CharacterStats _enemyStats;
        private readonly CharacterStats _playerStats;
        private readonly EnemyData      _enemyData;
        private readonly Func<float>    _randomSource;

        /// <param name="enemyData">
        /// May be null (standalone Battle scene testing without an assigned EnemyData).
        /// When null, on-hit condition procs are skipped.
        /// </param>
        public EnemyActionHandler(
            CharacterStats enemyStats,
            CharacterStats playerStats,
            EnemyData      enemyData    = null,
            Func<float>    randomSource = null)
        {
            _enemyStats   = enemyStats;
            _playerStats  = playerStats;
            _enemyData    = enemyData;
            _randomSource = randomSource ?? (() => UnityEngine.Random.value);
        }

        /// <summary>
        /// Executes the enemy's basic attack against the player.
        ///
        /// Resolution order:
        ///   1. Compute and deal damage: max(1, ATK - playerDEF)
        ///   2. Roll on-hit condition proc if conditions are met (enemy data present,
        ///      condition configured, player survived, roll passes, condition not already active)
        ///   3. Return AttackResult — IsCrit and IsImmune always false for enemy attacks;
        ///      ConditionApplied is None when no proc fired.
        ///
        /// The player has no physical immunity — enemy basic attacks always deal damage.
        /// </summary>
        public AttackResult ExecuteAttack()
        {
            int damage = Math.Max(1, _enemyStats.ATK - _playerStats.DEF);
            _playerStats.TakeDamage(damage);

            ChemicalCondition conditionApplied = ChemicalCondition.None;

            if (!_playerStats.IsDefeated
                && _enemyData != null
                && _enemyData.onHitCondition != ChemicalCondition.None
                && _randomSource() < _enemyData.onHitConditionChance
                && !_playerStats.HasCondition(_enemyData.onHitCondition))
            {
                int baseDamage = SpellEffectResolver.DoTDamageFor(_enemyData.onHitCondition);
                _playerStats.ApplyStatusCondition(_enemyData.onHitCondition, baseDamage);
                conditionApplied = _enemyData.onHitCondition;
            }

            return new AttackResult
            {
                Damage           = damage,
                IsCrit           = false,
                TargetDefeated   = _playerStats.IsDefeated,
                IsImmune         = false,
                ConditionApplied = conditionApplied
            };
        }
    }
}