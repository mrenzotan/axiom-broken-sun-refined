using NUnit.Framework;
using Axiom.Battle;

public class EnemyActionHandlerTests
{
    // Helper: creates initialized stats with given values.
    private static CharacterStats MakeStats(int maxHp, int atk = 0, int def = 0)
    {
        var s = new CharacterStats { MaxHP = maxHp, MaxMP = 0, ATK = atk, DEF = def, SPD = 0 };
        s.Initialize();
        return s;
    }

    // ---- Attack: damage formula ----

    [Test]
    public void ExecuteAttack_DealsDamage_UsingATKMinusDEF()
    {
        // enemy ATK=10, player DEF=4 → 6 damage; 50 - 6 = 44 HP remaining
        var enemy  = MakeStats(maxHp: 60, atk: 10, def: 0);
        var player = MakeStats(maxHp: 50, atk: 0,  def: 4);
        var handler = new EnemyActionHandler(enemy, player);

        handler.ExecuteAttack();

        Assert.AreEqual(44, player.CurrentHP);
    }

    [Test]
    public void ExecuteAttack_DealsMinimumOneDamage_WhenATKLessOrEqualDEF()
    {
        // enemy ATK=3 <= player DEF=10 → clamped to 1 damage; 50 - 1 = 49 HP remaining
        var enemy  = MakeStats(maxHp: 60, atk: 3,  def: 0);
        var player = MakeStats(maxHp: 50, atk: 0,  def: 10);
        var handler = new EnemyActionHandler(enemy, player);

        handler.ExecuteAttack();

        Assert.AreEqual(49, player.CurrentHP);
    }

    // ---- Attack: return value ----

    [Test]
    public void ExecuteAttack_ReturnsTrue_WhenPlayerDefeated()
    {
        // one-shot kill: enemy ATK=100, player DEF=0, HP=10
        var enemy  = MakeStats(maxHp: 60,  atk: 100, def: 0);
        var player = MakeStats(maxHp: 10,  atk: 0,   def: 0);
        var handler = new EnemyActionHandler(enemy, player);

        AttackResult result = handler.ExecuteAttack();

        Assert.IsTrue(result.TargetDefeated);
    }

    [Test]
    public void ExecuteAttack_ReturnsFalse_WhenPlayerAlive()
    {
        // low ATK vs high HP player — survives
        var enemy  = MakeStats(maxHp: 60,  atk: 5, def: 0);
        var player = MakeStats(maxHp: 100, atk: 0, def: 3);
        var handler = new EnemyActionHandler(enemy, player);

        AttackResult result = handler.ExecuteAttack();

        Assert.IsFalse(result.TargetDefeated);
    }

    // ---- Attack: does not affect enemy HP ----

    [Test]
    public void ExecuteAttack_DoesNotChangeEnemyHP()
    {
        var enemy  = MakeStats(maxHp: 60, atk: 10, def: 0);
        var player = MakeStats(maxHp: 50, atk: 0,  def: 3);
        var handler = new EnemyActionHandler(enemy, player);

        handler.ExecuteAttack();

        Assert.AreEqual(60, enemy.CurrentHP);
    }
}
