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
        [Tooltip("Empty Transform marking where the enemy battle visual is spawned. " +
                 "BattleController instantiates EnemyData.battleVisualPrefab as a child of this anchor " +
                 "in Start() and reassigns _enemyAnimator to the spawned instance.")]
        private Transform _enemySpawnAnchor;

        [SerializeField]
        [Tooltip("Seconds to pause after an action so the message log is readable before the next turn begins.")]
        private float _actionDelay = 1f;

        [SerializeField]
        [Tooltip("Assign the ItemCatalog ScriptableObject. Required for the Item action to function.")]
        private Axiom.Data.ItemCatalog _itemCatalog;

        [SerializeField]
        [Tooltip("Assign the ItemMenuUI component from the Battle Canvas.")]
        private ItemMenuUI _itemMenuUI;

        [SerializeField]
        [Tooltip("Assign the PostBattleFlowController component that owns the Victory/Defeat UI flow.")]
        private PostBattleFlowController _postBattleFlow;

        [SerializeField]
        [Tooltip("Assign the SpellListPanelUI component from the Battle Canvas.")]
        private SpellListPanelUI _spellListPanelUI;

        [SerializeField]
        [Tooltip("Optional. When the BattleEntry has a TutorialMode, this controller drives " +
                 "the scripted in-Battle tutorial. Leave unassigned to skip tutorials in standalone testing.")]
        private BattleTutorialController _tutorialController;

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
        /// Fires when the voice spell phase begins and Attack/Item/Flee should be locked.
        /// BattleHUD subscribes to disable those buttons while leaving Spell List consultable (DEV-92).
        /// </summary>
        public event Action OnSpellPhaseEntered;

        /// <summary>
        /// Fires when the voice spell phase ends for any reason (cancel, cast, not recognised,
        /// or state change away from PlayerTurn). BattleHUD subscribes to re-enable buttons (DEV-92).
        /// </summary>
        public event Action OnSpellPhaseExited;

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
        /// Fires when the player explicitly cancels the voice spell phase via
        /// <see cref="CancelSpellPhase"/>. Distinct from <see cref="OnSpellChargeAborted"/>:
        /// charge-aborted only resets the animator, while spell-phase-cancelled also tells
        /// <see cref="SpellInputUI"/> to hide every panel and return the player to the
        /// action menu. Cancel does not fire <see cref="OnSpellNotRecognized"/> — it is
        /// a deliberate opt-out, not an error path.
        /// </summary>
        public event Action OnSpellPhaseCancelled;

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

        // Tracks the enemy's currently displayed visual form. The form follows the enemy's
        // chemistry state (see SyncEnemyFormToConditions) rather than any independent timer.
        private int _currentEnemyForm = 0;

        [SerializeField]
        [Tooltip("Fallback seconds to wait for the enemy's form-change (morph) animation to finish before " +
                 "it acts, used only if the morph clip's Animation Event never fires. Set at or above the " +
                 "morph clip length.")]
        private float _morphDelay = 2f;

        private bool _enemyMorphComplete;

        [SerializeField]
        [Tooltip("Seconds to wait for AnimEvent_OnSpellFire before forcing spell resolution. " +
                 "Set to a value greater than your longest cast animation clip length.")]
        private float _spellFireTimeout = 3f;

        private Coroutine _spellFireTimeoutCoroutine;

        [SerializeField]
        [Tooltip("Background SpriteRenderer in the Battle scene. BattleEnvironmentService sets its sprite and tint based on the player's origin region. Leave unassigned to keep the static background.")]
        private SpriteRenderer _backgroundRenderer;

        private BattleEnvironmentService _environmentService;
        private EnemyVisualSpawner _visualSpawner;
        private BattleTurnProcessor _turnProcessor;

        // EnemyId of the enemy triggering this battle. Propagated from BattleEntry;
        // used on Victory to mark the enemy defeated so the Platformer restore step
        // can destroy it, preventing an infinite re-trigger loop.
        private string _battleEnemyId;
        private int _enemyStartHp = -1;

        // Phase system — tracks the enemy's current phase for HP-based transitions.
        private int _currentEnemyPhase = 1;

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
                if (_tutorialController != null)
                    _tutorialController.Setup(pending.TutorialMode);
            }

            // Apply dynamic battle background from the engagement origin (DEV-80).
            _environmentService = new BattleEnvironmentService();
            _environmentService.Apply(pending?.EnvironmentData, _backgroundRenderer);

            // Play battle music from the per-enemy BattleEnvironmentData.
            AudioClip battleMusic = pending?.EnvironmentData?.BattleMusic;
            if (battleMusic != null)
            {
                GameManager gm = GameManager.Instance;
                gm?.AudioManager?.PlayBgm(battleMusic, 1f);
            }

            // DEV-89: swap the enemy visual GameObject to match the triggering EnemyData.
            // Must run before InitializeFromTransition() so animator-event wiring inside
            // Initialize() hooks the live spawned instance, not the deleted scene placeholder.
            _visualSpawner = new EnemyVisualSpawner();
            _enemyAnimator = _visualSpawner.Spawn(_enemyData, _enemySpawnAnchor, _enemyAnimator);

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
                OnConditionDamageTick  -= _animationService.OnConditionDamageTick;
                OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
                OnSpellChargeStarted   -= _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     -= _animationService.OnSpellCastStarted;
                _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
                _enemyAnimator.OnPhaseChangeComplete -= OnEnemyPhaseChangeComplete;
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

            if (_spellListPanelUI != null)
            {
                _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
                _spellListPanelUI.Hide();
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
                _enemyData != null ? _enemyData.GetInnateConditionsForForm(_currentEnemyForm) : null,
                startHp: enemyStartHp);

            _isProcessingAction   = false;
            SetAwaitingVoiceSpell(false);

            // The enemy starts in form 0; its visual form thereafter follows its chemistry
            // state via SyncEnemyFormToConditions.
            _currentEnemyForm = 0;

            _actionHandler      = new PlayerActionHandler(_playerStats, _enemyStats);
            _enemyActionHandler = new EnemyActionHandler(_enemyStats, _playerStats);
            _resolver           = new SpellEffectResolver();
            _itemResolver       = new ItemEffectResolver();
            _turnProcessor      = new BattleTurnProcessor();
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
                OnConditionDamageTick  += _animationService.OnConditionDamageTick;
                OnCharacterDefeated    += _animationService.OnCharacterDefeated;
                OnSpellChargeStarted   += _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     += _animationService.OnSpellCastStarted;
                _playerAnimator.OnHitFrame += FirePlayerDamageVisuals;
                _enemyAnimator.OnHitFrame  += FireEnemyDamageVisuals;
                _playerAnimator.OnAttackSequenceComplete += OnPlayerSequenceComplete;
                _enemyAnimator.OnAttackSequenceComplete  += OnEnemySequenceComplete;
                _enemyAnimator.OnPhaseChangeComplete += OnEnemyPhaseChangeComplete;
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
            if (_isAwaitingVoiceSpell) return;
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
            StartVoiceSpellPhase();
        }

        public void PlayerSpellList()
        {
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;

            if (_spellListPanelUI != null && _spellListPanelUI.IsVisible)
            {
                _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
                _spellListPanelUI.Hide();
                return;
            }

            if (_spellListPanelUI == null) return;

            var gm = Axiom.Core.GameManager.Instance;
            SpellListPanelLogic logic = gm != null
                ? SpellListPanelLogic.BuildFromSpellUnlockService(gm.SpellUnlockService)
                : null;

            _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
            _spellListPanelUI.OnCloseClicked += HandleSpellPanelClose;

            if (logic == null)
                logic = new SpellListPanelLogic(null);

            _spellListPanelUI.Show(logic);
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
                SetAwaitingVoiceSpell(false);
                _isProcessingAction   = false;
                OnSpellPhaseExited?.Invoke();
                OnSpellCastRejected?.Invoke($"Not enough MP to cast {spell.spellName}.");
                Debug.Log($"[Battle] Spell rejected — insufficient MP for {spell.spellName}.");
                return;
            }

            SetAwaitingVoiceSpell(false);
            OnSpellPhaseExited?.Invoke();
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
                // DEV-86: enemy-side SpellVFX nudge. _enemyData is optional in standalone testing.
                Vector2 enemyOffset = _enemyData != null ? _enemyData.spellVfxOffset : Vector2.zero;
                Vector3 vfxPosition = spell.effectType == SpellEffectType.Damage
                    ? (_enemyAnimator  != null ? _enemyAnimator.transform.position + (Vector3)enemyOffset : Vector3.zero)
                    : (_playerAnimator != null ? _playerAnimator.transform.position : Vector3.zero);
                _spellVfxController.Play(spell, vfxPosition);
            }

            SpellResult result = _resolver.Resolve(spell, _playerStats, _enemyStats);

            switch (result.EffectType)
            {
                case SpellEffectType.Damage:
                    OnDamageDealt?.Invoke(_enemyStats, result.Amount, false);
                    CheckEnemyPhaseTransition();
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

            // If this spell changed the enemy's material condition (e.g. Liquid → Solid via a
            // Freeze reaction), update the enemy's sprite to the form that matches its new state.
            SyncEnemyFormToConditions();

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
            SetAwaitingVoiceSpell(false);
            _isProcessingAction   = false;
            OnSpellPhaseExited?.Invoke();
            OnSpellNotRecognized?.Invoke();
            OnSpellChargeAborted?.Invoke();
        }

        /// <summary>
        /// Called by <see cref="Axiom.Voice.SpellCastController"/> when Vosk returns a final
        /// result with empty text (e.g. PTT released without speaking, or first-call
        /// mic-activation latency on macOS leaving no captured audio). Stays in the voice
        /// spell phase so the player can simply press PTT again without re-clicking Spell.
        /// No-op outside the voice spell phase or outside PlayerTurn.
        /// </summary>
        public void NotifyVoiceResultEmpty()
        {
            if (!_isAwaitingVoiceSpell) return;
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;
            // Intentionally do NOT reset _isAwaitingVoiceSpell or fire OnSpellChargeAborted —
            // an empty result is "the player is still preparing", not "abort spell phase".
        }

        /// <summary>
        /// Aborts the voice spell phase at the player's request, returning to the action
        /// menu without consuming the turn or any MP. Fires <see cref="OnSpellChargeAborted"/>
        /// so the player animator returns to Idle, and <see cref="OnSpellPhaseCancelled"/>
        /// so <see cref="SpellInputUI"/> hides every panel.
        ///
        /// No-op outside the voice spell phase or outside <see cref="BattleState.PlayerTurn"/> —
        /// satisfying the DEV-91 AC that the cancel input does nothing when pressed from the
        /// action menu or during the enemy's turn.
        /// </summary>
        public void CancelSpellPhase()
        {
            if (!_isAwaitingVoiceSpell) return;
            if (_battleManager.CurrentState != BattleState.PlayerTurn) return;

            SetAwaitingVoiceSpell(false);
            _isProcessingAction   = false;
            OnSpellPhaseExited?.Invoke();
            // UI panel hide fires before animator reset so any OnSpellChargeAborted
            // subscriber sees the post-hide panel state.
            OnSpellPhaseCancelled?.Invoke();
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
            if (_isAwaitingVoiceSpell) return;

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
            if (_isAwaitingVoiceSpell) return;
            _battleManager.OnPlayerFled();
        }

        private void HandleSpellPanelClose()
        {
            if (_spellListPanelUI != null)
            {
                _spellListPanelUI.Hide();
                _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void HandleStateChanged(BattleState state)
        {
            Debug.Log($"[Battle] → {state}");
            OnBattleStateChanged?.Invoke(state);

            if (_isAwaitingVoiceSpell && state != BattleState.PlayerTurn)
            {
                SetAwaitingVoiceSpell(false);
                _isProcessingAction = false;
                OnSpellPhaseExited?.Invoke();
            }

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
                if (GameManager.Instance != null)
                    GameManager.Instance.ReturnToWorldScene();
                else
                    SceneManager.LoadScene("Platformer"); // Standalone Battle scene testing fallback
            }
            else if (state == BattleState.Victory)
            {
                SyncBattleHpToPlayerState();

                if (_postBattleFlow != null)
                {
                    _postBattleFlow.BeginVictoryFlow(_enemyData, _battleEnemyId);
                }
                else
                {
                    // Standalone Battle scene fallback — no orchestrator in the scene.
                    // Preserves the pre-DEV-36 direct-transition behaviour so isolated
                    // scene testing still completes.
                    if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    {
                        GameManager.Instance.MarkEnemyDefeated(_battleEnemyId);
                        GameManager.Instance.ClearDamagedEnemyHp(_battleEnemyId);
                    }
                    GameManager.Instance?.PersistToDisk();
                    if (GameManager.Instance != null)
                        GameManager.Instance.ReturnToWorldScene();
                    else
                        SceneManager.LoadScene("Platformer");
                }
            }
            else if (state == BattleState.Defeat)
            {
                SyncBattleHpToPlayerState();
                if (GameManager.Instance != null && !string.IsNullOrEmpty(_battleEnemyId))
                    GameManager.Instance.SetDamagedEnemyHp(_battleEnemyId, _enemyStats.CurrentHP);

                if (_postBattleFlow != null)
                    _postBattleFlow.BeginDefeatFlow();
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

        private void StartVoiceSpellPhase()
        {
            SetAwaitingVoiceSpell(true);
            OnSpellPhaseEntered?.Invoke();
            OnSpellChargeStarted?.Invoke();
            OnSpellPhaseStarted?.Invoke();
        }

        /// <summary>
        /// Single mutation point for <see cref="_isAwaitingVoiceSpell"/>. Mirrors the value
        /// onto <see cref="GameManager.SuppressPauseToggle"/> so the pause menu's Esc/Start
        /// poll yields to the voice spell phase. No-op when GameManager is absent (Edit
        /// Mode tests, standalone Battle scene without GameManager).
        /// </summary>
        private void SetAwaitingVoiceSpell(bool value)
        {
            _isAwaitingVoiceSpell = value;
            if (GameManager.Instance != null)
                GameManager.Instance.SuppressPauseToggle = value;
        }

        private void ProcessPlayerTurnStart()
        {
            ConditionTurnResult result = _turnProcessor.ProcessPlayerTurnStart(_playerStats);
            if (result.TotalDamageDealt > 0)
                OnConditionDamageTick?.Invoke(_playerStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

            if (_playerStats.IsDefeated)
            {
                OnCharacterDefeated?.Invoke(_playerStats);
                _battleManager.OnPlayerDefeatedByCondition();
                OnConditionsChanged?.Invoke(_playerStats);
                OnConditionsChanged?.Invoke(_enemyStats);
                return;
            }

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
            ConditionTurnResult result = _turnProcessor.ProcessEnemyTurnStart(_enemyStats);
            if (result.TotalDamageDealt > 0)
                OnConditionDamageTick?.Invoke(_enemyStats, result.TotalDamageDealt, Axiom.Data.ChemicalCondition.None);

            // Check if enemy should transition to a new phase based on HP (including condition damage)
            CheckEnemyPhaseTransition();

            // A material transformation may have expired this turn (e.g. Solid → Liquid as a
            // Freeze wears off), so re-sync the enemy's visual form to its chemistry state.
            bool morphStarted = SyncEnemyFormToConditions();

            if (_enemyStats.IsDefeated)
            {
                OnCharacterDefeated?.Invoke(_enemyStats);
                _battleManager.OnEnemyDefeatedByCondition();
                OnConditionsChanged?.Invoke(_enemyStats);
                OnConditionsChanged?.Invoke(_playerStats);
                return;
            }

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
            if (morphStarted) StartCoroutine(PlayMorphThenExecuteEnemyTurn());
            else              ExecuteEnemyTurn();
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

        private void OnEnemyPhaseChangeComplete() => _enemyMorphComplete = true;

        // When the enemy's form changes at the start of its turn (e.g. Solid wearing off →
        // Liquid), let the morph clip play to completion before it moves/attacks, so it never
        // attacks mid-morph. Falls back to _morphDelay if the morph clip's Animation Event is absent.
        private System.Collections.IEnumerator PlayMorphThenExecuteEnemyTurn()
        {
            _enemyMorphComplete = false;
            float elapsed = 0f;
            while (!_enemyMorphComplete && elapsed < _morphDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _enemyMorphComplete = false;
            ExecuteEnemyTurn();
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

            CheckEnemyPhaseTransition();
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
            
            CheckEnemyPhaseTransition();
        }

        /// <summary>
        /// Checks if the enemy's current HP has crossed a phase threshold and updates the phase accordingly.
        /// Called after the enemy takes damage. The animator transitions to the new phase automatically
        /// once the Phase parameter is updated.
        /// </summary>
        private void CheckEnemyPhaseTransition()
        {
            if (_enemyData == null || !_enemyData.isBoss || _enemyData.phaseThresholds.Count == 0) {
                Debug.Log("[Phase] Early exit - enemyData null, not boss, or no thresholds");
                return; // Only bosses have phase transitions; or no phases configured
            }

            float hpPercent = _enemyStats.CurrentHP / (float)_enemyStats.MaxHP * 100f;
            Debug.Log($"[Phase] HP%: {hpPercent}, currentPhase: {_currentEnemyPhase}, thresholds: {_enemyData.phaseThresholds.Count}");

            // Find the highest phase threshold we've crossed
            int nextPhase = _currentEnemyPhase;
            foreach (var threshold in _enemyData.phaseThresholds)
            {
                Debug.Log($"[Phase] Checking threshold: {threshold.phaseNumber} at {threshold.hpPercentageThreshold}%");
                if (hpPercent <= threshold.hpPercentageThreshold && threshold.phaseNumber > nextPhase)
                {
                    nextPhase = threshold.phaseNumber;
                }
            }

            // If phase changed, update the animator
            if (nextPhase != _currentEnemyPhase)
            {
                Debug.Log($"[Phase] Transitioning from phase {_currentEnemyPhase} to phase {nextPhase}");
                _currentEnemyPhase = nextPhase;
                if (_enemyAnimator != null)
                {
                    _enemyAnimator.SetPhase(_currentEnemyPhase);
                    _enemyAnimator.TriggerFormChange();
                }
                else
                {
                    Debug.Log("[Phase] _enemyAnimator is null");
                }
            }
        }

        /// <summary>
        /// Drives the enemy's visual form (Animator Phase) from its current chemistry state, so a
        /// multi-form enemy's sprite reflects its active material condition — Liquid shows the
        /// liquid form, Solid shows the ice form. Plays the morph transition only when the form
        /// actually changes. Single-form enemies and bosses (no formDefinitions) are unaffected.
        ///
        /// The chemistry condition lists are the single source of truth; this method only reads
        /// them and never mutates conditions. It replaces the previous form-swap system, which
        /// ran on a random timer and overwrote chemistry state (the DEV-50/DEV-47 conflict that
        /// broke the Liquid → Freeze → Solid tutorial flow).
        /// </summary>
        private bool SyncEnemyFormToConditions()
        {
            if (_enemyData == null || _enemyAnimator == null) return false;
            if (_enemyData.formDefinitions == null || _enemyData.formDefinitions.Count == 0) return false;

            int targetForm = _enemyData.GetFormIndexForConditions(_enemyStats.ActiveMaterialConditions);
            if (targetForm == _currentEnemyForm) return false;

            _currentEnemyForm = targetForm;
            _enemyAnimator.SetPhase(targetForm);
            _enemyAnimator.TriggerFormChange();

            Debug.Log($"[Form] Enemy visual synced to form {_currentEnemyForm} (driven by chemistry conditions)");
            return true;
        }

        private void OnPlayerSequenceComplete() => _playerSequenceComplete = true;
        private void OnEnemySequenceComplete()  => _enemySequenceComplete  = true;

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSceneReady -= InitializeFromTransition;
                // Clear the suppress flag in case the controller is destroyed mid-spell-phase
                // (e.g. scene transition during a stuck SpellInputPanel) — without this the
                // pause menu would stay yielding in the next scene.
                GameManager.Instance.SuppressPauseToggle = false;
            }

            if (_battleManager != null)
                _battleManager.OnStateChanged -= HandleStateChanged;

            if (_animationService != null)
            {
                OnPlayerActionStarted  -= _animationService.OnPlayerActionStarted;
                OnEnemyActionStarted   -= _animationService.OnEnemyActionStarted;
                OnDamageDealt          -= _animationService.OnDamageDealt;
                OnConditionDamageTick  -= _animationService.OnConditionDamageTick;
                OnCharacterDefeated    -= _animationService.OnCharacterDefeated;
                OnSpellChargeStarted   -= _animationService.OnSpellChargeStarted;
                OnSpellCastStarted     -= _animationService.OnSpellCastStarted;
            }

            if (_playerAnimator != null) _playerAnimator.OnHitFrame -= FirePlayerDamageVisuals;
            if (_enemyAnimator  != null) _enemyAnimator.OnHitFrame  -= FireEnemyDamageVisuals;
            if (_playerAnimator != null) _playerAnimator.OnAttackSequenceComplete -= OnPlayerSequenceComplete;
            if (_enemyAnimator  != null) _enemyAnimator.OnAttackSequenceComplete  -= OnEnemySequenceComplete;
            if (_enemyAnimator  != null) _enemyAnimator.OnPhaseChangeComplete -= OnEnemyPhaseChangeComplete;
            if (_playerAnimator != null) _playerAnimator.OnSpellFireFrame -= FireSpellVisuals;
            if (_playerAnimator != null) OnSpellChargeAborted -= _playerAnimator.TriggerResetCharge;

            if (_itemMenuUI != null)
            {
                _itemMenuUI.OnItemSelected -= HandleItemSelected;
                _itemMenuUI.OnCancelled    -= HandleItemCancelled;
            }

            if (_spellListPanelUI != null)
            {
                _spellListPanelUI.OnCloseClicked -= HandleSpellPanelClose;
            }
        }
    }
}
