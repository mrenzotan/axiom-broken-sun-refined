namespace Axiom.Battle
{
    /// <summary>
    /// Passed to BattleManager.StartBattle() to determine who acts first.
    /// Advantaged = player struck first (player goes first).
    /// Surprised  = enemy struck first (enemy goes first).
    /// </summary>
    public enum CombatStartState
    {
        Advantaged,
        Surprised
    }
}
