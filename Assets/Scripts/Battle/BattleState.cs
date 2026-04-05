namespace Axiom.Battle
{
    /// <summary>
    /// All discrete states the battle can be in at any moment.
    /// Victory, Defeat, and Fled are terminal states — no further transitions occur.
    /// </summary>
    public enum BattleState
    {
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat,
        Fled
    }
}
