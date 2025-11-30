using UnityEngine;
using System.Collections;
using System;

public class ChimeraBoss : Enemy
{
    // ИСПРАВЛЕНО: Убрана Фаза 3 (Phase3_Despair)
    private enum BossPhase { Phase0_Sleep, Phase1_Hunt, Phase2_Rage }
    private BossPhase currentPhase = BossPhase.Phase0_Sleep;

    [Header("Chimera Boss Settings")]
    // Поля из базового класса Enemy не показаны, но предполагается, что они существуют
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private Transform[] droneSpawnPoints;
    [SerializeField] private Collider2D chargedTailCollider; // Может быть не нужен, если используется OverlapCircle
    [SerializeField] private Collider2D tailAttackCollider; // Может быть не нужен, если используется OverlapCircle
    [SerializeField] private Collider2D biteAttackCollider; // Может быть не нужен, если используется OverlapCircle

    [Header("Tail / proximity settings")]
    [SerializeField] private Transform tailTransform;
    [SerializeField] private float tailRange = 2f;
    [SerializeField] private LayerMask tailHitMask = ~0; // ~0 = Everything
    [SerializeField] private float tailKnockbackForce = 3f;

    private SpriteRenderer localSpriteRenderer;
    // Предполагается, что эти компоненты есть у игрока
    private PlayerController playerController;
    private Rigidbody2D playerRb;
    private IDamageable playerDamageable; 

    [Header("HP Thresholds")]
    [SerializeField] private float phase2Threshold = 0.6f;
    // ИСПРАВЛЕНО: Удалены phase3Threshold и lastStandThreshold

    [Header("Attack Parameters")]
    [SerializeField] private float attackCooldownBase = 4.0f;
    [SerializeField] private float biteAttackDuration = 0.5f;
    [SerializeField] private float biteLungeDistance = 4f;
    [SerializeField] private float biteRadius = 2.5f;
    [SerializeField] private float chargedTailDuration = 3f;
    [SerializeField] private float tailAttackDuration = 0.6f;


    [Header("Damage Values")]
    [SerializeField] private float tailDamage = 12f;
    [SerializeField] private float biteDamage = 16f;
    [SerializeField] private float chargedTailTickDamage = 4f;

    [Header("Phase 0 Sleep Settings")]
    [SerializeField] private float sleepDuration = 5f;
    [SerializeField] private float playerSlowAmount = 0.5f;
    [SerializeField] private float wakeUpTriggerDistance = 3f;

    private float nextAttackTime;
    private bool isAttacking = false;
    private bool isCharging = false;
    private bool isSleeping = true;
    private float sleepTimer = 0f;
    private float lastChargedTailDamageTime = 0f;
    private bool hasSpawnedDrones = false; 

    // Используем 'new' для сокрытия унаследованных методов
    protected new void Start()
    {
        base.Start();

        localSpriteRenderer = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
            playerRb = playerObj.GetComponent<Rigidbody2D>();
            playerDamageable = playerObj.GetComponent<IDamageable>();
        }
        else
        {
            Debug.LogError("ChimeraBoss: Игрок не найден!");
            return;
        }

        currentPhase = BossPhase.Phase0_Sleep;
        state = State.Chase;
        nextAttackTime = Time.time + attackCooldownBase;
        isSleeping = true;
        sleepTimer = 0f;

        if (rb2d != null)
        {
            rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        Debug.Log("✅ ChimeraBoss инициализирована. Фаза: СОН");
    }

    // Используем 'new' для сокрытия унаследованных методов
    protected new void Update()
    {
        if (state == State.Dead) return;

        HandleInvulnerabilityBlinking(); 

        if (isSleeping && currentPhase == BossPhase.Phase0_Sleep)
        {
            HandleSleepPhase();
            return;
        }

        CheckPhaseTransition();
        HandleCombatPhase();
    }
    
    // ===== ФАЗА 0: СОН =====
    private void HandleSleepPhase()
    {
        if (target == null) return;

        sleepTimer += Time.deltaTime;

        float distToPlayer = Vector3.Distance(transform.position, target.position);
        if (distToPlayer < wakeUpTriggerDistance || sleepTimer > sleepDuration)
        {
            WakeUp();
            return;
        }

        if (playerRb != null)
        {
            // Используем linearVelocity, если это 2D проект
            if (playerRb.linearVelocity.magnitude > 0.1f)
            {
                playerRb.linearVelocity *= playerSlowAmount; 
            }
        }

        float pulse = Mathf.Sin(Time.time * 2f) * 0.2f;
        if (localSpriteRenderer != null)
            localSpriteRenderer.color = new Color(0.4f + pulse, 0.4f + pulse, 0.4f + pulse);
    }

    private void WakeUp()
    {
        isSleeping = false;
        currentPhase = BossPhase.Phase1_Hunt;
        state = State.Chase;
        nextAttackTime = Time.time + 1f;

        if (localSpriteRenderer != null)
            localSpriteRenderer.color = Color.white;

        Debug.Log("⚡ Химера пробуждается! ФАЗА 1: ОХОТА");
    }

    private void HandleCombatPhase()
    {
        if (target == null) return;

        FlipToTarget();

        if (isAttacking || isCharging || Time.time < nextAttackTime)
        {
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, target.position);
        if (distToPlayer > attackRange)
        {
            float dir = Mathf.Sign(target.position.x - transform.position.x);
            if (rb2d != null) rb2d.linearVelocity = new Vector2(dir * chaseSpeed, rb2d.linearVelocity.y);
        }
        else
        {
            if (rb2d != null) rb2d.linearVelocity = new Vector2(0, rb2d.linearVelocity.y);

            switch (currentPhase)
            {
                case BossPhase.Phase1_Hunt:
                    ChooseAttack(1, attackCooldownBase);
                    break;
                case BossPhase.Phase2_Rage:
                    ChooseAttack(2, attackCooldownBase * 0.6f);
                    break;
            }
        }
    }

    private void ChooseAttack(int phase, float cooldown)
    {
        float rnd = UnityEngine.Random.value;
        nextAttackTime = Time.time + cooldown;

        if (rb2d != null) rb2d.linearVelocity = new Vector2(0, rb2d.linearVelocity.y); 

        if (phase == 1)
        {
            // ФАЗА 1: Укус 60%, Хвост 40%
            if (rnd < 0.4f)
            {
                Debug.Log("🪶 Атака хвостом!");
                StartCoroutine(TailAttack());
            }
            else
            {
                Debug.Log("🦷 Атака укусом!");
                StartCoroutine(BiteAttack());
            }
        }
        else if (phase == 2)
        {
            // ФАЗА 2: Укус 30%, Заряженный хвост 35%, Дроны 35%
            if (rnd < 0.35f)
            {
                Debug.Log("🐝 Роевой выброс!");
                StartCoroutine(DroneSwarm());
            }
            else if (rnd < 0.7f) // 0.35 + 0.35 = 0.7
            {
                Debug.Log("⚡ Заряженный хвост!");
                StartCoroutine(ChargedTailAttack());
            }
            else // Остальное (0.7 до 1.0) = 30%
            {
                Debug.Log("🦷 Атака укусом!");
                StartCoroutine(BiteAttack());
            }
        }
    }

    private void CheckPhaseTransition()
    {
        float hpPercent = currentHealth / maxHealth;

        if (currentPhase == BossPhase.Phase1_Hunt && hpPercent <= phase2Threshold)
        {
            ChangePhase(BossPhase.Phase2_Rage);
        }
        // ИСПРАВЛЕНО: Убрана проверка перехода в Фазу 3
    }

    private void ChangePhase(BossPhase newPhase)
    {
        if (currentPhase == newPhase) return;

        currentPhase = newPhase;

        if (currentPhase == BossPhase.Phase2_Rage)
        {
            chaseSpeed *= 1.5f;
            if (localSpriteRenderer != null)
                localSpriteRenderer.color = new Color(1f, 0.4f, 0.4f);
            
            Debug.Log("🔥 Химера переходит в ФАЗУ 2: ЯРОСТЬ!");
        }
    }

    private void FlipToTarget()
    {
        if (target == null) return;

        float dir = target.position.x - transform.position.x;

        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir > 0 ? 1f : -1f);
        transform.localScale = s;
    }

    // ===== АТАКА ХВОСТОМ (ФАЗА 1-2) =====
    private IEnumerator TailAttack()
    {
        isAttacking = true;
        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        if (localSpriteRenderer != null) localSpriteRenderer.color = new Color(1f, 0.8f, 0.2f);
        yield return new WaitForSeconds(0.25f);

        Vector3 center = tailTransform != null ? tailTransform.position : transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, tailRange, tailHitMask);

        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            GameObject targetObj = hit.gameObject;
            bool applied = false;

            if (targetObj.CompareTag("Player") && playerDamageable != null)
            {
                playerDamageable.TakeDamage(tailDamage);
                applied = true;
            }

            var trgRb = targetObj.GetComponent<Rigidbody2D>();
            if (trgRb != null && applied)
            {
                Vector2 kb = (targetObj.transform.position - transform.position).normalized * tailKnockbackForce;
                trgRb.AddForce(kb, ForceMode2D.Impulse);
            }
        }

        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        yield return new WaitForSeconds(tailAttackDuration - 0.25f);
        isAttacking = false;
    }

    // ===== АТАКА УКУСОМ (ФАЗА 1-2) - ИСПРАВЛЕНА ТОЧКА УРОНА =====
    private IEnumerator BiteAttack()
    {
        isAttacking = true;
        if (target == null)
        {
            isAttacking = false;
            yield break;
        }

        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        float facingDir = transform.localScale.x > 0 ? 1f : -1f;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.right * facingDir * biteLungeDistance;

        float lungeTime = biteAttackDuration * 0.5f;
        float timer = 0f;

        while (timer < lungeTime)
        {
            transform.position = Vector3.Lerp(startPos, endPos, timer / lungeTime);
            timer += Time.deltaTime;

            if (localSpriteRenderer != null)
                localSpriteRenderer.color = new Color(1f, 0.5f, 0.5f);

            yield return null;
        }

        // Урон наносится в конечной точке рывка
        DealDamageInArea(transform.position, biteRadius, biteDamage);

        yield return new WaitForSeconds(0.1f);
        
        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    // ===== РОЕВОЙ ВЫБРОС (ФАЗА 2) =====
    private IEnumerator DroneSwarm()
    {
        isAttacking = true;
        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        if (localSpriteRenderer != null)
            localSpriteRenderer.color = new Color(0.5f, 0.2f, 1f);

        yield return new WaitForSeconds(0.7f);

        if (dronePrefab != null && droneSpawnPoints != null && droneSpawnPoints.Length > 0)
        {
            foreach (Transform spawnPoint in droneSpawnPoints)
            {
                if (spawnPoint == null) continue;

                GameObject drone = Instantiate(dronePrefab, spawnPoint.position, Quaternion.identity);
                Debug.Log("✅ Дрон создан!");
            }
        }

        yield return new WaitForSeconds(0.5f);
        if (localSpriteRenderer != null)
            localSpriteRenderer.color = originalColor;

        isAttacking = false;
    }

    // ===== ЗАРЯЖЕННЫЙ ХВОСТ (ФАЗА 2) =====
    private IEnumerator ChargedTailAttack()
    {
        isCharging = true;
        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;
        lastChargedTailDamageTime = Time.time;

        float chargeTimer = 0f;
        while (chargeTimer < chargedTailDuration)
        {
            chargeTimer += Time.deltaTime;

            if (localSpriteRenderer != null)
            {
                float intensity = Mathf.Sin(chargeTimer * 8f) * 0.3f + 0.7f;
                localSpriteRenderer.color = new Color(0.3f * intensity, 0.8f * intensity, 1f);
            }

            if (Time.time - lastChargedTailDamageTime >= 0.5f)
            {
                DealDamageInArea(transform.position, 3f, chargedTailTickDamage); 
                lastChargedTailDamageTime = Time.time;
            }

            yield return null;
        }

        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        isCharging = false;
    }
    
    // ===== УРОН ОТ КОНТАКТА С МОБОМ =====
    protected new void OnCollisionEnter2D(Collision2D collision)
    {
        if (target == null || collision.gameObject != target.gameObject || state == State.Dead || isSleeping)
        {
            return;
        }
        
        if (playerDamageable != null)
        {
            // Здесь используется attackDamage из базового класса Enemy
            playerDamageable.TakeDamage(attackDamage);

            Debug.Log($"💥 Контактный урон нанесен игроку: {attackDamage}");

            if (playerRb != null)
            {
                Vector2 kbDirection = (target.position - transform.position).normalized;
                playerRb.AddForce(kbDirection * 5f, ForceMode2D.Impulse);
            }
        }
    }

    // ===== УНИВЕРСАЛЬНАЯ ФУНКЦИЯ НАНЕСЕНИЯ УРОНА =====
    private void DealDamageInArea(Vector3 center, float radius, float damage)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(center, radius);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            GameObject targetObj = hit.gameObject;

            // Предполагаем, что игрок имеет тег "Player" и компонент IDamageable
            if (targetObj.CompareTag("Player") && playerDamageable != null)
            {
                playerDamageable.TakeDamage(damage);
            }
        }
    }

    // ===== МЕТОД ПОЛУЧЕНИЯ УРОНА =====
    public override void TakeDamage(float damage)
    {
        float oldHealth = currentHealth;
        
        base.TakeDamage(damage); 

        if (currentHealth < oldHealth)
        {
            float damageDealt = oldHealth - currentHealth;
            Debug.Log($"<color=red>💥 Босс Химера получил {damageDealt:F2} урона от игрока!</color> Оставшееся HP: {currentHealth:F2}/{maxHealth:F2} ({currentHealth / maxHealth * 100:F1}%).");
        }
        
        CheckPhaseTransition();
    }
    
    // Для совместимости с SendMessage
    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage);
    }

    public override void Die()
    {
        Debug.Log("☠️ Химера повержена!");
        base.Die();
    }
    
    // public void OnDrawGizmosSelected() { ... }
}