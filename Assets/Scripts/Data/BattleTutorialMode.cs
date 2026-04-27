namespace Axiom.Data
{
    /// <summary>
    /// Set by ExplorationEnemyCombatTrigger and propagated through BattleEntry.
    /// Read by BattleTutorialController on Battle scene load to choose the
    /// scripted tutorial flow (or none).
    ///
    /// Lives in Axiom.Data (not Axiom.Battle) because BattleEntry is in Axiom.Data
    /// and Axiom.Battle already references Axiom.Data — moving the enum here avoids
    /// a circular asmdef reference.
    /// </summary>
    public enum BattleTutorialMode
    {
        None,
        FirstBattle,
        SpellTutorial
    }
}
