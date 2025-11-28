using UnityEngine;

// Simple melee enemy: walks left/right, flips direction when hitting walls.
// Inherits from abstract Enemy base class.
public class MeleeEnemy : Enemy
{
    [Header("Melee Movement")]
    [Tooltip("Local horizontal patrol speed (overrides base walkSpeed for this enemy).")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("Distance to check for walls ahead (raycast).")]
    [SerializeField] private float wallCheckDistance = 0.2f;

    [Tooltip("Layers considered as obstacles/walls for flipping direction.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Optional point to cast the wall check from. If null, uses transform.position.")]
    [SerializeField] private Transform wallCheckPoint;

    private int moveDir = 1; // 1 = right, -1 = left

    protected override void Start()
    {
        base.Start();
        // If patrolSpeed not set, use base walkSpeed
        if (patrolSpeed <= 0f) patrolSpeed = walkSpeed;

        // default wall check point
        if (wallCheckPoint == null) wallCheckPoint = transform;

        // Randomize initial direction slightly for variety
        if (Random.value < 0.5f) moveDir = -1;
        ApplyFacing();
    }

    protected override void PatrolUpdate()
    {
        // Move horizontally using Rigidbody2D when available, otherwise try NavMeshAgent fallback
        if (rb2d != null)
        {
            Vector2 vel = rb2d.linearVelocity;
            vel.x = moveDir * patrolSpeed;
            rb2d.linearVelocity = vel;
        }
        else if (agent != null)
        {
            // Move agent slightly in local horizontal direction
            Vector3 step = transform.right * (moveDir * patrolSpeed * Time.deltaTime);
            agent.Move(step);
        }

        // Wall check via raycast in front of the enemy
        Vector2 origin = wallCheckPoint != null ? (Vector2)wallCheckPoint.position : (Vector2)transform.position;
        Vector2 dir = Vector2.right * moveDir;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallCheckDistance, obstacleLayers);
        if (hit.collider != null)
        {
            FlipDirection();
            return;
        }

        // Also check small overlap ahead to catch collisions
        Collider2D overlap = Physics2D.OverlapCircle(origin + dir * (wallCheckDistance * 0.5f), 0.05f, obstacleLayers);
        if (overlap != null)
        {
            FlipDirection();
            return;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If we hit an obstacle horizontally, flip direction
        foreach (var c in collision.contacts)
        {
            // Check the contact normal: if it's mostly horizontal opposite to moveDir, flip
            if (Mathf.Abs(c.normal.x) > 0.5f)
            {
                if (Mathf.Sign(c.normal.x) == -moveDir)
                {
                    FlipDirection();
                    return;
                }
            }
        }
    }

    private void FlipDirection()
    {
        moveDir *= -1;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (moveDir > 0 ? 1f : -1f);
        transform.localScale = s;
    }
}
