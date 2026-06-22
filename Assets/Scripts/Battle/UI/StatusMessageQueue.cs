using System;
using System.Collections.Generic;
using Axiom.Core;

namespace Axiom.Battle
{
    /// <summary>
    /// Plain C# state machine for queued, acknowledgment-gated battle narration.
    /// </summary>
    public sealed class StatusMessageQueue
    {
        private readonly Queue<string> _messages = new Queue<string>();
        private readonly TypewriterEffect _typewriter = new TypewriterEffect();
        private readonly float _charsPerSecond;

        public event Action<bool> BusyStateChanged;

        public string CurrentMessage => _messages.Count == 0 ? string.Empty : _messages.Peek();
        public string VisibleText => _typewriter.VisibleText;
        public bool IsBusy => _messages.Count > 0;
        public bool IsCurrentMessageComplete => !IsBusy || _typewriter.IsComplete;
        public int PendingCount => _messages.Count;

        public StatusMessageQueue(float charsPerSecond = 30f)
        {
            _charsPerSecond = Math.Max(0.01f, charsPerSecond);
        }

        public void Post(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Battle messages cannot be empty.", nameof(message));

            bool wasBusy = IsBusy;
            _messages.Enqueue(message);

            if (wasBusy)
                return;

            _typewriter.Start(message, _charsPerSecond);
            BusyStateChanged?.Invoke(true);
        }

        public void Update(float deltaTime)
        {
            if (IsBusy)
                _typewriter.Update(deltaTime);
        }

        public void Continue()
        {
            if (!IsBusy)
                return;

            if (!_typewriter.IsComplete)
            {
                _typewriter.SkipToEnd();
                return;
            }

            _messages.Dequeue();
            if (IsBusy)
            {
                _typewriter.Start(_messages.Peek(), _charsPerSecond);
                return;
            }

            BusyStateChanged?.Invoke(false);
        }
    }
}
