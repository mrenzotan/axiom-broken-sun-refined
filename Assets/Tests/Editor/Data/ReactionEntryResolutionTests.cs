using System.Collections.Generic;
using Axiom.Battle;
using Axiom.Data;
using NUnit.Framework;
using UnityEngine;

namespace Axiom.Tests.Data
{
    /// <summary>
    /// Verifies SpellEffectResolver's first-match-wins iteration over SpellData.reactions.
    /// </summary>
    public class ReactionEntryResolutionTests
    {
        private SpellEffectResolver _resolver;
        private CharacterStats _caster;
        private CharacterStats _target;

        [SetUp]
        public void SetUp()
        {
            _resolver = new SpellEffectResolver();
            _caster   = new CharacterStats { Name = "Caster", MaxHP = 100, MaxMP = 50, ATK = 10, DEF = 5, SPD = 8 };
            _target   = new CharacterStats { Name = "Target", MaxHP = 60,  MaxMP = 0,  ATK = 8,  DEF = 3, SPD = 5 };
            _caster.Initialize();
            _target.Initialize();
        }

        private static SpellData MakeDamageSpell(int power, params ReactionEntry[] reactions)
        {
            var spell = ScriptableObject.CreateInstance<SpellData>();
            spell.effectType = SpellEffectType.Damage;
            spell.power      = power;
            spell.reactions.AddRange(reactions);
            return spell;
        }

        // ── 1. Empty reactions list ──────────────────────────────────────────

        [Test]
        public void Resolve_EmptyReactions_TargetHasCondition_NoReactionFires()
        {
            _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Flammable });
            var spell = MakeDamageSpell(10);

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsFalse(result.ReactionTriggered);
            Assert.AreEqual(10, result.Amount);
            Assert.IsTrue(_target.HasCondition(ChemicalCondition.Flammable), "Condition should remain unconsumed.");

            Object.DestroyImmediate(spell);
        }

        // ── 2. Single reaction, condition present ────────────────────────────

        [Test]
        public void Resolve_SingleReaction_ConditionPresent_FiresAndConsumesCondition()
        {
            _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Flammable });
            var spell = MakeDamageSpell(10, new ReactionEntry
            {
                reactsWith          = ChemicalCondition.Flammable,
                reactionBonusDamage = 8
            });

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsTrue(result.ReactionTriggered);
            Assert.AreEqual(18, result.Amount);                                          // 10 + 8
            Assert.IsFalse(_target.HasCondition(ChemicalCondition.Flammable), "Condition should be consumed.");

            Object.DestroyImmediate(spell);
        }

        // ── 3. Single reaction, condition absent ─────────────────────────────

        [Test]
        public void Resolve_SingleReaction_ConditionAbsent_NoReactionFires()
        {
            var spell = MakeDamageSpell(10, new ReactionEntry
            {
                reactsWith          = ChemicalCondition.Flammable,
                reactionBonusDamage = 8
            });

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsFalse(result.ReactionTriggered);
            Assert.AreEqual(10, result.Amount);

            Object.DestroyImmediate(spell);
        }

        // ── 4. Two reactions, only second matches ────────────────────────────

        [Test]
        public void Resolve_TwoReactions_OnlySecondMatches_SecondReactionFires()
        {
            _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            var spell = MakeDamageSpell(10,
                new ReactionEntry { reactsWith = ChemicalCondition.Flammable, reactionBonusDamage = 5 },
                new ReactionEntry { reactsWith = ChemicalCondition.Liquid,    reactionBonusDamage = 12 }
            );

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsTrue(result.ReactionTriggered);
            Assert.AreEqual(22, result.Amount);                                          // 10 + 12

            Object.DestroyImmediate(spell);
        }

        // ── 5. Two reactions, both match — first wins ────────────────────────

        [Test]
        public void Resolve_TwoReactions_BothMatch_FirstListedWins()
        {
            // Give target both conditions
            _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Flammable });
            _target.ApplyStatusCondition(ChemicalCondition.Corroded, baseDamage: 4);

            var spell = MakeDamageSpell(10,
                new ReactionEntry { reactsWith = ChemicalCondition.Flammable, reactionBonusDamage = 5 },
                new ReactionEntry { reactsWith = ChemicalCondition.Corroded,  reactionBonusDamage = 12 }
            );

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsTrue(result.ReactionTriggered);
            Assert.AreEqual(15, result.Amount, "First reaction (bonus 5) should win, not second (bonus 12).");
            Assert.IsFalse(_target.HasCondition(ChemicalCondition.Flammable), "First reaction's condition consumed.");
            Assert.IsTrue(_target.HasCondition(ChemicalCondition.Corroded),   "Second reaction's condition untouched.");

            Object.DestroyImmediate(spell);
        }

        // ── 6. Reaction with transformsTo applies material transformation ────

        [Test]
        public void Resolve_ReactionWithTransformsTo_AppliesMaterialTransformationWithDuration()
        {
            _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
            var spell = MakeDamageSpell(10, new ReactionEntry
            {
                reactsWith             = ChemicalCondition.Liquid,
                reactionBonusDamage    = 0,
                transformsTo           = ChemicalCondition.Solid,
                transformationDuration = 2
            });

            SpellResult result = _resolver.Resolve(spell, _caster, _target);

            Assert.IsTrue(result.ReactionTriggered);
            Assert.IsTrue(result.MaterialTransformed);
            Assert.IsTrue (_target.HasCondition(ChemicalCondition.Solid),  "Target should now be Solid.");
            Assert.IsFalse(_target.HasCondition(ChemicalCondition.Liquid), "Liquid should be consumed.");

            Object.DestroyImmediate(spell);
        }
    }
}
