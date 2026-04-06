using NUnit.Framework;
using Axiom.Battle;

public class CharacterStatsTests
{
    private static CharacterStats MakeStats(int maxHp = 100, int maxMp = 30,
                                            int atk = 10, int def = 5, int spd = 8)
        => new CharacterStats { MaxHP = maxHp, MaxMP = maxMp, ATK = atk, DEF = def, SPD = spd };

    // ---- Initialize ----

    [Test]
    public void Initialize_SetsCurrentHPToMaxHP()
    {
        var stats = MakeStats(maxHp: 80);
        stats.Initialize();
        Assert.AreEqual(80, stats.CurrentHP);
    }

    [Test]
    public void Initialize_SetsCurrentMPToMaxMP()
    {
        var stats = MakeStats(maxMp: 40);
        stats.Initialize();
        Assert.AreEqual(40, stats.CurrentMP);
    }

    // ---- TakeDamage ----

    [Test]
    public void TakeDamage_ReducesCurrentHP_ByAmount()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(30);
        Assert.AreEqual(70, stats.CurrentHP);
    }

    [Test]
    public void TakeDamage_ClampsToZero_WhenOverkill()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(9999);
        Assert.AreEqual(0, stats.CurrentHP);
    }

    [Test]
    public void TakeDamage_ZeroDamage_LeavesHPUnchanged()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(0);
        Assert.AreEqual(100, stats.CurrentHP);
    }

    // ---- IsDefeated ----

    [Test]
    public void IsDefeated_ReturnsFalse_WhenHPAboveZero()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        Assert.IsFalse(stats.IsDefeated);
    }

    [Test]
    public void IsDefeated_ReturnsTrue_WhenHPIsZero()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(100);
        Assert.IsTrue(stats.IsDefeated);
    }

    // ---- Heal ----

    [Test]
    public void Heal_RestoresCurrentHP_ByAmount()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(40);   // CurrentHP = 60
        stats.Heal(20);
        Assert.AreEqual(80, stats.CurrentHP);
    }

    [Test]
    public void Heal_ClampsToMaxHP_WhenHealExceedsMax()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(10);   // CurrentHP = 90
        stats.Heal(9999);
        Assert.AreEqual(100, stats.CurrentHP);
    }

    [Test]
    public void Heal_ZeroAmount_LeavesHPUnchanged()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.TakeDamage(30);   // CurrentHP = 70
        stats.Heal(0);
        Assert.AreEqual(70, stats.CurrentHP);
    }

    // ---- SpendMP ----

    [Test]
    public void SpendMP_ReducesCurrentMP_WhenSufficientMP()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        bool result = stats.SpendMP(10);
        Assert.IsTrue(result);
        Assert.AreEqual(20, stats.CurrentMP);
    }

    [Test]
    public void SpendMP_ReturnsFalse_WhenInsufficientMP()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        bool result = stats.SpendMP(31);
        Assert.IsFalse(result);
    }

    [Test]
    public void SpendMP_DoesNotReduceMP_WhenInsufficientMP()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        stats.SpendMP(31);
        Assert.AreEqual(30, stats.CurrentMP);
    }

    [Test]
    public void SpendMP_ZeroAmount_ReturnsTrueAndLeavesMPUnchanged()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        bool result = stats.SpendMP(0);
        Assert.IsTrue(result);
        Assert.AreEqual(30, stats.CurrentMP);
    }

    [Test]
    public void SpendMP_ExactAmount_ReturnsTrueAndDrainsMP()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        bool result = stats.SpendMP(30);
        Assert.IsTrue(result);
        Assert.AreEqual(0, stats.CurrentMP);
    }

    // ---- RestoreMP ----

    [Test]
    public void RestoreMP_RestoresCurrentMP_ByAmount()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        stats.SpendMP(20);      // CurrentMP = 10
        stats.RestoreMP(15);
        Assert.AreEqual(25, stats.CurrentMP);
    }

    [Test]
    public void RestoreMP_ClampsToMaxMP_WhenRestoreExceedsMax()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        stats.SpendMP(10);      // CurrentMP = 20
        stats.RestoreMP(9999);
        Assert.AreEqual(30, stats.CurrentMP);
    }

    [Test]
    public void RestoreMP_ZeroAmount_LeavesMPUnchanged()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        stats.SpendMP(10);      // CurrentMP = 20
        stats.RestoreMP(0);
        Assert.AreEqual(20, stats.CurrentMP);
    }

    [Test]
    public void RestoreMP_ExactAmount_RestoresMPToMax()
    {
        var stats = MakeStats(maxMp: 30);
        stats.Initialize();
        stats.SpendMP(10);      // CurrentMP = 20
        stats.RestoreMP(10);
        Assert.AreEqual(30, stats.CurrentMP);
    }

    // ---- ApplyShield ----

    [Test]
    public void ApplyShield_SetsShieldHP()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyShield(30);
        Assert.AreEqual(30, stats.ShieldHP);
    }

    [Test]
    public void TakeDamage_WithShield_AbsorbsDamageBeforeHP()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.ApplyShield(20);
        stats.TakeDamage(15);
        Assert.AreEqual(5,   stats.ShieldHP);   // 20 - 15 = 5
        Assert.AreEqual(100, stats.CurrentHP);  // HP untouched
    }

    [Test]
    public void TakeDamage_ExceedingShield_ReducesHPByRemainder()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.ApplyShield(10);
        stats.TakeDamage(25);   // 10 absorbed by shield, 15 carries through
        Assert.AreEqual(0,  stats.ShieldHP);
        Assert.AreEqual(85, stats.CurrentHP);   // 100 - 15
    }

    [Test]
    public void Initialize_ResetsShieldHPToZero()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyShield(50);
        stats.Initialize();     // second init must clear shield
        Assert.AreEqual(0, stats.ShieldHP);
    }

    // ---- Condition helpers ----

    [Test]
    public void HasCondition_ReturnsFalse_WhenConditionAbsent()
    {
        var stats = MakeStats();
        stats.Initialize();
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
    }

    [Test]
    public void ApplyStatusCondition_AddsConditionToActiveList()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
        Assert.IsTrue(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
    }

    [Test]
    public void HasCondition_ReturnsTrue_WhenInActiveMaterialConditions()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid });
        Assert.IsTrue(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
    }

    [Test]
    public void ConsumeCondition_RemovesStatusCondition()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Burning);
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
    }

    [Test]
    public void ConsumeCondition_RemovesMaterialCondition()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Flammable });
        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Flammable);
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Flammable));
    }

    [Test]
    public void ApplyMaterialTransformation_ReplacesActiveConditionWithTemporaryOne()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid });
        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
        stats.ApplyMaterialTransformation(
            Axiom.Data.ChemicalCondition.Solid,
            Axiom.Data.ChemicalCondition.Liquid,
            duration: 2);
        Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Solid));
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
    }

    [Test]
    public void Initialize_ClearsAllConditionsAndRestoresInnate()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid });
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);
        stats.Initialize(); // re-init with no args — clears everything
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Burning));
        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid));
        Assert.AreEqual(0, stats.ShieldHP);
    }

    // ---- EffectiveATK ----

    [Test]
    public void EffectiveATK_HalvesATK_WhenCrystallized()
    {
        var stats = MakeStats(atk: 10);
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Crystallized, baseDamage: 0);
        Assert.AreEqual(5, stats.EffectiveATK);  // 10 / 2
    }

    [Test]
    public void EffectiveATK_ReturnsFullATK_WhenNotCrystallized()
    {
        var stats = MakeStats(atk: 10);
        stats.Initialize();
        Assert.AreEqual(10, stats.EffectiveATK);
    }

    // ---- IsPhysicallyImmune ----

    [Test]
    public void IsPhysicallyImmune_ReturnsTrue_WhenLiquid()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid });
        Assert.IsTrue(stats.IsPhysicallyImmune);
    }

    [Test]
    public void IsPhysicallyImmune_ReturnsTrue_WhenVapor()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Vapor });
        Assert.IsTrue(stats.IsPhysicallyImmune);
    }

    [Test]
    public void IsPhysicallyImmune_ReturnsFalse_WhenSolid()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Solid });
        Assert.IsFalse(stats.IsPhysicallyImmune);
    }

    // ---- ProcessConditionTurn ----

    [Test]
    public void ProcessConditionTurn_Burning_DealsDoTDamage()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Burning, baseDamage: 5);

        ConditionTurnResult result = stats.ProcessConditionTurn();

        Assert.AreEqual(5,  result.TotalDamageDealt);
        Assert.AreEqual(95, stats.CurrentHP);
    }

    [Test]
    public void ProcessConditionTurn_Frozen_SetsActionSkipped()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Frozen, baseDamage: 0);

        ConditionTurnResult result = stats.ProcessConditionTurn();

        Assert.IsTrue(result.ActionSkipped);
    }

    [Test]
    public void ProcessConditionTurn_Corroded_EscalatesDamageOnSecondTick()
    {
        var stats = MakeStats(maxHp: 100);
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Corroded, baseDamage: 4);

        // Tick 1: ×1.0 = 4 damage
        stats.ProcessConditionTurn();
        Assert.AreEqual(96, stats.CurrentHP);

        // Tick 2: ×1.5 = 6 damage
        stats.ProcessConditionTurn();
        Assert.AreEqual(90, stats.CurrentHP);
    }

    [Test]
    public void ProcessConditionTurn_ExpiredStatusCondition_IsRemoved()
    {
        var stats = MakeStats();
        stats.Initialize();
        stats.ApplyStatusCondition(Axiom.Data.ChemicalCondition.Frozen, baseDamage: 0); // 1-turn duration

        stats.ProcessConditionTurn(); // tick and expire

        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Frozen));
    }

    [Test]
    public void ProcessConditionTurn_ExpiredMaterialTransformation_RestoresInnateCondition()
    {
        var stats = MakeStats();
        stats.Initialize(new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid });

        // Simulate a Freeze reaction: consume Liquid, apply Solid for 1 turn
        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
        stats.ApplyMaterialTransformation(
            Axiom.Data.ChemicalCondition.Solid,
            Axiom.Data.ChemicalCondition.Liquid,
            duration: 1);

        stats.ProcessConditionTurn(); // Solid expires

        Assert.IsFalse(stats.HasCondition(Axiom.Data.ChemicalCondition.Solid));
        Assert.IsTrue (stats.HasCondition(Axiom.Data.ChemicalCondition.Liquid)); // restored
    }

    [Test]
    public void ProcessConditionTurn_NoConditions_ReturnsZeroDamageAndActionNotSkipped()
    {
        var stats = MakeStats();
        stats.Initialize();

        ConditionTurnResult result = stats.ProcessConditionTurn();

        Assert.AreEqual(0,     result.TotalDamageDealt);
        Assert.IsFalse(result.ActionSkipped);
    }

    // ---- GetMaterialTransformTurns ----

    [Test]
    public void GetMaterialTransformTurns_ActiveTransformation_ReturnsTurnsRemaining()
    {
        var stats = MakeStats();
        var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid };
        stats.Initialize(innate);

        // Simulate a Freeze reaction: consume Liquid, apply Solid for 2 turns
        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
        stats.ApplyMaterialTransformation(
            Axiom.Data.ChemicalCondition.Solid,
            Axiom.Data.ChemicalCondition.Liquid,
            2);

        Assert.AreEqual(2, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
    }

    [Test]
    public void GetMaterialTransformTurns_InnateCondition_ReturnsZero()
    {
        var stats = MakeStats();
        var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid };
        stats.Initialize(innate);

        // Liquid is innate/permanent — not a transformation
        Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Liquid));
    }

    [Test]
    public void GetMaterialTransformTurns_ConditionNotPresent_ReturnsZero()
    {
        var stats = MakeStats();
        stats.Initialize();

        Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
    }

    [Test]
    public void GetMaterialTransformTurns_AfterTransformationExpires_ReturnsZero()
    {
        var stats = MakeStats();
        var innate = new System.Collections.Generic.List<Axiom.Data.ChemicalCondition>
            { Axiom.Data.ChemicalCondition.Liquid };
        stats.Initialize(innate);

        stats.ConsumeCondition(Axiom.Data.ChemicalCondition.Liquid);
        stats.ApplyMaterialTransformation(
            Axiom.Data.ChemicalCondition.Solid,
            Axiom.Data.ChemicalCondition.Liquid,
            1);

        // Tick once — 1-turn transformation expires
        stats.ProcessConditionTurn();

        Assert.AreEqual(0, stats.GetMaterialTransformTurns(Axiom.Data.ChemicalCondition.Solid));
    }
}