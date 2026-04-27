namespace Axiom.Battle
{
    /// <summary>
    /// Output of one BattleTutorialFlow step. Each field is "no change" when null;
    /// non-null fields are applied by BattleTutorialController to the UI.
    /// PromptText: null = leave panel as-is, "" = hide, otherwise = show with this text.
    /// MarkComplete = true tells the controller to flip the persisted PlayerState flag.
    /// </summary>
    public readonly struct BattleTutorialAction
    {
        public string PromptText        { get; }
        public bool? AttackInteractable { get; }
        public bool? SpellInteractable  { get; }
        public bool? ItemInteractable   { get; }
        public bool? FleeInteractable   { get; }
        public bool MarkComplete        { get; }

        public BattleTutorialAction(
            string promptText = null,
            bool? attackInteractable = null,
            bool? spellInteractable = null,
            bool? itemInteractable = null,
            bool? fleeInteractable = null,
            bool markComplete = false)
        {
            PromptText = promptText;
            AttackInteractable = attackInteractable;
            SpellInteractable  = spellInteractable;
            ItemInteractable   = itemInteractable;
            FleeInteractable   = fleeInteractable;
            MarkComplete       = markComplete;
        }

        public static readonly BattleTutorialAction NoChange = new BattleTutorialAction();
    }
}
