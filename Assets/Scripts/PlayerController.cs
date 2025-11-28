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
    [Tooltip("Множитель гравитации при коротком прыжке. (не используется — удержание прыжка не замедляет взлёт)")]
    [SerializeField] private float lowJumpMultiplier = 2f;
    [Tooltip("Горизонтальный множитель скорости в воздухе (0..1). Меньше — меньше продвижения в прыжке)")]
    [SerializeField] private float airborneHorizontalMultiplier = 0.6666667f;
    [Header("5. Contact")]
    [Tooltip("Урон при контакте с врагом, если у врага не задано своё значение")]
    [SerializeField] private float contactDamage = 10f;
    [Tooltip("Сила отбрасывания при контакте")]
    [SerializeField] private float contactKnockback = 8f;
    [Tooltip("Вертикальная составляющая отбрасывания")]
    [SerializeField] private float contactKnockbackY = 4f;
    [Tooltip("Время отключения управления после отбрасывания")]
    [SerializeField] private float knockbackDuration = 0.25f;
    [Tooltip("Включить подробные логи для отладки контактов с врагами")]
    [SerializeField] private bool debugContact = false;
    [Tooltip("Доп. множитель гравитации, когда вертикальная скорость мала и игрок держит кнопку прыжка (убирает 'зависание' на вершине)")]
    [SerializeField] private float hangGravityMultiplier = 1.8f;
    [Tooltip("Порог вертикальной скорости (м/с). Если vy меньше этого и >0 — применяется hangGravityMultiplier при удержании прыжка)")]
    [SerializeField] private float hangVelocityThreshold = 0.6f;
    [Tooltip("Время сглаживания по горизонтали на земле (меньше — более резкое ускорение)")]
    [SerializeField] private float accelerationTimeGrounded = 0.05f;
    [Tooltip("Время сглаживания по горизонтали в воздухе (меньше — более отзывчиво)")]
    [SerializeField] private float accelerationTimeAirborne = 0.15f;
    [Tooltip("Coyote time — доп. окно после схода с платформы, в течение которого разрешён прыжок")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("Jump buffer — как долго запоминаем нажатие прыжка перед приземлением")]
    [SerializeField] private float jumpBufferTime = 0.12f;

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
    private Health health;
    private Vector2 moveInput;
    private bool isGrounded;
    private float velocityXSmoothing;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool isFacingRight = true;
    private bool canDash = true;
    private bool isDashing = false;
    private float defaultGravity;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        defaultGravity = rb.gravityScale;
        health = GetComponent<Health>();

        if (debugContact)
        {
            Debug.Log($"Player Start: layer={LayerMask.LayerToName(gameObject.layer)}, hasHealth={health != null}");
            Debug.Log($"PlayerController enemyLayers mask value: {enemyLayers.value}");
        }

        // NOTE: collisions between player and enemies should remain enabled so
        // OnCollisionEnter2D/OnTriggerEnter2D fire and the player can receive contact damage.
        // If you don't want physical pushing, set enemy Rigidbody2D to Kinematic in the Inspector.
    }

    private bool isKnocked = false;
    [Header("Contact - Knockback tuning")]
    [Tooltip("Multiplier applied to gravity during knockback (0..1). Lower = longer arc (less immediate fall).")]
    [SerializeField] private float knockbackGravityMultiplier = 0.75f;
    [Tooltip("Horizontal damping applied during knockback (higher = quicker stop).")]
    [SerializeField] private float knockbackHorizontalDamping = 4f;
    [Tooltip("Extra time to keep player control locked after knockback ends to cover animation length.")]
    [SerializeField] private float extraKnockbackLockTime = 0.15f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (debugContact) Debug.Log($"OnCollisionEnter2D with {collision.collider.name} (layer={LayerMask.LayerToName(collision.collider.gameObject.layer)})");
        HandleContactWithEnemy(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (debugContact) Debug.Log($"OnTriggerEnter2D with {collider.name} (layer={LayerMask.LayerToName(collider.gameObject.layer)})");
        HandleContactWithEnemy(collider);
    }

    private void HandleContactWithEnemy(Collider2D col)
    {
        if (isKnocked)
        {
            if (debugContact) Debug.Log("HandleContactWithEnemy: ignored because isKnocked=true");
            return;
        }

        // Если это враг (может находиться на родителе) или реализует IDamageable
        Enemy enemy = col.GetComponentInParent<Enemy>();
        if (debugContact) Debug.Log($"HandleContactWithEnemy: Enemy component on parent = {enemy != null} for {col.name}");

        IDamageable damageable = col.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = col.GetComponentInParent<IDamageable>();
        if (debugContact) Debug.Log($"HandleContactWithEnemy: found IDamageable = {damageable != null} on {col.name}");

        if (damageable != null)
        {
            // Получаем урон: при наличии Enemy берём его attackDamage, иначе используем контактный урон игрока
            float dmg = contactDamage;
            if (enemy != null)
                dmg = enemy.attackDamage;

            if (debugContact) Debug.Log($"HandleContactWithEnemy: applying damage {dmg}");
            // Наносим урон игроку через компонент Health, если есть
            if (health != null)
            {
                health.TakeDamage(dmg);
                if (debugContact) Debug.Log($"Player health after damage: {health.CurrentHealth}");
            }
            else
            {
                if (debugContact) Debug.LogWarning("HandleContactWithEnemy: player has no Health component");
            }

            // Вычисляем направление отбрасывания: из источника в игрока
            Vector2 knockDir = (transform.position - col.transform.position).normalized;
            Vector2 knockVel = new Vector2(knockDir.x * contactKnockback, contactKnockbackY);
            if (debugContact) Debug.Log($"Applying knockback {knockVel} for {knockbackDuration}s");
            StartCoroutine(ApplyKnockback(knockVel, knockbackDuration));
        }
        else
        {
            if (debugContact) Debug.Log($"HandleContactWithEnemy: no damageable component found on {col.name}");
        }
    }

    private System.Collections.IEnumerator ApplyKnockback(Vector2 velocity, float duration)
    {
        isKnocked = true;

        if (rb == null)
        {
            yield break;
        }

        // Prevent player input while knocked
        moveInput = Vector2.zero;

        // Disable animator to prevent animation from moving the root transform during knockback
        bool prevAnimEnabled = true;
        if (anim != null)
        {
            prevAnimEnabled = anim.enabled;
            if (anim.enabled)
                anim.enabled = false;
        }

        // Интерпретируем incoming velocity.y как желаемую высоту вершины дуги (peak height)
        float peakHeight = velocity.y;
        float horizSpeed = velocity.x;

        // Используем физическую формулу: v_y = sqrt(2 * g * h)
        // g = |Physics2D.gravity.y| * gravityScale (используем defaultGravity для расчёта)
        float g = Mathf.Abs(Physics2D.gravity.y) * defaultGravity;
        if (g <= 0f)
        {
            // fallback — просто применяем переданную скорость
            rb.linearVelocity = new Vector2(horizSpeed, peakHeight);
        }
        else
        {
            float initialVy = 0f;
            if (peakHeight > 0f)
                initialVy = Mathf.Sqrt(2f * g * peakHeight);

            // Устанавливаем начальную скорость для естественной параболы
            rb.linearVelocity = new Vector2(horizSpeed, initialVy);
        }

        float t = 0f;
        float total = duration + extraKnockbackLockTime;
        while (t < total)
        {
            t += Time.deltaTime;

            // Горизонтальное затухание: приближаем к 0
            Vector2 v = rb.linearVelocity;
            v.x = Mathf.Lerp(v.x, 0f, Time.deltaTime * knockbackHorizontalDamping);
            rb.linearVelocity = v;

            yield return null;
        }

        // Гарантируем восстановление управления/гравитации
        rb.gravityScale = defaultGravity;

        // Restore animator
        if (anim != null)
            anim.enabled = prevAnimEnabled;

        isKnocked = false;
    }

    private void Update()
    {
        if (isDashing || isKnocked) return;

        // Ввод движения
        moveInput.x = Input.GetAxisRaw("Horizontal");

        // Проверяем землю здесь, чтобы ввод прыжка видел актуальное состояние
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        else
            isGrounded = false;

        // Прыжок — используем jump buffer: запоминаем нажатие
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            // уменьшаем счётчик буфера
            if (jumpBufferCounter > 0)
                jumpBufferCounter -= Time.deltaTime;
        }

        // Атака (кнопка X)
        if (!isKnocked && Input.GetKeyDown(KeyCode.X))
        {
            if (Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }

        // Рывок (кнопка C)
        if (!isKnocked && Input.GetKeyDown(KeyCode.C) && canDash)
        {
            StartCoroutine(DashAbility());
        }

        FlipController();
    }

    private void FixedUpdate()
    {
        if (isDashing || isKnocked) return;
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        else
            isGrounded = false;

        // Coyote time: если недавно были на земле — даём дополнительное окно
        if (isGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.fixedDeltaTime;

        // Горизонтальная плавность движения
        float targetVelocityX = moveInput.x * moveSpeed;
        // Уменьшаем горизонтальную скорость в воздухе
        if (!isGrounded)
            targetVelocityX *= airborneHorizontalMultiplier;
        float smoothing = isGrounded ? accelerationTimeGrounded : accelerationTimeAirborne;
        if (rb != null)
        {
            float newVelX = Mathf.SmoothDamp(rb.linearVelocity.x, targetVelocityX, ref velocityXSmoothing, smoothing);
            rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
        }
        else
        {
            // Rigidbody missing — silently ignore in build
        }

        // Попытка прыжка: если есть буфер и есть coyote или стоим на земле
        if (jumpBufferCounter > 0f && (isGrounded || coyoteCounter > 0f))
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

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
            // Rigidbody missing — ignore
        }
    }

    private void ApplyBetterJumpPhysics()
    {
        if (isKnocked) return;
        if (rb == null) return;

        if (rb.linearVelocity.y < 0f)
        {
            // Быстрое  падение
            rb.gravityScale = defaultGravity * fallMultiplier;
        }
        else if (rb.linearVelocity.y > 0f)
        {
            // Если отпущена кнопка — короткий прыжок
            if (!Input.GetButton("Jump"))
            {
                rb.gravityScale = defaultGravity * lowJumpMultiplier;
            }
            else
            {
                // Игрок держит кнопку: применяем нормальную гравитацию,
                // но если вертикальная скорость очень мала (почти вершина), добавляем небольшую дополнительную гравитацию,
                // чтобы избежать «зависания» на вершине.
                if (rb.linearVelocity.y > 0f && rb.linearVelocity.y < hangVelocityThreshold)
                    rb.gravityScale = defaultGravity * hangGravityMultiplier;
                else
                    rb.gravityScale = defaultGravity;
            }
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