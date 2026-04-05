using System;
using NUnit.Framework;
using Axiom.Battle;
using Axiom.Data;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectResolverTests
{
    private SpellEffectResolver _resolver;
    private CharacterStats _caster;
    private CharacterStats _target;

    [SetUp]
    public void SetUp()
    {
        _resolver = new SpellEffectResolver();
        _caster   = new CharacterStats { Name = "Kael",      MaxHP = 100, MaxMP = 50, ATK = 10, DEF = 5,  SPD = 8 };
        _target   = new CharacterStats { Name = "VoidWraith", MaxHP = 60,  MaxMP = 0,  ATK = 8,  DEF = 3,  SPD = 5 };
        _caster.Initialize();
        _target.Initialize();
    }

    private static SpellData MakeSpell(
        SpellEffectType effect = SpellEffectType.Damage,
        int power = 10,
        int mpCost = 0,
        ChemicalCondition inflicts = ChemicalCondition.None,
        ChemicalCondition reactsWith = ChemicalCondition.None,
        int reactionBonus = 0,
        ChemicalCondition transformsTo = ChemicalCondition.None,
        int transformDuration = 0)
    {
        var spell = ScriptableObject.CreateInstance<SpellData>();
        spell.effectType          = effect;
        spell.power               = power;
        spell.mpCost              = mpCost;
        spell.inflictsCondition   = inflicts;
        spell.reactsWith          = reactsWith;
        spell.reactionBonusDamage = reactionBonus;
        spell.transformsTo        = transformsTo;
        spell.transformationDuration = transformDuration;
        return spell;
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Test]
    public void Resolve_NullSpell_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(null, _caster, _target));
    }

    [Test]
    public void Resolve_NullCaster_ThrowsArgumentNullException()
    {
        var spell = MakeSpell();
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(spell, null, _target));
    }

    [Test]
    public void Resolve_NullTarget_ThrowsArgumentNullException()
    {
        var spell = MakeSpell();
        Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(spell, _caster, null));
    }

    // ── Primary effect: Damage ────────────────────────────────────────────────

    [Test]
    public void Resolve_DamageSpell_DealsDamagePowerToTarget()
    {
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 15);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Damage, result.EffectType);
        Assert.AreEqual(15,  result.Amount);
        Assert.AreEqual(45,  _target.CurrentHP);  // 60 - 15
        Assert.IsFalse(result.TargetDefeated);
    }

    [Test]
    public void Resolve_DamageSpell_SetsTargetDefeated_WhenHPReachesZero()
    {
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 60);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.TargetDefeated);
        Assert.IsTrue(_target.IsDefeated);
    }

    // ── Primary effect: Heal ──────────────────────────────────────────────────

    [Test]
    public void Resolve_HealSpell_RestoresCasterHP()
    {
        _caster.TakeDamage(30);    // CurrentHP = 70
        var spell = MakeSpell(effect: SpellEffectType.Heal, power: 20);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Heal, result.EffectType);
        Assert.AreEqual(20,  result.Amount);
        Assert.AreEqual(90,  _caster.CurrentHP);  // 70 + 20
    }

    [Test]
    public void Resolve_HealSpell_DoesNotAffectTarget()
    {
        _caster.TakeDamage(30);
        var spell = MakeSpell(effect: SpellEffectType.Heal, power: 20);
        _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(60, _target.CurrentHP);  // unchanged
    }

    // ── Primary effect: Shield ────────────────────────────────────────────────

    [Test]
    public void Resolve_ShieldSpell_AppliesShieldToCaster()
    {
        var spell = MakeSpell(effect: SpellEffectType.Shield, power: 25);
        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(SpellEffectType.Shield, result.EffectType);
        Assert.AreEqual(25, result.Amount);
        Assert.AreEqual(25, _caster.ShieldHP);
    }

    [Test]
    public void Resolve_ShieldSpell_DoesNotAffectTarget()
    {
        var spell = MakeSpell(effect: SpellEffectType.Shield, power: 25);
        _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(0, _target.ShieldHP);
    }

    // ── Reaction system ───────────────────────────────────────────────────────

    [Test]
    public void Resolve_WithReactsWith_WhenConditionPresent_TriggersReactionAndBonusDamage()
    {
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Flammable });
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Flammable,
            reactionBonus: 5);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.ReactionTriggered);
        Assert.AreEqual(15, result.Amount);          // 10 base + 5 bonus
        Assert.AreEqual(45, _target.CurrentHP);      // 60 - 15
        Assert.IsFalse(_target.HasCondition(ChemicalCondition.Flammable)); // consumed
    }

    [Test]
    public void Resolve_WithReactsWith_WhenConditionAbsent_NoReactionFires()
    {
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Flammable,
            reactionBonus: 5);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsFalse(result.ReactionTriggered);
        Assert.AreEqual(10, result.Amount);
    }

    [Test]
    public void Resolve_HealSpell_ReactionChecksAndAppliesToCaster()
    {
        // Neutralize: heal spell that reacts with Corroded on the caster
        _caster.Initialize();
        _caster.ApplyStatusCondition(ChemicalCondition.Corroded, baseDamage: 4);
        var spell = MakeSpell(
            effect: SpellEffectType.Heal,
            power: 10,
            reactsWith: ChemicalCondition.Corroded,
            reactionBonus: 5);
        _caster.TakeDamage(20);  // CurrentHP = 80

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.ReactionTriggered);
        Assert.AreEqual(15, result.Amount);          // 10 + 5 bonus
        Assert.AreEqual(95, _caster.CurrentHP);      // 80 + 15
        Assert.IsFalse(_caster.HasCondition(ChemicalCondition.Corroded)); // consumed
    }

    // ── Phase-change transformation ───────────────────────────────────────────

    [Test]
    public void Resolve_WithTransformsTo_AppliesMaterialTransformation()
    {
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            reactsWith: ChemicalCondition.Liquid,
            transformsTo: ChemicalCondition.Solid,
            transformDuration: 2);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.IsTrue(result.MaterialTransformed);
        Assert.IsTrue (_target.HasCondition(ChemicalCondition.Solid));
        Assert.IsFalse(_target.HasCondition(ChemicalCondition.Liquid));
    }

    // ── Inflict condition ─────────────────────────────────────────────────────

    [Test]
    public void Resolve_WithInflictsCondition_WhenNotPresent_AppliesCondition()
    {
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            inflicts: ChemicalCondition.Burning);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(ChemicalCondition.Burning, result.ConditionApplied);
        Assert.IsTrue(_target.HasCondition(ChemicalCondition.Burning));
    }

    [Test]
    public void Resolve_WithInflictsCondition_WhenAlreadyPresent_DoesNotDuplicate()
    {
        _target.ApplyStatusCondition(ChemicalCondition.Burning, baseDamage: 5);
        var spell = MakeSpell(
            effect: SpellEffectType.Damage,
            power: 10,
            inflicts: ChemicalCondition.Burning);

        _resolver.Resolve(spell, _caster, _target);

        int count = 0;
        foreach (var e in _target.ActiveStatusConditions)
            if (e.Condition == ChemicalCondition.Burning) count++;
        Assert.AreEqual(1, count); // still only one
    }

    // ── Physical immunity ─────────────────────────────────────────────────────

    [Test]
    public void Resolve_DamageSpell_TargetLiquid_StillDealsSpellDamage()
    {
        // Spells bypass physical immunity — only basic attacks are blocked
        _target.Initialize(new List<ChemicalCondition> { ChemicalCondition.Liquid });
        var spell = MakeSpell(effect: SpellEffectType.Damage, power: 15);

        SpellResult result = _resolver.Resolve(spell, _caster, _target);

        Assert.AreEqual(15, result.Amount);
        Assert.AreEqual(45, _target.CurrentHP);
    }
}
