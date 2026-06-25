using System.Collections.Generic;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// One badge to render: a chemical condition, its remaining turns, and whether it is
    /// a permanent innate condition. Innate badges use TurnsRemaining == 0 and render with
    /// no counter; time-limited badges carry a positive turn count.
    /// </summary>
    public readonly struct ConditionBadge
    {
        public readonly ChemicalCondition Condition;
        public readonly int TurnsRemaining;
        public readonly bool IsInnate;

        public ConditionBadge(ChemicalCondition condition, int turnsRemaining, bool isInnate)
        {
            Condition      = condition;
            TurnsRemaining = turnsRemaining;
            IsInnate       = isInnate;
        }
    }

    /// <summary>
    /// Pure selection/ordering/dedup logic for the condition badge row, extracted so it can
    /// be EditMode-tested without a scene (mirrors SpellListPanelLogic / SpellInputUILogic).
    /// </summary>
    public static class ConditionBadgeLogic
    {
        /// <summary>
        /// Builds the ordered badge list for a character.
        /// Innate badges (no turn counter) come first, then time-limited badges sorted by the
        /// order in which they were applied. A condition shown as a time-limited badge is never
        /// also shown as an innate badge (time-limited takes precedence).
        /// Pass innateConditions == null (or empty) for characters with no innate conditions
        /// (e.g. the player) to render time-limited badges only.
        /// </summary>
        public static List<ConditionBadge> BuildBadges(
            CharacterStats stats, IReadOnlyList<ChemicalCondition> innateConditions)
        {
            var result = new List<ConditionBadge>();
            if (stats == null) return result;

            // Time-limited entries: status conditions + active material transformations (turns > 0).
            var timed = new List<(ChemicalCondition condition, int turns, int order)>();

            foreach (var entry in stats.ActiveStatusConditions)
                timed.Add((entry.Condition, entry.TurnsRemaining, entry.AppliedOrder));

            foreach (var condition in stats.ActiveMaterialConditions)
            {
                int turns = stats.GetMaterialTransformTurns(condition);
                if (turns > 0)
                    timed.Add((condition, turns, stats.GetMaterialTransformOrder(condition)));
            }

            timed.Sort((a, b) => a.order.CompareTo(b.order));

            // Innate badges first, skipping any condition already represented by a time-limited
            // badge (dedup) and any innate condition already added.
            var shown = new HashSet<ChemicalCondition>();
            if (innateConditions != null)
            {
                foreach (var condition in innateConditions)
                {
                    if (condition == ChemicalCondition.None) continue; // "no condition" — never a badge
                    if (TimedContains(timed, condition)) continue;
                    if (!shown.Add(condition)) continue;
                    result.Add(new ConditionBadge(condition, 0, isInnate: true));
                }
            }

            // Then the time-limited badges in applied order.
            foreach (var t in timed)
                result.Add(new ConditionBadge(t.condition, t.turns, isInnate: false));

            return result;
        }

        private static bool TimedContains(
            List<(ChemicalCondition condition, int turns, int order)> timed, ChemicalCondition condition)
        {
            foreach (var t in timed)
                if (t.condition == condition) return true;
            return false;
        }
    }
}
