using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// MonoBehaviour wrapper for BattleManager, PlayerActionHandler, and EnemyActionHandler.
    /// Handles Unity lifecycle only (Start, OnDestroy).
    ///
    /// Fires UI events so BattleHUD can react without calling into battle logic:
    ///   OnBattleStateChanged — proxies BattleManager.OnStateChanged
    ///   OnDamageDealt        — fires after any attack lands (player or enemy)
    ///   OnCharacterDefeated  — fires when a character's HP reaches zero
    ///
    /// In Phase 4, Initialize() will be called by GameManager on scene load.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Used for standalone Battle scene testing only. Phase 4 will override via GameManager.")]
        private CombatStartState _startState = CombatStartState.Advantaged;

        [SerializeField]
        [Tooltip("Player stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _playerStats = new CharacterStats
            { Name = "Kael", MaxHP = 100, MaxMP = 30, ATK = 12, DEF = 6, SPD = 8 };

        [SerializeField]
        [Tooltip("Enemy stats. Set values in Inspector for Battle scene testing.")]
        private CharacterStats _enemyStats = new CharacterStats
            { Name = "Void Wraith", MaxHP = 60, MaxMP = 0, ATK = 8, DEF = 4, SPD = 5 };

        [SerializeField]
        [Tooltip("Assign the BattleHUD MonoBehaviour from the Battle scene.")]
        private BattleHUD _battleHUD;

        [SerializeField]
        [Tooltip("Assign the SpellInputUI component from the Battle Canvas.")]
        private SpellInputUI _spellInputUI;

        [SerializeField]
        [Tooltip("Attach the PlayerBattleAnimator component from the player GameObject in the Battle scene.")]
        private PlayerBattleAnimator _playerAnimator;

        [SerializeField]
        [Tooltip("Attach the EnemyBattleAnimator component from the enemy GameObject in the Battle scene.")]
        private EnemyBattleAnimator _enemyAnimator;

        [SerializeField]
        [Tooltip("Assign the SpellVFXController from the Battle scene. Leave unassigned to skip VFX/SFX on spell cast.")]
        private SpellVFXController _spellVfxController;

        [SerializeField]
        [Tooltip("EnemyData ScriptableObject for the enemy in this battle. Provides innateConditions for the enemy's CharacterStats. Optional — leave unassigned for standalone testing without conditions.")]
        private Axiom.Data.EnemyData _enemyData;

        [SerializeField]
        [Tooltip("Seconds to pause after an action so the message log is readable before the next turn begins.")]
        private float _actionDelay = 1f;

        // ── UI Events ────────────────────────────────────────────────────────
        /// <summary>Proxies BattleManager.OnStateChanged so BattleHUD can subscribe here.</summary>
        public event Action<BattleState> OnBattleStateChanged;

        /// <summary>
        /// Fires after every attack. Parameters: target CharacterStats, damage dealt, isCrit.
        /// </summary>
        public event Action<CharacterStats, int, bool> OnDamageDealt;

        /// <summary>Fires when a character's HP reaches zero.</summary>
        public event Action<CharacterStats> OnCharacterDefeated;

        /// <summary>Fires at the start of the player's attack, before damage is calculated.</summary>
        public event Action OnPlayerActionStarted;

        /// <summary>Fires at the start of the enemy's attack, before damage is calculated.</summary>
        public event Action OnEnemyActionStarted;

        /// <summary>
        /// Fires when the player selects the Spell action, opening the voice spell phase.
        /// SpellInputUI subscribes to show the PTT prompt panel.
        /// </summary>
        public event Action OnSpellPhaseStarted;

        /// <summary>
        /// Fires when Vosk returns a recognized spell name before execution resolves.
        /// SpellInputUI subscribes to display the spell name briefly.
        /// </summary>
        public event Action<SpellData> OnSpellRecognized;

        /// <summary>
        /// Fires when Vosk returns a final result that does not match any unlocked spell.
        /// SpellInputUI subscribes to show the "Not recognized" error panel.
        /// Raised via <see cref="NotifySpellNotRecognized"/> (called from SpellCastController).
        /// </summary>
        public event Action OnSpellNotRecognized;

        /// <summary>
        /// Fires when a spell cast is rejected because the caster has insufficient MP.
        /// Parameter: the rejection reason message string.
        /// SpellInputUI subscribes to show the rejection message.
        /// </summary>
        public event Action<string> OnSpellCastRejected;

        /// <summary>
        /// Fires when a Heal spell resolves. Parameters: target CharacterStats, HP amount healed.
        /// BattleHUD subscribes to update the HP bar.
        /// </summary>
        public event Action<CharacterStats, int> OnSpellHealed;

        /// <summary>
        /// Fires when a Shield spell resolves. Parameters: target CharacterStats, shield HP amount.
        /// BattleHUD subscribes to display a blue shield number.
        /// </summary>
        public event Action<CharacterStats, int> OnShieldApplied;

        /// <summary>
        /// Fires when a condition DoT ticks at the start of a character's turn.
        /// Parameters: afflicted CharacterStats, damage dealt, the condition that dealt it.
        /// BattleHUD subscribes to show a floating DoT number.
        /// </summary>
        public event Action<CharacterStats, int, Axiom.Data.ChemicalCondition> OnConditionDamageTick;

        /// <summary>
        /// Fires when a physical attack is fully blocked by the target's material state
        /// (Liquid or Vapor — IsPhysicallyImmune). No damage is dealt.
        /// Parameters: attacker CharacterStats, target CharacterStats.
        /// BattleHUD subscribes to show a specific immunity message in the log.
        /// </summary>
        public event Action<CharacterStats, CharacterStats> OnPhysicalAttackImmune;

        /// <summary>
        /// Fires when a character's active condition list may have changed —
        /// after ProcessConditionTurn() ticks conditions, or after a spell applies a new condition.
        /// Parameter: the CharacterStats whose conditions changed.
        /// BattleHUD subscribes to refresh ConditionBadgeUI for the matching character.
        /// </summary>
        public event Action<CharacterStats> OnConditionsChanged;

        /// <summary>
        /// Fires when a character's turn is skipped because they are Frozen.
        /// Parameter: the CharacterStats whose action was skipped.
        /// BattleHUD subscribes to post a "can't move" message in the log.
        /// </summary>
        public event Action<CharacterStats> OnActionSkipped;

        // ── Private fields ───────────────────────────────────────────────────
        private BattleManager _battleManager;
        private PlayerActionHandler _actionHandler;
        private EnemyActionHandler _enemyActionHandler;
        private bool _isProcessingAction;
        private BattleAnimationService _animationService;
        private AttackResult _pendingPlayerAttack;
        private AttackResult _pendingEnemyAttack;
        private bool _playerDamageVisualsFired;
        private bool _enemyDamageVisualsFired;
        private bool _playerSequenceComplete;
        private bool _enemySequenceComplete;
        private bool _isAwaitingVoiceSpell;
        private SpellEffectResolver _resolver;

        private void Start()
        {
            Initialize(_startState);
        }

        /// <summary>
        /// Called by GameManager (Phase 4) to start the battle with the correct start state.
        /// Also called from Start() during isolated Battle scene testing.
        /// </summary>
        public void Initialize(CombatStartState startState)
        {
            if (_animationService != null)
            {
                OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
                OnDamageDealt          -= _animationService.OnDamageDealt;
                OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
                _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
                _animationService = null;
            }

            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            if (_enemyData != null)
            {
                _enemyStats.Name  = _enemyData.enemyName;
                _enemyStats.MaxHP = _enemyData.maxHP;
                _enemyStats.MaxMP = _enemyData.maxMP;
                _enemyStats.ATK   = _enemyData.atk;
                _enemyStats.DEF   = _enemyData.def;
                _enemyStats.SPD   = _enemyData.spd;
            }

            _playerStats.Initialize();
            _enemyStats.Initialize(_enemyData != null ? _enemyData.innateConditions : null);

            _isProcessingAction   = false;
            _isAwaitingVoiceSpell = false;

            _actionHandler      = new PlayerActionHandler(_playerStats, _enemyStats);
            _enemyActionHandler = new EnemyActionHandler(_enemyStats, _playerStats);
            _resolver           = new SpellEffectResolver();
            _battleManager      = new BattleManager();
            _battleManager.OnStateChanged += HandleStateChanged;

            _battleHUD?.Setup(this, _playerStats, _enemyStats);
            _spellInputUI?.Setup(this);

            if (_playerAnimator != null && _enemyAnimator != null)
            {
                _animationService = new BattleAnimationService(
                    _playerStats, _enemyStats,
                    _playerAnimator.TriggerAttack, _playerAnimator.TriggerHurt, _playerAnimator.TriggerDefeat,
                    _enemyAnimator.TriggerAttack,  _enemyAnimator.TriggerHurt,  _enemyAnimator.TriggerDefeat);

                OnPlayerActionStarted  += _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   += _animationService.OnEnemyActionStarted;
                OnDamageDealt          += _animationService.OnDamageDealt;
                OnCharacterDefeated    += _animationService.OnCharacterDefeated;
                _playerAnimator.OnHitFrame += FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  += FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete += OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  += OnEnemySequenceComplete;
            }

            _battleManager.StartBattle(startState);
        }

        // ── Player action methods — wired via ActionMenuUI.OnAttack etc. ─────

        /// <summary>Executes the Attack action. No-op outside PlayerTurn or while an action is processing.</summary>
        public void PlayerAttack()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (_isProcessingAction) return;
            _isProcessingAction = true;

            OnPlayerActionStarted?.Invoke();

            _pendingPlayerAttack      = _actionHandler.ExecuteAttack();
            _playerDamageVisualsFired = false;
            _playerSequenceComplete   = false;

            // Immediate fallback: fire visuals now if animators are not wired (no hit frame will come).
            if (_animationService == null)
                FirePlayerDamageVisuals();

            StartCoroutine(CompletePlayerAction(_pendingPlayerAttack.TargetDefeated));
        }

        private System.Collections.IEnumerator CompletePlayerAction(bool targetDefeated)
        {
            float elapsed = 0f;
            while (!_playerSequenceComplete && elapsed < _actionDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _playerSequenceComplete = false;
            // Safety net: fires if the animation event was never triggered.
            FirePlayerDamageVisuals();
            _playerDamageVisualsFired = false;
            _isProcessingAction = false;
            _battleManager.OnPlayerActionComplete(targetDefeated);
        }

        /// <summary>
        /// Called by ActionMenuUI when the player selects the Spell action.
        /// Enters the voice spell phase: shows the PTT prompt and blocks other actions
        /// until a spell is recognized via <see cref="OnSpellCast"/>.
        /// No-op outside PlayerTurn or while an action is already processing.
        /// </summary>
        public void PlayerSpell()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (_isProcessingAction) return;
            _isProcessingAction   = true;
            _isAwaitingVoiceSpell = true;
            OnSpellPhaseStarted?.Invoke();
        }

        /// <summary>
        /// Called by Axiom.Voice.SpellCastController when a recognized spell name
        /// matches an unlocked spell during the voice spell phase.
        /// Guards against calls outside the voice spell phase or outside PlayerTurn.
        /// Deducts MP first — if insufficient, fires OnSpellCastRejected and lets
        /// the player choose again without advancing the turn.
        /// </summary>
        public void OnSpellCast(SpellData spell)
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (!_isAwaitingVoiceSpell) return;

            if (!_playerStats.SpendMP(spell.mpCost))
            {
                _isAwaitingVoiceSpell = false;
                _isProcessingAction   = false;
                OnSpellCastRejected?.Invoke($"Not enough MP to cast {spell.spellName}.");
                Debug.Log($"[Battle] Spell rejected — insufficient MP for {spell.spellName}.");
                return;
            }

            _isAwaitingVoiceSpell     = false;
            _playerDamageVisualsFired = true;
            OnSpellRecognized?.Invoke(spell);
            _spellVfxController?.Play(spell);

            SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);

            switch (result.EffectType)
            {
                case SpellEffectType.Damage:
                    OnDamageDealt?.Invoke(_enemyStats, result.Amount, false);
                    if (result.TargetDefeated)
                        OnCharacterDefeated?.Invoke(_enemyStats);
                    break;
                case SpellEffectType.Heal:
                    OnSpellHealed?.Invoke(_playerStats, result.Amount);
                    break;
                case SpellEffectType.Shield:
                    OnShieldApplied?.Invoke(_playerStats, result.Amount);
                    break;
            }

            // Conditions on either character may have changed due to the spell.
            OnConditionsChanged?.Invoke(_playerStats);
            OnConditionsChanged?.Invoke(_enemyStats);

            // Zero-damage ping so BattleHUD refreshes MP bar after the spend.
            OnDamageDealt?.Invoke(_playerStats, 0, false);

            Debug.Log($"[Battle] Spell cast: {spell.spellName} → {result.EffectType} {result.Amount}" +
                      $"{(result.ReactionTriggered ? " [REACTION]" : string.Empty)}");

            StartCoroutine(CompletePlayerAction(result.TargetDefeated));
        }

        /// <summary>
        /// Called by <see cref="Axiom.Voice.SpellCastController"/> when Vosk returns a final
        /// result that does not match any unlocked spell during the voice spell phase.
        /// No-op outside the voice spell phase or outside PlayerTurn.
        /// </summary>
        public void NotifySpellNotRecognized()
        {
            if (!_isAwaitingVoiceSpell) return;
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            OnSpellNotRecognized?.Invoke();
        }

        /// <summary>Executes the Item placeholder action. No-op outside PlayerTurn or while an action is processing.</summary>
        public void PlayerItem()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (_isProcessingAction) return;
            _isProcessingAction = true;
            _playerDamageVisualsFired = true; // No damage visuals for item placeholder
            string message = _actionHandler.ExecuteItem();
            Debug.Log($"[Battle] Item: {message}");
            StartCoroutine(CompletePlayerAction(targetDefeated: false));
        }

        /// <summary>Executes Flee. No-op outside PlayerTurn.</summary>
        public void PlayerFlee()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (_isProcessingAction) return;
            _battleManager.OnPlayerFled();
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void HandleStateChanged(BattleState state)
        {
            Debug.Log($"[Battle] → {state}");
            OnBattleStateChanged?.Invoke(state);

            if (state == BattleState.PlayerTurn)
                ProcessPlayerTurnStart();
            else if (state == BattleState.EnemyTurn)
                ProcessEnemyTurnStart();
            else if (state == BattleState.Fled)
                SceneManager.LoadScene("Platformer");
        }

        private void ProcessPlayerTurnStart()
        {
            ConditionTurnResult result = _playerStats.ProcessConditionTurn();
            if (result.TotalDamageDealt > 0)
                OnConditionDamageTick?.Invoke(_playerStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

            if (result.ActionSkipped)
            {
                Debug.Log("[Battle] Player is Frozen — turn skipped.");
                OnActionSkipped?.Invoke(_playerStats);
                _isProcessingAction = true;
                _playerDamageVisualsFired = true;
                StartCoroutine(CompletePlayerAction(targetDefeated: false));
            }

            OnConditionsChanged?.Invoke(_playerStats);
            OnConditionsChanged?.Invoke(_enemyStats);
        }

        private void ProcessEnemyTurnStart()
        {
            ConditionTurnResult result = _enemyStats.ProcessConditionTurn();
            if (result.TotalDamageDealt > 0)
                OnConditionDamageTick?.Invoke(_enemyStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

            if (result.ActionSkipped)
            {
                Debug.Log("[Battle] Enemy is Frozen — turn skipped.");
                OnActionSkipped?.Invoke(_enemyStats);
                _battleManager.OnEnemyActionComplete(false);
                OnConditionsChanged?.Invoke(_enemyStats);
                OnConditionsChanged?.Invoke(_playerStats);
                return;
            }

            OnConditionsChanged?.Invoke(_enemyStats);
            OnConditionsChanged?.Invoke(_playerStats);
            ExecuteEnemyTurn();
        }

        private void ExecuteEnemyTurn()
        {
            OnEnemyActionStarted?.Invoke();

            _pendingEnemyAttack      = _enemyActionHandler.ExecuteAttack();
            _enemyDamageVisualsFired = false;
            _enemySequenceComplete   = false;

            // Immediate fallback: fire visuals now if animators are not wired.
            if (_animationService == null)
                FireEnemyDamageVisuals();

            StartCoroutine(CompleteEnemyAction(_pendingEnemyAttack.TargetDefeated));
        }

        private System.Collections.IEnumerator CompleteEnemyAction(bool targetDefeated)
        {
            float elapsed = 0f;
            while (!_enemySequenceComplete && elapsed < _actionDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _enemySequenceComplete = false;
            FireEnemyDamageVisuals();
            _enemyDamageVisualsFired = false;
            _battleManager.OnEnemyActionComplete(targetDefeated);
        }

        private void FirePlayerDamageVisuals()
        {
            if (_playerDamageVisualsFired) return;
            _playerDamageVisualsFired = true;

            if (_pendingPlayerAttack.IsImmune)
            {
                OnPhysicalAttackImmune?.Invoke(_playerStats, _enemyStats);
                return; // No damage occurred — skip OnDamageDealt to avoid a "0 damage" floating number
            }

            OnDamageDealt?.Invoke(_enemyStats, _pendingPlayerAttack.Damage, _pendingPlayerAttack.IsCrit);
            if (_pendingPlayerAttack.TargetDefeated)
                OnCharacterDefeated?.Invoke(_enemyStats);
        }

        private void FireEnemyDamageVisuals()
        {
            if (_enemyDamageVisualsFired) return;
            _enemyDamageVisualsFired = true;

            if (_pendingEnemyAttack.IsImmune)
            {
                OnPhysicalAttackImmune?.Invoke(_enemyStats, _playerStats);
                return; // No damage occurred — skip OnDamageDealt
            }

            OnDamageDealt?.Invoke(_playerStats, _pendingEnemyAttack.Damage, _pendingEnemyAttack.IsCrit);
            if (_pendingEnemyAttack.TargetDefeated)
                OnCharacterDefeated?.Invoke(_playerStats);
        }

        private void OnPlayerSequenceComplete() => _playerSequenceComplete = true;
        private void OnEnemySequenceComplete()  => _enemySequenceComplete  = true;

        private void OnDestroy()
        {
            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            if (_animationService != null)
            {
                OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
                OnDamageDealt          -= _animationService.OnDamageDealt;
                OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
            }

            if (_playerAnimator != null) _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
            if (_enemyAnimator  != null) _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
            if (_playerAnimator != null) _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
            if (_enemyAnimator  != null) _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
        }
    }
}
