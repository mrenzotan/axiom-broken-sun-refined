// Assets/Scripts/Core/WorldSnapshot.cs
using System;
using System.Collections.Generic;

namespace Axiom.Core
{
    /// <summary>
    /// Stores the Platformer world state captured immediately before a battle begins.
    /// Held by <see cref="GameManager"/> and consumed by PlatformerWorldRestoreController on return.
    /// </summary>
    public sealed class WorldSnapshot
    {
        private readonly Dictionary<string, EnemyWorldState> _enemies =
            new Dictionary<string, EnemyWorldState>(StringComparer.Ordinal);

        private readonly Dictionary<string, bool> _interactables =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Records an enemy's position by stable ID.
        /// Silently ignores null/whitespace IDs.
        /// If the same ID is captured twice the second call overwrites the first.
        /// </summary>
        public void CaptureEnemy(string enemyId, float positionX, float positionY)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) return;
            _enemies[enemyId] = new EnemyWorldState(positionX, positionY);
        }

        /// <returns>
        /// <c>true</c> and populates <paramref name="state"/> if the ID is found;
        /// <c>false</c> and <c>null</c> state for unknown or invalid IDs.
        /// </returns>
        public bool TryGetEnemy(string enemyId, out EnemyWorldState state)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                state = null;
                return false;
            }
            return _enemies.TryGetValue(enemyId, out state);
        }

        /// <summary>
        /// Records an interactable object's active state by stable ID.
        /// Silently ignores null/whitespace IDs.
        /// If the same ID is captured twice the second call overwrites the first.
        /// </summary>
        public void CaptureInteractable(string objectId, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return;
            _interactables[objectId] = isActive;
        }

        /// <returns>
        /// <c>true</c> and populates <paramref name="isActive"/> if the ID is found;
        /// <c>false</c> and <c>false</c> for unknown or invalid IDs.
        /// </returns>
        public bool TryGetInteractable(string objectId, out bool isActive)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                isActive = default;
                return false;
            }
            return _interactables.TryGetValue(objectId, out isActive);
        }
    }
}
