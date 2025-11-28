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
    [Tooltip("Multiplier applied to walking speed immediately after dash (e.g. 1.5 = 50% faster)")]
    [SerializeField] private float postDashRunMultiplier = 1.5f;
    [Tooltip("Rate at which the post-dash speed multiplier decays back to 1 (units per second)")]
    [SerializeField] private float postDashDecayRate = 0.8f;

    [Header("4. Settings - Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("5. Contact")]
    [Tooltip("Урон при контакте с врагом, если у врага не задано своё значение")]
    [SerializeField] private float contactDamage = 10f;
    [Tooltip("Сила отбрасывания при контакте")]
    [SerializeField] private float contactKnockback = 8f;
    [Tooltip("Вертикальная составляющая отбрасывания")]
    [SerializeField] private float contactKnockbackY = 4f;
    [Tooltip("Время отключения управления после отбрасывания")]
    [SerializeField] private float knockbackDuration = 0.25f;
    [Tooltip("Время неуязвимости после получения урона (секунды)")]
    [SerializeField] private float postDamageInvulnerability = 1f;
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
    [Header("Contact - Knockback tuning")]
    [Tooltip("Multiplier applied to gravity during knockback (0..1). Lower = longer arc (less immediate fall).")]
    [SerializeField] private float knockbackGravityMultiplier = 0.75f;
    [Tooltip("Horizontal damping applied during knockback (higher = quicker stop).")]
    [SerializeField] private float knockbackHorizontalDamping = 4f;
    [Tooltip("Extra time to keep player control locked after knockback ends to cover animation length.")]
    [SerializeField] private float extraKnockbackLockTime = 0.15f;
    [Header("Invulnerability - Visuals")]
    [Tooltip("Интервал мигания в секундах во время инвulnerability")]
    [SerializeField] private float invulnerabilityBlinkInterval = 0.12f;
    [Tooltip("Альфа при мигании (0..1)")]
    [SerializeField] private float invulnerabilityBlinkAlpha = 0.35f;
    [Tooltip("Имя слоя, в который будет временно помещён игрок во время инвulnerability. Создайте этот слой в Project Settings -> Tags and Layers.")]
    [SerializeField] private string invulnerableLayerName = "InvulnerablePlayer";

    // --- Внутренние переменные ---
    private Rigidbody2D rb;
    private Animator anim;
    private Health health;
    private Collider2D[] playerColliders;
    private bool[] prevPlayerColliderIsTrigger;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private int invulnerableLayerIndex = -1;
    private int originalLayerIndex = -1;
    private Vector2 moveInput;
    private bool isGrounded;
    private float velocityXSmoothing;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool isFacingRight = true;
    private bool canDash = true;
    private bool isDashing = false;
    private bool isPostDashRunning = false;
    private float postDashCurrentMultiplier = 1f;
    private float defaultGravity;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        playerColliders = GetComponents<Collider2D>();
        if (playerColliders != null && playerColliders.Length > 0)
        {
            prevPlayerColliderIsTrigger = new bool[playerColliders.Length];
            for (int i = 0; i < playerColliders.Length; i++)
                prevPlayerColliderIsTrigger[i] = playerColliders[i] != null ? playerColliders[i].isTrigger : false;
        }

        // cache invulnerable layer index (must exist in project)
        if (!string.IsNullOrEmpty(invulnerableLayerName))
        {
            invulnerableLayerIndex = LayerMask.NameToLayer(invulnerableLayerName);
            if (invulnerableLayerIndex == -1 && debugContact)
                Debug.LogWarning($"PlayerController: invulnerable layer '{invulnerableLayerName}' not found. Layer-swap invulnerability will be skipped.");
        }
        defaultGravity = rb.gravityScale;
        health = GetComponent<Health>();

        // cache sprite renderers for blink effect
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++) originalColors[i] = spriteRenderers[i].color;
        }

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
    private bool isInvulnerable = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (debugContact) Debug.Log($"OnCollisionEnter2D with {collision.collider.name} (layer={LayerMask.LayerToName(collision.collider.gameObject.layer)})");
        // Detect stomps: if player collides from above (contact normal pointing up), treat as player stomping the enemy
        bool handledAsStomp = false;
        try
        {
            IDamageable otherDamageable = collision.collider.GetComponentInParent<IDamageable>();
            Enemy otherEnemy = collision.collider.GetComponentInParent<Enemy>();
            if (otherDamageable != null && collision.contacts != null && collision.contacts.Length > 0)
            {
                foreach (var cp in collision.contacts)
                {
                    // contact normal points from the other collider to this collider (player). If normal.y > 0.5f -> player is above the other collider
                    if (cp.normal.y > 0.5f && rb != null && rb.linearVelocity.y <= 1.0f)
                    {
                        // Player landed on top of the other object — per request, player should receive damage (not enemy)
                        float dmg = contactDamage;
                        Enemy e = collision.collider.GetComponentInParent<Enemy>();
                        if (e != null) dmg = e.AttackDamage;

                        if (health != null)
                        {
                            health.TakeDamage(dmg);
                            if (debugContact) Debug.Log($"Player stomped on {collision.collider.name} and took {dmg} damage");
                        }

                        // small bounce to avoid getting stuck
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * 0.5f);

                        // Start invulnerability and blinking. Pass enemy colliders so ignores happen immediately.
                        if (postDamageInvulnerability > 0f)
                        {
                            Collider2D[] enemyCols = null;
                            Enemy en = collision.collider.GetComponentInParent<Enemy>();
                            if (en != null)
                                enemyCols = en.GetComponentsInChildren<Collider2D>(true);
                            else
                                enemyCols = new Collider2D[] { collision.collider };

                            StartCoroutine(TemporarilyIgnoreEnemyCollisions(postDamageInvulnerability, enemyCols));
                        }

                        handledAsStomp = true;
                        break;
                    }
                }
            }
        }
        catch { }

        if (!handledAsStomp)
            HandleContactWithEnemy(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (debugContact) Debug.Log($"OnTriggerEnter2D with {collider.name} (layer={LayerMask.LayerToName(collider.gameObject.layer)})");
        HandleContactWithEnemy(collider);
    }

    private void HandleContactWithEnemy(Collider2D col)
    {
        if (isKnocked || isInvulnerable)
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
                dmg = enemy.AttackDamage;

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
            StartCoroutine(ApplyKnockback(knockVel, knockbackDuration, col));

            // Start brief invulnerability and ignore collisions with enemy layers while invulnerable
            if (postDamageInvulnerability > 0f)
            {
                Collider2D[] enemyCols = null;
                if (enemy != null)
                    enemyCols = enemy.GetComponentsInChildren<Collider2D>(true);
                else
                    enemyCols = new Collider2D[] { col };

                StartCoroutine(TemporarilyIgnoreEnemyCollisions(postDamageInvulnerability, enemyCols));
            }
        }
        else
        {
            if (debugContact) Debug.Log($"HandleContactWithEnemy: no damageable component found on {col.name}");
        }
    }

    private System.Collections.IEnumerator ApplyKnockback(Vector2 velocity, float duration, Collider2D sourceCollider)
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

        // NOTE: collision ignoring for enemy colliders is handled centrally in TemporarilyIgnoreEnemyCollisions.
        // We avoid per-source toggles here to prevent races where ApplyKnockback would re-enable collisions
        // earlier than the invulnerability duration.

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

        // collisions are restored by TemporarilyIgnoreEnemyCollisions; do not re-toggle them here.

        isKnocked = false;
    }

    private System.Collections.IEnumerator TemporarilyIgnoreEnemyCollisions(float duration, Collider2D[] preIgnored = null)
    {
        if (isInvulnerable) yield break; // already invulnerable

        isInvulnerable = true;

        // start blink coroutine
        StartCoroutine(BlinkWhileInvulnerable());

        // If an invulnerable layer is provided, switch the player's GameObject to that layer
        // This preserves normal collisions with ground while allowing selective ignores with enemies.
        originalLayerIndex = gameObject.layer;
        if (invulnerableLayerIndex >= 0)
        {
            try { gameObject.layer = invulnerableLayerIndex; } catch { }
        }

        int playerLayer = gameObject.layer;

        // collect individual layer indices from enemyLayers mask
        var layers = new System.Collections.Generic.List<int>();
        int mask = enemyLayers.value;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
                layers.Add(i);
        }

        // disable collisions between player layer and each enemy layer (if any were configured)
        if (layers.Count > 0)
        {
            foreach (int layer in layers)
            {
                try { Physics2D.IgnoreLayerCollision(playerLayer, layer, true); } catch { }
            }
        }

        // Additionally, find all Collider2D on enemy layers and ignore collisions individually.
        // If `enemyLayers` mask is empty, fall back to scanning `Enemy` components to find colliders.
        var ignoredColliders = new System.Collections.Generic.List<Collider2D>();

        // If the caller provided specific colliders (the enemy we just hit), ignore them immediately
        if (preIgnored != null && preIgnored.Length > 0)
        {
            foreach (var pcoll in preIgnored)
            {
                if (pcoll == null) continue;
                // avoid player's own colliders
                bool isPlayerCollider = false;
                if (playerColliders != null)
                {
                    foreach (var p in playerColliders)
                    {
                        if (p == null) continue;
                        if (p == pcoll) { isPlayerCollider = true; break; }
                    }
                }
                if (isPlayerCollider) continue;

                try
                {
                    if (playerColliders != null)
                    {
                        foreach (var pc in playerColliders)
                        {
                            if (pc == null) continue;
                            Physics2D.IgnoreCollision(pc, pcoll, true);
                        }
                    }
                    if (!ignoredColliders.Contains(pcoll)) ignoredColliders.Add(pcoll);
                }
                catch { }
            }
        }
        try
        {
            if (mask == 0)
            {
                if (debugContact) Debug.Log("PlayerController: enemyLayers mask is empty — scanning for Enemy components as fallback.");
                Enemy[] enemies = FindObjectsOfType<Enemy>();
                foreach (var en in enemies)
                {
                    if (en == null) continue;
                    Collider2D[] cols = en.GetComponentsInChildren<Collider2D>(true);
                    foreach (var c in cols)
                    {
                        if (c == null) continue;
                        // avoid ignoring player's own colliders
                        bool isPlayerCollider = false;
                        if (playerColliders != null)
                        {
                            foreach (var pc in playerColliders)
                            {
                                if (pc == c) { isPlayerCollider = true; break; }
                            }
                        }
                        if (isPlayerCollider) continue;
                        try
                        {
                            if (playerColliders != null)
                            {
                                foreach (var pc in playerColliders)
                                {
                                    if (pc == null) continue;
                                    Physics2D.IgnoreCollision(pc, c, true);
                                }
                            }
                            ignoredColliders.Add(c);
                        }
                        catch { }
                    }
                }
            }
            else
            {
                Collider2D[] all = FindObjectsOfType<Collider2D>();
                foreach (var c in all)
                {
                    if (c == null) continue;
                    bool isPlayerCollider = false;
                    if (playerColliders != null)
                    {
                        foreach (var pc in playerColliders)
                        {
                            if (c == pc)
                            {
                                isPlayerCollider = true;
                                break;
                            }
                        }
                    }
                    if (isPlayerCollider) continue;
                    int l = c.gameObject.layer;
                    if ((mask & (1 << l)) != 0)
                    {
                        try
                        {
                            if (playerColliders != null)
                            {
                                foreach (var pc in playerColliders)
                                {
                                    if (pc == null) continue;
                                    Physics2D.IgnoreCollision(pc, c, true);
                                }
                            }
                            ignoredColliders.Add(c);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        if (debugContact)
        {
            var names = new System.Text.StringBuilder();
            foreach (var c in ignoredColliders) if (c != null) names.Append(c.gameObject.name).Append(',');
            Debug.Log($"Player is invulnerable for {duration} seconds — ignoring collisions with enemy layers ({layers.Count}) and {ignoredColliders.Count} colliders. [{names}]");
            if (ignoredColliders.Count == 0)
                Debug.LogWarning("PlayerController: no enemy colliders were detected/ignored — check enemyLayers or that Enemy colliders exist on scene objects.");
        }

        // If any ignored collider is currently touching the player, apply a small horizontal push
        // so the player is not stuck exactly overlapping/adjacent and can move through.
        try
        {
            if (rb != null && ignoredColliders.Count > 0)
            {
                bool anyTouching = false;
                foreach (var ec in ignoredColliders)
                {
                    if (ec == null) continue;
                    if (playerColliders != null)
                    {
                        foreach (var pc in playerColliders)
                        {
                            if (pc == null) continue;
                            bool touching = false;
                            try { touching = ec.IsTouching(pc); } catch { }
                            if (touching)
                            {
                                anyTouching = true;
                                // compute horizontal push direction away from enemy center
                                float dir = Mathf.Sign(transform.position.x - ec.transform.position.x);
                                float pushSpeed = Mathf.Max(1f, contactKnockback * 0.5f);
                                rb.linearVelocity = new Vector2(dir * pushSpeed, rb.linearVelocity.y);
                                if (debugContact) Debug.Log($"PlayerController: applied push-through velocity {dir * pushSpeed} because touching {ec.gameObject.name}");
                                break;
                            }
                        }
                        if (anyTouching) break;
                    }
                }
            }
        }
        catch { }

        yield return new WaitForSeconds(duration);

        // restore collisions (layer-based)
        foreach (int layer in layers)
        {
            try { Physics2D.IgnoreLayerCollision(playerLayer, layer, false); } catch { }
        }

        // restore individual collider ignores
        foreach (var c in ignoredColliders)
        {
            if (c == null) continue;
            try
            {
                if (playerColliders != null)
                {
                    foreach (var pc in playerColliders)
                    {
                        if (pc == null) continue;
                        Physics2D.IgnoreCollision(pc, c, false);
                    }
                }
            }
            catch { }
        }

        if (debugContact)
        {
            var restored = new System.Text.StringBuilder();
            foreach (var c in ignoredColliders) if (c != null) restored.Append(c.gameObject.name).Append(',');
            Debug.Log($"PlayerController: restored collisions for {ignoredColliders.Count} colliders [{restored}]");
        }
        // restore player's layer if it was swapped
        if (invulnerableLayerIndex >= 0 && originalLayerIndex >= 0)
        {
            try { gameObject.layer = originalLayerIndex; } catch { }
        }

        isInvulnerable = false;

        if (debugContact) Debug.Log("Player invulnerability ended — collision restored.");
    }

    private System.Collections.IEnumerator BlinkWhileInvulnerable()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            yield break;

        while (isInvulnerable)
        {
            // set blink alpha
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                Color c = originalColors != null && i < originalColors.Length ? originalColors[i] : spriteRenderers[i].color;
                c.a = invulnerabilityBlinkAlpha;
                spriteRenderers[i].color = c;
            }

            yield return new WaitForSeconds(invulnerabilityBlinkInterval);

            // restore original
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                if (originalColors != null && i < originalColors.Length)
                    spriteRenderers[i].color = originalColors[i];
            }

            yield return new WaitForSeconds(invulnerabilityBlinkInterval);
        }

        // ensure restore at the end
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null) continue;
            if (originalColors != null && i < originalColors.Length)
                spriteRenderers[i].color = originalColors[i];
        }
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

    private void FixedUpdate() {
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
        // Apply post-dash running multiplier (decays back to 1 over time)
        if (postDashCurrentMultiplier > 1f)
        {
            targetVelocityX *= postDashCurrentMultiplier;
            // decay towards 1
            postDashCurrentMultiplier = Mathf.MoveTowards(postDashCurrentMultiplier, 1f, postDashDecayRate * Time.fixedDeltaTime);
            if (postDashCurrentMultiplier <= 1f + 1e-4f)
            {
                postDashCurrentMultiplier = 1f;
                isPostDashRunning = false;
            }
            else
            {
                isPostDashRunning = true;
            }
        }
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
        // Update animator parameters
        if (anim != null)
        {
            // isMoving: true when player has horizontal speed and is on ground
            bool moving = false;
            if (rb != null)
                moving = Mathf.Abs(rb.linearVelocity.x) > 0.1f && isGrounded;
            else
                moving = Mathf.Abs(moveInput.x) > 0.1f && isGrounded;
            anim.SetBool("isMoving", moving);

            // isRunning: true while moving or during post-dash boosted run
            bool running = false;
            if (rb != null)
                running = Mathf.Abs(rb.linearVelocity.x) > 0.1f || isPostDashRunning;
            else
                running = Mathf.Abs(moveInput.x) > 0.1f || isPostDashRunning;
            anim.SetBool("isRunning", running);

            // isJumping: true while player is moving upward (simple heuristic)
            bool jumping = false;
            if (rb != null)
                jumping = rb.linearVelocity.y > 0.1f && !isGrounded;
            else
                jumping = !isGrounded && Input.GetButton("Jump");
            anim.SetBool("isJumping", jumping);
            // yVelocity: current vertical speed (can be used to decide rising vs falling in Animator)
            float yVel = 0f;
            if (rb != null) yVel = rb.linearVelocity.y;
            // set with slight damping so transitions aren't janky
            try { anim.SetFloat("yVelocity", yVel, 0.05f, Time.deltaTime); } catch { anim.SetFloat("yVelocity", yVel); }
        }
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

        // Start post-dash running boost: increases walking speed temporarily and decays back to normal
        postDashCurrentMultiplier = postDashRunMultiplier;
        if (postDashCurrentMultiplier > 1f) isPostDashRunning = true;

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