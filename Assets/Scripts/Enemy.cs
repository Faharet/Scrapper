using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    // Настройки
    public float maxHealth = 100f;
    public float walkSpeed = 2f;
    public float chaseSpeed = 4f;
    public float detectRadius = 8f;
    public float attackRange = 1.8f;
    public float attackDamage = 20f;
    public float attackCooldown = 1.4f;
    public float loseTargetTime = 3f;
    public LayerMask playerLayer;
    public LayerMask obstructionMask;
    public Transform[] patrolPoints;

    // Компоненты
    NavMeshAgent agent;
    Animator animator;
    Transform target;
    int currentPatrolIndex = 0;

    // Состояние
    float currentHealth;
    float lastAttackTime = -999f;
    float lastSeenTime = -999f;
    enum State { Patrol, Chase, Attack, Dead }
    State state = State.Patrol;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;

        if (agent == null)
        {   
            Debug.LogWarning($"{name}: NavMeshAgent component not found — movement disabled.");
        }
        else
        {
            agent.speed = walkSpeed;

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                agent.SetDestination(patrolPoints[0].position);
            }
        }

        if (animator == null)
        {
            Debug.LogWarning($"{name}: Animator component not found — animations disabled.");
        }
    }

    void Update()
    {
        if (state == State.Dead) return;

        DetectPlayer();

        switch (state)
        {
            case State.Patrol:
                PatrolUpdate();
                break;
            case State.Chase:
                ChaseUpdate();
                break;
            case State.Attack:
                AttackUpdate();
                break;
        }

        if (animator != null && agent != null)
            animator.SetFloat("Speed", agent.velocity.magnitude);

        SnapToNavMesh();
    }

    void DetectPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius, playerLayer);
        if (hits == null || hits.Length == 0)
        {
            if (state == State.Chase && Time.time - lastSeenTime > loseTargetTime)
            {
                LoseTarget();
            }
            return;
        }

        Transform nearest = null;
        float bestDist = Mathf.Infinity;
        foreach (var c in hits)
        {
            if (c == null) continue;
            Vector3 dir = (c.transform.position - transform.position).normalized;
            float dist = Vector3.Distance(transform.position, c.transform.position);

            if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, dist, obstructionMask))
            {
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = c.transform;
                }
            }
        }

        if (nearest != null)
        {
            target = nearest;
            lastSeenTime = Time.time;
            if (state != State.Chase && state != State.Attack)
            {
                StartChase();
            }
        }
    }

    void PatrolUpdate()
    {
        if (agent == null) return;
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    void StartChase()
    {
        state = State.Chase;
        if (agent != null) agent.speed = chaseSpeed;
    }

    void ChaseUpdate()
    {
        if (target == null)
        {
            state = State.Patrol;
            if (agent != null) agent.speed = walkSpeed;
            return;
        }

        if (agent != null)
        {
            agent.SetDestination(target.position);
        }
        else
        {
            Vector3 dir = (target.position - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            state = State.Attack;
            if (animator != null) animator.SetTrigger("Attack");
            if (agent != null) agent.isStopped = true;
        }
    }

    void AttackUpdate()
    {
        if (target == null)
        {
            EndAttack();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange + 0.5f)
        {
            EndAttack();
            return;
        }

        if (agent == null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }
    }

    // Вызвать из Animation Event в момент удара
    public void DealDamage()
    {
        if (target == null) return;

        // Используем TryGetComponent с интерфейсом, чтобы избежать выделений и ошибок компиляции
        if (target.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(attackDamage);
        }

        lastAttackTime = Time.time;
    }

    void EndAttack()
    {
        state = State.Chase;
        if (agent != null) agent.isStopped = false;
    }

    void LoseTarget()
    {
        target = null;
        state = State.Patrol;
        if (agent != null)
        {
            agent.speed = walkSpeed;
            if (patrolPoints != null && patrolPoints.Length > 0)
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    public void TakeDamage(float amount)
    {
        if (state == State.Dead) return;
        currentHealth -= amount;
        if (animator != null) animator.SetTrigger("Hit");

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            // Можно установить target на атакующего игрока (если передавать Transform атакующего)
        }
    }

    void Die()
    {
        state = State.Dead;
        if (animator != null) animator.SetBool("IsDead", true);
        if (agent != null) agent.isStopped = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    void SnapToNavMesh()
    {
        if (agent == null) return;

        // Ищем ближайшую точку NavMesh в радиусе 1 м
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            // Смещаем по высоте на baseOffset агента, чтобы стоял над поверхностью
            Vector3 snapPos = hit.position + Vector3.up * agent.baseOffset;
            // Teleport agent к корректной высоте — безопаснее, чем прямое присваивание transform.position
            agent.Warp(snapPos);
        }
    }
}
