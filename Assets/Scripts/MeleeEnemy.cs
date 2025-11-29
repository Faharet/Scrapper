using UnityEngine;

/// <summary>
/// Простая мелли-AI: ходит влево-вправо, разворачивается при стене.
/// Полностью совместим с Enemy2D.
/// </summary>
public class MeleeEnemy : Enemy
{
    [Header("Melee Movement")]
    [Tooltip("Скорость патруля (если 0 — берём walkSpeed из Enemy).")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("Дистанция проверки стены вперёд.")]
    [SerializeField] private float wallCheckDistance = 0.3f;

    [Tooltip("Слои препятствий.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Точка откуда кастовать Raycast (если null — transform).")]
    [SerializeField] private Transform wallCheckPoint;

    private int moveDir = 1; // 1 = вправо, -1 = влево

    protected override void Start()
    {
        base.Start();

        if (patrolSpeed <= 0f)
            patrolSpeed = walkSpeed;

        if (wallCheckPoint == null)
            wallCheckPoint = transform;

        // Немного разнообразия
        if (Random.value < 0.5f)
            moveDir = -1;

        ApplyFacing();
    }

    /// <summary>
    /// Патруль — работает ТОЛЬКО если Enemy находится в State.Patrol
    /// </summary>
    protected override void PatrolUpdate()
    {
        if (rb2d == null) return;

        // Движение по X
        rb2d.linearVelocity = new Vector2(moveDir * patrolSpeed, rb2d.linearVelocity.y);

        // Проверка стены
        Vector2 origin = wallCheckPoint.position;
        Vector2 dir = new Vector2(moveDir, 0f);

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallCheckDistance, obstacleLayers);
        if (hit.collider != null)
        {
            FlipDirection();
            return;
        }

        // Маленькая дополнительная проверка
        Collider2D overlap = Physics2D.OverlapCircle(
            origin + dir * (wallCheckDistance * 0.5f),
            0.05f,
            obstacleLayers
        );

        if (overlap != null)
        {
            FlipDirection();
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Любой боковой удар — разворот
        foreach (var c in collision.contacts)
        {
            if (Mathf.Abs(c.normal.x) > 0.5f)
            {
                // Столкновение ВПЕРЕДИ
                if (Mathf.Sign(c.normal.x) == -moveDir)
                {
                    FlipDirection();
                }
            }
        }
    }
}
