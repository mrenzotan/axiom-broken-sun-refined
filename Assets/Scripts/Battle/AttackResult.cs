namespace Axiom.Battle
{
    /// <summary>
    /// Return value of ExecuteAttack() on action handlers.
    /// Carries damage amount, crit flag, and defeat flag so BattleController
    /// can fire the correct UI events without querying stats again.
    /// </summary>
    public struct AttackResult
    {
        public int Damage;
        public bool IsCrit;
        public bool TargetDefeated;
    }
}
