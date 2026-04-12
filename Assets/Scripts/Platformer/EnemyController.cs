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

    [Header("Visual")]
    [SerializeField] private Transform visualTransform;

    private Rigidbody2D _rb;
    private EnemyPatrolBehavior _behavior;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

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
        Collider2D hit  = Physics2D.OverlapCircle(transform.position, aggroRadius, playerLayer);
        bool detected   = hit != null;
        Vector2 playerPos = detected ? (Vector2)hit.transform.position : Vector2.zero;

        float xVel = _behavior.Tick((Vector2)transform.position, detected, playerPos, Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector2(xVel, _rb.linearVelocity.y);

        if (visualTransform != null)
            visualTransform.localScale = new Vector3(_behavior.FacingDirectionX, 1f, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
    }
}
