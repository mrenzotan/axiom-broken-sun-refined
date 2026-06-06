using System;

namespace Axiom.Voice
{
    public sealed class BootstrapResourceGuard
    {
        private bool _teardownRequested;

        public bool TeardownRequested => _teardownRequested;

        public void RequestTeardown() => _teardownRequested = true;

        public T TakeOrDispose<T>(T candidate) where T : class, IDisposable
        {
            if (candidate == null)
                return null;

            if (_teardownRequested)
            {
                candidate.Dispose();
                return null;
            }

            return candidate;
        }
    }
}
