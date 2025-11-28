using System.Collections;
using UnityEngine;

// ВНИМАНИЕ: Имя класса (PlayerController) должно совпадать с именем файла (PlayerController.cs)
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("1. Settings - Movement")]
    [Tooltip("Базовая скорость бега.")]
    [SerializeField] private float moveSpeed = 10f;
    [Tooltip("Сила прыжка.")]
    [SerializeField] private float jumpForce = 16f;
    [Tooltip("Множитель гравитации при падении.")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [Tooltip("Множитель гравитации при коротком прыжке.")]
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("2. Settings - Combat")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.7f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private float attackRate = 2f;
    private float nextAttackTime = 0f;

    [Header("3. Settings - Electro-Dash Ability")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private LayerMask dashInteractLayers;

    [Header("4. Settings - Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // --- Внутренние переменные ---
    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool isFacingRight = true;
    private bool canDash = true;
    private bool isDashing = false;
    private float defaultGravity;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        defaultGravity = rb.gravityScale;
    }

    private void Update()
    {
        if (isDashing) return;

        // Ввод движения
        moveInput.x = Input.GetAxisRaw("Horizontal");

        // Проверяем землю здесь, чтобы ввод прыжка видел актуальное состояние
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        else
            isGrounded = false;

        // Прыжок
        if (Input.GetButtonDown("Jump"))
        {
            if (isGrounded)
            {
                Debug.Log($"Jump pressed - grounded={isGrounded}");
                Jump();
            }
            else
            {
                Debug.Log($"Jump pressed but not grounded - grounded={isGrounded}");
                if (groundCheck == null)
                {
                    Debug.LogWarning("PlayerController: groundCheck is null. Assign a Transform under the player positioned near the feet.");
                }
                else
                {
                    Collider2D[] hits = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer);
                    Debug.Log($"OverlapCircleAll hit count: {hits.Length}. groundCheckPos={groundCheck.position}, radius={groundCheckRadius}, groundLayerMask={groundLayer.value}");
                    if (hits.Length > 0)
                    {
                        foreach (var h in hits)
                        {
                            Debug.Log($"Hit: {h.name}, layer={LayerMask.LayerToName(h.gameObject.layer)}");
                        }
                    }
                    else
                    {
                        Collider2D[] hitsAny = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius);
                        Debug.Log($"Overlap without mask hit count: {hitsAny.Length}");
                        if (hitsAny.Length > 0)
                        {
                            foreach (var h in hitsAny)
                                Debug.Log($"HitAny: {h.name}, layer={LayerMask.LayerToName(h.gameObject.layer)} (layerIndex={h.gameObject.layer})");
                        }
                    }
                }
            }
        }

        // Атака
        if (Input.GetButtonDown("Fire1"))
        {
            if (Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }

        // Рывок (Shift)
        if (Input.GetButtonDown("Fire3") && canDash)
        {
            StartCoroutine(DashAbility());
        }

        FlipController();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        else
            isGrounded = false;

        if (rb != null)
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
        else
            Debug.LogWarning("PlayerController: Rigidbody2D is null in FixedUpdate");
        ApplyBetterJumpPhysics();
    }

    private void Jump()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        else
        {
            Debug.LogWarning("PlayerController: Jump attempted but Rigidbody2D is null");
        }
    }

    private void ApplyBetterJumpPhysics()
    {
        if (rb == null) return;

        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = defaultGravity * fallMultiplier;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.gravityScale = defaultGravity * lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }
    }

    private void FlipController()
    {
        if (moveInput.x > 0 && !isFacingRight) Flip();
        else if (moveInput.x < 0 && isFacingRight) Flip();
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    private void Attack()
    {
        // anim.SetTrigger("Attack"); // Раскомментировать когда будет анимация
        if (attackPoint == null) return;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(meleeDamage);
            }
        }
    }

    private IEnumerator DashAbility()
    {
        canDash = false;
        isDashing = true;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        float dashDirection = isFacingRight ? 1f : -1f;
        
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);

        // Проверка столкновений во время рывка
        Vector2 dashBoxSize = new Vector2(dashSpeed * dashDuration, GetComponent<BoxCollider2D>().size.y);
        Collider2D[] dashHits = Physics2D.OverlapBoxAll(transform.position, dashBoxSize, 0f, dashInteractLayers);

        foreach (Collider2D hit in dashHits)
        {
             IDamageable damageable = hit.GetComponent<IDamageable>();
             if (damageable != null)
             {
                 damageable.TakeDamage(meleeDamage * 2);
             }
        }
        
        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = defaultGravity;
        rb.linearVelocity = Vector2.zero;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}