using System;
using Axiom.Data;

namespace Axiom.Core
{
    /// <summary>
    /// Plain C# service that awards XP, detects level-ups, applies stat growth
    /// via <see cref="PlayerState.GrowStats"/>, and fires <see cref="OnLevelUp"/>
    /// once per level gained.
    ///
    /// Ownership:   <c>GameManager</c> constructs and holds the singleton instance.
    /// Threading:   all methods run on the Unity main thread.
    /// Persistence: operates on <see cref="PlayerState.Level"/> and <see cref="PlayerState.Xp"/>
    ///              which are already part of <c>SaveData</c> — no separate save hook needed.
    /// Level cap:   defined by <see cref="CharacterData.xpToNextLevelCurve"/> length.
    /// </summary>
    public sealed class ProgressionService
    {
        private readonly PlayerState   _state;
        private readonly CharacterData _data;

        /// <summary>
        /// Fires synchronously once per level gained, in ascending order.
        /// For a multi-level award (e.g. L1 → L3), fires twice: (1→2) then (2→3).
        /// </summary>
        public event Action<LevelUpResult> OnLevelUp;

        public ProgressionService(PlayerState state, CharacterData data)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _data  = data  ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// XP required for the player's current level to advance to the next level.
        /// Returns 0 when the player is at the level cap (no further level-ups possible).
        /// </summary>
        public int XpForNextLevelUp
        {
            get
            {
                int index = _state.Level - 1;
                if (_data.xpToNextLevelCurve == null) return 0;
                if (index < 0 || index >= _data.xpToNextLevelCurve.Length) return 0;
                return _data.xpToNextLevelCurve[index];
            }
        }

        /// <summary>
        /// Adds <paramref name="amount"/> XP to <see cref="PlayerState.Xp"/>.
        /// If the accumulated XP meets or exceeds <see cref="XpForNextLevelUp"/>,
        /// levels up — subtracting the threshold, incrementing level, applying stat growth,
        /// and firing <see cref="OnLevelUp"/>. Repeats for multi-level awards.
        /// At the level cap, XP continues to accumulate on <see cref="PlayerState.Xp"/>
        /// but no level-ups fire.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When <paramref name="amount"/> is negative.</exception>
        public void AwardXp(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "amount cannot be negative.");
            if (amount == 0) return;

            int carried = _state.Xp + amount;

            while (true)
            {
                int threshold = XpForNextLevelUp;
                if (threshold <= 0 || carried < threshold)
                {
                    _state.ApplyProgression(_state.Level, carried);
                    return;
                }

                carried -= threshold;
                int previousLevel = _state.Level;
                int newLevel      = previousLevel + 1;

                _state.ApplyProgression(newLevel, 0);
                _state.GrowStats(
                    _data.maxHpPerLevel,
                    _data.maxMpPerLevel,
                    _data.atkPerLevel,
                    _data.defPerLevel,
                    _data.spdPerLevel);

                OnLevelUp?.Invoke(new LevelUpResult(
                    previousLevel: previousLevel,
                    newLevel:      newLevel,
                    deltaMaxHp:    _data.maxHpPerLevel,
                    deltaMaxMp:    _data.maxMpPerLevel,
                    deltaAttack:   _data.atkPerLevel,
                    deltaDefense:  _data.defPerLevel,
                    deltaSpeed:    _data.spdPerLevel));
            }
        }
    }
}
