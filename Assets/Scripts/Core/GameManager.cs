using UnityEngine;
using Axiom.Data;

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
        public static GameManager Instance { get; private set; }

        public PlayerState PlayerState { get; private set; }

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

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            PlayerState = new PlayerState(maxHp: 100, maxMp: 50, attack: 10, defense: 5, speed: 8);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
