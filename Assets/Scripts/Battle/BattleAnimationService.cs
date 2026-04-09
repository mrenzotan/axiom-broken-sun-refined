using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# service that maps BattleController events to character animation delegates.
    /// No Unity API calls — all Animator interaction is handled by the MonoBehaviour adapters
    /// (PlayerBattleAnimator, EnemyBattleAnimator) which inject their trigger methods as Actions.
    /// </summary>
    public class BattleAnimationService
    {
        private readonly CharacterStats _playerStats;
        private readonly CharacterStats _enemyStats;

        private readonly Action _playerAttack;
        private readonly Action _playerHurt;
        private readonly Action _playerDefeat;
        private readonly Action _playerCharge;
        private readonly Action _playerCast;
        private readonly Action _enemyAttack;
        private readonly Action _enemyHurt;
        private readonly Action _enemyDefeat;

        public BattleAnimationService(
            CharacterStats playerStats,
            CharacterStats enemyStats,
            Action playerAttack,
            Action playerHurt,
            Action playerDefeat,
            Action playerCharge,
            Action playerCast,
            Action enemyAttack,
            Action enemyHurt,
            Action enemyDefeat)
        {
            _playerStats  = playerStats;
            _enemyStats   = enemyStats;
            _playerAttack = playerAttack;
            _playerHurt   = playerHurt;
            _playerDefeat = playerDefeat;
            _playerCharge = playerCharge;
            _playerCast   = playerCast;
            _enemyAttack  = enemyAttack;
            _enemyHurt    = enemyHurt;
            _enemyDefeat  = enemyDefeat;
        }

        /// <summary>Call when the player starts an attack action.</summary>
        public void OnPlayerActionStarted() => _playerAttack?.Invoke();

        /// <summary>Call when the player enters the charge animation state (waiting for voice input).</summary>
        public void OnSpellChargeStarted() => _playerCharge?.Invoke();

        /// <summary>Call when a spell is recognized and the cast animation begins.</summary>
        public void OnSpellCastStarted()   => _playerCast?.Invoke();

        /// <summary>Call when the enemy starts an attack action.</summary>
        public void OnEnemyActionStarted() => _enemyAttack?.Invoke();

        /// <summary>
        /// Determines which character was hit and triggers their hurt animation.
        /// Signature matches BattleController.OnDamageDealt.
        /// </summary>
        public void OnDamageDealt(CharacterStats target, int damage, bool isCrit)
        {
            if (damage <= 0) return; // Zero-damage pings (e.g. MP bar refresh) must not trigger hurt.
            if (target == _playerStats) { _playerHurt?.Invoke();  return; }
            if (target == _enemyStats)  { _enemyHurt?.Invoke();   return; }
            // Unknown target — do nothing rather than misfire on the wrong character.
        }

        /// <summary>
        /// Determines which character was defeated and triggers their defeat animation.
        /// Signature matches BattleController.OnCharacterDefeated.
        /// </summary>
        public void OnCharacterDefeated(CharacterStats character)
        {
            if (character == _playerStats) { _playerDefeat?.Invoke(); return; }
            if (character == _enemyStats)  { _enemyDefeat?.Invoke();  return; }
            // Unknown character — do nothing rather than misfire on the wrong character.
        }
    }
}
