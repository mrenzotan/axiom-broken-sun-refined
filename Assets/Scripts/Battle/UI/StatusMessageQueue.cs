namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# rolling 2-line message buffer for battle narration.
    /// Extracted from StatusMessageUI so queue logic is testable without Unity.
    /// </summary>
    public class StatusMessageQueue
    {
        private string _line1 = string.Empty;
        private string _line2 = string.Empty;

        /// <summary>
        /// Adds a message. Pushes the current bottom line to the top,
        /// discarding the oldest top line.
        /// </summary>
        public void Post(string message)
        {
            _line1 = _line2;
            _line2 = message;
        }

        /// <summary>
        /// Returns the display string: one or two lines separated by a newline.
        /// Returns empty string if no messages have been posted.
        /// </summary>
        public string GetDisplay()
        {
            if (string.IsNullOrEmpty(_line1) && string.IsNullOrEmpty(_line2))
                return string.Empty;
            if (string.IsNullOrEmpty(_line1))
                return _line2;
            return $"{_line1}\n{_line2}";
        }
    }
}
