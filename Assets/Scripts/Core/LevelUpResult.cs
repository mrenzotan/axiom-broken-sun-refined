namespace Axiom.Core
{
    /// <summary>
    /// Immutable payload describing one level gain. Fired by
    /// <see cref="ProgressionService.OnLevelUp"/> — once per level gained,
    /// even during multi-level XP awards.
    /// </summary>
    public readonly struct LevelUpResult
    {
        public int PreviousLevel { get; }
        public int NewLevel      { get; }
        public int DeltaMaxHp    { get; }
        public int DeltaMaxMp    { get; }
        public int DeltaAttack   { get; }
        public int DeltaDefense  { get; }
        public int DeltaSpeed    { get; }

        public LevelUpResult(
            int previousLevel, int newLevel,
            int deltaMaxHp, int deltaMaxMp,
            int deltaAttack, int deltaDefense, int deltaSpeed)
        {
            PreviousLevel = previousLevel;
            NewLevel      = newLevel;
            DeltaMaxHp    = deltaMaxHp;
            DeltaMaxMp    = deltaMaxMp;
            DeltaAttack   = deltaAttack;
            DeltaDefense  = deltaDefense;
            DeltaSpeed    = deltaSpeed;
        }
    }
}
