using System;
using Axiom.Data;

namespace Axiom.Voice
{
    /// <summary>
    /// Plain C# — sequences one deferred platformer spell cast: trigger the cast animation,
    /// then resolve the world effect exactly once on the animation's fire-frame (or a timeout),
    /// never twice. The MonoBehaviour seam supplies the callbacks; logic lives here for EditMode tests.
    /// </summary>
    public class PlatformerCastSequencer
    {
        private readonly Action<SpellData> _beginCast;
        private readonly Action<SpellData> _resolve;
        private readonly Action _endCast;

        private SpellData _pendingSpell;

        public bool IsCasting => _pendingSpell != null;

        public PlatformerCastSequencer(
            Action<SpellData> beginCast,
            Action<SpellData> resolve,
            Action endCast)
        {
            _beginCast = beginCast ?? throw new ArgumentNullException(nameof(beginCast));
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            _endCast = endCast ?? throw new ArgumentNullException(nameof(endCast));
        }

        /// <summary>Starts a cast. No-op (false) if a cast is already in flight or spell is null.</summary>
        public bool RequestCast(SpellData spell)
        {
            if (spell == null) return false;
            if (_pendingSpell != null) return false;

            _pendingSpell = spell;
            _beginCast(spell);
            return true;
        }

        /// <summary>
        /// Called by the cast animation's fire-frame event AND by the timeout fallback.
        /// Resolves the pending cast exactly once; later calls are ignored until the next RequestCast.
        /// </summary>
        public void NotifyFireFrame()
        {
            if (_pendingSpell == null) return;

            SpellData spell = _pendingSpell;
            _pendingSpell = null;
            _resolve(spell);
            _endCast();
        }
    }
}
