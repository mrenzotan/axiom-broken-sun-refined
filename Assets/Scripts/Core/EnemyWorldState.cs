// Assets/Scripts/Core/EnemyWorldState.cs
namespace Axiom.Core
{
    /// <summary>
    /// Immutable snapshot of one enemy's world position, captured before a battle begins.
    /// </summary>
    public sealed class EnemyWorldState
    {
        public float PositionX { get; }
        public float PositionY { get; }

        public EnemyWorldState(float positionX, float positionY)
        {
            PositionX = positionX;
            PositionY = positionY;
        }
    }
}
