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
        public string ActiveSceneName { get; private set; }

        // Phase 5 (Data Layer) will replace List<string> with proper ItemData references.
        public List<string> InventoryItemIds { get; }

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
            ActiveSceneName = string.Empty;
            InventoryItemIds = new List<string>();
        }

        public void SetCurrentHp(int value) =>
            CurrentHp = Math.Clamp(value, 0, MaxHp);

        public void SetCurrentMp(int value) =>
            CurrentMp = Math.Clamp(value, 0, MaxMp);

        public void SetActiveScene(string sceneName) =>
            ActiveSceneName = sceneName ?? string.Empty;
    }
}
