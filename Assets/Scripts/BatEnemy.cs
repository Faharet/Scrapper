using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BatEnemy : Enemy
{
    private enum BatState { Idle, Circling, Diving, FleeingAway, CalmingDown, Returning }
    private BatState batState = BatState.Idle;

    [Header("Circling Settings")]
    [SerializeField] private float flightSpeed = 4.5f;
    [SerializeField] private float circlingHeight = 3.2f;
    [SerializeField] private float minAllowedHeight = 1.8f;
    [SerializeField] private float heightRestoreForce = 7f;
    [SerializeField] private float noiseStrength = 0.9f;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Attack & Flee")]
    [SerializeField] private float diveMultiplier = 3.2f;
    [SerializeField] private float fleeSpeed = 11f;
    [SerializeField] private float fleeDuration = 0.9f;
    [SerializeField] private float calmDownTime = 2.5f;
    [SerializeField] [Range(0.01f, 0.3f)] private float attackChancePerSec = 0.1f;

    // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: плавное направление кружения
    private float circleDirection = 1f;           // 1 = по часовой, -1 = против
    private float directionChangeTimer = 0f;
    [SerializeField] private float directionChangeInterval = 6f; // меняет направление каждые ~6 сек

    private Vector2 desiredVelocity;
    private Vector2 velocitySmooth;

    protected override void Start()
    {
        base.Start();
        rb2d.gravityScale = 0f;
        rb2d.linearDamping = 3f;
        if (animator == null) animator = GetComponent<Animator>();

        circleDirection = Random.value > 0.5f ? 1f : -1f;
        StartCoroutine(InitialIdle());
    }

    private IEnumerator InitialIdle()
    {
        batState = BatState.Idle;
        rb2d.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(2f);
        if (target != null) StartChase();
    }

    protected override void Update()
    {
        base.Update();
        if (target == null || rb2d == null) return;

        if (Vector2.Distance(transform.position, target.position) > detectRadius * 1.6f)
        {
            LoseTarget();
            return;
        }

        // Аварийный подъём
        float heightDiff = transform.position.y - target.position.y;
        if (heightDiff < minAllowedHeight - 0.4f)
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x * 0.7f, heightRestoreForce * 2f);

        switch (batState)
        {
            case BatState.Circling:
                CirclingBehaviour();
                TryDiveAttack();
                UpdateDirectionChange();
                break;
            case BatState.FleeingAway:
                FleeingBehaviour();
                break;
            case BatState.CalmingDown:
                CalmingDownBehaviour();
                break;
            case BatState.Returning:
                ReturningBehaviour();
                break;
        }

        if (Mathf.Abs(rb2d.linearVelocity.x) > 0.1f)
            FlipSprite(rb2d.linearVelocity.x);

        if (animator != null)
        {
            animator.SetFloat("Speed", rb2d.linearVelocity.magnitude);
            animator.SetBool("IsAttacking", batState == BatState.Diving);
            animator.SetBool("IsScared", batState == BatState.FleeingAway || batState == BatState.CalmingDown);
        }
    }

    private void UpdateDirectionChange()
    {
        directionChangeTimer += Time.deltaTime;
        if (directionChangeTimer >= directionChangeInterval)
        {
            directionChangeTimer = 0f;
            circleDirection *= -1f; // плавно меняем направление кружения
        }
    }

    private void CirclingBehaviour()
    {
        Vector2 playerPos = target.position;
        Vector2 desiredPos = new Vector2(playerPos.x, playerPos.y + circlingHeight);
        Vector2 toDesired = desiredPos - (Vector2)transform.position;

        // Сильная тяга к высоте
        Vector2 heightForce = toDesired * heightRestoreForce;

        // Плавное круговое движение (БЕЗ резких смен направления!)
        Vector2 tangent = new Vector2(-toDesired.y, toDesired.x).normalized;
        Vector2 circleVel = tangent * flightSpeed * circleDirection;

        // Мягкий шум
        Vector2 noise = new Vector2(
            Mathf.PerlinNoise(Time.time * 0.8f + 100f, 0) - 0.5f,
            Mathf.PerlinNoise(0, Time.time * 0.8f + 200f) - 0.5f) * noiseStrength;

        desiredVelocity = circleVel + heightForce + noise;

        // Плавное применение скорости — главное, что убирает дёрганье
        rb2d.linearVelocity = Vector2.SmoothDamp(rb2d.linearVelocity, desiredVelocity, ref velocitySmooth, smoothTime);
    }

    private void TryDiveAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        if (Random.value < attackChancePerSec * Time.deltaTime)
            StartCoroutine(DiveAndFleeRoutine());
    }

    private IEnumerator DiveAndFleeRoutine()
    {
        batState = BatState.Diving;
        Vector2 diveDir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        rb2d.linearVelocity = diveDir * flightSpeed * diveMultiplier;
        if (animator != null) animator.SetTrigger("Attack");

        while (transform.position.y > target.position.y + minAllowedHeight + 0.2f)
            yield return null;

        DealDamage();
        lastAttackTime = Time.time;

        // Убегаем от игрока
        batState = BatState.FleeingAway;
        Vector2 fleeDir = ((Vector2)transform.position - (Vector2)target.position).normalized;
        fleeDir.y = Mathf.Max(0.7f, fleeDir.y);
        rb2d.linearVelocity = fleeDir.normalized * fleeSpeed;

        yield return new WaitForSeconds(fleeDuration);

        batState = BatState.CalmingDown;
        yield return new WaitForSeconds(calmDownTime);

        batState = BatState.Returning;
    }

    private void FleeingBehaviour()
    {
        rb2d.linearVelocity += Random.insideUnitCircle * 0.7f;
    }

    private void CalmingDownBehaviour()
    {
        rb2d.linearVelocity = Vector2.Lerp(rb2d.linearVelocity, Vector2.zero, Time.deltaTime * 2f);
        rb2d.linearVelocity += Random.insideUnitCircle * 0.3f;
    }

    private void ReturningBehaviour()
    {
        Vector2 playerPos = target.position;
        Vector2 returnPos = new Vector2(playerPos.x + Random.Range(-1.2f, 1.2f), playerPos.y + circlingHeight + Random.Range(0f, 0.6f));
        Vector2 toReturn = returnPos - (Vector2)transform.position;

        if (toReturn.sqrMagnitude < 1f)
        {
            batState = BatState.Circling;
            return;
        }

        rb2d.linearVelocity = toReturn.normalized * flightSpeed * 1.3f;
        if (transform.position.y < playerPos.y + circlingHeight - 0.5f)
            rb2d.linearVelocity += Vector2.up * heightRestoreForce * 0.8f;
    }

    protected override void StartChase()
    {
        state = State.Chase;
        batState = BatState.Circling;
        if (animator != null) animator.SetBool("IsChasing", true);
    }

    protected override void LoseTarget()
    {
        base.LoseTarget();
        batState = BatState.Idle;
        rb2d.linearVelocity = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("IsChasing", false);
            animator.SetBool("IsAttacking", false);
            animator.SetBool("IsScared", false);
        }
        StartCoroutine(InitialIdle());
    }

    private void FlipSprite(float velX)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * Mathf.Sign(velX);
        transform.localScale = s;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Vector3 h = target.position + Vector3.up * circlingHeight;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(h, 0.4f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(target.position + Vector3.up * minAllowedHeight - Vector3.right * 3f,
                        target.position + Vector3.up * minAllowedHeight + Vector3.right * 3f);
    }
}