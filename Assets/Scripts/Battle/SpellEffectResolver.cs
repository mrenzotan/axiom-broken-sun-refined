using System;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Pure C# resolver that applies a spell's effects to combat stats.
    /// No MonoBehaviour — created and held by BattleController.
    ///
    /// Resolution order (per chemistry-condition-combat-system-design spec):
    ///   1. Null guards
    ///   2. Reaction check — if reactsWith condition present on effect target, reaction fires
    ///   3. Primary effect — Damage to opposing target, or Heal/Shield to caster
    ///   4. Inflict check — status condition applied to effect target if not already present
    ///   5. Return SpellResult
    ///
    /// DoT base damage values are defined as constants here and passed to
    /// CharacterStats.ApplyStatusCondition(). They are intentionally flat for Phase 2.
    /// </summary>
    public class SpellEffectResolver
    {
        // ── DoT damage constants ─────────────────────────────────────────────
        private const int BurningDoTDamage      = 5;
        private const int EvaporatingDoTDamage  = 3;
        private const int CorrodedBaseDoTDamage = 4;

        /// <summary>
        /// Resolves a spell cast by caster against an opposing target.
        ///
        /// Effect targeting:
        ///   Damage → applied to opposingTarget (the enemy)
        ///   Heal / Shield → applied to caster (self-targeted effects)
        ///
        /// Reactions always check the primary effect target (opposingTarget for Damage,
        /// caster for Heal/Shield). MP has already been deducted by BattleController.
        /// </summary>
        public SpellResult Resolve(SpellData spell, CharacterStats caster, CharacterStats opposingTarget)
        {
            if (spell           == null) throw new ArgumentNullException(nameof(spell));
            if (caster          == null) throw new ArgumentNullException(nameof(caster));
            if (opposingTarget  == null) throw new ArgumentNullException(nameof(opposingTarget));

            // The character the spell's effect applies to
            CharacterStats effectTarget = spell.effectType == SpellEffectType.Damage
                ? opposingTarget
                : caster;

            bool reactionTriggered   = false;
            bool materialTransformed = false;
            int  bonusDamage         = 0;

            // ── 2. Reaction check ────────────────────────────────────────────
            if (spell.reactsWith != ChemicalCondition.None
                && effectTarget.HasCondition(spell.reactsWith))
            {
                reactionTriggered = true;
                bonusDamage       = spell.reactionBonusDamage;

                effectTarget.ConsumeCondition(spell.reactsWith);

                if (spell.transformsTo != ChemicalCondition.None)
                {
                    effectTarget.ApplyMaterialTransformation(
                        spell.transformsTo,
                        spell.reactsWith,
                        spell.transformationDuration);
                    materialTransformed = true;
                }
            }

            // ── 3. Primary effect ────────────────────────────────────────────
            int     magnitude      = spell.power + bonusDamage;
            bool    targetDefeated = false;

            switch (spell.effectType)
            {
                case SpellEffectType.Damage:
                    opposingTarget.TakeDamage(magnitude);
                    targetDefeated = opposingTarget.IsDefeated;
                    break;
                case SpellEffectType.Heal:
                    caster.Heal(magnitude);
                    break;
                case SpellEffectType.Shield:
                    caster.ApplyShield(magnitude);
                    break;
            }

            // ── 4. Inflict check ─────────────────────────────────────────────
            ChemicalCondition conditionApplied = ChemicalCondition.None;
            if (spell.inflictsCondition != ChemicalCondition.None
                && !effectTarget.HasCondition(spell.inflictsCondition))
            {
                int baseDamage = DoTDamageFor(spell.inflictsCondition);
                effectTarget.ApplyStatusCondition(spell.inflictsCondition, baseDamage);
                conditionApplied = spell.inflictsCondition;
            }

            return new SpellResult
            {
                EffectType          = spell.effectType,
                Amount              = magnitude,
                TargetDefeated      = targetDefeated,
                ReactionTriggered   = reactionTriggered,
                MaterialTransformed = materialTransformed,
                ConditionApplied    = conditionApplied
            };
        }

        private static int DoTDamageFor(ChemicalCondition condition)
        {
            switch (condition)
            {
                case ChemicalCondition.Burning:     return BurningDoTDamage;
                case ChemicalCondition.Evaporating: return EvaporatingDoTDamage;
                case ChemicalCondition.Corroded:    return CorrodedBaseDoTDamage;
                default:                            return 0;
            }
        }
    }
}
