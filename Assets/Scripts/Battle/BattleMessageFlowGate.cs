using System;
using System.Collections.Generic;

namespace Axiom.Battle
{
    public sealed class BattleMessageFlowGate
    {
        private readonly Queue<Action> _continuations = new Queue<Action>();

        public bool IsBlocked { get; private set; }

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
            while (!IsBlocked && _continuations.Count > 0)
                _continuations.Dequeue().Invoke();
        }

        public void ContinueWhenReady(Action continuation)
        {
            if (continuation == null)
                throw new ArgumentNullException(nameof(continuation));

            if (!IsBlocked)
                continuation();
            else
                _continuations.Enqueue(continuation);
        }
    }
}
