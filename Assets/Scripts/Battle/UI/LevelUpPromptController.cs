using System;
using System.Collections.Generic;
using Axiom.Core;

namespace Axiom.Battle.UI
{
    /// <summary>
    /// Plain C# logic for the level-up prompt. Queues one <see cref="LevelUpResult"/>
    /// per level gained, plus the list of spell names unlocked at that level.
    /// <see cref="LevelUpPromptUI"/> drives the view; this class owns the state machine.
    /// </summary>
    public sealed class LevelUpPromptController
    {
        public readonly struct Entry
        {
            public LevelUpResult Result        { get; }
            public IReadOnlyList<string> NewSpellNames { get; }
            public Entry(LevelUpResult result, IReadOnlyList<string> newSpellNames)
            {
                Result        = result;
                NewSpellNames = newSpellNames;
            }
        }

        private readonly Queue<Entry> _pending = new Queue<Entry>();

        /// <summary>Fires when <see cref="Dismiss"/> empties the queue.</summary>
        public event Action OnDismissed;

        public bool IsPending => _pending.Count > 0;

        /// <summary>The current entry. Throws if <see cref="IsPending"/> is false.</summary>
        public Entry Current
        {
            get
            {
                if (_pending.Count == 0)
                    throw new InvalidOperationException("No pending level-up entry.");
                return _pending.Peek();
            }
        }

        /// <summary>
        /// Appends a level-up entry to the queue.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="newSpellNames"/> is null.</exception>
        public void Enqueue(LevelUpResult result, IReadOnlyList<string> newSpellNames)
        {
            if (newSpellNames == null) throw new ArgumentNullException(nameof(newSpellNames));
            _pending.Enqueue(new Entry(result, newSpellNames));
        }

        /// <summary>
        /// Dismisses the current entry. If the queue becomes empty, fires <see cref="OnDismissed"/>.
        /// No-op when empty.
        /// </summary>
        public void Dismiss()
        {
            if (_pending.Count == 0) return;
            _pending.Dequeue();
            if (_pending.Count == 0)
                OnDismissed?.Invoke();
        }
    }
}
