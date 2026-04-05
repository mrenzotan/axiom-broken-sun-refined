using NUnit.Framework;
using Axiom.Battle;

public class BattleAnimationServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CharacterStats MakeStats(string name, int hp = 100, int atk = 10, int def = 5, int spd = 5)
    {
        var s = new CharacterStats { Name = name, MaxHP = hp, MaxMP = 0, ATK = atk, DEF = def, SPD = spd };
        s.Initialize();
        return s;
    }

    private static BattleAnimationService MakeService(
        CharacterStats player, CharacterStats enemy,
        System.Action playerAttack = null, System.Action playerHurt = null, System.Action playerDefeat = null,
        System.Action enemyAttack = null,  System.Action enemyHurt = null,  System.Action enemyDefeat = null)
    {
        return new BattleAnimationService(
            player, enemy,
            playerAttack ?? (() => {}), playerHurt ?? (() => {}), playerDefeat ?? (() => {}),
            enemyAttack  ?? (() => {}), enemyHurt  ?? (() => {}), enemyDefeat  ?? (() => {}));
    }

    // ── Attack animations ─────────────────────────────────────────────────────

    [Test]
    public void OnPlayerActionStarted_InvokesPlayerAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerAttack: () => called = true);

        svc.OnPlayerActionStarted();

        Assert.IsTrue(called);
    }

    [Test]
    public void OnPlayerActionStarted_DoesNotInvokeEnemyAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyAttack: () => called = true);

        svc.OnPlayerActionStarted();

        Assert.IsFalse(called);
    }

    [Test]
    public void OnEnemyActionStarted_InvokesEnemyAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyAttack: () => called = true);

        svc.OnEnemyActionStarted();

        Assert.IsTrue(called);
    }

    [Test]
    public void OnEnemyActionStarted_DoesNotInvokePlayerAttack()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerAttack: () => called = true);

        svc.OnEnemyActionStarted();

        Assert.IsFalse(called);
    }

    // ── Hurt animations ───────────────────────────────────────────────────────

    [Test]
    public void OnDamageDealt_TargetIsEnemy_InvokesEnemyHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyHurt: () => called = true);

        svc.OnDamageDealt(enemy, damage: 10, isCrit: false);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsEnemy_DoesNotInvokePlayerHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerHurt: () => called = true);

        svc.OnDamageDealt(enemy, damage: 10, isCrit: false);

        Assert.IsFalse(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsPlayer_InvokesPlayerHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerHurt: () => called = true);

        svc.OnDamageDealt(player, damage: 8, isCrit: false);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnDamageDealt_TargetIsPlayer_DoesNotInvokeEnemyHurt()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyHurt: () => called = true);

        svc.OnDamageDealt(player, damage: 8, isCrit: false);

        Assert.IsFalse(called);
    }

    // ── Defeat animations ─────────────────────────────────────────────────────

    [Test]
    public void OnCharacterDefeated_IsEnemy_InvokesEnemyDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyDefeat: () => called = true);

        svc.OnCharacterDefeated(enemy);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnCharacterDefeated_IsEnemy_DoesNotInvokePlayerDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerDefeat: () => called = true);

        svc.OnCharacterDefeated(enemy);

        Assert.IsFalse(called);
    }

    [Test]
    public void OnCharacterDefeated_IsPlayer_InvokesPlayerDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, playerDefeat: () => called = true);

        svc.OnCharacterDefeated(player);

        Assert.IsTrue(called);
    }

    [Test]
    public void OnCharacterDefeated_IsPlayer_DoesNotInvokeEnemyDefeat()
    {
        bool called = false;
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = MakeService(player, enemy, enemyDefeat: () => called = true);

        svc.OnCharacterDefeated(player);

        Assert.IsFalse(called);
    }

    // ── Null safety ───────────────────────────────────────────────────────────

    [Test]
    public void NullDelegates_DoNotThrow()
    {
        var player = MakeStats("Player");
        var enemy  = MakeStats("Enemy");
        var svc = new BattleAnimationService(player, enemy, null, null, null, null, null, null);

        Assert.DoesNotThrow(() => svc.OnPlayerActionStarted());
        Assert.DoesNotThrow(() => svc.OnEnemyActionStarted());
        Assert.DoesNotThrow(() => svc.OnDamageDealt(player, 5, false));
        Assert.DoesNotThrow(() => svc.OnCharacterDefeated(enemy));
    }

    // ── Unknown target guard ──────────────────────────────────────────────────

    [Test]
    public void OnDamageDealt_UnknownTarget_InvokesNothing()
    {
        bool playerCalled = false;
        bool enemyCalled  = false;
        var player  = MakeStats("Player");
        var enemy   = MakeStats("Enemy");
        var unknown = MakeStats("Unknown");
        var svc = MakeService(player, enemy,
            playerHurt: () => playerCalled = true,
            enemyHurt:  () => enemyCalled  = true);

        svc.OnDamageDealt(unknown, damage: 10, isCrit: false);

        Assert.IsFalse(playerCalled);
        Assert.IsFalse(enemyCalled);
    }
}
