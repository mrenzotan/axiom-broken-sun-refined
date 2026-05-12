using System;
using System.Collections.Generic;

namespace Axiom.Core
{
    public sealed class PlayerState
    {
        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }
        public int MaxMp { get; private set; }
        public int CurrentMp { get; private set; }
        public int Attack { get; private set; }
        public int Defense { get; private set; }
        public int Speed { get; private set; }
        public int Level { get; private set; }
        public int Xp { get; private set; }
        public float WorldPositionX { get; private set; }
        public float WorldPositionY { get; private set; }
        public string ActiveSceneName { get; private set; }

        /// <summary>
        /// True after <see cref="SetWorldPosition"/> (save capture or load). Consumed by the platformer
        /// player spawn so fresh <see cref="PlayerState"/> (new game) keeps the scene default transform.
        /// </summary>
        public bool HasPendingWorldPositionApply { get; private set; }
        public List<string> UnlockedSpellIds { get; }
        public Inventory Inventory { get; }

        private readonly HashSet<string> _activatedCheckpointIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _activatedCheckpointIdsList = new List<string>();

        /// <summary>Ordered list of activated checkpoint IDs.</summary>
        public IReadOnlyList<string> ActivatedCheckpointIds => _activatedCheckpointIdsList;

        /// <summary>
        /// World position of the most recently touched save point. Used by
        /// <c>GameManager.RespawnAtLastCheckpoint</c> after pit-death and battle defeat.
        /// Independent of <see cref="WorldPositionX"/>/<see cref="WorldPositionY"/>, which
        /// also captures pre-battle position and would otherwise overwrite the respawn point.
        /// </summary>
        public float LastCheckpointPositionX { get; private set; }
        public float LastCheckpointPositionY { get; private set; }
        public string LastCheckpointSceneName { get; private set; } = string.Empty;

        /// <summary>
        /// Progression snapshot captured by <see cref="CaptureCheckpointProgression"/> when
        /// the player touches a save point. Restored by <see cref="RestoreCheckpointProgression"/>
        /// on death, so XP/levels gained between checkpoints roll back — preventing
        /// die-and-respawn XP farming. <see cref="HasCheckpointProgression"/> is true
        /// once a checkpoint has captured a snapshot.
        /// </summary>
        public int CheckpointLevel { get; private set; }
        public int CheckpointXp { get; private set; }
        public int CheckpointMaxHp { get; private set; }
        public int CheckpointMaxMp { get; private set; }
        public int CheckpointAttack { get; private set; }
        public int CheckpointDefense { get; private set; }
        public int CheckpointSpeed { get; private set; }
        private List<string> _checkpointUnlockedSpellIds = new List<string>();
        public IReadOnlyList<string> CheckpointUnlockedSpellIds => _checkpointUnlockedSpellIds;
        public bool HasCheckpointProgression => CheckpointLevel > 0;

        /// <summary>
        /// Persisted across save/quit/reload. Set true the first time the player
        /// dies and respawns. Used by FirstDeathPromptController to fire the
        /// "you respawn at the last torch" prompt at most once per save.
        /// </summary>
        public bool HasSeenFirstDeath { get; private set; }

        /// <summary>Persisted. Set true the first time the player takes spike contact damage.</summary>
        public bool HasSeenFirstSpikeHit { get; private set; }

        /// <summary>Persisted. Set true on Victory of the first battle (IceSlime, Surprised).</summary>
        public bool HasCompletedFirstBattleTutorial { get; private set; }

        /// <summary>Persisted. Set true on Victory of the spell-tutorial battle (Meltspawn, Advantaged).</summary>
        public bool HasCompletedSpellTutorialBattle { get; private set; }

        /// <summary>
        /// Persisted. Set true when the player reaches the Level_1-2 exit,
        /// unlocking spellbook and items buttons in all subsequent exploration scenes.
        /// </summary>
        public bool ExplorationMenusUnlocked { get; set; }

        public void MarkFirstDeathSeen()                  { HasSeenFirstDeath = true; }
        public void MarkFirstSpikeHitSeen()               { HasSeenFirstSpikeHit = true; }
        public void MarkFirstBattleTutorialCompleted()    { HasCompletedFirstBattleTutorial = true; }
        public void MarkSpellTutorialBattleCompleted()    { HasCompletedSpellTutorialBattle = true; }

        /// <summary>
        /// Bulk-applies persisted tutorial flags. Used by GameManager.ApplySaveData on load.
        /// </summary>
        public void RestoreTutorialFlags(
            bool hasSeenFirstDeath,
            bool hasSeenFirstSpikeHit,
            bool hasCompletedFirstBattleTutorial,
            bool hasCompletedSpellTutorialBattle)
        {
            HasSeenFirstDeath = hasSeenFirstDeath;
            HasSeenFirstSpikeHit = hasSeenFirstSpikeHit;
            HasCompletedFirstBattleTutorial = hasCompletedFirstBattleTutorial;
            HasCompletedSpellTutorialBattle = hasCompletedSpellTutorialBattle;
        }

        public PlayerState(int maxHp, int maxMp, int attack, int defense, int speed)
        {
            if (maxHp <= 0)  throw new ArgumentOutOfRangeException(nameof(maxHp),  "maxHp must be greater than zero.");
            if (maxMp < 0)   throw new ArgumentOutOfRangeException(nameof(maxMp),  "maxMp cannot be negative.");
            if (attack < 0)  throw new ArgumentOutOfRangeException(nameof(attack),  "attack cannot be negative.");
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense), "defense cannot be negative.");
            if (speed < 0)   throw new ArgumentOutOfRangeException(nameof(speed),   "speed cannot be negative.");

            MaxHp = maxHp;
            CurrentHp = maxHp;
            MaxMp = maxMp;
            CurrentMp = maxMp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Level = 1;
            Xp = 0;
            WorldPositionX = 0f;
            WorldPositionY = 0f;
            ActiveSceneName = string.Empty;
            UnlockedSpellIds = new List<string>();
            Inventory = new Inventory();
        }

        public void SetCurrentHp(int value) =>
            CurrentHp = Math.Clamp(value, 0, MaxHp);

        public void SetCurrentMp(int value) =>
            CurrentMp = Math.Clamp(value, 0, MaxMp);

        public void ApplyVitals(int maxHp, int maxMp, int currentHp, int currentMp)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "maxHp must be greater than zero.");
            if (maxMp < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMp), "maxMp cannot be negative.");

            MaxHp = maxHp;
            MaxMp = maxMp;
            SetCurrentHp(currentHp);
            SetCurrentMp(currentMp);
        }

        public void ApplyProgression(int level, int xp)
        {
            Level = Math.Max(1, level);
            Xp = Math.Max(0, xp);
        }

        /// <summary>
        /// Overwrites base combat stats with persisted values. Used by
        /// <c>GameManager.ApplySaveData</c> to restore level-up stat growth on load.
        /// Deltas are not applied — this is an absolute write.
        /// All values must be non-negative.
        /// </summary>
        public void ApplyStats(int attack, int defense, int speed)
        {
            if (attack  < 0) throw new ArgumentOutOfRangeException(nameof(attack),  "attack cannot be negative.");
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense), "defense cannot be negative.");
            if (speed   < 0) throw new ArgumentOutOfRangeException(nameof(speed),   "speed cannot be negative.");

            Attack  = attack;
            Defense = defense;
            Speed   = speed;
        }

        /// <summary>
        /// Applies additive stat growth (level-up). All deltas must be non-negative.
        /// Current HP/MP are healed up to the new max — classic JRPG level-up behavior.
        /// </summary>
        public void GrowStats(int deltaMaxHp, int deltaMaxMp, int deltaAttack, int deltaDefense, int deltaSpeed)
        {
            if (deltaMaxHp   < 0) throw new ArgumentOutOfRangeException(nameof(deltaMaxHp),   "deltaMaxHp cannot be negative.");
            if (deltaMaxMp   < 0) throw new ArgumentOutOfRangeException(nameof(deltaMaxMp),   "deltaMaxMp cannot be negative.");
            if (deltaAttack  < 0) throw new ArgumentOutOfRangeException(nameof(deltaAttack),  "deltaAttack cannot be negative.");
            if (deltaDefense < 0) throw new ArgumentOutOfRangeException(nameof(deltaDefense), "deltaDefense cannot be negative.");
            if (deltaSpeed   < 0) throw new ArgumentOutOfRangeException(nameof(deltaSpeed),   "deltaSpeed cannot be negative.");

            if (deltaMaxHp == 0 && deltaMaxMp == 0 && deltaAttack == 0 && deltaDefense == 0 && deltaSpeed == 0)
                return;

            MaxHp   += deltaMaxHp;
            MaxMp   += deltaMaxMp;
            Attack  += deltaAttack;
            Defense += deltaDefense;
            Speed   += deltaSpeed;

            CurrentHp = MaxHp;
            CurrentMp = MaxMp;
        }

        public void SetWorldPosition(float x, float y)
        {
            WorldPositionX = x;
            WorldPositionY = y;
            HasPendingWorldPositionApply = true;
        }

        /// <summary>Clears <see cref="HasPendingWorldPositionApply"/> after the platformer has applied the snapshot.</summary>
        public void ClearPendingWorldPositionApply() => HasPendingWorldPositionApply = false;

        public void SetUnlockedSpellIds(IEnumerable<string> spellIds)
        {
            UnlockedSpellIds.Clear();
            if (spellIds == null)
                return;

            foreach (string spellId in spellIds)
            {
                if (string.IsNullOrWhiteSpace(spellId))
                    continue;

                UnlockedSpellIds.Add(spellId);
            }
        }

        public void SetActiveScene(string sceneName) =>
            ActiveSceneName = sceneName ?? string.Empty;

        /// <summary>
        /// Records the most recently touched save point so respawn after defeat returns
        /// the player there even if a later <see cref="SetWorldPosition"/> call overwrote
        /// <see cref="WorldPositionX"/>/<see cref="WorldPositionY"/> (e.g. battle entry).
        /// </summary>
        public void SetLastCheckpoint(string sceneName, float positionX, float positionY)
        {
            LastCheckpointSceneName = sceneName ?? string.Empty;
            LastCheckpointPositionX = positionX;
            LastCheckpointPositionY = positionY;
        }

        /// <summary>
        /// Snapshots Level/XP/stats and unlocked spells so a later death can roll them
        /// back to this checkpoint state. Called by <c>GameManager.SetLastCheckpoint</c>.
        /// </summary>
        public void CaptureCheckpointProgression()
        {
            CheckpointLevel   = Level;
            CheckpointXp      = Xp;
            CheckpointMaxHp   = MaxHp;
            CheckpointMaxMp   = MaxMp;
            CheckpointAttack  = Attack;
            CheckpointDefense = Defense;
            CheckpointSpeed   = Speed;

            _checkpointUnlockedSpellIds.Clear();
            _checkpointUnlockedSpellIds.AddRange(UnlockedSpellIds);
        }

        /// <summary>
        /// Restores the snapshot captured by <see cref="CaptureCheckpointProgression"/>.
        /// No-op when no snapshot exists (legacy save or pre-checkpoint death).
        /// </summary>
        public void RestoreCheckpointProgression()
        {
            if (!HasCheckpointProgression) return;

            ApplyVitals(CheckpointMaxHp, CheckpointMaxMp, CheckpointMaxHp, CheckpointMaxMp);
            ApplyStats(CheckpointAttack, CheckpointDefense, CheckpointSpeed);
            ApplyProgression(CheckpointLevel, CheckpointXp);
            SetUnlockedSpellIds(_checkpointUnlockedSpellIds);
        }

        /// <summary>
        /// Bulk setter for the snapshot fields. Used by <c>GameManager.ApplySaveData</c>
        /// to restore the snapshot from disk.
        /// </summary>
        public void SetCheckpointProgression(
            int level, int xp,
            int maxHp, int maxMp,
            int attack, int defense, int speed,
            IEnumerable<string> unlockedSpellIds)
        {
            CheckpointLevel   = level;
            CheckpointXp      = xp;
            CheckpointMaxHp   = maxHp;
            CheckpointMaxMp   = maxMp;
            CheckpointAttack  = attack;
            CheckpointDefense = defense;
            CheckpointSpeed   = speed;

            _checkpointUnlockedSpellIds.Clear();
            if (unlockedSpellIds == null) return;
            foreach (string id in unlockedSpellIds)
                if (!string.IsNullOrWhiteSpace(id))
                    _checkpointUnlockedSpellIds.Add(id);
        }

        /// <summary>Returns true if the given checkpoint has been activated this session.</summary>
        public bool HasActivatedCheckpoint(string checkpointId)
        {
            if (string.IsNullOrWhiteSpace(checkpointId))
                return false;

            return _activatedCheckpointIds.Contains(checkpointId);
        }

        /// <summary>
        /// Marks the checkpoint as activated. Returns true on first activation, false if already activated or ID is invalid.
        /// Invalid IDs (null/empty/whitespace) are silently ignored and return false.
        /// </summary>
        public bool MarkCheckpointActivated(string checkpointId)
        {
            if (string.IsNullOrWhiteSpace(checkpointId))
                return false;

            if (!_activatedCheckpointIds.Add(checkpointId))
                return false;

            _activatedCheckpointIdsList.Add(checkpointId);
            return true;
        }

        /// <summary>
        /// Replaces the entire activated checkpoint set. Null or empty input clears the set.
        /// Invalid IDs (null/empty/whitespace) are silently skipped.
        /// </summary>
        public void SetActivatedCheckpointIds(IEnumerable<string> checkpointIds)
        {
            _activatedCheckpointIds.Clear();
            _activatedCheckpointIdsList.Clear();

            if (checkpointIds == null)
                return;

            foreach (string id in checkpointIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (_activatedCheckpointIds.Add(id))
                    _activatedCheckpointIdsList.Add(id);
            }
        }
    }
}
