namespace Axiom.Core
{
    /// <summary>
    /// Identifies which overlay style to use for a scene transition.
    /// Color and timing are owned by SceneTransitionService.
    /// </summary>
    public enum TransitionStyle
    {
        /// <summary>Platformer → Battle: 0.2s flash to white, 0.8s reveal from white.</summary>
        WhiteFlash,
        /// <summary>Battle → Platformer: 0.5s fade to black, 0.5s reveal from black.</summary>
        BlackFade,
    }
}
