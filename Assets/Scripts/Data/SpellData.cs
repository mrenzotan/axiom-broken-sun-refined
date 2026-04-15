using System.Collections.Generic;
using UnityEngine;

namespace Axiom.Data
{
    [CreateAssetMenu(fileName = "NewSpellData", menuName = "Axiom/Data/Spell Data")]
    public class SpellData : ScriptableObject
    {
        [Tooltip("The spoken trigger word or phrase the player says to cast this spell. MUST be lowercase — Vosk only recognizes lowercase input.")]
        public string spellName;

        [Tooltip("The core chemistry concept this spell belongs to — used for spellbook grouping, UI sorting, and tutorial callouts. Does not drive combat math; chemistry interactions are driven by ChemicalCondition fields below.")]
        public ChemistryConcept concept = ChemistryConcept.None;

        [Tooltip("The type of effect this spell applies: Damage (targets enemy), Heal (targets caster), or Shield (targets caster).")]
        public SpellEffectType effectType;

        [Tooltip("Base magnitude: damage dealt, HP restored, or shield HP added.")]
        public int power;

        [Tooltip("MP cost to cast this spell.")]
        public int mpCost;

        [Tooltip("Requirements the player must meet before this spell appears in their grammar. Null/default = available from game start.")]
        public SpellUnlockCondition unlockCondition = new SpellUnlockCondition();

        [Header("Chemistry Condition System")]

        [Tooltip("Status condition applied to the spell's primary target after it resolves. None if no condition is inflicted.")]
        public ChemicalCondition inflictsCondition;

        [Tooltip("How many turns the inflicted condition lasts. 0 = default duration for that condition.")]
        public int inflictsConditionDuration;

        [Tooltip("Reactions this spell can trigger. Evaluated in order — the first entry whose reactsWith matches the effect target fires; later entries are ignored.")]
        public List<ReactionEntry> reactions = new List<ReactionEntry>();

        [Header("Spell Effects")]

        [Tooltip("Sprite animation clip played at the VFX spawn point when this spell is cast. Leave empty for no visual effect.")]
        public AnimationClip castVfxClip;

        [Tooltip("Sound effects played when this spell is cast. Assign 1-5 clips — one is chosen at random each cast. Leave empty for no audio effect.")]
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
