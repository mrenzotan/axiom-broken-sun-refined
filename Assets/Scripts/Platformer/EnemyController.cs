using UnityEngine;

/// <summary>
/// MonoBehaviour — Unity lifecycle and physics bridge only.
/// All patrol/aggro/return logic is delegated to EnemyPatrolBehavior.
///
/// Required prefab structure:
///   Enemy (root — Rigidbody2D, Collider2D, EnemyController)
///   └── Visual (child — SpriteRenderer, Animator)
///          EnemyController sets Visual's Transform.localScale.x for sprite flipping.
///          Never use SpriteRenderer.FlipX — see GAME_PLAN.md §6 Sprite Flipping.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed      = 3f;
    [SerializeField] private float chaseSpeed       = 5f;
    [SerializeField] private float waypointThreshold = 0.2f;

    [Header("Aggro")]
    [SerializeField] private float aggroRadius        = 5f;
    [SerializeField] private float deaggroGracePeriod = 0.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Ledge Detection")]
    [SerializeField] private float ledgeCheckOffsetX = 0.4f;  // horizontal distance ahead of feet to cast from
    [SerializeField] private float ledgeCheckDepth   = 0.6f;  // how far down to cast
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    private Rigidbody2D _rb;
    private EnemyPatrolBehavior _behavior;
    private float _visualOffsetX;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _visualOffsetX = visualTransform != null ? visualTransform.localPosition.x : 0f;

        var waypoints = new Vector2[patrolPoints != null ? patrolPoints.Length : 0];
        for (int i = 0; i < waypoints.Length; i++)
            waypoints[i] = patrolPoints[i].position;

        _behavior = new EnemyPatrolBehavior(
            waypoints,
            patrolSpeed,
            chaseSpeed,
            waypointThreshold,
            deaggroGracePeriod);
    }

    private void FixedUpdate()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, aggroRadius, playerLayer);
        bool detected = hit != null;
        Vector2 playerPos = detected ? (Vector2)hit.transform.position : Vector2.zero;

        if (detected && IsLedgeAhead())
            detected = false;

        float xVel = _behavior.Tick((Vector2)transform.position, detected, playerPos, Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(xVel, _rb.linearVelocity.y);

        if (visualTransform != null)
        {
            Vector3 s = visualTransform.localScale;
            s.x = Mathf.Abs(s.x) * _behavior.FacingDirectionX;
            visualTransform.localScale = s;

            Vector3 p = visualTransform.localPosition;
            p.x = Mathf.Abs(_visualOffsetX) * _behavior.FacingDirectionX;
            visualTransform.localPosition = p;
        }
    }

    private bool IsLedgeAhead()
    {
        float ahead = _behavior.FacingDirectionX * ledgeCheckOffsetX;
        Vector2 origin = (Vector2)transform.position + new Vector2(ahead, 0f);
        return !Physics2D.Raycast(origin, Vector2.down, ledgeCheckDepth, groundLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);

        Gizmos.color = Color.red;
        float ahead = Application.isPlaying && _behavior != null
            ? _behavior.FacingDirectionX * ledgeCheckOffsetX
            : ledgeCheckOffsetX;
        Vector2 ledgeOrigin = (Vector2)transform.position + new Vector2(ahead, 0f);
        Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector2.down * ledgeCheckDepth);
    }
}
