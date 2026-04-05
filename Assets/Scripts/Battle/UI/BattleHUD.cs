using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Axiom.Battle
{
    /// <summary>
    /// Thin coordinator MonoBehaviour for the Battle scene UI.
    /// Subscribes to BattleController events and delegates to the five UI components.
    ///
    /// Called by BattleController.Initialize() via Setup().
    /// All Inspector references must be assigned in the Battle scene.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        [Header("Enemy Panel")]
        [SerializeField] private HealthBarUI _enemyHealthBar;
        [SerializeField] private TMP_Text    _enemyNameText;

        [Header("Party Panel (single slot for Phase 2)")]
        [SerializeField] private HealthBarUI _partyHealthBar;
        [SerializeField] private TMP_Text    _partyNameText;
        [SerializeField] private RectTransform _partySlotRect;

        [Header("Enemy Slot RectTransform (for floating numbers + arrow)")]
        [SerializeField] private RectTransform _enemySlotRect;

        [Header("Sprite Transforms (for turn indicator)")]
        [SerializeField] private Transform _playerSpriteTransform;
        [SerializeField] private Transform _enemySpriteTransform;

        [Header("UI Components")]
        [SerializeField] private ActionMenuUI          _actionMenuUI;
        [SerializeField] private TurnIndicatorUI       _turnIndicatorUI;
        [SerializeField] private FloatingNumberSpawner _floatingNumberSpawner;
        [SerializeField] private StatusMessageUI       _statusMessageUI;

        // ── Internal state ────────────────────────────────────────────────────
        private BattleController _battleController;
        private CharacterStats   _playerStats;
        private CharacterStats   _enemyStats;

        // Maps CharacterStats → their slot RectTransform for floating numbers.
        private readonly Dictionary<CharacterStats, RectTransform> _statToRect
            = new Dictionary<CharacterStats, RectTransform>();

        /// <summary>
        /// Called by BattleController.Initialize().
        /// Subscribes to events and initialises all UI to starting values.
        /// </summary>
        public void Setup(BattleController battleController, CharacterStats playerStats, CharacterStats enemyStats)
        {
            // Unsubscribe from previous controller if reinitialised mid-session
            if (_battleController != null)
                Unsubscribe();

            _battleController = battleController;
            _playerStats      = playerStats;
            _enemyStats       = enemyStats;

            _statToRect[playerStats] = _partySlotRect;
            _statToRect[enemyStats]  = _enemySlotRect;

            // Wire action menu callbacks to BattleController
            _actionMenuUI.OnAttack = _battleController.PlayerAttack;
            _actionMenuUI.OnSpell  = _battleController.PlayerSpell;
            _actionMenuUI.OnItem   = _battleController.PlayerItem;
            _actionMenuUI.OnFlee   = _battleController.PlayerFlee;

            // Subscribe to battle events
            _battleController.OnBattleStateChanged += HandleStateChanged;
            _battleController.OnDamageDealt        += HandleDamageDealt;
            _battleController.OnCharacterDefeated  += HandleCharacterDefeated;

            // Initialise display
            _enemyNameText.text  = enemyStats.Name;
            _enemyHealthBar.SetHP(enemyStats.CurrentHP, enemyStats.MaxHP);

            _partyNameText.text = playerStats.Name;
            _partyHealthBar.SetHP(playerStats.CurrentHP, playerStats.MaxHP);
            _partyHealthBar.SetMP(playerStats.CurrentMP, playerStats.MaxMP);
        }

        private void OnDestroy() => Unsubscribe();

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleStateChanged(BattleState state)
        {
            bool isPlayerTurn = state == BattleState.PlayerTurn;

            _actionMenuUI.SetInteractable(isPlayerTurn);

            if (state == BattleState.PlayerTurn)
            {
                _turnIndicatorUI.SetActiveTarget(_playerSpriteTransform);
                _statusMessageUI.Post("Your turn.");
            }
            else if (state == BattleState.EnemyTurn)
            {
                _turnIndicatorUI.SetActiveTarget(_enemySpriteTransform);
                _statusMessageUI.Post($"{_enemyStats.Name}'s turn.");
            }
            else if (state == BattleState.Victory)
            {
                _turnIndicatorUI.SetActiveTarget(null);
                _statusMessageUI.Post("Victory!");
            }
            else if (state == BattleState.Defeat)
            {
                _turnIndicatorUI.SetActiveTarget(null);
            }
            else if (state == BattleState.Fled)
            {
                _turnIndicatorUI.SetActiveTarget(null);
            }
        }

        private void HandleDamageDealt(CharacterStats target, int amount, bool isCrit)
        {
            // Update health bar
            if (target == _playerStats)
            {
                _partyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
                _partyHealthBar.SetMP(target.CurrentMP, target.MaxMP);
            }
            else if (target == _enemyStats)
            {
                _enemyHealthBar.SetHP(target.CurrentHP, target.MaxHP);
            }

            // Floating number
            if (_statToRect.TryGetValue(target, out RectTransform rect))
            {
                var numberType = isCrit
                    ? FloatingNumberSpawner.NumberType.Crit
                    : FloatingNumberSpawner.NumberType.Damage;
                _floatingNumberSpawner.Spawn(rect, amount, numberType);
            }

            // Status message
            bool isEnemyAttacking = target == _playerStats;
            string attacker = isEnemyAttacking ? _enemyStats.Name : _playerStats.Name;
            string defender = isEnemyAttacking ? _playerStats.Name : _enemyStats.Name;
            string critTag  = isCrit ? " Critical hit!" : string.Empty;
            _statusMessageUI.Post($"{attacker} attacks! {defender} takes {amount} damage.{critTag}");
        }

        private void HandleCharacterDefeated(CharacterStats character)
        {
            if (character == _playerStats)
            {
                _partyHealthBar.SetHP(0, character.MaxHP);
                _statusMessageUI.Post($"{character.Name} was defeated...");
            }
            else if (character == _enemyStats)
            {
                _enemyHealthBar.SetHP(0, character.MaxHP);
                _statusMessageUI.Post($"{character.Name} was defeated!");
            }
        }

        private void Unsubscribe()
        {
            if (_battleController == null) return;
            _battleController.OnBattleStateChanged -= HandleStateChanged;
            _battleController.OnDamageDealt        -= HandleDamageDealt;
            _battleController.OnCharacterDefeated  -= HandleCharacterDefeated;
        }
    }
}
