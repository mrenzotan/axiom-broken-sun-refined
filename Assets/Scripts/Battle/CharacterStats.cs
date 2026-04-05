using System;

namespace Axiom.Battle
{
    /// <summary>
    /// Serializable plain C# class holding a character's base stats and runtime HP/MP.
    /// No MonoBehaviour — attach as a SerializeField on BattleController to set values in the Inspector.
    /// Call Initialize() before battle begins to reset CurrentHP/CurrentMP to their maximums.
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public string Name = string.Empty;
        public int MaxHP;
        public int MaxMP;
        public int ATK;
        public int DEF;
        public int SPD;

        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public bool IsDefeated => CurrentHP <= 0;

        /// <summary>Resets CurrentHP and CurrentMP to their maximum values. Call once per battle start.</summary>
        public void Initialize()
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Reduces CurrentHP by <paramref name="amount"/>, clamped to zero.</summary>
        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }

        /// <summary>Restores CurrentHP by <paramref name="amount"/>, clamped to MaxHP.</summary>
        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> MP.
        /// Returns true and deducts MP if sufficient; returns false and leaves MP unchanged if not.
        /// </summary>
        public bool SpendMP(int amount)
        {
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        /// <summary>Restores CurrentMP by <paramref name="amount"/>, clamped to MaxMP.</summary>
        public void RestoreMP(int amount)
        {
            CurrentMP = Math.Min(MaxMP, CurrentMP + amount);
        }
    }
}
