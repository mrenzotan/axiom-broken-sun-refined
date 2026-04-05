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
}