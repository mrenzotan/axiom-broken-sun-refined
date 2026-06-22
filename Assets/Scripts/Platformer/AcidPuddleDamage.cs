using System;

namespace Axiom.Platformer
{
    /// <summary>
    /// Pure escalation curve for the acid puddle's damage-over-time. Tick 0 deals the
    /// mild base percent; each subsequent tick multiplies by <paramref name="growthFactor"/>,
    /// rounded away from zero and clamped to <paramref name="maxTickPercent"/>. Kept
    /// separate from the MonoBehaviour so the curve is unit-testable without a scene.
    /// </summary>
    public static class AcidPuddleDamage
    {
        public static int PercentForTick(int tickIndex, int baseTickPercent, float growthFactor, int maxTickPercent)
        {
            if (tickIndex < 0) tickIndex = 0;

            double raw = baseTickPercent * Math.Pow(growthFactor, tickIndex);
            int rounded = (int)Math.Round(raw, MidpointRounding.AwayFromZero);

            if (rounded < 0) rounded = 0;
            if (rounded > maxTickPercent) rounded = maxTickPercent;
            return rounded;
        }
    }
}
