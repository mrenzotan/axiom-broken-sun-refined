/// <summary>
/// Calculates the world-space X offset for a parallax background layer.
/// parallaxFactor 0 = pinned to world (no parallax); 1 = infinitely distant.
/// </summary>
public class ParallaxBackground
{
    public float ParallaxFactor { get; }
    private readonly float _startPositionX;

    public ParallaxBackground(float startPositionX, float parallaxFactor)
    {
        _startPositionX = startPositionX;
        ParallaxFactor  = parallaxFactor;
    }

    /// <param name="cameraX">Camera's current world-space X position.</param>
    /// <returns>World-space X position for this background layer.</returns>
    public float CalculateOffsetX(float cameraX)
    {
        return _startPositionX + cameraX * (1f - ParallaxFactor);
    }
}