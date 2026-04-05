using NUnit.Framework;
using Axiom.Battle;

public class PlayerActionHandlerTests
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
        // player ATK=15, enemy DEF=5 → 10 damage; 50 - 10 = 40 HP remaining
        // randomSource pinned to 1.0f (≥ CritChance 0.2f) to guarantee no crit
        var player = MakeStats(maxHp: 100, atk: 15, def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy, randomSource: () => 1f);

        handler.ExecuteAttack();

        Assert.AreEqual(40, enemy.CurrentHP);
    }

    [Test]
    public void ExecuteAttack_DealsCritDamage_WhenCritLands()
    {
        // player ATK=15, enemy DEF=5 → baseDamage=10, crit=(int)(10*1.5f)=15; 50-15=35 HP remaining
        // randomSource pinned to 0.0f (< CritChance 0.2f) to guarantee crit
        var player = MakeStats(maxHp: 100, atk: 15, def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy, randomSource: () => 0f);

        AttackResult result = handler.ExecuteAttack();

        Assert.IsTrue(result.IsCrit);
        Assert.AreEqual(35, enemy.CurrentHP);
    }

    [Test]
    public void ExecuteAttack_DealsMinimumOneDamage_WhenATKLessOrEqualDEF()
    {
        // player ATK=3 <= enemy DEF=10 → clamped to 1 damage; 50 - 1 = 49 HP remaining
        var player = MakeStats(maxHp: 100, atk: 3,  def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 10);
        var handler = new PlayerActionHandler(player, enemy);

        handler.ExecuteAttack();

        Assert.AreEqual(49, enemy.CurrentHP);
    }

    // ---- Attack: return value ----

    [Test]
    public void ExecuteAttack_ReturnsTrue_WhenEnemyDefeated()
    {
        // 1-shot kill: ATK=100, DEF=0, HP=10
        var player = MakeStats(maxHp: 100, atk: 100, def: 0);
        var enemy  = MakeStats(maxHp: 10,  atk: 0,   def: 0);
        var handler = new PlayerActionHandler(player, enemy);

        AttackResult result = handler.ExecuteAttack();

        Assert.IsTrue(result.TargetDefeated);
    }

    [Test]
    public void ExecuteAttack_ReturnsFalse_WhenEnemyAlive()
    {
        // Low ATK vs high HP enemy — survives
        var player = MakeStats(maxHp: 100, atk: 10, def: 0);
        var enemy  = MakeStats(maxHp: 100, atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy);

        AttackResult result = handler.ExecuteAttack();

        Assert.IsFalse(result.TargetDefeated);
    }

    // ---- Attack: does not affect player HP ----

    [Test]
    public void ExecuteAttack_DoesNotChangePlayerHP()
    {
        var player = MakeStats(maxHp: 100, atk: 10, def: 0);
        var enemy  = MakeStats(maxHp: 50,  atk: 0,  def: 5);
        var handler = new PlayerActionHandler(player, enemy);

        handler.ExecuteAttack();

        Assert.AreEqual(100, player.CurrentHP);
    }

    // ---- Spell placeholder ----

    [Test]
    public void ExecuteSpell_ReturnsPlaceholderMessage()
    {
        var handler = new PlayerActionHandler(MakeStats(100), MakeStats(50));
        Assert.AreEqual("No spells yet.", handler.ExecuteSpell());
    }

    // ---- Item placeholder ----

    [Test]
    public void ExecuteItem_ReturnsPlaceholderMessage()
    {
        var handler = new PlayerActionHandler(MakeStats(100), MakeStats(50));
        Assert.AreEqual("No items.", handler.ExecuteItem());
    }
}
