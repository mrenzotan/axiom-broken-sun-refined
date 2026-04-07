namespace Axiom.Battle
{
    /// <summary>
    /// Return value of ExecuteAttack() on action handlers.
    /// Carries damage amount, crit flag, defeat flag, and immunity flag so BattleController
    /// can fire the correct UI events without querying stats again.
    /// IsImmune is true when the target's active material conditions (Liquid or Vapor) make
    /// them immune to physical attacks. In that case Damage is 0.
    /// </summary>
    public struct AttackResult
    {
        public int Damage;
        public bool IsCrit;
        public bool TargetDefeated;
        public bool IsImmune;
    }
}
