using System;
using System.Collections.Generic;
using Axiom.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Axiom.Core
{
    /// <summary>
    /// Persistent singleton that survives scene loads and owns the cross-scene PlayerState.
    ///
    /// Access pattern for other systems — store a local reference, never a new static field:
    ///
    ///   private PlayerState _playerState;
    ///
    ///   void Start()
    ///   {
    ///       _playerState = GameManager.Instance.PlayerState;
    ///   }
    ///
    /// Do NOT write: public static GameManager instance; in any other class.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const string DefaultContinueScene = "Platformer";

        public static GameManager Instance { get; private set; }

        [SerializeField]
        [Tooltip("CharacterData for the player. Seeds PlayerState base stats on new game and lazy initialization. Assign CD_Player_Kaelen on the GameManager prefab.")]
        private Axiom.Data.CharacterData _playerCharacterData;

        [SerializeField]
        [Tooltip("Master catalog of every SpellData in the game. Required for level-up spell grants and save-load ID resolution.")]
        private SpellCatalog _spellCatalog;

        [SerializeField]
        [Tooltip("Scene loaded for the opening cutscene when starting a new game.")]
        private string _cutsceneSceneName = "Cutscene";

        private SpellUnlockService _spellUnlockService;

        /// <summary>
        /// Runtime-owned spell unlock service. Lazily constructed on first access so
        /// Edit Mode tests work without Awake running.
        /// Subscribes: BattleVoiceBootstrap listens to OnSpellUnlocked for live grammar rebuild.
        /// </summary>
        public SpellUnlockService SpellUnlockService
        {
            get
            {
                EnsureSpellUnlockService();
                return _spellUnlockService;
            }
        }

        private ProgressionService _progressionService;

        /// <summary>
        /// Runtime-owned progression service. Lazily constructed on first access so
        /// Edit Mode tests work without Awake running.
        /// On level-up it fires OnLevelUp, which GameManager forwards into
        /// <see cref="SpellUnlockService.NotifyPlayerLevel"/> to grant new spells.
        /// </summary>
        public ProgressionService ProgressionService
        {
            get
            {
                EnsureProgressionService();
                return _progressionService;
            }
        }

        private PlayerState _playerState;

        /// <summary>
        /// Lazily creates default state on first access so Edit Mode tests work without Awake running.
        /// </summary>
        public PlayerState PlayerState
        {
            get
            {
                EnsurePlayerState();
                return _playerState;
            }
            private set => _playerState = value;
        }

        /// <summary>
        /// DEV-36 calls this after a victorious battle. Negative amounts throw;
        /// zero is a no-op. Level-up events propagate to <see cref="SpellUnlockService"/>.
        /// </summary>
        public void AwardXp(int amount)
        {
            EnsurePlayerState();
            EnsureProgressionService();
            _progressionService?.AwardXp(amount);
        }

        /// <summary>The scene transition controller on this prefab's child hierarchy.</summary>
        private SceneTransitionController _sceneTransition;
        private SaveService _saveService;
        public SceneTransitionController SceneTransition =>
            _sceneTransition ??= GetComponentInChildren<SceneTransitionController>();

        private AudioManager _audioManager;
        /// <summary>
        /// Public accessor for scene-specific controllers (CutsceneUI, BattleController)
        /// to play dynamic music on the BGM bus.
        /// </summary>
        public AudioManager AudioManager =>
            _audioManager ??= GetComponentInChildren<AudioManager>();

        /// <summary>
        /// Fires after the scene transition fade-in completes.
        /// Subscribers must unsubscribe immediately in their callback to prevent phantom calls
        /// on subsequent transitions.
        /// Raised by RaiseSceneReady(), called by SceneTransitionController.
        /// </summary>
        public event Action OnSceneReady;

        /// <summary>
        /// Set by ExplorationEnemyCombatTrigger before loading the Battle scene.
        /// Consumed and cleared by BattleController.Start() on Battle scene load.
        /// Null when no battle transition is pending (normal state).
        /// </summary>
        public BattleEntry PendingBattle { get; private set; }

        /// <summary>Sets the pending battle context before transitioning to the Battle scene.</summary>
        public void SetPendingBattle(BattleEntry entry) => PendingBattle = entry;

        /// <summary>
        /// Clears the pending battle context after BattleController has consumed it.
        /// Safe to call when PendingBattle is already null.
        /// </summary>
        public void ClearPendingBattle() => PendingBattle = null;

        // Transient (not persisted) — set when the player dies, consumed by the
        // post-respawn FirstDeathPromptController exactly once. No-ops if the player
        // has already seen the first-death prompt (HasSeenFirstDeath == true).
        private bool _firstDeathPromptPending;

        /// <summary>
        /// Called by PlayerDeathHandler immediately before RespawnAtLastCheckpoint.
        /// No-op when HasSeenFirstDeath is already true.
        /// </summary>
        public void NotifyDiedAndRespawning()
        {
            EnsurePlayerState();
            if (_playerState != null && !_playerState.HasSeenFirstDeath)
                _firstDeathPromptPending = true;
        }

        /// <summary>
        /// Called by FirstDeathPromptController in the post-respawn scene.
        /// Returns true at most once per pending death; clears the flag on read.
        /// </summary>
        public bool ConsumeFirstDeathPromptPending()
        {
            bool wasPending = _firstDeathPromptPending;
            _firstDeathPromptPending = false;
            return wasPending;
        }

        /// <summary>
        /// Snapshot of the Platformer world state captured immediately before a battle.
        /// Non-null only between the battle transition and the first Platformer scene restore.
        /// Set by <see cref="SetWorldSnapshot"/>; cleared by PlatformerWorldRestoreController
        /// after restoration completes.
        /// </summary>
        public WorldSnapshot CurrentWorldSnapshot { get; private set; }

        /// <summary>Sets the world snapshot. Replaces any existing snapshot.</summary>
        public void SetWorldSnapshot(WorldSnapshot snapshot) => CurrentWorldSnapshot = snapshot;

        /// <summary>Clears the world snapshot. Safe to call when already null.</summary>
        public void ClearWorldSnapshot() => CurrentWorldSnapshot = null;

        /// <summary>
        /// Enemies defeated in the current playthrough. Populated by BattleController on
        /// Victory; consulted by PlatformerWorldRestoreController to destroy defeated
        /// enemies after a battle-return scene load, preventing them from re-triggering
        /// combat when the player is restored into their prior position (which sits
        /// inside the enemy's trigger).
        /// </summary>
        // Per-scene tracking: a death in one level only wipes that level's defeated set,
        // so backtracking through a previously-cleared level still finds those enemies dead.
        // Scene bucket key is the originating level scene (PlayerState.ActiveSceneName at the
        // moment of the kill), NOT the Battle scene — so writes from BattleController land
        // under the correct level.
        private readonly Dictionary<string, HashSet<string>> _defeatedEnemiesByScene =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Dictionary<string, int>> _damagedEnemyHpByScene =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        private readonly HashSet<string> _collectedPickupIds =
            new HashSet<string>(StringComparer.Ordinal);

        // ── Defeated enemies ────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the given enemy ID has been recorded as defeated in the
        /// player's current originating scene (<see cref="PlayerState.ActiveSceneName"/>).
        /// Use the InScene overload for explicit scene control (e.g. tests).
        /// </summary>
        public bool IsEnemyDefeated(string enemyId) =>
            IsEnemyDefeatedInScene(GetActiveSceneBucket(), enemyId);

        public bool IsEnemyDefeatedInScene(string sceneName, string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return false;
            string key = sceneName ?? string.Empty;
            return _defeatedEnemiesByScene.TryGetValue(key, out HashSet<string> set) && set.Contains(enemyId);
        }

        /// <summary>
        /// Records the enemy as defeated under the player's current originating scene.
        /// Null/empty IDs are silently ignored.
        /// </summary>
        public void MarkEnemyDefeated(string enemyId) =>
            MarkEnemyDefeatedInScene(GetActiveSceneBucket(), enemyId);

        public void MarkEnemyDefeatedInScene(string sceneName, string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            string key = sceneName ?? string.Empty;
            if (!_defeatedEnemiesByScene.TryGetValue(key, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _defeatedEnemiesByScene[key] = set;
            }
            set.Add(enemyId);
        }

        /// <summary>Clears every scene's defeated set. Used by StartNewGame.</summary>
        public void ClearDefeatedEnemies() => _defeatedEnemiesByScene.Clear();

        /// <summary>
        /// Clears only the named scene's defeated set. Used by RespawnAtLastCheckpoint
        /// so dying in level A respawns its enemies but leaves level B's progress intact.
        /// </summary>
        public void ClearDefeatedEnemiesInScene(string sceneName) =>
            _defeatedEnemiesByScene.Remove(sceneName ?? string.Empty);

        /// <summary>
        /// Read-only view of defeated enemies in the given scene. Empty when the scene
        /// has no recorded defeats. Used by BossVictoryTrigger via BossVictoryChecker.
        /// </summary>
        public IEnumerable<string> DefeatedEnemyIdsInScene(string sceneName)
        {
            string key = sceneName ?? string.Empty;
            return _defeatedEnemiesByScene.TryGetValue(key, out HashSet<string> set)
                ? set
                : Array.Empty<string>();
        }

        // ── Damaged enemy HP ────────────────────────────────────────────────

        public int GetDamagedEnemyHp(string enemyId) =>
            GetDamagedEnemyHpInScene(GetActiveSceneBucket(), enemyId);

        public int GetDamagedEnemyHpInScene(string sceneName, string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return -1;
            string key = sceneName ?? string.Empty;
            return _damagedEnemyHpByScene.TryGetValue(key, out Dictionary<string, int> sceneMap) &&
                   sceneMap.TryGetValue(enemyId, out int hp)
                ? hp
                : -1;
        }

        public void SetDamagedEnemyHp(string enemyId, int currentHp) =>
            SetDamagedEnemyHpInScene(GetActiveSceneBucket(), enemyId, currentHp);

        public void SetDamagedEnemyHpInScene(string sceneName, string enemyId, int currentHp)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            string key = sceneName ?? string.Empty;
            if (!_damagedEnemyHpByScene.TryGetValue(key, out Dictionary<string, int> sceneMap))
            {
                sceneMap = new Dictionary<string, int>(StringComparer.Ordinal);
                _damagedEnemyHpByScene[key] = sceneMap;
            }
            sceneMap[enemyId] = currentHp;
        }

        public void ClearDamagedEnemyHp(string enemyId) =>
            ClearDamagedEnemyHpInScene(GetActiveSceneBucket(), enemyId);

        public void ClearDamagedEnemyHpInScene(string sceneName, string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            string key = sceneName ?? string.Empty;
            if (_damagedEnemyHpByScene.TryGetValue(key, out Dictionary<string, int> sceneMap))
                sceneMap.Remove(enemyId);
        }

        /// <summary>Clears every scene's damaged-HP overrides. Used by StartNewGame.</summary>
        public void ClearAllDamagedEnemyHp() => _damagedEnemyHpByScene.Clear();

        /// <summary>Clears one scene's damaged-HP overrides. Used by RespawnAtLastCheckpoint.</summary>
        public void ClearAllDamagedEnemyHpInScene(string sceneName) =>
            _damagedEnemyHpByScene.Remove(sceneName ?? string.Empty);

        private string GetActiveSceneBucket()
        {
            EnsurePlayerState();
            return PlayerState.ActiveSceneName ?? string.Empty;
        }

        public bool IsPickupCollected(string pickupId) =>
            !string.IsNullOrEmpty(pickupId) && _collectedPickupIds.Contains(pickupId);

        public void MarkPickupCollected(string pickupId)
        {
            if (!string.IsNullOrEmpty(pickupId))
                _collectedPickupIds.Add(pickupId);
        }

        public void ClearCollectedPickups() => _collectedPickupIds.Clear();

        public IEnumerable<string> CollectedPickupIds => _collectedPickupIds;

        public void RestoreCollectedPickups(IEnumerable<string> pickupIds)
        {
            _collectedPickupIds.Clear();
            if (pickupIds == null) return;

            foreach (string id in pickupIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _collectedPickupIds.Add(id);
            }
        }

        public bool HasSaveFile() => _saveService != null && _saveService.HasSave();

        public void CaptureWorldSnapshot(Vector2 worldPosition)
        {
            EnsurePlayerState();
            PlayerState.SetWorldPosition(worldPosition.x, worldPosition.y);

            string sceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrWhiteSpace(sceneName))
                PlayerState.SetActiveScene(sceneName);
        }

        public SaveData BuildSaveData()
        {
            EnsurePlayerState();

            string sceneName = PlayerState.ActiveSceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
                sceneName = SceneManager.GetActiveScene().name;

            return new SaveData
            {
                playerLevel = PlayerState.Level,
                playerXp = PlayerState.Xp,
                currentHp = PlayerState.CurrentHp,
                currentMp = PlayerState.CurrentMp,
                maxHp = PlayerState.MaxHp,
                maxMp = PlayerState.MaxMp,
                attack  = PlayerState.Attack,
                defense = PlayerState.Defense,
                speed   = PlayerState.Speed,
                unlockedSpellIds = CopyStringList(PlayerState.UnlockedSpellIds),
                inventory = PlayerState.Inventory.ToSaveEntries(),
                worldPositionX = PlayerState.WorldPositionX,
                worldPositionY = PlayerState.WorldPositionY,
                activeSceneName = sceneName ?? string.Empty,
                activatedCheckpointIds = CopyReadOnlyStringList(PlayerState.ActivatedCheckpointIds),
                lastCheckpointPositionX = PlayerState.LastCheckpointPositionX,
                lastCheckpointPositionY = PlayerState.LastCheckpointPositionY,
                lastCheckpointSceneName = PlayerState.LastCheckpointSceneName ?? string.Empty,
                checkpointLevel   = PlayerState.CheckpointLevel,
                checkpointXp      = PlayerState.CheckpointXp,
                checkpointMaxHp   = PlayerState.CheckpointMaxHp,
                checkpointMaxMp   = PlayerState.CheckpointMaxMp,
                checkpointAttack  = PlayerState.CheckpointAttack,
                checkpointDefense = PlayerState.CheckpointDefense,
                checkpointSpeed   = PlayerState.CheckpointSpeed,
                checkpointUnlockedSpellIds = CopyReadOnlyStringList(PlayerState.CheckpointUnlockedSpellIds),
                defeatedEnemiesPerScene = BuildDefeatedEnemiesPerScene(_defeatedEnemiesByScene),
                damagedEnemyHpPerScene = BuildDamagedEnemyHpPerScene(_damagedEnemyHpByScene),
                collectedPickupIds = CopyHashSet(_collectedPickupIds),
                hasSeenFirstDeath = PlayerState.HasSeenFirstDeath,
                hasSeenFirstSpikeHit = PlayerState.HasSeenFirstSpikeHit,
                hasCompletedFirstBattleTutorial = PlayerState.HasCompletedFirstBattleTutorial,
                hasCompletedSpellTutorialBattle = PlayerState.HasCompletedSpellTutorialBattle,
                hasExplorationMenusUnlocked = PlayerState.ExplorationMenusUnlocked
            };
        }

        public void ApplySaveData(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsurePlayerState();

            int targetMaxHp = data.maxHp > 0 ? data.maxHp : PlayerState.MaxHp;
            int targetMaxMp = data.maxMp >= 0 ? data.maxMp : PlayerState.MaxMp;

            PlayerState.ApplyVitals(targetMaxHp, targetMaxMp, data.currentHp, data.currentMp);
            // Legacy saves (pre-DEV-65) have attack/defense/speed == 0. Treat zero as
            // "field absent" and keep the base stats PlayerState was seeded with from
            // CharacterData. Non-zero values reflect real stored (and possibly grown) stats.
            int targetAttack  = data.attack  > 0 ? data.attack  : PlayerState.Attack;
            int targetDefense = data.defense > 0 ? data.defense : PlayerState.Defense;
            int targetSpeed   = data.speed   > 0 ? data.speed   : PlayerState.Speed;
            PlayerState.ApplyStats(targetAttack, targetDefense, targetSpeed);
            PlayerState.ApplyProgression(data.playerLevel, data.playerXp);
            PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());
            EnsureSpellUnlockService();
            _spellUnlockService?.RestoreFromIds(data.unlockedSpellIds ?? Array.Empty<string>());
            _spellUnlockService?.NotifyPlayerLevel(PlayerState.Level);
            PlayerState.Inventory.LoadFromSaveEntries(data.inventory);
            PlayerState.SetWorldPosition(data.worldPositionX, data.worldPositionY);
            PlayerState.SetActiveScene(data.activeSceneName ?? string.Empty);
            PlayerState.SetActivatedCheckpointIds(data.activatedCheckpointIds ?? Array.Empty<string>());
            PlayerState.SetLastCheckpoint(
                data.lastCheckpointSceneName ?? string.Empty,
                data.lastCheckpointPositionX,
                data.lastCheckpointPositionY);
            PlayerState.SetCheckpointProgression(
                data.checkpointLevel,
                data.checkpointXp,
                data.checkpointMaxHp,
                data.checkpointMaxMp,
                data.checkpointAttack,
                data.checkpointDefense,
                data.checkpointSpeed,
                data.checkpointUnlockedSpellIds ?? Array.Empty<string>());
            RestoreDefeatedEnemiesPerScene(data);
            RestoreDamagedEnemyHpPerScene(data);
            PlayerState.RestoreTutorialFlags(
                data.hasSeenFirstDeath,
                data.hasSeenFirstSpikeHit,
                data.hasCompletedFirstBattleTutorial,
                data.hasCompletedSpellTutorialBattle);
            PlayerState.ExplorationMenusUnlocked = data.hasExplorationMenusUnlocked;
            RestoreCollectedPickups(data.collectedPickupIds);
        }

        public void PersistToDisk()
        {
            EnsureSaveService();
            _saveService.Save(BuildSaveData());
        }

        public bool TryLoadFromDiskIntoGame()
        {
            EnsureSaveService();
            if (!_saveService.TryLoad(out SaveData data))
            {
                Debug.LogWarning("[GameManager] No valid save data found.");
                return false;
            }

            ApplySaveData(data);
            return true;
        }

        public bool TryContinueGame()
        {
            if (!TryLoadFromDiskIntoGame())
                return false;

            string sceneToLoad = string.IsNullOrWhiteSpace(PlayerState.ActiveSceneName)
                ? DefaultContinueScene
                : PlayerState.ActiveSceneName;

            LoadScene(sceneToLoad);
            return true;
        }

        /// <summary>
        /// Loads the scene the player was last in (set by <see cref="CaptureWorldSnapshot"/>).
        /// Used by Flee and Victory to return from Battle to whichever level the encounter
        /// originated in. Falls back to <see cref="DefaultContinueScene"/> when no scene
        /// has been recorded (e.g. standalone Battle scene play).
        /// </summary>
        public void ReturnToWorldScene()
        {
            EnsurePlayerState();
            string sceneToLoad = string.IsNullOrWhiteSpace(PlayerState.ActiveSceneName)
                ? DefaultContinueScene
                : PlayerState.ActiveSceneName;

            LoadScene(sceneToLoad);
        }

        /// <summary>
        /// Heals the player to full and loads the scene of the most recently touched save
        /// point, teleporting to its position. Returns false when no checkpoint has been
        /// activated this playthrough — caller is responsible for the game-over fallback.
        /// Used by both pit-death (PlayerDeathHandler) and battle defeat (PostBattleFlowController).
        /// </summary>
        public bool RespawnAtLastCheckpoint(TransitionStyle style)
        {
            EnsurePlayerState();
            if (PlayerState.ActivatedCheckpointIds.Count == 0)
                return false;

            string sceneToLoad = string.IsNullOrWhiteSpace(PlayerState.LastCheckpointSceneName)
                ? PlayerState.ActiveSceneName
                : PlayerState.LastCheckpointSceneName;

            if (string.IsNullOrWhiteSpace(sceneToLoad))
                return false;

            // Roll back progression to the checkpoint snapshot — Level/XP/stats/spells.
            // Heal afterwards so MaxHp/MaxMp from the snapshot drive the full-heal target.
            PlayerState.RestoreCheckpointProgression();
            EnsureSpellUnlockService();
            _spellUnlockService?.RestoreFromIds(PlayerState.UnlockedSpellIds);
            _spellUnlockService?.NotifyPlayerLevel(PlayerState.Level);

            PlayerState.SetCurrentHp(PlayerState.MaxHp);
            PlayerState.SetCurrentMp(PlayerState.MaxMp);
            PlayerState.SetWorldPosition(
                PlayerState.LastCheckpointPositionX,
                PlayerState.LastCheckpointPositionY);
            PlayerState.SetActiveScene(sceneToLoad);

            // Stale per-encounter state from the battle that just ended (or that the
            // player was about to fight before falling in a pit) must not bleed into
            // the respawn scene's restore step.
            ClearPendingBattle();
            ClearWorldSnapshot();

            // Dying restarts the checkpoint scene only — defeated/damaged enemies in
            // other scenes the player has already cleared stay defeated.
            ClearDefeatedEnemiesInScene(sceneToLoad);
            ClearAllDamagedEnemyHpInScene(sceneToLoad);

            PersistToDisk();
            LoadScene(sceneToLoad, style);
            return true;
        }

        /// <summary>
        /// Records the most recently touched save point's position and current scene as
        /// the respawn destination, AND snapshots progression (Level/XP/stats/unlocked
        /// spells) so a later death rolls those back too — preventing XP-farm loops.
        /// Persists immediately so Continue from MainMenu and post-defeat respawn share
        /// the same source of truth.
        /// </summary>
        public void SetLastCheckpoint(float positionX, float positionY)
        {
            EnsurePlayerState();
            PlayerState.SetLastCheckpoint(
                SceneManager.GetActiveScene().name,
                positionX,
                positionY);
            PlayerState.CaptureCheckpointProgression();
            EnsureSaveService();
            _saveService.Save(BuildSaveData());
        }

        /// <summary>
        /// Resets all player state to new-game defaults and loads the first scene.
        /// Called by MainMenuUI when the player begins a fresh playthrough.
        /// </summary>
        public void StartNewGame()
        {
            if (_playerCharacterData == null)
            {
                Debug.LogError(
                    "[GameManager] _playerCharacterData is not assigned. Cannot start a new game without base stats.",
                    this);
                return;
            }

            PlayerState = new PlayerState(
                maxHp:   _playerCharacterData.baseMaxHP,
                maxMp:   _playerCharacterData.baseMaxMP,
                attack:  _playerCharacterData.baseATK,
                defense: _playerCharacterData.baseDEF,
                speed:   _playerCharacterData.baseSPD);

            // Rebuild ProgressionService against the fresh PlayerState.
            if (_progressionService != null)
                _progressionService.OnLevelUp -= HandleLevelUp;
            _progressionService = null;
            EnsureProgressionService();

            ClearPendingBattle();
            ClearWorldSnapshot();
            ClearDefeatedEnemies();
            ClearAllDamagedEnemyHp();
            ClearCollectedPickups();

            EnsureSaveService();
            _saveService.DeleteSave();

            EnsureSpellUnlockService();
            if (_spellUnlockService != null)
            {
                // Reset the service by restoring from an empty list, then grant level-1 starters.
                _spellUnlockService.RestoreFromIds(Array.Empty<string>());
                _spellUnlockService.NotifyPlayerLevel(PlayerState.Level);
            }

            LoadScene(_cutsceneSceneName);
        }

        /// <summary>
        /// Attempts to perform a one-time full HP/MP restore for the given checkpoint.
        /// Returns false for invalid ID or already-activated checkpoint.
        /// On success, heals to max, marks activated, and persists to disk.
        /// </summary>
        public bool TryActivateCheckpointRegen(string checkpointId, out int healedHp, out int healedMp)
        {
            healedHp = 0;
            healedMp = 0;

            if (string.IsNullOrWhiteSpace(checkpointId))
                return false;

            EnsurePlayerState();

            if (PlayerState.HasActivatedCheckpoint(checkpointId))
                return false;

            healedHp = Math.Max(0, PlayerState.MaxHp - PlayerState.CurrentHp);
            healedMp = Math.Max(0, PlayerState.MaxMp - PlayerState.CurrentMp);

            PlayerState.SetCurrentHp(PlayerState.MaxHp);
            PlayerState.SetCurrentMp(PlayerState.MaxMp);
            PlayerState.MarkCheckpointActivated(checkpointId);
            PersistToDisk();

            return true;
        }

#if UNITY_INCLUDE_TESTS
        public void SetSaveServiceForTests(SaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        /// <summary>
        /// Test-only injector for the player CharacterData. Must be called after
        /// <c>AddComponent&lt;GameManager&gt;()</c> and before the first <see cref="PlayerState"/>
        /// access so the lazy <see cref="EnsurePlayerState"/> path can read base stats.
        /// </summary>
        public void SetPlayerCharacterDataForTests(Axiom.Data.CharacterData characterData)
        {
            _playerCharacterData = characterData
                ?? throw new ArgumentNullException(nameof(characterData));
        }
#endif

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
            EnsureSaveService();
        }

        private void OnDestroy()
        {
            if (_spellUnlockService != null)
                _spellUnlockService.OnSpellUnlocked -= HandleSpellUnlocked;

            if (_progressionService != null)
                _progressionService.OnLevelUp -= HandleLevelUp;

            if (Instance == this)
                Instance = null;
        }

        private void EnsurePlayerState()
        {
            if (_playerState != null) return;
            if (_playerCharacterData == null)
            {
                Debug.LogError(
                    "[GameManager] _playerCharacterData is not assigned. Assign CD_Player_Kaelen on the GameManager prefab.",
                    this);
                return;
            }

            _playerState = new PlayerState(
                maxHp:   _playerCharacterData.baseMaxHP,
                maxMp:   _playerCharacterData.baseMaxMP,
                attack:  _playerCharacterData.baseATK,
                defense: _playerCharacterData.baseDEF,
                speed:   _playerCharacterData.baseSPD);
        }

        private void EnsureSaveService()
        {
            _saveService ??= new SaveService();
        }

        private void EnsureSpellUnlockService()
        {
            if (_spellUnlockService != null) return;
            if (_spellCatalog == null) return; // No catalog assigned — skip silently (Edit Mode tests, isolated scenes).

            _spellUnlockService = new SpellUnlockService(_spellCatalog);
            _spellUnlockService.OnSpellUnlocked += HandleSpellUnlocked;
        }

        private void HandleSpellUnlocked(SpellData _)
        {
            // Mirror the unlocked set into PlayerState so SaveData round-trips correctly.
            EnsurePlayerState();
            PlayerState.SetUnlockedSpellIds(_spellUnlockService.UnlockedSpellNames);
        }

        private void EnsureProgressionService()
        {
            if (_progressionService != null) return;
            if (_playerCharacterData == null) return; // Edit Mode tests with no CharacterData — skip silently.

            EnsurePlayerState();
            if (_playerState == null) return;

            _progressionService = new ProgressionService(_playerState, _playerCharacterData);
            _progressionService.OnLevelUp += HandleLevelUp;
        }

        private void HandleLevelUp(LevelUpResult result)
        {
            EnsureSpellUnlockService();
            _spellUnlockService?.NotifyPlayerLevel(result.NewLevel);
        }

        /// <summary>
        /// Single entry point for scene loads from GameManager. Uses DEV-34
        /// <see cref="SceneTransitionController"/> when present; otherwise falls back to an immediate load.
        /// </summary>
        private void LoadScene(string sceneName, TransitionStyle style = TransitionStyle.BlackFade)
        {
            if (!Application.isPlaying)
                return;

            SceneTransitionController transition = SceneTransition;
            if (transition != null)
                transition.BeginTransition(sceneName, style);
            else
                SceneManager.LoadScene(sceneName);
        }

        private static string[] CopyStringList(List<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            values.CopyTo(copy, 0);
            return copy;
        }

        private static string[] CopyReadOnlyStringList(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        private static string[] CopyHashSet(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            values.CopyTo(copy);
            return copy;
        }

        /// <summary>
        /// Called by SceneTransitionController after the fade-in completes.
        /// Notifies all subscribers that the scene is ready for initialization.
        /// </summary>
        public void RaiseSceneReady() => OnSceneReady?.Invoke();

        private static DefeatedEnemiesSceneEntry[] BuildDefeatedEnemiesPerScene(
            Dictionary<string, HashSet<string>> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<DefeatedEnemiesSceneEntry>();

            var entries = new DefeatedEnemiesSceneEntry[source.Count];
            int i = 0;
            foreach (var kvp in source)
            {
                string[] ids = new string[kvp.Value.Count];
                kvp.Value.CopyTo(ids);
                entries[i++] = new DefeatedEnemiesSceneEntry { sceneName = kvp.Key, enemyIds = ids };
            }
            return entries;
        }

        private static DamagedEnemyHpSceneEntry[] BuildDamagedEnemyHpPerScene(
            Dictionary<string, Dictionary<string, int>> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<DamagedEnemyHpSceneEntry>();

            var entries = new DamagedEnemyHpSceneEntry[source.Count];
            int i = 0;
            foreach (var kvp in source)
            {
                var perScene = new EnemyHpSaveEntry[kvp.Value.Count];
                int j = 0;
                foreach (var hp in kvp.Value)
                    perScene[j++] = new EnemyHpSaveEntry { enemyId = hp.Key, currentHp = hp.Value };
                entries[i++] = new DamagedEnemyHpSceneEntry { sceneName = kvp.Key, entries = perScene };
            }
            return entries;
        }

        private void RestoreDefeatedEnemiesPerScene(SaveData data)
        {
            _defeatedEnemiesByScene.Clear();
            if (data == null) return;

            if (data.defeatedEnemiesPerScene != null)
            {
                foreach (DefeatedEnemiesSceneEntry sceneEntry in data.defeatedEnemiesPerScene)
                {
                    string scene = sceneEntry.sceneName ?? string.Empty;
                    if (sceneEntry.enemyIds == null) continue;

                    HashSet<string> set = null;
                    foreach (string id in sceneEntry.enemyIds)
                    {
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        if (set == null && !_defeatedEnemiesByScene.TryGetValue(scene, out set))
                        {
                            set = new HashSet<string>(StringComparer.Ordinal);
                            _defeatedEnemiesByScene[scene] = set;
                        }
                        set.Add(id);
                    }
                }
            }

            // Migrate saveVersion 1: flat list goes under whichever scene the save was in.
            if (data.saveVersion < 2 && data.defeatedEnemyIds != null && data.defeatedEnemyIds.Length > 0)
            {
                string scene = string.IsNullOrWhiteSpace(data.activeSceneName)
                    ? string.Empty
                    : data.activeSceneName;
                if (!_defeatedEnemiesByScene.TryGetValue(scene, out HashSet<string> set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    _defeatedEnemiesByScene[scene] = set;
                }
                foreach (string id in data.defeatedEnemyIds)
                    if (!string.IsNullOrWhiteSpace(id))
                        set.Add(id);
            }
        }

        private void RestoreDamagedEnemyHpPerScene(SaveData data)
        {
            _damagedEnemyHpByScene.Clear();
            if (data == null) return;

            if (data.damagedEnemyHpPerScene != null)
            {
                foreach (DamagedEnemyHpSceneEntry sceneEntry in data.damagedEnemyHpPerScene)
                {
                    string scene = sceneEntry.sceneName ?? string.Empty;
                    if (sceneEntry.entries == null) continue;

                    Dictionary<string, int> map = null;
                    foreach (EnemyHpSaveEntry hp in sceneEntry.entries)
                    {
                        if (string.IsNullOrWhiteSpace(hp.enemyId) || hp.currentHp < 0) continue;
                        if (map == null && !_damagedEnemyHpByScene.TryGetValue(scene, out map))
                        {
                            map = new Dictionary<string, int>(StringComparer.Ordinal);
                            _damagedEnemyHpByScene[scene] = map;
                        }
                        map[hp.enemyId] = hp.currentHp;
                    }
                }
            }

            if (data.saveVersion < 2 && data.damagedEnemyHp != null && data.damagedEnemyHp.Length > 0)
            {
                string scene = string.IsNullOrWhiteSpace(data.activeSceneName)
                    ? string.Empty
                    : data.activeSceneName;
                if (!_damagedEnemyHpByScene.TryGetValue(scene, out Dictionary<string, int> map))
                {
                    map = new Dictionary<string, int>(StringComparer.Ordinal);
                    _damagedEnemyHpByScene[scene] = map;
                }
                foreach (EnemyHpSaveEntry hp in data.damagedEnemyHp)
                    if (!string.IsNullOrWhiteSpace(hp.enemyId) && hp.currentHp >= 0)
                        map[hp.enemyId] = hp.currentHp;
            }
        }
    }
}
