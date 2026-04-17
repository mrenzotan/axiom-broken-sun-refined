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
