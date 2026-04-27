using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# state machine driving the in-Battle tutorial. Pure logic — no Unity types
    /// beyond CombatStartState (an enum from Axiom.Data). All side effects (prompt show/hide,
    /// button lock toggles, persisted-flag flips) are returned as BattleTutorialAction values
    /// for BattleTutorialController to apply. Per CLAUDE.md: MonoBehaviours only handle
    /// Unity lifecycle; logic lives in plain C#.
    ///
    /// Source of truth for all prompt strings — the Inspector cannot override these because
    /// the flow needs to react to specific contents (e.g., "freeze" vs other spells). If
    /// you want designer-tweakable copy, route the prompts through the controller and replace
    /// these constants with method parameters.
    /// </summary>
    public sealed class BattleTutorialFlow
    {
        // Prompt copy — keep in one place for review.
        private const string FirstBattle_Init        = "The Frostbug surprised you — it acts first.";
        private const string FirstBattle_PressAttack = "Press Attack to strike.";

        private const string SpellTutorial_Init             = "This Meltspawn is Liquid — physical attacks pass right through. Try Attack to see.";
        private const string SpellTutorial_LiquidBlocks     = "Liquid blocks physical damage. Next turn, cast a spell.";
        private const string SpellTutorial_PressSpellFreeze = "Press Spell, then say 'Freeze' aloud.";
        private const string SpellTutorial_FrozenSolid      = "Frozen — enemy skips a turn. Solid — physical attacks now hit.";
        private const string SpellTutorial_StrikeWhileSolid = "Strike while it's Solid!";
        private const string SpellTutorial_ClosingLine      = "Each spell turns the tide differently. Use the right one.";

        private readonly BattleTutorialMode _mode;
        private readonly CombatStartState _startState;

        // FirstBattle state
        private bool _firstBattle_pressAttackShown;

        // SpellTutorial state — counts player turns we've handled so we know which prompt to show.
        private int _spell_playerTurnsObserved;
        private bool _spell_waitingForFreezeConditions;
        private bool _spell_postFreezeTurnReached;

        public BattleTutorialFlow(BattleTutorialMode mode, CombatStartState startState)
        {
            _mode = mode;
            _startState = startState;
        }

        public BattleTutorialMode Mode => _mode;

        public BattleTutorialAction OnInit()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    return new BattleTutorialAction(
                        promptText: FirstBattle_Init,
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);
                case BattleTutorialMode.SpellTutorial:
                    return new BattleTutorialAction(
                        promptText: SpellTutorial_Init,
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);
                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnPlayerTurnStarted()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    if (!_firstBattle_pressAttackShown)
                    {
                        _firstBattle_pressAttackShown = true;
                        return new BattleTutorialAction(
                            promptText: FirstBattle_PressAttack,
                            attackInteractable: true,
                            spellInteractable: false,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    // Subsequent player turns — re-apply button lock only.
                    return new BattleTutorialAction(
                        attackInteractable: true,
                        spellInteractable: false,
                        itemInteractable: false,
                        fleeInteractable: false);

                case BattleTutorialMode.SpellTutorial:
                    _spell_playerTurnsObserved++;
                    if (_spell_playerTurnsObserved == 1)
                    {
                        // Turn 1: same as init prompt — already shown by OnInit. Re-apply button lock only.
                        return new BattleTutorialAction(
                            attackInteractable: true,
                            spellInteractable: false,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    if (_spell_playerTurnsObserved == 2)
                    {
                        // Turn 2: unlock Spell, prompt to cast Freeze.
                        return new BattleTutorialAction(
                            promptText: SpellTutorial_PressSpellFreeze,
                            attackInteractable: true,
                            spellInteractable: true,
                            itemInteractable: false,
                            fleeInteractable: false);
                    }
                    // Turn 3+: post-Freeze world. Strike while Solid.
                    _spell_postFreezeTurnReached = true;
                    return new BattleTutorialAction(
                        promptText: SpellTutorial_StrikeWhileSolid,
                        attackInteractable: true,
                        spellInteractable: true,
                        itemInteractable: false,
                        fleeInteractable: false);

                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnPlayerAttackImmune()
        {
            if (_mode != BattleTutorialMode.SpellTutorial)
                return BattleTutorialAction.NoChange;
            return new BattleTutorialAction(promptText: SpellTutorial_LiquidBlocks);
        }

        public BattleTutorialAction OnPlayerAttackHit()
        {
            switch (_mode)
            {
                case BattleTutorialMode.FirstBattle:
                    return new BattleTutorialAction(promptText: string.Empty);
                case BattleTutorialMode.SpellTutorial when _spell_postFreezeTurnReached:
                    return new BattleTutorialAction(promptText: SpellTutorial_ClosingLine);
                default:
                    return BattleTutorialAction.NoChange;
            }
        }

        public BattleTutorialAction OnSpellCast(string spellName)
        {
            if (_mode != BattleTutorialMode.SpellTutorial) return BattleTutorialAction.NoChange;
            // Hide prompt while the cast resolves; OnConditionsChanged will show the next prompt.
            _spell_waitingForFreezeConditions = true;
            return new BattleTutorialAction(promptText: string.Empty);
        }

        public BattleTutorialAction OnConditionsChanged()
        {
            if (_mode != BattleTutorialMode.SpellTutorial) return BattleTutorialAction.NoChange;
            if (!_spell_waitingForFreezeConditions) return BattleTutorialAction.NoChange;
            _spell_waitingForFreezeConditions = false;
            return new BattleTutorialAction(promptText: SpellTutorial_FrozenSolid);
        }

        public BattleTutorialAction OnBattleEnded(bool victory)
        {
            if (_mode == BattleTutorialMode.None)
                return BattleTutorialAction.NoChange;
            return new BattleTutorialAction(
                promptText: string.Empty,
                markComplete: victory);
        }
    }
}
