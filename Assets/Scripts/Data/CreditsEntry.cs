namespace Axiom.Core
{
    /// <summary>
    /// A single entry in the credits roll.
    /// Role is displayed smaller above the name.
    /// Pass an empty role to render a section header (name only, larger).
    /// Pass both empty to insert a blank spacer.
    /// </summary>
    [System.Serializable]
    public class CreditsEntry
    {
        public string role;  // e.g. "Game Design" — shown small above name
        public string name;  // e.g. "Patrick"
    }
}