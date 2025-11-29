using UnityEngine;
using System;
using System.Collections;
using System.Reflection;

public class ChimeraBoss : Enemy
{
    private enum BossPhase { Phase0_Sleep, Phase1_Hunt, Phase2_Rage, Phase3_Despair }
    private BossPhase currentPhase = BossPhase.Phase0_Sleep;

    [Header("Chimera Boss Settings")]
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private Transform[] droneSpawnPoints;
    [SerializeField] private Collider2D chargedTailCollider;
    [SerializeField] private Collider2D tailAttackCollider;
    [SerializeField] private Collider2D biteAttackCollider;
    [SerializeField] private Collider2D selfDestructAoE;

    [Header("Tail / proximity settings")]
    [SerializeField] private Transform tailTransform;              
    [SerializeField] private float tailRange = 2f;                        
    [SerializeField] private LayerMask tailHitMask = ~0;
    [SerializeField] private float tailInvulDuration = 0.8f;
    [SerializeField] private float tailBlinkInterval = 0.08f;

    private SpriteRenderer localSpriteRenderer;
    private PlayerController playerController;
    private Rigidbody2D playerRb;

    [Header("HP Thresholds")]
    [SerializeField] private float phase2Threshold = 0.7f;
    [SerializeField] private float phase3Threshold = 0.3f;
    [SerializeField] private float lastStandThreshold = 0.1f;

    [Header("Attack Parameters")]
    [SerializeField] private float attackCooldownBase = 3.5f;
    [SerializeField] private float attackDelayBase = 0.3f;
    [SerializeField] private float biteAttackDuration = 0.5f;
    [SerializeField] private float biteLungeDistance = 4f;
    [SerializeField] private float biteRadius = 2.5f;
    [SerializeField] private float chargedTailDuration = 3f;
    [SerializeField] private float selfDestructChargeTime = 3f;
    [SerializeField] private float finalLungeDuration = 1.2f;
    [SerializeField] private float tailAttackDuration = 0.6f;

    [Header("Damage Values")]
    [SerializeField] private float tailDamage = 15f;
    [SerializeField] private float biteDamage = 20f;
    [SerializeField] private float chargedTailTickDamage = 5f;
    [SerializeField] private float selfDestructDamage = 50f;

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
        }
        else
        {
            Debug.LogError("ChimeraBoss: Игрок не найден!");
            return;
        }

        if (rb2d != null)
        {
            rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        currentPhase = BossPhase.Phase0_Sleep;
        state = State.Chase;
        nextAttackTime = Time.time + attackCooldownBase;
        isSleeping = true;
        sleepTimer = 0f;

        Debug.Log("✅ ChimeraBoss инициализирована. Фаза: СОН");
    }

    protected new void Update()
    {
        if (localSpriteRenderer != null)
        {
            HandleInvulnerabilityBlinking();
        }

        if (state == State.Dead) return;

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
            if (playerRb.linearVelocity.magnitude > 0.1f)
                playerRb.linearVelocity *= playerSlowAmount;
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

        if (state == State.Chase) FlipToTarget();

        if (state != State.Chase || isAttacking || isCharging || Time.time < nextAttackTime)
        {
            return;
        }

        switch (currentPhase)
        {
            case BossPhase.Phase1_Hunt:
                ChooseAttack(1, attackCooldownBase);
                break;
            case BossPhase.Phase2_Rage:
                ChooseAttack(2, attackCooldownBase * 0.6f);
                break;
            case BossPhase.Phase3_Despair:
                ChooseAttack(3, attackCooldownBase * 0.4f);
                break;
        }
    }

    private void ChooseAttack(int phase, float cooldown)
{
    float rnd = UnityEngine.Random.value;
    nextAttackTime = Time.time + cooldown;

    if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

    if (phase == 1)
    {
        // ✅ ФАЗА 1: Укус 60%, Хвост 40%
        if (rnd < 0.4f)
        {
             Debug.Log("⚡ Заряженный хвост!");
            StartCoroutine(ChargedTailAttack());
        }
        else
        {
            Debug.Log("🦷 Атака укусом!");
            StartCoroutine(BiteAttack());
        }
    }
    else if (phase == 2)
    {
        // ✅ ФАЗА 2: Укус 30%, Заряженный хвост 35%, Дроны 35%
        if (rnd < 0.35f)
        {
            Debug.Log("🐝 Роевой выброс!");
            StartCoroutine(DroneSwarm());
        }
        else if (rnd < 0.7f)
        {
            Debug.Log("⚡ Заряженный хвост!");
            StartCoroutine(ChargedTailAttack());
        }
        else
        {
            Debug.Log("🦷 Атака укусом!");
            StartCoroutine(BiteAttack());
        }
    }
    else if (phase == 3)
    {
        if (currentHealth / maxHealth <= lastStandThreshold)
        {
            Debug.Log("💥 ПОСЛЕДНИЙ РЫВОК!");
            StartCoroutine(FinalLunge());
            nextAttackTime = Time.time + 100f;
        }
        // ✅ ФАЗА 3: Укус 35%, Саморазрушение 35%, Комбо 30%
        else if (rnd < 0.35f)
        {
            Debug.Log("💣 Саморазрушение!");
            StartCoroutine(SelfDestructCharge());
        }
        else if (rnd < 0.65f)
        {
            Debug.Log("🔥 Комбинированная атака!");
            StartCoroutine(CombinedAttack());
        }
        else
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
        else if (currentPhase == BossPhase.Phase2_Rage && hpPercent <= phase3Threshold)
        {
            ChangePhase(BossPhase.Phase3_Despair);
        }
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
        else if (currentPhase == BossPhase.Phase3_Despair)
        {
            chaseSpeed *= 1.3f;
            if (localSpriteRenderer != null)
                localSpriteRenderer.color = new Color(0.8f, 0.1f, 0.1f);
            Debug.Log("⚡ Химера переходит в ФАЗУ 3: ОТЧАЯНИЕ!");
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

    // ===== АТАКА ХВОСТОМ (ФАЗА 1) =====
    private IEnumerator TailAttack()
    {
        isAttacking = true;
        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        if (localSpriteRenderer != null) localSpriteRenderer.color = new Color(1f, 0.8f, 0.2f);
        yield return new WaitForSeconds(0.25f);

        Vector3 center = tailTransform != null ? tailTransform.position : transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, tailRange, tailHitMask);

        Debug.Log($"🪶 TailAttack: найдено {hits.Length} коллайдеров в радиусе {tailRange}");

        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            GameObject targetObj = hit.gameObject;
            bool applied = false;

            try
            {
                var comps = targetObj.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    if (TryDealToComponent(comp, tailDamage))
                    {
                        applied = true;
                        break;
                    }
                }

                if (!applied)
                {
                    targetObj.SendMessage("TakeDamage", (int)tailDamage, SendMessageOptions.DontRequireReceiver);
                    targetObj.SendMessage("TakeDamage", tailDamage, SendMessageOptions.DontRequireReceiver);
                    applied = true;
                }

                var trgRb = targetObj.GetComponent<Rigidbody2D>();
                if (trgRb != null)
                {
                    Vector2 kb = (targetObj.transform.position - transform.position).normalized * 3f;
                    trgRb.AddForce(kb, ForceMode2D.Impulse);
                }

                if (applied)
                {
                    EnsureInvulnerabilityAndBlink(targetObj, tailInvulDuration, tailBlinkInterval);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"TailAttack: ошибка при обработке цели {targetObj.name}: {ex.Message}");
            }
        }

        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        yield return new WaitForSeconds(0.2f);
        isAttacking = false;
    }

    // ===== АТАКА УКУСОМ (ФАЗА 1-2) =====
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

        DealDamageInArea(transform.position, biteRadius, biteDamage); 

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

    // ===== САМОРАЗРУШЕНИЕ (ФАЗА 3) =====
    private IEnumerator SelfDestructCharge()
    {
        isCharging = true;
        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        float chargeTimer = 0f;
        while (chargeTimer < selfDestructChargeTime)
        {
            chargeTimer += Time.deltaTime;

            if (localSpriteRenderer != null)
            {
                float pulse = Mathf.Sin(chargeTimer * 15f) * 0.5f + 0.5f;
                localSpriteRenderer.color = new Color(1f, pulse * 0.3f, 0f);
            }

            yield return null;
        }

        DealDamageInArea(transform.position, 4f, selfDestructDamage);

        if (localSpriteRenderer != null)
            localSpriteRenderer.color = new Color(1f, 0.5f, 0f);

        yield return new WaitForSeconds(0.3f);

        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        isCharging = false;
    }

    // ===== КОМБИНИРОВАННАЯ АТАКА (ФАЗА 3) =====
    private IEnumerator CombinedAttack()
    {
        isAttacking = true;

        StartCoroutine(BiteAttack());
        yield return new WaitForSeconds(0.5f);

        if (target != null)
        {
            StartCoroutine(TailAttack());
        }

        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    // ===== ПОСЛЕДНИЙ РЫВОК (ФАЗА 3, 10% HP) =====
    private IEnumerator FinalLunge()
    {
        isAttacking = true;
        if (target == null)
        {
            isAttacking = false;
            yield break;
        }

        Color originalColor = localSpriteRenderer != null ? localSpriteRenderer.color : Color.white;

        Vector3 startPos = transform.position;
        Vector3 endPos = target.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 2f);

        float timer = 0f;
        while (timer < finalLungeDuration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, timer / finalLungeDuration);

            if (localSpriteRenderer != null)
                localSpriteRenderer.color = new Color(1f, 0.2f, 0.2f);

            timer += Time.deltaTime;
            yield return null;
        }

        DealDamageInArea(transform.position, 2f, biteDamage * 1.5f);

        if (localSpriteRenderer != null) localSpriteRenderer.color = originalColor;
        yield return new WaitForSeconds(0.5f);
        isAttacking = false;
    }

    // ===== УРОН ОТ КОНТАКТА С МОБОМ =====
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (target == null || collision.gameObject != target.gameObject || state == State.Dead || isSleeping)
        {
            return;
        }
        
        bool applied = false;
        try
        {
            var comps = target.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                if (TryDealToComponent(comp, attackDamage)) 
                {
                    applied = true;
                    break;
                }
            }
            
            if (!applied)
            {
                target.gameObject.SendMessage("TakeDamage", (int)attackDamage, SendMessageOptions.DontRequireReceiver);
                target.gameObject.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                applied = true;
            }

            if (applied)
            {
                Debug.Log($"💥 Контактный урон нанесен игроку: {attackDamage}");
                
                if (playerRb != null)
                {
                    Vector2 kbDirection = (target.position - transform.position).normalized;
                    playerRb.AddForce(kbDirection * 5f, ForceMode2D.Impulse); 
                }
                
                EnsureInvulnerabilityAndBlink(target.gameObject, tailInvulDuration, tailBlinkInterval);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"OnCollisionEnter2D: ошибка при нанесении контактного урона игроку: {ex.Message}");
        }
    }

    // ===== УНИВЕРСАЛЬНАЯ ФУНКЦИЯ НАНЕСЕНИЯ УРОНА =====
    private void DealDamageInArea(Vector3 center, float radius, float damage)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(center, radius);

        Debug.Log($"🎯 Поиск объектов в радиусе {radius}: найдено {hitColliders.Length}");

        foreach (Collider2D hit in hitColliders)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            try
            {
                GameObject targetObj = hit.gameObject;

                targetObj.SendMessage("TakeDamage", (int)damage, SendMessageOptions.DontRequireReceiver);
                targetObj.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
                targetObj.SendMessage("ReceiveDamage", (int)damage, SendMessageOptions.DontRequireReceiver);
                targetObj.SendMessage("Hurt", (int)damage, SendMessageOptions.DontRequireReceiver);

                bool applied = false;
                var comps = targetObj.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp == null) continue;
                    if (TryDealToComponent(comp, damage))
                    {
                        applied = true;
                        break;
                    }
                }

                if (!applied)
                {
                    Debug.LogWarning($"⚠ Не найден способ нанести урон объекту '{targetObj.name}'.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при попытке нанести урон: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // Попытаться применить урон к конкретному компоненту через рефлексию
    private bool TryDealToComponent(Component comp, float damage)
    {
        Type t = comp.GetType();

        // 1) Найти метод с одним числовым параметром и вызвать
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var m in methods)
        {
            var pars = m.GetParameters();
            if (pars.Length == 1 && IsNumericType(pars[0].ParameterType))
            {
                try
                {
                    object arg = Convert.ChangeType(damage, pars[0].ParameterType);
                    m.Invoke(comp, new object[] { arg });
                    Debug.Log($"💥 Нанесено {damage} урона через {t.Name}.{m.Name}()");
                    return true;
                }
                catch { }
            }
        }

        // 2) Попробовать поля/свойства с именами health/hp/currentHealth и уменьшить
        string[] names = new[] { "health", "Health", "hp", "HP", "currentHealth", "CurrentHealth" };
        foreach (var n in names)
        {
            try
            {
                var prop = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead && prop.CanWrite && IsNumericType(prop.PropertyType))
                {
                    object curObj = prop.GetValue(comp);
                    if (curObj != null)
                    {
                        double cur = Convert.ToDouble(curObj);
                        double next = cur - damage;
                        object setVal = Convert.ChangeType(next, prop.PropertyType);
                        prop.SetValue(comp, setVal);
                        Debug.Log($"💥 Нанесено {damage} урона через свойство {t.Name}.{n}");
                        return true;
                    }
                }

                var field = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && IsNumericType(field.FieldType))
                {
                    object curObj = field.GetValue(comp);
                    if (curObj != null)
                    {
                        double cur = Convert.ToDouble(curObj);
                        double next = cur - damage;
                        object setVal = Convert.ChangeType(next, field.FieldType);
                        field.SetValue(comp, setVal);
                        Debug.Log($"💥 Нанесено {damage} урона через поле {t.Name}.{n}");
                        return true;
                    }
                }
            }
            catch { }
        }

        return false;
    }

    private static bool IsNumericType(Type type)
    {
        if (type == null) return false;
        if (type.IsEnum) return false;
        TypeCode tc = Type.GetTypeCode(type);
        switch (tc)
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return true;
            default:
                return false;
        }
    }

    // Добавляет компонент мерцания/неуязвимости к цели
    private void EnsureInvulnerabilityAndBlink(GameObject targetObj, float duration, float blinkInterval)
    {
        if (targetObj == null) return;

        var blink = targetObj.GetComponent<BlinkAndInvul>();
        if (blink == null)
        {
            blink = targetObj.AddComponent<BlinkAndInvul>();
        }
        blink.StartBlinkAndInvul(duration, blinkInterval);
    }

    protected override void Die()
    {
        if (rb2d != null) rb2d.constraints = RigidbodyConstraints2D.FreezeAll;

        if (localSpriteRenderer != null)
            localSpriteRenderer.color = Color.gray;

        Debug.Log("☠️ Химера повержена!");
        Destroy(gameObject, 2.0f);
        base.Die();
    }

    private void OnDrawGizmosSelected()
    {
        // 1. Визуализация хвоста
        if (tailTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(tailTransform.position, tailRange);
        }
        
        // 2. Визуализация рывка/укуса
        if (transform != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, biteRadius);
            
            if (!Application.isPlaying)
            {
                float facingDir = transform.localScale.x > 0 ? 1f : -1f;
                Vector3 startPos = transform.position;
                Vector3 endPos = startPos + Vector3.right * facingDir * biteLungeDistance;
                
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(startPos, endPos);
                Gizmos.DrawWireSphere(endPos, 0.5f);
            }
        }
    }
}

/// <summary>
/// Вспомогательный компонент: при добавлении/запуске даёт объекту временную неуязвимость и мерцание.
/// </summary>
public class BlinkAndInvul : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration;
    private float interval;
    private Coroutine blinkCoroutine;

    private static readonly string[] invNames = new[] { "invulnerable", "isInvulnerable", "Invulnerable", "isInvul", "invul" };
    private FieldInfo invField;
    private PropertyInfo invProp;
    private MethodInfo setInvMethod;
    private Component invComponent;

    public void StartBlinkAndInvul(float duration, float interval)
    {
        this.duration = duration;
        this.interval = Mathf.Max(0.02f, interval);
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        PrepareInvComponent();
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void PrepareInvComponent()
    {
        try
        {
            if (invField != null && invComponent != null) invField.SetValue(invComponent, false);
            if (invProp != null && invComponent != null) invProp.SetValue(invComponent, false);
            if (setInvMethod != null && invComponent != null) setInvMethod.Invoke(invComponent, new object[] { false });
        }
        catch { }
        
        invField = null;
        invProp = null;
        setInvMethod = null;
        invComponent = null;
        
        var comps = GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == this) continue;
            var t = c.GetType();
            
            foreach (var n in invNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool))
                {
                    invField = f;
                    invComponent = c;
                    invField.SetValue(c, true);
                    return;
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                {
                    invProp = p;
                    invComponent = c;
                    invProp.SetValue(c, true);
                    return;
                }
            }
            var m = t.GetMethod("SetInvulnerable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null)
            {
                setInvMethod = m;
                invComponent = c;
                try { setInvMethod.Invoke(c, new object[] { true }); } catch { }
                return;
            }
        }
    }

    private IEnumerator BlinkRoutine()
    {
        sr = GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        bool visible = true;

        PrepareInvComponent();

        while (elapsed < duration)
        {
            if (sr != null)
            {
                visible = !visible;
                sr.enabled = visible;
            }
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
        if (sr != null) sr.enabled = true;
        try
        {
            if (invField != null && invComponent != null) invField.SetValue(invComponent, false);
            if (invProp != null && invComponent != null) invProp.SetValue(invComponent, false);
            if (setInvMethod != null && invComponent != null) setInvMethod.Invoke(invComponent, new object[] { false });
        } 
        catch { }
        
        Destroy(this);
    }
}