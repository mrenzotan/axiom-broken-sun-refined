using System;
using System.Collections.Generic;

namespace Axiom.Battle
{
    /// <summary>
    /// Restricts which spells the player may cast during a scripted tutorial step.
    /// Carried inside <see cref="BattleTutorialAction"/> and stored on BattleController,
    /// which consults it in OnSpellCast before spending MP. An empty allow-set means
    /// "no restriction". Pure C# so it is EditMode-testable per the MonoBehaviour-separation
    /// standard (CLAUDE.md).
    /// </summary>
    public sealed class TutorialSpellGate
    {
        private readonly HashSet<string> _allowed;

        /// <summary>Shared "anything goes" gate. Use it to clear a restriction.</summary>
        public static readonly TutorialSpellGate Unrestricted = new TutorialSpellGate(null, null);

        public TutorialSpellGate(IEnumerable<string> allowedSpellNames, string rejectionMessage)
        {
            _allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (allowedSpellNames != null)
            {
                foreach (string name in allowedSpellNames)
                    if (!string.IsNullOrEmpty(name)) _allowed.Add(name);
            }
            RejectionMessage = rejectionMessage;
        }

        public string RejectionMessage { get; }

        /// <summary>
        /// True when this gate actually narrows the castable set (non-empty allow-list).
        /// Lets the tutorial controller tell a "funnel to one spell" step from a clearing
        /// (Unrestricted) step without inspecting the allow-list.
        /// </summary>
        public bool IsRestricting => _allowed.Count > 0;

        /// <summary>
        /// True when unrestricted (empty allow-set), or when <paramref name="spellName"/> is
        /// in the allow-set (case-insensitive). The empty-set check comes first so an
        /// unrestricted gate never rejects, regardless of the argument.
        /// </summary>
        public bool IsAllowed(string spellName)
        {
            if (_allowed.Count == 0) return true;
            if (string.IsNullOrEmpty(spellName)) return false;
            return _allowed.Contains(spellName);
        }
    }
}
