namespace Axiom.Data
{
    /// <summary>
    /// Cross-scene battle context. Set by the overworld trigger before loading the Battle
    /// scene; consumed and cleared by BattleController.Start() on Battle scene load.
    ///
    /// EnemyData may be null — BattleController falls back to its Inspector-configured
    /// stats when null, preserving standalone Battle scene testing.
    /// </summary>
    public sealed class BattleEntry
    {
        public CombatStartState StartState { get; }
        public EnemyData EnemyData { get; }
        public string EnemyId { get; }

        public BattleEntry(CombatStartState startState, EnemyData enemyData, string enemyId = null)
        {
            StartState = startState;
            EnemyData = enemyData;
            EnemyId = enemyId;
        }
    }
}
