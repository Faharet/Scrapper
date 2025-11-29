using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BatEnemy : Enemy
{
    private enum BatState { Idle, RiseAbove, DashAttack, Recover, Return }
    private BatState batState = BatState.Idle;

    [Header("Idle Hover Settings")]
    [SerializeField] private float idleRadius = 0.6f;         // радиус кружения на месте
    [SerializeField] private float idleAngularSpeed = 1.2f;   // угловая скорость (радианы/сек)
    [SerializeField] private float idleHoverAmplitude = 0.25f;// амплитуда вертикального покачивания
    [SerializeField] private float idleHoverSpeed = 1.5f;     // скорость вертикального покачивания
    [SerializeField] private float aggroRange = 8f;           // радиус агра игрока
    [SerializeField] private LayerMask playerLayers;          // слои, где находится игрок

    private Vector2 idleCenter;
    private float idleAngle;

    [Header("Rise & Dash Settings")]
    [SerializeField] private float riseHeight = 2.2f;          // сколько выше головы игрока встать
    [SerializeField] private float riseSpeed = 5.0f;           // скорость подъёма к точке над игроком
    [SerializeField] private float horizontalAlignSpeed = 4.0f;// скорость выравнивания по X над игроком
    [SerializeField] private float dashSpeed = 12.0f;          // скорость рывка к игроку
    [SerializeField] private float dashDuration = 0.35f;       // длительность рывка
    [SerializeField] private float recoverTime = 0.6f;         // время восстановления после рывка
    [SerializeField] private float reengageDelay = 0.8f;       // пауза перед повторным заходом
    [SerializeField] private float detectMaxDistance = 12f;    // максимум дистанции, при превышении теряем цель

    private Vector2 velocitySmooth;

    protected override void Start()
    {
        base.Start();
        rb2d.gravityScale = 0f;
        rb2d.linearDamping = 3f;
        if (animator == null) animator = GetComponent<Animator>();
        batState = BatState.Idle;
        idleCenter = transform.position;
        idleAngle = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override void Update()
    {
        // не вызываем base.Update(), чтобы избежать старых шаблонов поведения Enemy
        if (state == State.Dead || rb2d == null) return;

        // если нет цели — поведение ожидания и проверка на агр
        if (target == null)
        {
            IdleHoverBehaviour();
            TryAcquireTarget();
            return;
        }

        // если игрок слишком далеко — теряем цель
        if (Vector2.Distance(transform.position, target.position) > detectMaxDistance)
        {
            LoseTarget();
            return;
        }

        switch (batState)
        {
            case BatState.Idle:
                IdleHoverBehaviour();
                break;
            case BatState.RiseAbove:
                RiseAboveBehaviour();
                break;
            case BatState.DashAttack:
                // поведение выполняется в корутине
                break;
            case BatState.Recover:
                rb2d.linearVelocity = Vector2.Lerp(rb2d.linearVelocity, Vector2.zero, Time.deltaTime * 3f);
                break;
            case BatState.Return:
                // лёгкий возврат к позиции над игроком, затем снова RiseAbove
                ReturnBehaviour();
                break;
        }

        // визуальный разворот по X
        if (Mathf.Abs(rb2d.linearVelocity.x) > 0.05f)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(rb2d.linearVelocity.x);
            transform.localScale = s;
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", rb2d.linearVelocity.magnitude);
            animator.SetBool("IsAttacking", batState == BatState.DashAttack);
        }
    }

    private void IdleHoverBehaviour()
    {
        // Кружение вокруг исходной точки + лёгкое вертикальное покачивание
        idleAngle += idleAngularSpeed * Time.deltaTime;
        Vector2 targetPos = idleCenter
                          + new Vector2(Mathf.Cos(idleAngle), Mathf.Sin(idleAngle)) * idleRadius
                          + new Vector2(0f, Mathf.Sin(Time.time * idleHoverSpeed) * idleHoverAmplitude);

        Vector2 toTarget = targetPos - (Vector2)transform.position;
        Vector2 desiredVel = toTarget * 2.5f; // мягкое притяжение к точке
        rb2d.linearVelocity = Vector2.Lerp(rb2d.linearVelocity, desiredVel, Time.deltaTime * 3f);
    }

    private void TryAcquireTarget()
    {
        // Пытаемся найти игрока в радиусе агра
        Collider2D hit = Physics2D.OverlapCircle(transform.position, aggroRange, playerLayers);
        if (hit != null)
        {
            target = hit.transform;
            StartChase();
        }
        else
        {
            // запасной вариант: поиск по тегу "Player" если слои не заданы
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist <= aggroRange)
                {
                    target = player.transform;
                    StartChase();
                }
            }
        }
    }

    private void RiseAboveBehaviour()
    {
        // целевая точка над игроком
        Vector2 targetAbove = new Vector2(target.position.x, target.position.y + riseHeight);
        Vector2 toAbove = targetAbove - (Vector2)transform.position;

        // отдельные компоненты движения: подъём и выравнивание по X
        Vector2 move = new Vector2(
            Mathf.Clamp(toAbove.x, -1f, 1f) * horizontalAlignSpeed,
            Mathf.Clamp(toAbove.y, -1f, 1f) * riseSpeed
        );

        rb2d.linearVelocity = Vector2.SmoothDamp(rb2d.linearVelocity, move, ref velocitySmooth, 0.12f);

        // когда достаточно близко — запускаем рывок
        if (toAbove.sqrMagnitude < 0.25f)
        {
            StartCoroutine(DashAttackRoutine());
        }
    }

    private IEnumerator DashAttackRoutine()
    {
        batState = BatState.DashAttack;
        lastAttackTime = Time.time;
        if (animator != null) animator.SetTrigger("Attack");

        float t = 0f;
        // направление к игроку на момент старта
        Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;

        while (t < dashDuration)
        {
            // корректируем направление слегка, чтобы тянуться к актуальной позиции игрока
            Vector2 desiredDir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            dir = Vector2.Lerp(dir, desiredDir, Time.deltaTime * 6f).normalized;
            rb2d.linearVelocity = dir * dashSpeed;
            t += Time.deltaTime;
            yield return null;
        }

        // завершение — уход в восстановление
        batState = BatState.Recover;
        yield return new WaitForSeconds(recoverTime);

        batState = BatState.Return;
        yield return new WaitForSeconds(reengageDelay);

        batState = BatState.RiseAbove;
    }

    private void ReturnBehaviour()
    {
        Vector2 targetAbove = new Vector2(target.position.x, target.position.y + riseHeight);
        Vector2 toAbove = targetAbove - (Vector2)transform.position;
        rb2d.linearVelocity = Vector2.Lerp(rb2d.linearVelocity, toAbove.normalized * Mathf.Max(3f, rb2d.linearVelocity.magnitude), Time.deltaTime * 4f);

        if (toAbove.sqrMagnitude < 0.49f)
        {
            batState = BatState.RiseAbove;
        }
    }

    // Вход в режим атаки: как только заметили игрока — начинаем подъём
    protected override void StartChase()
    {
        state = State.Chase;
        batState = BatState.RiseAbove;
        if (animator != null) animator.SetBool("IsChasing", true);
    }

    protected override void LoseTarget()
    {
        target = null;
        state = State.Patrol;
        batState = BatState.Idle;
        idleCenter = transform.position; // обновляем центр кружения туда, где потеряли цель
        rb2d.linearVelocity = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("IsChasing", false);
            animator.SetBool("IsAttacking", false);
        }
    }

    // Урон наносится телом летучей мыши по контакту
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state == State.Dead) return; // не наносим урон после смерти
        if (collision.gameObject.CompareTag("Player") && collision.gameObject.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.TakeDamage(attackDamage);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (state == State.Dead) return; // не наносим урон после смерти
        if (col.gameObject.CompareTag("Player") && col.gameObject.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.TakeDamage(attackDamage);
        }
    }
}