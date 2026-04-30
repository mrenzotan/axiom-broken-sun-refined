using System;

namespace Axiom.Platformer.UI
{
    public static class PlatformerHpHudFormatter
    {
        public static string Format(int currentHp, int maxHp)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "maxHp must be greater than zero.");

            return $"HP {currentHp}/{maxHp}";
        }
    }
}
