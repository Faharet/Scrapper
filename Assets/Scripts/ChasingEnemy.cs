// Файл: ChasingEnemy.cs (Без изменений, так как все исправлено в Enemy.cs)

using UnityEngine;

/// <summary>
/// AI врага: патрулирует в радиусе от начальной точки и агрессивно преследует игрока.
/// </summary>
public class ChasingEnemy : Enemy
{
    // ... (Fields)
    [Header("Patrol Settings")]
    [Tooltip("Радиус патрулирования от начальной позиции.")]
    [SerializeField] private float patrolRadius = 5f;
    
    [Tooltip("Дистанция проверки стены вперёд.")]
    [SerializeField] private float wallCheckDistance = 0.3f;
    
    [Tooltip("Слои препятствий (стен, платформ).")]
    [SerializeField] private LayerMask obstacleLayers;
    
    [Tooltip("Точка откуда кастовать Raycast (если null — transform).")]
    [SerializeField] private Transform wallCheckPoint;

    private Vector3 startPosition;
    private int moveDir = 1; 

    protected override void Start()
    {
        base.Start();

        startPosition = transform.position;

        if (wallCheckPoint == null)
            wallCheckPoint = transform;

        moveDir = Random.value < 0.5f ? -1 : 1;

        ApplyFacing();
    }

    // -----------------------------
    // PATROL (ПАТРУЛЬ В РАДИУСЕ)
    // -----------------------------
    protected override void PatrolUpdate()
    {
        if (rb2d == null) return;

        // Проверка границ патруля
        float currentX = transform.position.x;
        float startX = startPosition.x;

        if (currentX < startX - patrolRadius)
        {
            moveDir = 1; 
            ApplyFacing();
        }
        else if (currentX > startX + patrolRadius)
        {
            moveDir = -1; 
            ApplyFacing();
        }
        else
        {
            CheckForObstacle();
        }

        rb2d.linearVelocity = new Vector2(moveDir * walkSpeed, rb2d.linearVelocity.y);

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(rb2d.linearVelocity.x));
    }

    // -----------------------------
    // CHASE (ПРЕСЛЕДОВАНИЕ) - ПЕРЕОПРЕДЕЛЕНИЕ
    // -----------------------------
    protected override void ChaseUpdate()
    {
        // Теперь здесь происходит и движение, и остановка, и нанесение урона.
        base.ChaseUpdate(); 
        
        if (target != null)
        {
            // Поворот спрайта
            float dir = Mathf.Sign(target.position.x - transform.position.x);
            moveDir = (int)dir;
            ApplyFacing();
        }
    }


    // -----------------------------
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // -----------------------------
    private void CheckForObstacle()
    {
        Vector2 origin = wallCheckPoint.position;
        Vector2 dir = new Vector2(moveDir, 0f);

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallCheckDistance, obstacleLayers);
        if (hit.collider != null)
        {
            FlipDirection();
            return;
        }

        Collider2D overlap = Physics2D.OverlapCircle(origin + dir * (wallCheckDistance * 0.5f), 0.05f, obstacleLayers);
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
        if (state != State.Patrol) return;

        foreach (var c in collision.contacts)
        {
            if (Mathf.Abs(c.normal.x) > 0.5f && Mathf.Sign(c.normal.x) == -moveDir)
            {
                FlipDirection();
            }
        }
    }

    // -----------------------------
    // GIZMOS
    // -----------------------------
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(startPosition.x - patrolRadius, startPosition.y, 0),
                            new Vector3(startPosition.x - patrolRadius, startPosition.y + 1f, 0));
            Gizmos.DrawLine(new Vector3(startPosition.x + patrolRadius, startPosition.y, 0),
                            new Vector3(startPosition.x + patrolRadius, startPosition.y + 1f, 0));
        }

        if (wallCheckPoint != null)
        {
            Gizmos.color = Color.blue;
            Vector3 targetPos = wallCheckPoint.position + (Vector3.right * moveDir * wallCheckDistance);
            Gizmos.DrawLine(wallCheckPoint.position, targetPos);
            Gizmos.DrawSphere(targetPos, 0.05f);
        }
    }
}