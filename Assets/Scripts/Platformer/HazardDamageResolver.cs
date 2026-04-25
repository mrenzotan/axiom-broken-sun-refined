using System;

namespace Axiom.Platformer
{
    public enum HazardMode
    {
        InstantKO,
        PercentMaxHpDamage,
    }

    public readonly struct HazardDamageResult
    {
        public int NewHp { get; }
        public bool IsFatal { get; }

        public HazardDamageResult(int newHp, bool isFatal)
        {
            NewHp = newHp;
            IsFatal = isFatal;
        }
    }

    public static class HazardDamageResolver
    {
        public static HazardDamageResult Resolve(
            int currentHp,
            int maxHp,
            HazardMode mode,
            int percentMaxHpDamage)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "maxHp must be greater than zero.");

            if (mode == HazardMode.InstantKO)
                return new HazardDamageResult(newHp: 0, isFatal: true);

            int damage = (maxHp * percentMaxHpDamage + 99) / 100;
            int newHp = Math.Max(0, currentHp - damage);
            return new HazardDamageResult(newHp, isFatal: newHp == 0);
        }
    }
}
