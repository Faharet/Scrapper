using UnityEngine;
using UnityEngine.AI;

public abstract class Enemy : MonoBehaviour, IDamageable
{
	[Header("Stats")]
	[SerializeField] protected float maxHealth = 100f;
	[SerializeField] protected float walkSpeed = 2f;
	[SerializeField] protected float chaseSpeed = 4f;
	[SerializeField] protected float detectRadius = 8f;
	[SerializeField] protected float attackRange = 1.8f;
	[SerializeField] protected float attackDamage = 20f;
	// Public accessor so external objects (like PlayerController) can read attack damage
	public float AttackDamage => attackDamage;
	[SerializeField] protected float attackCooldown = 1.4f;
	[SerializeField] protected float loseTargetTime = 3f;
	[SerializeField] protected LayerMask playerLayer;
	[SerializeField] protected LayerMask obstructionMask;
	[SerializeField] protected Transform[] patrolPoints;

	[Header("Physics")]
	[SerializeField] protected bool preventBeingPushed = true;
	[SerializeField] protected Collider2D[] triggerColliders;
	[SerializeField] protected float massWhenPreventPushed = 75f;

	// Components (cached)
	protected NavMeshAgent agent;
	protected Animator animator;
	protected Rigidbody2D rb2d;

	// State
	protected Transform target;
	protected int currentPatrolIndex;
	protected float currentHealth;
	protected float lastAttackTime = -999f;
	protected float lastSeenTime = -999f;

	protected enum State { Patrol, Chase, Attack, Dead }
	protected State state = State.Patrol;

	protected virtual void Awake()
	{
		agent = GetComponent<NavMeshAgent>();
		animator = GetComponent<Animator>();
		rb2d = GetComponent<Rigidbody2D>();
	}

	protected virtual void Start()
	{
		currentHealth = maxHealth;

		if (agent != null)
		{
			agent.speed = walkSpeed;
			if (patrolPoints != null && patrolPoints.Length > 0)
				agent.SetDestination(patrolPoints[0].position);
		}

		if (preventBeingPushed)
		{
			if (rb2d != null && massWhenPreventPushed > 0f)
			{
				rb2d.mass = massWhenPreventPushed;
				rb2d.freezeRotation = true;
			}

			if (triggerColliders != null && triggerColliders.Length > 0)
			{
				foreach (var c in triggerColliders)
					if (c != null) c.isTrigger = true;
			}
		}
	}

	protected virtual void Update()
	{
		if (state == State.Dead) return;

		DetectPlayer();

		switch (state)
		{
			case State.Patrol: PatrolUpdate(); break;
			case State.Chase: ChaseUpdate(); break;
			case State.Attack: AttackUpdate(); break;
		}

		if (animator != null && agent != null)
			animator.SetFloat("Speed", agent.velocity.magnitude);

		SnapToNavMesh();
	}

	protected virtual void DetectPlayer()
	{
		Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius, playerLayer);
		if (hits == null || hits.Length == 0)
		{
			if (state == State.Chase && Time.time - lastSeenTime > loseTargetTime)
				LoseTarget();
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
				StartChase();
		}
	}

	protected virtual void PatrolUpdate()
	{
		if (agent == null) return;
		if (patrolPoints == null || patrolPoints.Length == 0) return;
		if (!agent.pathPending && agent.remainingDistance < 0.5f)
		{
			currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
			agent.SetDestination(patrolPoints[currentPatrolIndex].position);
		}
	}

	protected virtual void StartChase()
	{
		state = State.Chase;
		if (agent != null) agent.speed = chaseSpeed;
	}

	protected virtual void ChaseUpdate()
	{
		if (target == null)
		{
			state = State.Patrol;
			if (agent != null) agent.speed = walkSpeed;
			return;
		}

		if (agent != null)
			agent.SetDestination(target.position);

		float dist = Vector3.Distance(transform.position, target.position);
		if (dist <= attackRange && Time.time - lastAttackTime >= attackCooldown)
		{
			state = State.Attack;
			if (animator != null) animator.SetTrigger("Attack");
			if (agent != null) agent.isStopped = true;
		}
	}

	protected virtual void AttackUpdate()
	{
		if (target == null) { EndAttack(); return; }
		float dist = Vector3.Distance(transform.position, target.position);
		if (dist > attackRange + 0.5f) { EndAttack(); return; }
	}

	public virtual void DealDamage()
	{
		if (target == null) return;
		if (target.TryGetComponent<IDamageable>(out var damageable))
		{
			damageable.TakeDamage(attackDamage);
		}
		lastAttackTime = Time.time;
	}

	protected virtual void EndAttack()
	{
		state = State.Chase;
		if (agent != null) agent.isStopped = false;
	}

	protected virtual void LoseTarget()
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

	public virtual void TakeDamage(float amount)
	{
		if (state == State.Dead) return;
		currentHealth -= amount;
		if (animator != null) animator.SetTrigger("Hit");
		if (currentHealth <= 0f) Die();
	}

	public virtual void Heal(float amount)
	{
		if (state == State.Dead) return;
		currentHealth += amount;
		currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
	}

	public virtual float CurrentHealth => currentHealth;

	protected virtual void Die()
	{
		state = State.Dead;
		if (animator != null) animator.SetBool("IsDead", true);
		if (agent != null) agent.isStopped = true;
	}

	protected virtual void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, detectRadius);
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, attackRange);
	}

	protected virtual void SnapToNavMesh()
	{
		if (agent == null) return;
		if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
		{
			Vector3 snapPos = hit.position + Vector3.up * agent.baseOffset;
			agent.Warp(snapPos);
		}
	}
}