using UnityEngine;

/// <summary>
/// Unity wrapper for a reusable pushable ice block.
/// Handles collision callbacks and Rigidbody2D writes only; push decisions live in PushableIceBlockMotion.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PushableIceBlock : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Push")]
    [SerializeField, Range(0.1f, 1f)] private float playerPushSpeedMultiplier = 0.7f;
    [SerializeField, Min(0f)] private float maxPushSpeed = 5.6f;
    [SerializeField, Range(0.1f, 1f)] private float sideNormalThreshold = 0.7f;
    [SerializeField, Min(0f)] private float minPushVelocity = 0.05f;

    [Header("Stop Feel")]
    [SerializeField, Min(0f)] private float stopDeceleration = 18f;

    [Header("Ground Check")]
    [SerializeField, Min(0.001f)] private float groundCheckHeight = 0.08f;
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.08f;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private bool _hasPushRequest;
    private float _requestedVelocityX;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    private void Reset()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 1f;
        }

        Collider2D blockCollider = GetComponent<Collider2D>();
        if (blockCollider != null)
            blockCollider.isTrigger = false;
    }

    private void FixedUpdate()
    {
        float velocityX = _hasPushRequest
            ? _requestedVelocityX
            : Mathf.MoveTowards(_rb.linearVelocity.x, 0f, stopDeceleration * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector2(velocityX, _rb.linearVelocity.y);
        _hasPushRequest = false;
        _requestedVelocityX = 0f;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!IsInLayerMask(collision.gameObject.layer, playerLayer))
            return;

        if (!collision.collider.CompareTag("Player"))
            return;

        Rigidbody2D playerRb = collision.rigidbody;
        if (playerRb == null)
            return;

        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        bool blockGrounded = IsGrounded();
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (!PushableIceBlockMotion.CanPush(
                    playerX: playerRb.position.x,
                    blockX: _rb.position.x,
                    playerVelocityX: playerRb.linearVelocity.x,
                    contactNormal: contact.normal,
                    blockGrounded: blockGrounded,
                    sideNormalThreshold: sideNormalThreshold,
                    minPushVelocity: minPushVelocity))
            {
                continue;
            }

            player.RequestExternalMoveSpeedMultiplier(playerPushSpeedMultiplier);
            _requestedVelocityX = PushableIceBlockMotion.GetPushVelocityX(
                playerRb.linearVelocity.x,
                maxPushSpeed);
            _hasPushRequest = true;
            return;
        }
    }

    private bool IsGrounded()
    {
        Bounds bounds = _collider.bounds;
        var origin = new Vector2(bounds.center.x, bounds.min.y + groundCheckHeight * 0.5f);
        var size = new Vector2(bounds.size.x * 0.9f, groundCheckHeight);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
