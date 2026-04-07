namespace Axiom.Battle
{
    /// <summary>
    /// Stateless (stateful but Unity-free) state machine for the spell input UI panel.
    /// Tracks which panel should be visible and what text to display.
    /// Contains no Unity types — fully testable in Edit Mode.
    ///
    /// Owned and driven by SpellInputUI.
    /// </summary>
    public class SpellInputUILogic
    {
        public enum State
        {
            Idle,
            PromptVisible,
            Listening,
            SpellRecognized,
            NotRecognized,
            Rejected         // Cast attempted but rejected (e.g. insufficient MP)
        }

        /// <summary>The current display state of the spell input UI.</summary>
        public State  CurrentState       { get; private set; } = State.Idle;

        /// <summary>
        /// The name of the recognized spell, populated by ShowResult.
        /// Null in all other states.
        /// </summary>
        public string RecognizedSpellName { get; private set; }

        /// <summary>
        /// The rejection reason message, populated by ShowRejection.
        /// Null in all other states.
        /// </summary>
        public string RejectionMessage   { get; private set; }

        /// <summary>Transition to PromptVisible. Clears stored spell name and rejection message.</summary>
        public void ShowPrompt()
        {
            CurrentState        = State.PromptVisible;
            RecognizedSpellName = null;
            RejectionMessage    = null;
        }

        /// <summary>Transition to Listening. Clears stored spell name and rejection message.</summary>
        public void StartListening()
        {
            CurrentState        = State.Listening;
            RecognizedSpellName = null;
            RejectionMessage    = null;
        }

        /// <summary>Transition to SpellRecognized and store the spell name for display.</summary>
        public void ShowResult(string spellName)
        {
            CurrentState        = State.SpellRecognized;
            RecognizedSpellName = spellName;
            RejectionMessage    = null;
        }

        /// <summary>Transition to NotRecognized. Clears stored text.</summary>
        public void ShowError()
        {
            CurrentState        = State.NotRecognized;
            RecognizedSpellName = null;
            RejectionMessage    = null;
        }

        /// <summary>
        /// Transition to Rejected and store the rejection reason.
        /// Called when a spell is recognized but cast fails (e.g. insufficient MP).
        /// </summary>
        public void ShowRejection(string message)
        {
            CurrentState        = State.Rejected;
            RecognizedSpellName = null;
            RejectionMessage    = message;
        }

        /// <summary>Return to Idle. Clears all stored text.</summary>
        public void Hide()
        {
            CurrentState        = State.Idle;
            RecognizedSpellName = null;
            RejectionMessage    = null;
        }
    }
}
