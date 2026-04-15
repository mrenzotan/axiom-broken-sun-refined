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

        /// <summary>The scene transition controller on this prefab's child hierarchy.</summary>
        private SceneTransitionController _sceneTransition;
        private SaveService _saveService;
        public SceneTransitionController SceneTransition =>
            _sceneTransition ??= GetComponentInChildren<SceneTransitionController>();

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
        private readonly HashSet<string> _defeatedEnemyIds =
            new HashSet<string>(StringComparer.Ordinal);

        public bool IsEnemyDefeated(string enemyId) =>
            !string.IsNullOrEmpty(enemyId) && _defeatedEnemyIds.Contains(enemyId);

        public void MarkEnemyDefeated(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId))
                _defeatedEnemyIds.Add(enemyId);
        }

        public void ClearDefeatedEnemies() => _defeatedEnemyIds.Clear();

        /// <summary>
        /// Read-only view of the defeated-enemy set. Used by BuildSaveData to
        /// project the set into the SaveData DTO. Do not cast and mutate — use
        /// MarkEnemyDefeated / ClearDefeatedEnemies / RestoreDefeatedEnemies.
        /// </summary>
        public IEnumerable<string> DefeatedEnemyIds => _defeatedEnemyIds;

        /// <summary>
        /// Replaces the defeated-enemy set with the provided IDs. Null or whitespace
        /// IDs in the input are skipped. A null input clears the set.
        /// Called on Continue after ApplySaveData to restore cross-session state.
        /// </summary>
        public void RestoreDefeatedEnemies(IEnumerable<string> enemyIds)
        {
            _defeatedEnemyIds.Clear();
            if (enemyIds == null)
                return;

            foreach (string id in enemyIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _defeatedEnemyIds.Add(id);
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
                unlockedSpellIds = CopyStringList(PlayerState.UnlockedSpellIds),
                inventory = BuildInventoryEntries(PlayerState.InventoryItemIds),
                worldPositionX = PlayerState.WorldPositionX,
                worldPositionY = PlayerState.WorldPositionY,
                activeSceneName = sceneName ?? string.Empty,
                activatedCheckpointIds = CopyReadOnlyStringList(PlayerState.ActivatedCheckpointIds),
                defeatedEnemyIds = CopyHashSet(_defeatedEnemyIds)
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
            PlayerState.ApplyProgression(data.playerLevel, data.playerXp);
            PlayerState.SetUnlockedSpellIds(data.unlockedSpellIds ?? Array.Empty<string>());
            PlayerState.SetInventoryItemIds(ExpandInventory(data.inventory));
            PlayerState.SetWorldPosition(data.worldPositionX, data.worldPositionY);
            PlayerState.SetActiveScene(data.activeSceneName ?? string.Empty);
            PlayerState.SetActivatedCheckpointIds(data.activatedCheckpointIds ?? Array.Empty<string>());
            RestoreDefeatedEnemies(data.defeatedEnemyIds);
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
        /// Resets all player state to new-game defaults and loads the first scene.
        /// Called by MainMenuUI when the player begins a fresh playthrough.
        /// </summary>
        public void StartNewGame()
        {
            PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
            ClearPendingBattle();
            ClearWorldSnapshot();
            ClearDefeatedEnemies();

            EnsureSaveService();
            _saveService.DeleteSave();

            LoadScene("Platformer");
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
    EnsurePlayerState();
    EnsureSaveService();
}

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void EnsurePlayerState()
        {
            if (_playerState == null)
                _playerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
        }

        private void EnsureSaveService()
        {
            _saveService ??= new SaveService();
        }

        /// <summary>
        /// Single entry point for scene loads from GameManager. Uses DEV-34
        /// <see cref="SceneTransitionController"/> when present; otherwise falls back to an immediate load.
        /// </summary>
        private void LoadScene(string sceneName)
        {
            if (!Application.isPlaying)
                return;

            SceneTransitionController transition = SceneTransition;
            if (transition != null)
                transition.BeginTransition(sceneName, TransitionStyle.BlackFade);
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

        private static InventorySaveEntry[] BuildInventoryEntries(List<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
                return Array.Empty<InventorySaveEntry>();

            var countsById = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (string itemId in itemIds)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (!countsById.TryGetValue(itemId, out int count))
                {
                    countsById[itemId] = 1;
                    order.Add(itemId);
                    continue;
                }

                countsById[itemId] = count + 1;
            }

            var entries = new InventorySaveEntry[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                string itemId = order[i];
                entries[i] = new InventorySaveEntry
                {
                    itemId = itemId,
                    quantity = countsById[itemId]
                };
            }

            return entries;
        }

        private static IEnumerable<string> ExpandInventory(InventorySaveEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                yield break;

            foreach (InventorySaveEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.itemId))
                    continue;
                if (entry.quantity <= 0)
                    continue;

                for (int i = 0; i < entry.quantity; i++)
                    yield return entry.itemId;
            }
        }
    }
}
