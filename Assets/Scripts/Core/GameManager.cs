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

        public PlayerState PlayerState { get; private set; }

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
                activatedCheckpointIds = CopyReadOnlyStringList(PlayerState.ActivatedCheckpointIds)
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

            SceneManager.LoadScene(sceneToLoad);
            return true;
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
            if (PlayerState == null)
                PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
        }

        private void EnsureSaveService()
        {
            _saveService ??= new SaveService();
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
