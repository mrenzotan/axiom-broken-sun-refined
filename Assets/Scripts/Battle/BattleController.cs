using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Axiom.Core;
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
        [Tooltip("CharacterData ScriptableObject for the player. Provides base stats at Level 1. Assign CD_Player_Kaelen from Assets/Data/Characters/.")]
        private Axiom.Data.CharacterData _playerData;

        private CharacterStats _playerStats;

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

        [SerializeField]
        [Tooltip("Assign the ItemCatalog ScriptableObject. Required for the Item action to function.")]
        private Axiom.Data.ItemCatalog _itemCatalog;

        [SerializeField]
        [Tooltip("Assign the ItemMenuUI component from the Battle Canvas.")]
        private ItemMenuUI _itemMenuUI;

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
        /// Fires when the player enters the spell charge state (waiting for voice input).
        /// BattleAnimationService subscribes to route this to PlayerBattleAnimator.TriggerCharge().
        /// </summary>
        public event Action OnSpellChargeStarted;

        /// <summary>
        /// Fires when a spell is recognized and the cast animation begins.
        /// BattleAnimationService subscribes to route this to PlayerBattleAnimator.TriggerCast().
        /// </summary>
        public event Action OnSpellCastStarted;

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
        /// Fires when the voice spell phase ends without a cast being dispatched —
        /// either because Vosk returned empty text (silent PTT release) or because
        /// the recognized word did not match any unlocked spell.
        /// <see cref="PlayerBattleAnimator"/> subscribes via <see cref="Initialize"/> to reset IsCharging.
        /// </summary>
        public event Action OnSpellChargeAborted;

        /// <summary>
        /// Fires when a Heal spell resolves. Parameters: target CharacterStats, HP amount healed.
        /// BattleHUD subscribes to update the HP bar.
        /// </summary>
        public event Action<CharacterStats, int> OnSpellHealed;

        /// <summary>
        /// Fires when an item is used in battle. Parameters: target CharacterStats, amount of effect, effect type.
        /// BattleHUD subscribes to show floating numbers and status messages.
        /// </summary>
        public event Action<CharacterStats, int, Axiom.Data.ItemEffectType> OnItemUsed;

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
        private ItemEffectResolver _itemResolver;
        private SpellData   _pendingSpell;
        private SpellResult _pendingSpellResult;

        [SerializeField]
        [Tooltip("Seconds to wait for AnimEvent_OnSpellFire before forcing spell resolution. " +
                 "Set to a value greater than your longest cast animation clip length.")]
        private float _spellFireTimeout = 3f;

        private Coroutine _spellFireTimeoutCoroutine;

        // EnemyId of the enemy triggering this battle. Propagated from BattleEntry;
        // used on Victory to mark the enemy defeated so the Platformer restore step
        // can destroy it, preventing an infinite re-trigger loop.
        private string _battleEnemyId;
        private int _enemyStartHp = -1;

        private void Start()
        {
            var pending = GameManager.Instance?.PendingBattle;
            if (pending != null)
            {
                _startState    = pending.StartState;
                _enemyData     = pending.EnemyData;
                _battleEnemyId = pending.EnemyId;
                _enemyStartHp  = pending.EnemyCurrentHp;
                GameManager.Instance.ClearPendingBattle();
            }

            if (GameManager.Instance?.SceneTransition?.IsTransitioning == true)
                GameManager.Instance.OnSceneReady += InitializeFromTransition;
            else
                InitializeFromTransition();
        }

        private void InitializeFromTransition()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= InitializeFromTransition;
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
                OnSpellChargeStarted   -= _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     -= _animationService.OnSpellCastStarted;
                _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
                _playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
                _animationService = null;
            }

            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            if (_playerAnimator != null)
                OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;

            if (_itemMenuUI != null)
            {
                _itemMenuUI.OnItemSelected -= HandleItemSelected;
                _itemMenuUI.OnCancelled    -= HandleItemCancelled;
            }

            if (_playerData == null)
            {
                Debug.LogError(
                    "[Battle] _playerData is null. Assign CD_Player_Kaelen on the BattleController in the Battle scene.",
                    this);
                return;
            }

            _playerStats = new CharacterStats { Name = _playerData.characterName };

            if (GameManager.Instance != null)
            {
                PlayerState ps = GameManager.Instance.PlayerState;
                if (ps == null)
                {
                    Debug.LogError(
                        "[Battle] GameManager.PlayerState is null — check that CharacterData is assigned on the GameManager prefab.",
                        this);
                    return;
                }
                _playerStats.MaxHP = ps.MaxHp;
                _playerStats.MaxMP = ps.MaxMp;
                _playerStats.ATK   = ps.Attack;
                _playerStats.DEF   = ps.Defense;
                _playerStats.SPD   = ps.Speed;
            }
            else
            {
                // Standalone Battle scene testing (no GameManager in scene).
                _playerStats.MaxHP = _playerData.baseMaxHP;
                _playerStats.MaxMP = _playerData.baseMaxMP;
                _playerStats.ATK   = _playerData.baseATK;
                _playerStats.DEF   = _playerData.baseDEF;
                _playerStats.SPD   = _playerData.baseSPD;
            }

            if (_enemyData != null)
            {
                _enemyStats.Name  = _enemyData.enemyName;
                _enemyStats.MaxHP = _enemyData.maxHP;
                _enemyStats.MaxMP = _enemyData.maxMP;
                _enemyStats.ATK   = _enemyData.atk;
                _enemyStats.DEF   = _enemyData.def;
                _enemyStats.SPD   = _enemyData.spd;
            }

            int? playerStartHp = GameManager.Instance != null
                ? GameManager.Instance.PlayerState.CurrentHp
                : (int?)null;
            int? playerStartMp = GameManager.Instance != null
                ? GameManager.Instance.PlayerState.CurrentMp
                : (int?)null;

            _playerStats.Initialize(startHp: playerStartHp, startMp: playerStartMp);

            int? enemyStartHp = _enemyStartHp >= 0 ? _enemyStartHp : (int?)null;
            _enemyStats.Initialize(
                _enemyData != null ? _enemyData.innateConditions : null,
                startHp: enemyStartHp);

            _isProcessingAction   = false;
            _isAwaitingVoiceSpell = false;

            _actionHandler      = new PlayerActionHandler(_playerStats, _enemyStats);
            _enemyActionHandler = new EnemyActionHandler(_enemyStats, _playerStats);
            _resolver           = new SpellEffectResolver();
            _itemResolver       = new ItemEffectResolver();
            _battleManager      = new BattleManager();
            _battleManager.OnStateChanged += HandleStateChanged;

            _battleHUD?.Setup(this, _playerStats, _enemyStats);
            _spellInputUI?.Setup(this);

            if (_itemMenuUI != null)
            {
                _itemMenuUI.OnItemSelected += HandleItemSelected;
                _itemMenuUI.OnCancelled    += HandleItemCancelled;
            }

            if (_playerAnimator != null && _enemyAnimator != null)
            {
                _animationService = new BattleAnimationService(
                    _playerStats, _enemyStats,
                    _playerAnimator.TriggerAttack, _playerAnimator.TriggerHurt, _playerAnimator.TriggerDefeat,
                    _playerAnimator.TriggerCharge, _playerAnimator.TriggerCast,
                    _enemyAnimator.TriggerAttack,  _enemyAnimator.TriggerHurt,  _enemyAnimator.TriggerDefeat);

                OnPlayerActionStarted  += _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   += _animationService.OnEnemyActionStarted;
                OnDamageDealt          += _animationService.OnDamageDealt;
                OnCharacterDefeated    += _animationService.OnCharacterDefeated;
                OnSpellChargeStarted   += _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     += _animationService.OnSpellCastStarted;
                _playerAnimator.OnHitFrame += FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  += FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete += OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  += OnEnemySequenceComplete;
                _playerAnimator.OnSpellFireFrame += FireSpellVisuals;
                OnSpellChargeAborted += _playerAnimator.TriggerResetCharge;
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
            OnSpellChargeStarted?.Invoke();
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

            _isAwaitingVoiceSpell = false;
            _pendingSpell         = spell;
            _playerDamageVisualsFired = true; // Spell path does not go through FirePlayerDamageVisuals

            // Show the spell name in SpellInputUI during the cast animation.
            OnSpellRecognized?.Invoke(spell);

            OnSpellCastStarted?.Invoke();

            if (_animationService == null)
            {
                FireSpellVisuals();
            }
            else
            {
                // Safety net: if AnimEvent_OnSpellFire never fires (missing animation event or
                // clip interrupted), resolve spell visuals after the timeout so the turn advances.
                _spellFireTimeoutCoroutine = StartCoroutine(SpellFireTimeoutCoroutine());
            }
        }

        private void FireSpellVisuals()
        {
            // Cancel the safety-net timeout — either the animation event fired on time,
            // or the timeout itself called us. Either way the coroutine is no longer needed.
            if (_spellFireTimeoutCoroutine != null)
            {
                StopCoroutine(_spellFireTimeoutCoroutine);
                _spellFireTimeoutCoroutine = null;
            }

            if (_pendingSpell == null) return;
            SpellData spell = _pendingSpell;
            _pendingSpell = null;

            if (_spellVfxController != null)
            {
                Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
                    ? (_enemyAnimator  != null ? _enemyAnimator.transform.position  : Vector3.zero)
                    : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
                _spellVfxController.Play(spell, vfxPosition);
            }

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
            _isAwaitingVoiceSpell = false;
            _isProcessingAction   = false;
            OnSpellNotRecognized?.Invoke();
            OnSpellChargeAborted?.Invoke();
        }

        /// <summary>
        /// Called by <see cref="Axiom.Voice.SpellCastController"/> when Vosk returns a final
        /// result with empty text (e.g. PTT released without speaking). Resets the charge
        /// animation if the player is still in the voice spell phase.
        /// No-op outside the voice spell phase or outside PlayerTurn.
        /// </summary>
        public void NotifyVoiceResultEmpty()
        {
            if (!_isAwaitingVoiceSpell) return;
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            _isAwaitingVoiceSpell = false;
            _isProcessingAction   = false;
            OnSpellChargeAborted?.Invoke();
        }

        /// <summary>
        /// Opens the item selection menu. No-op outside PlayerTurn, while processing, or
        /// when ItemCatalog/ItemMenuUI are not assigned (standalone testing).
        /// If the player's inventory is empty, posts a status message and returns.
        /// </summary>
        public void PlayerItem()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            if (_isProcessingAction) return;

            if (_itemCatalog == null || _itemMenuUI == null)
            {
                Debug.Log("[Battle] Item action unavailable — ItemCatalog or ItemMenuUI not assigned.");
                return;
            }

            var gm = Axiom.Core.GameManager.Instance;
            if (gm == null)
            {
                Debug.Log("[Battle] Item action unavailable — no GameManager.");
                return;
            }

            var availableItems = new System.Collections.Generic.List<(Axiom.Data.ItemData item, int quantity)>();
            foreach (var kvp in gm.PlayerState.Inventory.GetAll())
            {
                if (kvp.Value <= 0) continue;
                if (!_itemCatalog.TryGetItem(kvp.Key, out Axiom.Data.ItemData itemData)) continue;
                if (itemData.itemType != Axiom.Data.ItemType.Consumable) continue;
                availableItems.Add((itemData, kvp.Value));
            }

            if (availableItems.Count == 0)
            {
                Debug.Log("[Battle] No usable items in inventory.");
                return;
            }

            _isProcessingAction = true;
            _itemMenuUI.Show(availableItems);
        }

        private void HandleItemSelected(Axiom.Data.ItemData item)
        {
            _itemMenuUI.Hide();

            var gm = Axiom.Core.GameManager.Instance;
            if (gm == null || item == null)
            {
                _isProcessingAction = false;
                return;
            }

            gm.PlayerState.Inventory.Remove(item.itemId);

            ItemUseResult result = _itemResolver.Resolve(item, _playerStats);

            OnItemUsed?.Invoke(_playerStats, result.Amount, result.EffectType);

            OnConditionsChanged?.Invoke(_playerStats);

            // Zero-damage ping so BattleHUD refreshes HP/MP bars.
            OnDamageDealt?.Invoke(_playerStats, 0, false);

            Debug.Log($"[Battle] Item used: {item.displayName} → {result.EffectType} {result.Amount}");

            _playerDamageVisualsFired = true;
            StartCoroutine(CompletePlayerAction(targetDefeated: false));
        }

        private void HandleItemCancelled()
        {
            _itemMenuUI.Hide();
            _isProcessingAction = false;
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
            {
                SyncBattleHpToPlayerState();
                if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);
                GameManager.Instance?.PersistToDisk();
                if (GameManager.Instance?.SceneTransition != null)
                    GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
                else
                    SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
            }
            else if (state == BattleState.Victory)
            {
                SyncBattleHpToPlayerState();
                // Placeholder — DEV-37 will insert XP/loot screen before this transition.
                if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                {
                    GameManager.Instance.MarkEnemyDefeated(_battleEnemyId);
                    GameManager.Instance.ClearDamagedEnemyHp(_battleEnemyId);
                }
                GameManager.Instance?.PersistToDisk();
                if (GameManager.Instance?.SceneTransition != null)
                    GameManager.Instance.SceneTransition.BeginTransition("Platformer", TransitionStyle.BlackFade);
                else
                    SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
            }
            else if (state == BattleState.Defeat)
            {
                SyncBattleHpToPlayerState();
                if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);
            }
        }

        /// <summary>
        /// Copies the player's current battle HP/MP back into the persistent GameManager.PlayerState.
        /// Called before PersistToDisk on every terminal battle state (Victory, Defeat, Fled).
        /// No-op when GameManager is absent (standalone Battle scene testing).
        /// </summary>
        private void SyncBattleHpToPlayerState()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.PlayerState.SetCurrentHp(_playerStats.CurrentHP);
            GameManager.Instance.PlayerState.SetCurrentMp(_playerStats.CurrentMP);
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

        private System.Collections.IEnumerator SpellFireTimeoutCoroutine()
        {
            yield return new WaitForSeconds(_spellFireTimeout);
            _spellFireTimeoutCoroutine = null;
            Debug.LogWarning(
                "[Battle] AnimEvent_OnSpellFire did not fire within timeout — resolving spell via fallback.",
                this);
            FireSpellVisuals();
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
            if (GameManager.Instance != null)
                GameManager.Instance.OnSceneReady -= InitializeFromTransition;

            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            if (_animationService != null)
            {
                OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
                OnDamageDealt          -= _animationService.OnDamageDealt;
                OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
                OnSpellChargeStarted   -= _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     -= _animationService.OnSpellCastStarted;
            }

            if (_playerAnimator != null) _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
            if (_enemyAnimator  != null) _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
            if (_playerAnimator != null) _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
            if (_enemyAnimator  != null) _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
            if (_playerAnimator != null) _playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
            if (_playerAnimator != null) OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;

            if (_itemMenuUI != null)
            {
                _itemMenuUI.OnItemSelected -= HandleItemSelected;
                _itemMenuUI.OnCancelled    -= HandleItemCancelled;
            }
        }
    }
}
