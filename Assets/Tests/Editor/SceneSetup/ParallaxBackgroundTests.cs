using NUnit.Framework;

public class ParallaxBackgroundTests
{
    [Test]
    public void CalculateOffsetX_ReturnsStartPosition_WhenCameraAtZero()
    {
        var layer = new ParallaxBackground(startPositionX: 5f, parallaxFactor: 0.5f);
        Assert.AreEqual(5f, layer.CalculateOffsetX(cameraX: 0f), 0.001f,
            "At camera X=0 the layer must sit at its start position");
    }

    [Test]
    public void CalculateOffsetX_LayerMovesAtHalfCameraSpeed_ForFactor0point5()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.5f);
        // travel = cameraX * (1 - 0.5) = 10 * 0.5 = 5
        Assert.AreEqual(5f, layer.CalculateOffsetX(cameraX: 10f), 0.001f,
            "Factor 0.5: layer should move at half the camera speed");
    }

    [Test]
    public void CalculateOffsetX_LayerDoesNotMove_WhenFactorIsOne()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 1.0f);
        // travel = cameraX * (1 - 1) = 0 — layer pinned in world space (infinitely far)
        Assert.AreEqual(0f, layer.CalculateOffsetX(cameraX: 100f), 0.001f,
            "Factor 1.0: layer must stay at startPositionX regardless of camera movement");
    }

    [Test]
    public void CalculateOffsetX_LayerMovesWithCamera_WhenFactorIsZero()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.0f);
        // travel = cameraX * (1 - 0) = cameraX — layer tracks camera exactly (no parallax)
        Assert.AreEqual(10f, layer.CalculateOffsetX(cameraX: 10f), 0.001f,
            "Factor 0: layer moves at full camera speed (pinned to world)");
    }

    [Test]
    public void CalculateOffsetX_WorksNegativeCameraDirection()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.7f);
        // travel = -10 * (1 - 0.7) = -3
        Assert.AreEqual(-3f, layer.CalculateOffsetX(cameraX: -10f), 0.001f,
            "Parallax must work when camera moves left (negative X)");
    }

    [Test]
    public void ParallaxFactor_IsPreservedFromConstructor()
    {
        var layer = new ParallaxBackground(startPositionX: 0f, parallaxFactor: 0.9f);
        Assert.AreEqual(0.9f, layer.ParallaxFactor, 0.001f);
    }
}
