using UnityEngine;

/// <summary>
/// Plain C# push decision helper for PushableIceBlock.
/// No MonoBehaviour lifecycle and no direct Rigidbody writes live here.
/// </summary>
public static class PushableIceBlockMotion
{
    public static bool CanPush(
        float playerX,
        float blockX,
        float playerVelocityX,
        Vector2 contactNormal,
        bool blockGrounded,
        float sideNormalThreshold,
        float minPushVelocity)
    {
        if (!blockGrounded)
            return false;

        if (Mathf.Abs(contactNormal.x) < sideNormalThreshold)
            return false;

        if (Mathf.Abs(contactNormal.x) <= Mathf.Abs(contactNormal.y))
            return false;

        if (Mathf.Abs(playerVelocityX) < minPushVelocity)
            return false;

        float directionFromPlayerToBlock = blockX - playerX;
        if (Mathf.Abs(directionFromPlayerToBlock) < 0.001f)
            return false;

        return Mathf.Sign(playerVelocityX) == Mathf.Sign(directionFromPlayerToBlock);
    }

    public static float GetPushVelocityX(float playerVelocityX, float maxPushSpeed)
    {
        float speed = Mathf.Min(Mathf.Abs(playerVelocityX), Mathf.Max(0f, maxPushSpeed));
        return Mathf.Sign(playerVelocityX) * speed;
    }
}
