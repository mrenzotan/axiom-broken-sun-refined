/// <summary>
/// Plain C# rising-edge detector for enemy aggro. Returns true only on the call where the
/// player is newly detected (a false -> true transition), so the aggro "!" indicator fires
/// once per detection instead of every frame the player stays inside the aggro radius.
/// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
/// </summary>
public class AggroAlertGate
{
    private bool _wasDetected;

    /// <summary>
    /// Records the current detection state and reports whether this is a new detection.
    /// </summary>
    /// <param name="detected">
    /// True when the enemy currently detects the player (inside the aggro radius and able to chase).
    /// </param>
    /// <returns>True only on the rising edge: not detected on the previous call, detected now.</returns>
    public bool RegisterDetection(bool detected)
    {
        bool rising = detected && !_wasDetected;
        _wasDetected = detected;
        return rising;
    }
}
