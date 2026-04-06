using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell. MUST be lowercase — Vosk only recognizes lowercase input.")]
        public string spellName;

        [Tooltip("The type of effect this spell applies: Damage (targets enemy), Heal (targets caster), or Shield (targets caster).")]
        public SpellEffectType effectType;

        [Tooltip("Base magnitude: damage dealt, HP restored, or shield HP added. Stat-based modifiers are not applied in Phase 2.")]
        public int power;

        [Tooltip("MP cost to cast this spell.")]
        public int mpCost;

        [Header("Chemistry Condition System")]

        [Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted. Spells can never directly apply a material condition via this field.")]
        public ChemicalCondition inflictsCondition;

        [Tooltip("How many turns the inflicted condition lasts. 0 = use the default duration for that condition (Frozen: 1, Evaporating: 2, Burning: 2, Corroded: 3, Crystallized: 2).")]
        public int inflictsConditionDuration;

        [Tooltip("The condition (material or status) this spell reacts with if already present on the target. None if the spell has no reaction.")]
        public ChemicalCondition reactsWith;

        [Tooltip("Flat bonus added to the spell's primary effect when a reaction triggers. For Damage: bonus damage. For Heal: bonus HP restored. For Shield: bonus shield HP.")]
        public int reactionBonusDamage;

        [Tooltip("Material condition temporarily applied to the target when a phase-change reaction fires. None if this reaction causes no material transformation.")]
        public ChemicalCondition transformsTo;

        [Tooltip("How many turns the transformed material condition lasts before the innate condition is restored. Only meaningful when transformsTo != None.")]
        public int transformationDuration;

        [Header("Spell Effects")]

        [Tooltip("Sprite animation clip played at the VFX spawn point when this spell is cast. Leave empty for no visual effect.")]
        public AnimationClip castVfxClip;

        [Tooltip("Sound effects played when this spell is cast. Assign 1-5 clips — one is chosen at random each cast to avoid repetition. Leave empty for no audio effect.")]
        public AudioClip[] castSfxVariants;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spellName != null)
                spellName = spellName.ToLower();
        }
#endif
    }
}
