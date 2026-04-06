using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Renders a horizontal row of colored pill badges for a character's active
    /// time-limited conditions (status conditions + temporary material transformations).
    ///
    /// Call Refresh() whenever the character's condition list may have changed.
    /// Permanent innate material conditions (e.g. always-Liquid) are not shown —
    /// only conditions with a turn countdown appear.
    ///
    /// Inspector setup required:
    ///   _badgePrefab — a prefab with an Image (background, on root) + TMP_Text child
    ///   _container   — a Transform with HorizontalLayoutGroup + ContentSizeFitter
    /// </summary>
    public class ConditionBadgeUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Prefab for one badge. Root must have an Image; child must have a TMP_Text.")]
        private GameObject _badgePrefab;

        [SerializeField]
        [Tooltip("Parent container. Add HorizontalLayoutGroup + ContentSizeFitter (horizontal).")]
        private Transform _container;

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

            // Status conditions — always time-limited (Frozen, Burning, Evaporating, Corroded, Crystallized)
            foreach (var entry in stats.ActiveStatusConditions)
                SpawnBadge(entry.Condition, entry.TurnsRemaining);

            // Material conditions — only show if they are temporary transformations (turns > 0)
            foreach (var condition in stats.ActiveMaterialConditions)
            {
                int turns = stats.GetMaterialTransformTurns(condition);
                if (turns > 0)
                    SpawnBadge(condition, turns);
            }
        }

        private void SpawnBadge(ChemicalCondition condition, int turnsRemaining)
        {
            GameObject badge = Instantiate(_badgePrefab, _container);

            TMP_Text label = badge.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{LabelFor(condition)} ({turnsRemaining})";

            Image bg = badge.GetComponent<Image>();
            if (bg != null)
                bg.color = ColorFor(condition);
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
