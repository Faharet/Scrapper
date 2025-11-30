using UnityEngine;

public abstract class Enemy : MonoBehaviour, IDamageable
{
    // -----------------------------
    // Stats
    // -----------------------------
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float walkSpeed = 2f;
    [SerializeField] protected float chaseSpeed = 4f;
    [SerializeField] protected float detectRadius = 8f;
    [SerializeField] protected float attackRange = 1.8f;
    [SerializeField] protected float attackDamage = 20f;
    public float AttackDamage => attackDamage;

    [SerializeField] protected float attackCooldown = 1.4f;
    [SerializeField] protected float attackDelay = 0.5f; // Задержка перед нанесением урона
    
    // ВРЕМЯ НЕУЯЗВИМОСТИ (I-FRAMES)
    [Tooltip("Время неуязвимости после получения урона (i-frames).")]
    [SerializeField] protected float invulnerabilityTime = 0.2f; 
    
    [SerializeField] protected float loseTargetTime = 3f;

    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask obstructionMask;
    [SerializeField] protected Transform[] patrolPoints;

    // -----------------------------
    // Physics
    // -----------------------------
    [Header("Physics")]
    [SerializeField] protected bool preventBeingPushed = true;
    [SerializeField] protected Collider2D[] triggerColliders;
    [SerializeField] protected float massWhenPreventPushed = 75f;

    // -----------------------------
    // Components
    // -----------------------------
    protected Animator animator;
    protected Rigidbody2D rb2d;
    protected SpriteRenderer spriteRenderer; // ОБЪЯВЛЕН: Компонент для мигания

    // -----------------------------
    // State
    // -----------------------------
    protected Transform target;
    protected int currentPatrolIndex;
    protected float currentHealth;
    protected float lastAttackTime = -999f;
    protected float lastSeenTime = -999f;
    
    protected float lastHitTime = -999f; // ОБЪЯВЛЕН: Время последнего получения урона
    
    protected bool hasDealtDamage = false; 

    protected enum State { Patrol, Chase, Attack, Dead }
    protected State state = State.Patrol;

    // -----------------------------
    // Awake / Start
    // -----------------------------
    protected virtual void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // ИНИЦИАЛИЗАЦИЯ SPRITE RENDERER

        if (spriteRenderer == null)
        {
            Debug.LogError($"SpriteRenderer не найден на объекте {gameObject.name}. Добавьте компонент SpriteRenderer для работы мигания!", this);
        }
    }

    protected virtual void Start()
    {
        currentHealth = maxHealth;

        if (preventBeingPushed && rb2d != null)
        {
            rb2d.mass = massWhenPreventPushed;
            rb2d.freezeRotation = true;
        }

        if (triggerColliders != null)
            foreach (var c in triggerColliders)
                if (c != null)
                    c.isTrigger = true;

        // установить первую точку патруля
        if (patrolPoints != null && patrolPoints.Length > 0)
            currentPatrolIndex = 0;

        // Игнорируем столкновения между всеми врагами
        IgnoreEnemyCollisions();
    }

    private void IgnoreEnemyCollisions()
    {
        // Находим всех врагов в сцене и игнорируем физические столкновения между ними
        Enemy[] allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Collider2D[] myColliders = GetComponents<Collider2D>();

        foreach (Enemy otherEnemy in allEnemies)
        {
            if (otherEnemy == this) continue;

            Collider2D[] otherColliders = otherEnemy.GetComponents<Collider2D>();
            
            foreach (Collider2D myCol in myColliders)
            {
                if (myCol.isTrigger) continue; // триггеры не участвуют в физике

                foreach (Collider2D otherCol in otherColliders)
                {
                    if (otherCol.isTrigger) continue;
                    Physics2D.IgnoreCollision(myCol, otherCol, true);
                }
            }
        }
    }

    // -----------------------------
    // Update
    // -----------------------------
    protected virtual void Update()
    {
        if (state == State.Dead) return;

        DetectPlayer();
        HandleInvulnerabilityBlinking(); // ВЫЗОВ: ОБРАБОТКА МИГАНИЯ В КАЖДОМ КАДРЕ

        switch (state)
        {
            case State.Patrol: PatrolUpdate(); break;
            case State.Chase:  ChaseUpdate(); break;
            case State.Attack: AttackUpdate(); break;
        }
    }
    
    // -----------------------------
    // INVULNERABILITY BLINKING (ЛОГИКА МИГАНИЯ)
    // -----------------------------
    protected virtual void HandleInvulnerabilityBlinking()
    {
        if (spriteRenderer == null) return; // Выход, если компонент не найден

        // Если время с последнего удара меньше времени неуязвимости
        bool isInvulnerable = Time.time < lastHitTime + invulnerabilityTime;

        if (isInvulnerable)
        {
            // Мигание: показываем/скрываем спрайт каждые ~0.05 секунды (20 раз в секунду)
            if ((int)(Time.time * 20) % 2 == 0)
            {
                spriteRenderer.enabled = false;
            }
            else
            {
                spriteRenderer.enabled = true;
            }
        }
        else
        {
            // Убеждаемся, что спрайт виден, когда неуязвимость закончилась
            if (!spriteRenderer.enabled)
                spriteRenderer.enabled = true;
        }
    }

    // -----------------------------
    // DETECT PLAYER (2D)
    // -----------------------------
    protected virtual void DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);

        if (hit == null)
        {
            if (state == State.Chase && Time.time - lastSeenTime > loseTargetTime)
                LoseTarget();
            return;
        }

        // игрок есть
        target = hit.transform;
        lastSeenTime = Time.time;

        if (state == State.Patrol)
            StartChase();
    }

    // -----------------------------
    // PATROL (2D)
    // -----------------------------
    protected virtual void PatrolUpdate()
    {
        if (patrolPoints == null || patrolPoints.Length == 0 || rb2d == null)
            return;

        Transform point = patrolPoints[currentPatrolIndex];

        float dir = Mathf.Sign(point.position.x - transform.position.x);

        rb2d.linearVelocity = new Vector2(dir * walkSpeed, rb2d.linearVelocity.y);

        if (Mathf.Abs(point.position.x - transform.position.x) < 0.2f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(rb2d.linearVelocity.x));
    }

    // -----------------------------
    // START CHASE
    // -----------------------------
    protected virtual void StartChase()
    {
        state = State.Chase;

        if (animator != null)
            animator.SetBool("IsChasing", true);
    }

    // -----------------------------
    // CHASE (2D)
    // -----------------------------
    protected virtual void ChaseUpdate()
    {
        if (target == null)
        {
            LoseTarget();
            return;
        }

        float dir = Mathf.Sign(target.position.x - transform.position.x);

        rb2d.linearVelocity = new Vector2(dir * chaseSpeed, rb2d.linearVelocity.y);

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(rb2d.linearVelocity.x));

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            state = State.Attack;
            rb2d.linearVelocity = new Vector2(0, rb2d.linearVelocity.y);

            // Устанавливаем время НАЧАЛА АТАКИ для отсчета attackDelay
            lastAttackTime = Time.time; 
            hasDealtDamage = false; // Начинаем новый цикл

            if (animator != null)
            {
                animator.SetTrigger("Attack");
                animator.ResetTrigger("Idle");
                animator.ResetTrigger("Run");
            }
        }
    }

    // -----------------------------
    // ATTACK
    // -----------------------------
    protected virtual void AttackUpdate()
    {
        if (target == null)
        {
            EndAttack();
            return;
        }
        
        float currentTime = Time.time;
        float timeSinceAttackStart = currentTime - lastAttackTime;
        float dist = Vector2.Distance(transform.position, target.position);

        // ФАЗА УДАРА: Если время замаха (attackDelay) прошло, и урон еще не нанесен
        if (!hasDealtDamage && timeSinceAttackStart >= attackDelay)
        {
            // Урон наносится, только если цель все еще в радиусе поражения
            if (dist <= attackRange + 0.5f) 
            {
                DealDamage();
            }
            // Флаг устанавливается в любом случае, чтобы избежать повторного урона в этом цикле
            hasDealtDamage = true; 
        }

        // ФАЗА ВОССТАНОВЛЕНИЯ: Прошел ли полный цикл атаки (attackCooldown)?
        if (timeSinceAttackStart >= attackCooldown)
        {
             // Если игрок все еще в радиусе, начинаем новую атаку (перезапуск замаха)
            if (dist <= attackRange + 0.5f)
            {
                // Перезапуск цикла
                lastAttackTime = currentTime; 
                hasDealtDamage = false;
                
                if (animator != null)
                    animator.SetTrigger("Attack");
            }
            else
            {
                // Игрок убежал
                EndAttack();
            }
        }
    }

    public virtual void DealDamage()
    {
        if (target == null) return;

        // Здесь DealDamage вызывает TakeDamage на цели (игроке)
        if (target.CompareTag("Player") && target.TryGetComponent<IDamageable>(out var dmg))
            dmg.TakeDamage(attackDamage);
    }

    protected virtual void EndAttack()
    {
        state = State.Chase;
        hasDealtDamage = false; // Сбрасываем флаг при выходе
    }

    // -----------------------------
    // LOSE TARGET
    // -----------------------------
    protected virtual void LoseTarget()
    {
        target = null;
        state = State.Patrol;

        if (animator != null)
            animator.SetBool("IsChasing", false);
    }

    // -----------------------------
    // TAKE DAMAGE - ЛОГИКА АКТИВАЦИИ НЕУЯЗВИМОСТИ
    // -----------------------------
    public virtual void TakeDamage(float amount)
    {
        if (state == State.Dead) 
        {
            Debug.Log($"{gameObject.name}: TakeDamage ignored - already dead");
            return;
        }

        // ПРОВЕРКА НЕУЯЗВИМОСТИ: Если i-frames активны, урон игнорируется
        if (Time.time < lastHitTime + invulnerabilityTime) 
        {
            Debug.Log($"{gameObject.name}: TakeDamage ignored - in invulnerability (remaining: {Mathf.Max(0, lastHitTime + invulnerabilityTime - Time.time):F2}s)");
            return;
        }

        currentHealth -= amount;
        lastHitTime = Time.time; // ОБНОВЛЕНИЕ: Это активирует мигание через HandleInvulnerabilityBlinking()

        Debug.Log($"{gameObject.name}: TakeDamage({amount}) -> Health: {currentHealth:F1}/{maxHealth:F1} (i-frames for {invulnerabilityTime}s)");

        if (animator != null)
            animator.SetTrigger("Hit");

        if (currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name}: Health <= 0, calling Die()");
            Die();
        }
    }

    public virtual void Heal(float amount)
    {
        if (state == State.Dead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
    }

    public float CurrentHealth => currentHealth;

    // -----------------------------
    // DIE
    // -----------------------------
    public virtual void Die()
    {
        Debug.Log($"{gameObject.name}: Die() called - starting fade out animation");
        
        state = State.Dead;

        // Устанавливаем триггер Die в аниматоре
        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.ResetTrigger("Idle");
            animator.ResetTrigger("Run");
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Hit");
        }

        // Остановить движение
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.simulated = false; // отключаем физику, чтобы враг не взаимодействовал
        }

        // Отключаем все корутины поведения (например, атаки/патруль у наследников)
        StopAllCoroutines();

        // Отключаем все коллайдеры, чтобы враг не наносил урон и не блокировал
        var allCols = GetComponents<Collider2D>();
        foreach (var c in allCols)
        {
            c.enabled = false;
        }

        // Установить анимацию смерти
        if (animator != null)
            animator.SetBool("IsDead", true);

        // Запустить исчезновение с анимацией на 1.5 секунды
        if (spriteRenderer != null)
            StartCoroutine(FadeOutAndDestroy(1.5f));
        else
        {
            Debug.Log($"{gameObject.name}: No SpriteRenderer found, destroying immediately");
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator FadeOutAndDestroy(float fadeDuration)
    {
        Debug.Log($"{gameObject.name}: FadeOutAndDestroy started ({fadeDuration}s)");
        
        float elapsedTime = 0f;
        Color originalColor = spriteRenderer.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            Color newColor = originalColor;
            newColor.a = alpha;
            spriteRenderer.color = newColor;

            yield return null;
        }

        // Убедиться что alpha = 0
        Color finalColor = originalColor;
        finalColor.a = 0f;
        spriteRenderer.color = finalColor;

        Debug.Log($"{gameObject.name}: FadeOutAndDestroy finished, destroying object");
        // Уничтожить объект
        Destroy(gameObject);
    }

    // -----------------------------
    // GIZMOS
    // -----------------------------
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}