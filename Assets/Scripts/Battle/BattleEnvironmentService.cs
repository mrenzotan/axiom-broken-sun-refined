using UnityEngine;
using Axiom.Data;

namespace Axiom.Battle
{
    /// <summary>
    /// Applies a BattleEnvironmentData SO to a SpriteRenderer.
    /// Pure C# — zero Unity lifecycle. Call from BattleController.Start() before battle begins.
    /// </summary>
    public sealed class BattleEnvironmentService
    {
        /// <summary>
        /// Applies the environment data to the given renderer.
        /// If either argument is null, silently no-ops (fallback to static background).
        /// </summary>
        public void Apply(BattleEnvironmentData environmentData, SpriteRenderer backgroundRenderer)
        {
            if (environmentData == null) return;
            if (backgroundRenderer == null) return;

            backgroundRenderer.sprite = environmentData.backgroundSprite;
            backgroundRenderer.color = environmentData.ambientTint;
        }
    }
}
