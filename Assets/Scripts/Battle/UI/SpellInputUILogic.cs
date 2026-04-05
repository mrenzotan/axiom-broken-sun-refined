namespace Axiom.Battle
{
    /// <summary>
    /// Stateless (stateful but Unity-free) state machine for the spell input UI panel.
    /// Tracks which panel should be visible and what recognized spell name to display.
    /// Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Owned and driven by <see cref="SpellInputUI"/>.
    /// </summary>
    public class SpellInputUILogic
    {
        public enum State
        {
            Idle,
            PromptVisible,
            Listening,
            SpellRecognized,
            NotRecognized
        }

        /// <summary>The current display state of the spell input UI.</summary>
        public State  CurrentState       { get; private set; } = State.Idle;

        /// <summary>
        /// The name of the recognized spell, populated by <see cref="ShowResult"/>.
        /// Null in all other states.
        /// </summary>
        public string RecognizedSpellName { get; private set; }

        /// <summary>Transition to <see cref="State.PromptVisible"/>. Clears any stored spell name.</summary>
        public void ShowPrompt()
        {
            CurrentState        = State.PromptVisible;
            RecognizedSpellName = null;
        }

        /// <summary>Transition to <see cref="State.Listening"/>. Clears any stored spell name.</summary>
        public void StartListening()
        {
            CurrentState        = State.Listening;
            RecognizedSpellName = null;
        }

        /// <summary>Transition to <see cref="State.SpellRecognized"/> and store the spell name for display.</summary>
        public void ShowResult(string spellName)
        {
            CurrentState        = State.SpellRecognized;
            RecognizedSpellName = spellName;
        }

        /// <summary>Transition to <see cref="State.NotRecognized"/>. Clears any stored spell name.</summary>
        public void ShowError()
        {
            CurrentState        = State.NotRecognized;
            RecognizedSpellName = null;
        }

        /// <summary>Return to <see cref="State.Idle"/>. Clears any stored spell name.</summary>
        public void Hide()
        {
            CurrentState        = State.Idle;
            RecognizedSpellName = null;
        }
    }
}
