using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Renders a wrapping flow of colored pill badges for a character's active
    /// time-limited conditions (status conditions + temporary material transformations).
    ///
    /// Call Refresh() whenever the character's condition list may have changed.
    /// Permanent innate material conditions (e.g. always-Liquid) are not shown —
    /// only conditions with a turn countdown appear.
    ///
    /// Inspector setup required:
    ///   _badgePrefab — prefab root: Image + ContentSizeFitter (both axes) +
    ///                  HorizontalLayoutGroup (for padding); child: TMP_Text.
    ///                  RectTransform anchor and pivot must both be top-left (0, 1).
    ///   _container   — a plain RectTransform with NO LayoutGroup.
    ///                  Anchor/pivot top-left. Width should match the max allowed row
    ///                  width (e.g. 400 to match EnemyPanel). This script sets height.
    /// </summary>
    public class ConditionBadgeUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Prefab for one badge. Root: Image + ContentSizeFitter + HorizontalLayoutGroup; child: TMP_Text. Anchor and pivot must be top-left (0,1).")]
        private GameObject _badgePrefab;

        [SerializeField]
        [Tooltip("Container RectTransform. Must NOT have a LayoutGroup — this script positions children directly. Anchor and pivot top-left. Width = max row width.")]
        private RectTransform _container;

        [SerializeField] private float _badgeSpacing = 4f;
        [SerializeField] private float _rowSpacing    = 4f;

        /// <summary>
        /// Clears and rebuilds the badge row from the character's current condition state.
        /// Safe to call with a null stats argument (clears the row).
        /// </summary>
        public void Refresh(CharacterStats stats)
        {
            if (_container == null || _badgePrefab == null)
            {
                Debug.LogError("[ConditionBadgeUI] _container or _badgePrefab is not assigned.", this);
                return;
            }

            // Clear existing badges
            foreach (Transform child in _container)
                Destroy(child.gameObject);

            if (stats == null) return;

            // Build a unified list of (condition, turns, appliedOrder) sorted chronologically.
            var entries = new List<(ChemicalCondition condition, int turns, int order)>();

            foreach (var entry in stats.ActiveStatusConditions)
                entries.Add((entry.Condition, entry.TurnsRemaining, entry.AppliedOrder));

            foreach (var condition in stats.ActiveMaterialConditions)
            {
                int turns = stats.GetMaterialTransformTurns(condition);
                if (turns > 0)
                    entries.Add((condition, turns, stats.GetMaterialTransformOrder(condition)));
            }

            var badges = entries
                .OrderBy(e => e.order)
                .Select(e => SpawnBadge(e.condition, e.turns))
                .ToList();

            // Force each badge's ContentSizeFitter to compute its size before layout
            foreach (var badge in badges)
                LayoutRebuilder.ForceRebuildLayoutImmediate(badge);

            LayoutBadges(badges);
        }

        /// <summary>
        /// Positions badges in a left-to-right flow, wrapping to the next row when the
        /// next badge would exceed the container's width.
        /// Also resizes the container's height to fit all rows.
        /// </summary>
        private void LayoutBadges(List<RectTransform> badges)
        {
            float maxWidth  = _container.rect.width;
            float x         = 0f;
            float y         = 0f;
            float rowHeight = 0f;

            foreach (var badge in badges)
            {
                float w = badge.rect.width;
                float h = badge.rect.height;

                // Wrap if this badge would overflow the row (skip wrap check for first badge in row)
                if (x > 0f && x + w > maxWidth)
                {
                    x = 0f;
                    y -= rowHeight + _rowSpacing;
                    rowHeight = 0f;
                }

                badge.anchoredPosition = new Vector2(x, y);
                x += w + _badgeSpacing;
                if (h > rowHeight) rowHeight = h;
            }

            // Resize container height to tightly wrap all rows
            _container.sizeDelta = new Vector2(_container.sizeDelta.x, Mathf.Abs(y) + rowHeight);
        }

        private RectTransform SpawnBadge(ChemicalCondition condition, int turnsRemaining)
        {
            GameObject badge = Instantiate(_badgePrefab, _container);

            TMP_Text label = badge.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = $"{LabelFor(condition)} ({turnsRemaining})";
                label.ForceMeshUpdate(); // ensure TMP computes size before ForceRebuildLayoutImmediate
            }

            Image bg = badge.GetComponent<Image>();
            if (bg != null)
                bg.color = ColorFor(condition);

            return badge.GetComponent<RectTransform>();
        }

        private static string LabelFor(ChemicalCondition condition)
        {
            switch (condition)
            {
                case ChemicalCondition.Frozen:       return "Frozen";
                case ChemicalCondition.Burning:      return "Burning";
                case ChemicalCondition.Evaporating:  return "Evaporating";
                case ChemicalCondition.Corroded:     return "Corroded";
                case ChemicalCondition.Crystallized: return "Crystal";
                case ChemicalCondition.Solid:        return "Solid";
                case ChemicalCondition.Vapor:        return "Vapor";
                case ChemicalCondition.Liquid:       return "Liquid";
                default:
                    Debug.LogWarning($"[ConditionBadgeUI] No label defined for {condition}");
                    return condition.ToString();
            }
        }

        private static Color ColorFor(ChemicalCondition condition)
        {
            switch (condition)
            {
                case ChemicalCondition.Frozen:
                case ChemicalCondition.Solid:        return new Color(0.23f, 0.56f, 0.83f); // blue
                case ChemicalCondition.Burning:      return new Color(0.79f, 0.25f, 0.13f); // red-orange
                case ChemicalCondition.Evaporating:
                case ChemicalCondition.Vapor:        return new Color(0.60f, 0.80f, 0.90f); // pale blue
                case ChemicalCondition.Corroded:     return new Color(0.47f, 0.70f, 0.22f); // acid green
                case ChemicalCondition.Crystallized: return new Color(0.60f, 0.44f, 0.85f); // purple
                default:
                    Debug.LogWarning($"[ConditionBadgeUI] No color defined for {condition}");
                    return new Color(0.50f, 0.50f, 0.50f); // grey
            }
        }
    }
}
