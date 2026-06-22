using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    /// <summary>
    /// Defines when an enemy transitions to a new phase based on HP threshold.
    /// </summary>
    [System.Serializable]
    public class EnemyPhaseThreshold
    {
        [Tooltip("The phase number to transition to (e.g., 2 for Phase 2 / Liquid phase).")]
        public int phaseNumber = 1;

        [Tooltip("HP percentage at which this phase is triggered. At 50, the phase changes when HP drops to 50% of max.")]
        [Range(0, 100)] public int hpPercentageThreshold = 50;
    }

    ///<summary>
    /// Defines the innate material conditions for a specific enemy form.
    /// Used for multi-form enemies to swap material conditions on form change.
    /// </summary>
    [System.Serializable]
    public class EnemyFormData
    {
        [Tooltip("The form index this data applies to (0 for first form, 1 for second, etc).")]
        public int formIndex;

        [Tooltip("Optional display name for editor clarity e.g. 'Liquid', 'Ice'.")]
        public string formName;

        [Tooltip("Innate material conditions active when in this form.")]
        public List<ChemicalCondition> innateConditions = new List<ChemicalCondition>();
    }
    /// <summary>
    /// ScriptableObject holding all data for one enemy type.
    /// innateConditions defines the enemy's material composition — what it is made of.
    /// These are copied into CharacterStats.InnateConditions on battle init.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "Axiom/Data/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Tooltip("Display name shown in the Battle UI.")]
        public string enemyName;

        public int maxHP;
        public int maxMP;
        public int atk;
        public int def;
        public int spd;

        [Tooltip("XP awarded to the player on defeat.")]
        [Min(0)] public int xpReward;

        [Tooltip("If true, this enemy supports phase transitions based on HP thresholds. Regular enemies should leave this unchecked.")]
        public bool isBoss;

        [Tooltip("Optional story cutscene to play after this boss is defeated. Ignored for non-boss enemies.")]
        public CutsceneData postDefeatCutscene;

        [Tooltip("1–2 material conditions the enemy starts every combat with. Defines what the enemy is made of — determines physical immunity, reaction targets, and other combat interactions.")]
        public List<ChemicalCondition> innateConditions = new List<ChemicalCondition>();

        [Tooltip("Battle scene prefab instantiated by BattleController on scene load. " +
                 "Must contain (or have a child with) SpriteRenderer + Animator + EnemyBattleAnimator. " +
                 "Leave null only for enemies whose battle prefab has not been authored yet — " +
                 "BattleController will warn and fall back to the Inspector-assigned animator.")]
        public GameObject battleVisualPrefab;

        [Tooltip("Possible item drops. Each entry rolls independently against its dropChance.")]
        public List<LootEntry> loot = new List<LootEntry>();

        [Tooltip("Pixel-aligned world-space offset (in Battle scene units) added to the enemy's transform position when spawning SpellVFX for Damage spells. Default (0, 0) preserves current behavior. Tune per-enemy so the VFX lands on the sprite's visual center.")]
        public Vector2 spellVfxOffset = Vector2.zero;

        [Tooltip("Phase transitions based on HP thresholds. Leave empty for single-phase enemies. Phases should be sorted by phaseNumber in ascending order.")]
        public List<EnemyPhaseThreshold> phaseThresholds = new List<EnemyPhaseThreshold>();

        [Tooltip("Per-form innate condition definitions for multi-form enemies. " + "Leave empty for single-form enemies innate conditions is used as fallback.")]
        public List<EnemyFormData> formDefinitions = new List<EnemyFormData>();

        public List<ChemicalCondition> GetInnateConditionsForForm(int formIndex)
        {
            if (formDefinitions != null)
            {
                var form = formDefinitions.Find(f => f.formIndex == formIndex);
                if (form != null)
                {
                    return form.innateConditions;
                }
            }
            return innateConditions;
        }

        /// <summary>
        /// Returns the form index whose innate conditions are currently active, so a
        /// multi-form enemy's visual can follow its chemistry state — e.g. an enemy that
        /// is Liquid shows its liquid form, and one transformed to Solid shows its ice form.
        /// A form matches when every one of its innate conditions is present in
        /// <paramref name="activeMaterialConditions"/>. Returns 0 (the default form) when
        /// there are no form definitions or none match — single-form enemies and bosses
        /// are therefore unaffected.
        /// </summary>
        public int GetFormIndexForConditions(List<ChemicalCondition> activeMaterialConditions)
        {
            if (formDefinitions == null || activeMaterialConditions == null)
                return 0;

            foreach (var form in formDefinitions)
            {
                if (form.innateConditions == null || form.innateConditions.Count == 0)
                    continue;

                bool allPresent = true;
                foreach (var condition in form.innateConditions)
                {
                    if (!activeMaterialConditions.Contains(condition))
                    {
                        allPresent = false;
                        break;
                    }
                }

                if (allPresent)
                    return form.formIndex;
            }

            return 0;
        }
    }
}
