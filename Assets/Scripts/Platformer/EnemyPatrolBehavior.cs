using UnityEngine;

/// <summary>
/// Plain C# class — all enemy patrol/aggro/return state machine logic lives here.
/// No MonoBehaviour, no Unity lifecycle. Injected into EnemyController.
///
/// Tick() returns desired X velocity. The caller (EnemyController) applies it to Rigidbody2D
/// and sets Transform.localScale.x based on FacingDirectionX.
/// </summary>
public class EnemyPatrolBehavior
{
    public enum State { Patrol, Aggro, Returning }

    public State CurrentState { get; private set; }

    /// <summary>1f when facing right, -1f when facing left.
    /// Set visualTransform.localScale.x to this value in EnemyController.</summary>
    public float FacingDirectionX { get; private set; }

    private readonly Vector2[] _waypoints;
    private readonly float _patrolSpeed;
    private readonly float _chaseSpeed;
    private readonly float _waypointThreshold;
    private readonly float _deaggroGracePeriod;

    private int _currentWaypointIndex;
    private float _deaggroTimer;

    public EnemyPatrolBehavior(
        Vector2[] waypoints,
        float patrolSpeed,
        float chaseSpeed,
        float waypointThreshold = 0.2f,
        float deaggroGracePeriod = 0.5f)
    {
        _waypoints = waypoints ?? new Vector2[0];
        _patrolSpeed = patrolSpeed;
        _chaseSpeed = chaseSpeed;
        _waypointThreshold = waypointThreshold;
        _deaggroGracePeriod = deaggroGracePeriod;
        CurrentState = State.Patrol;
        FacingDirectionX = 1f;
    }

    /// <summary>
    /// Call from EnemyController.FixedUpdate(). Returns desired X velocity.
    /// Caller must apply: rb.linearVelocity = new Vector2(result, rb.linearVelocity.y)
    /// </summary>
    public float Tick(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        switch (CurrentState)
        {
            case State.Patrol:    return TickPatrol(currentPosition, playerDetected, playerPosition, deltaTime);
            case State.Aggro:     return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
            case State.Returning: return TickReturning(currentPosition, playerDetected, playerPosition, deltaTime);
            default:              return 0f;
        }
    }

    private float TickPatrol(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (playerDetected)
        {
            CurrentState = State.Aggro;
            _deaggroTimer = _deaggroGracePeriod;
            return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
        }

        if (_waypoints.Length == 0)
            return 0f;

        Vector2 target = _waypoints[_currentWaypointIndex];
        float dx = target.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;
            return 0f;
        }

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _patrolSpeed;
    }

    private float TickAggro(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (!playerDetected)
        {
            _deaggroTimer -= deltaTime;
            if (_deaggroTimer <= 0f)
            {
                CurrentState = State.Returning;
                return 0f;
            }
            return FacingDirectionX * _chaseSpeed;
        }

        _deaggroTimer = _deaggroGracePeriod;
        float dx = playerPosition.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
            return 0f;

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _chaseSpeed;
    }

    private float TickReturning(Vector2 currentPosition, bool playerDetected, Vector2 playerPosition, float deltaTime)
    {
        if (playerDetected)
        {
            CurrentState = State.Aggro;
            _deaggroTimer = _deaggroGracePeriod;
            return TickAggro(currentPosition, playerDetected, playerPosition, deltaTime);
        }

        if (_waypoints.Length == 0)
        {
            CurrentState = State.Patrol;
            return 0f;
        }

        Vector2 target = _waypoints[_currentWaypointIndex];
        float dx = target.x - currentPosition.x;

        if (Mathf.Abs(dx) <= _waypointThreshold)
        {
            CurrentState = State.Patrol;
            return 0f;
        }

        FacingDirectionX = dx > 0f ? 1f : -1f;
        return FacingDirectionX * _patrolSpeed;
    }
}
