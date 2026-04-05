using NUnit.Framework;
using Axiom.Battle;

public class BattleManagerTests
{
    // ---- CombatStartState routing ----

    [Test]
    public void StartBattle_Advantaged_SetsPlayerTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    [Test]
    public void StartBattle_Surprised_SetsEnemyTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    // ---- PlayerTurn transitions ----

    [Test]
    public void OnPlayerActionComplete_EnemyAlive_TransitionsToEnemyTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        manager.OnPlayerActionComplete(enemyDefeated: false);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    [Test]
    public void OnPlayerActionComplete_EnemyDefeated_TransitionsToVictory()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        manager.OnPlayerActionComplete(enemyDefeated: true);
        Assert.AreEqual(BattleState.Victory, manager.CurrentState);
    }

    // ---- EnemyTurn transitions ----

    [Test]
    public void OnEnemyActionComplete_PlayerAlive_TransitionsToPlayerTurn()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        manager.OnEnemyActionComplete(playerDefeated: false);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    [Test]
    public void OnEnemyActionComplete_PlayerDefeated_TransitionsToDefeat()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised);
        manager.OnEnemyActionComplete(playerDefeated: true);
        Assert.AreEqual(BattleState.Defeat, manager.CurrentState);
    }

    // ---- Guard clauses: wrong-state calls are no-ops ----

    [Test]
    public void OnPlayerAction_DuringEnemyTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised); // starts at EnemyTurn
        manager.OnPlayerActionComplete(enemyDefeated: false);
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }

    [Test]
    public void OnEnemyAction_DuringPlayerTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged); // starts at PlayerTurn
        manager.OnEnemyActionComplete(playerDefeated: false);
        Assert.AreEqual(BattleState.PlayerTurn, manager.CurrentState);
    }

    // ---- OnStateChanged event ----

    [Test]
    public void OnStateChanged_FiresWithNewState_OnStartBattle()
    {
        var manager = new BattleManager();
        BattleState? captured = null;
        manager.OnStateChanged += s => captured = s;

        manager.StartBattle(CombatStartState.Advantaged);

        Assert.AreEqual(BattleState.PlayerTurn, captured);
    }

    [Test]
    public void OnStateChanged_FiresWithNewState_OnTransition()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged);
        BattleState? captured = null;
        manager.OnStateChanged += s => captured = s;

        manager.OnPlayerActionComplete(enemyDefeated: false);

        Assert.AreEqual(BattleState.EnemyTurn, captured);
    }

    // ---- Flee ----

    [Test]
    public void OnPlayerFled_DuringPlayerTurn_TransitionsToFled()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Advantaged); // PlayerTurn
        manager.OnPlayerFled();
        Assert.AreEqual(BattleState.Fled, manager.CurrentState);
    }

    [Test]
    public void OnPlayerFled_DuringEnemyTurn_IsIgnored()
    {
        var manager = new BattleManager();
        manager.StartBattle(CombatStartState.Surprised); // EnemyTurn
        manager.OnPlayerFled();
        Assert.AreEqual(BattleState.EnemyTurn, manager.CurrentState);
    }
}
