using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    // FSM
    private enum State { Idle, Patrol, Wander, Chase, Attack, Stunned }
    private State state = State.Idle;

    // Components
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Transform player;

    // Idle
    [Header("Idle")]
    public float idleTime = 2f;
    private float idleTimer;

    // Patrol / wander
    [Header("Movement")]
    public Transform[] patrolPoints;
    public float wanderRadius = 8f;
    private int patrolIndex = 0;

    // Perception
    [Header("Perception")]
    public float sightRange = 12f;
    public float sightAngle = 120f;
    public LayerMask sightLayerMask = ~0; // default everything
    public float attackRange = 1.6f;
    public float timeToForgetPlayer = 4f;
    private Vector3 lastKnownPlayerPosition;
    private float lastSeenTime;

    // Movement speeds
    [Header("Speeds")]
    public float patrolSpeed = 2.2f;
    public float chaseSpeed = 3.8f;

    // Attack
    [Header("Attack")]
    public float attackCooldown = 1f;
    private float lastAttackTime = -999f;

    // Health
    [Header("Health")]
    public int maxHealth = 3;
    private int currentHealth;

    // Separation / crowd avoidance
    [Header("Crowd")]
    public float separationRadius = 2.0f;
    public float separationStrength = 1.2f;
    public LayerMask enemyLayerMask;

    // Anti-stuck retreat
    [Header("Anti-Stuck")]
    public float minSeparationDistance = 0.8f;
    public float retreatDistance = 1.2f;
    public float stuckVelocityThreshold = 0.05f;

    // Stun / physics
    [Header("Stun / Knockback")]
    public float stunDuration = 1.0f;
    public float knockbackForce = 6f;
    private float stunTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // NavMesh controls movement by default
        rb.isKinematic = true;
        currentHealth = maxHealth;
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        idleTimer = idleTime;

        // if no patrol points, start wandering
        if (patrolPoints == null || patrolPoints.Length == 0)
            state = State.Wander;
        else
            state = State.Idle;
    }

    void Update()
    {
        switch (state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Wander: UpdateWander(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Stunned: UpdateStunned(); break;
        }

        // resolve local deadlocks between agents
        ResolveAgentStuck();

        // perception only when not stunned
        if (state != State.Stunned)
            TryDetectPlayer();
    }

    // ----------------- STATES -----------------

    void UpdateIdle()
    {
        agent.isStopped = true;
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            agent.isStopped = false;
            state = (patrolPoints != null && patrolPoints.Length > 0) ? State.Patrol : State.Wander;
        }
    }

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            state = State.Wander;
            return;
        }

        // go to current patrol point if we don't have a path
        if (!agent.hasPath)
            agent.SetDestination(patrolPoints[patrolIndex].position);

        // reached point -> idle and go to next
        if (!agent.pathPending && agent.remainingDistance <= 0.8f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            state = State.Idle;
            idleTimer = idleTime;
        }

        ApplySeparation();
    }

    void UpdateWander()
    {
        agent.speed = patrolSpeed;
        if (!agent.hasPath || agent.remainingDistance < 1f)
        {
            Vector3 rnd = Random.insideUnitSphere * wanderRadius + transform.position;
            if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        ApplySeparation();
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;

        // pathfind to last known player position
        agent.SetDestination(lastKnownPlayerPosition);

        // if we have direct vision and are within attack range, attack
        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange && IsPlayerVisible())
        {
            state = State.Attack;
            agent.isStopped = true;
            return;
        }

        // no recent sight -> give up and wander
        if (Time.time - lastSeenTime > timeToForgetPlayer)
        {
            state = State.Wander;
            agent.isStopped = false;
        }

        ApplySeparation();
    }

    void UpdateAttack()
    {
        // simple timed attack, deals damage if player has PlayerHealth
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformAttack();
        }

        // if player moved away, resume chase
        if (player == null || Vector3.Distance(transform.position, player.position) > attackRange + 0.5f)
        {
            agent.isStopped = false;
            state = State.Chase;
        }
    }

    void UpdateStunned()
    {
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f) EndStun();
    }

    // ----------------- PERCEPTION -----------------

    void TryDetectPlayer()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > sightRange) return;

        // fov check
        Vector3 toPlayer = (player.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return;

        // raycast to check occlusion
        Vector3 eye = transform.position + Vector3.up * 0.9f;
        Vector3 dir = (player.position - eye).normalized;
        if (Physics.Raycast(eye, dir, out RaycastHit hit, sightRange, sightLayerMask))
        {
            if (hit.transform == player || hit.collider.CompareTag("Player"))
            {
                lastKnownPlayerPosition = player.position;
                lastSeenTime = Time.time;
                if (state != State.Chase && state != State.Attack)
                    state = State.Chase;
            }
        }
    }

    bool IsPlayerVisible()
    {
        if (player == null) return false;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > sightRange) return false;

        Vector3 eye = transform.position + Vector3.up * 0.9f;
        Vector3 dir = (player.position - eye).normalized;
        if (Physics.Raycast(eye, dir, out RaycastHit hit, sightRange, sightLayerMask))
            return hit.transform == player || hit.collider.CompareTag("Player");
        return false;
    }

    // called by player gun when firing (noise)
    public void OnNoiseHeard(Vector3 noisePosition)
    {
        lastKnownPlayerPosition = noisePosition;
        lastSeenTime = Time.time;
        if (state != State.Stunned)
            state = State.Chase;
    }

    // ----------------- ATTACK / DAMAGE -----------------

    void PerformAttack()
    {
        // try to damage player if they have PlayerCharacterController
        if (player != null)
        {
            var pcc = player.GetComponent<PlayerCharacterController>();
            if (pcc != null)
            {
                pcc.TakeDamage(1f); // deal 1 HP of damage
            }
            else
            {
                Debug.Log($"{name} would attack player (no PlayerCharacterController found).");
            }
        }
    }


    // simple health on the enemy itself
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
            Die();
        else
            OnDamageReaction();
    }

    void OnDamageReaction()
    {
        // small reaction: go to last known player position and chase
        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            lastSeenTime = Time.time;
            state = State.Chase;
        }
    }

    void Die()
    {
        // disable agent and collider, then deactivate
        if (agent != null) agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        // optional: spawn death FX here
        gameObject.SetActive(false);
    }

    // ----------------- SEPARATION / ANTI-STUCK -----------------

    // small offset away from nearby enemies so they don't bunch up
    Vector3 ComputeSeparationOffset()
    {
        Vector3 offset = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, enemyLayerMask);
        int count = 0;
        foreach (var c in hits)
        {
            if (c.gameObject == this.gameObject) continue;
            Vector3 away = transform.position - c.transform.position;
            float d = away.magnitude;
            if (d > 0.001f)
            {
                offset += away.normalized / d;
                count++;
            }
        }

        if (count > 0)
        {
            offset = (offset / count) * separationStrength;
            offset.y = 0f;
        }
        return offset;
    }

    void ApplySeparation()
    {
        if (agent.pathPending) return;
        if (agent.hasPath || agent.remainingDistance > 0.1f)
        {
            Vector3 baseDest = agent.destination;
            Vector3 sep = ComputeSeparationOffset();
            agent.SetDestination(baseDest + sep);
        }
    }

    // If agents are jammed (low velocity and close together) retreat a bit
    void ResolveAgentStuck()
    {
        if (!agent.enabled || !agent.hasPath) return;
        if (agent.velocity.magnitude >= stuckVelocityThreshold) return;

        Collider[] nearby = Physics.OverlapSphere(transform.position, minSeparationDistance, enemyLayerMask);
        if (nearby.Length <= 1) return;

        Vector3 retreatDir = Vector3.zero;
        foreach (var c in nearby)
        {
            if (c.gameObject == gameObject) continue;
            Vector3 away = transform.position - c.transform.position;
            if (away.sqrMagnitude > 0.001f)
                retreatDir += away.normalized;
        }

        if (retreatDir.sqrMagnitude < 0.0001f) return;

        retreatDir.Normalize();
        Vector3 retreatTarget = transform.position + retreatDir * retreatDistance;
        if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, retreatDistance, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    // ----------------- STUN / KNOCKBACK -----------------

    // externally callable: apply knockback/stun from explosion or bullet special effect
    public void ApplyKnockback(Vector3 sourcePosition, float force = -1f, float duration = -1f)
    {
        if (force <= 0f) force = knockbackForce;
        if (duration <= 0f) duration = stunDuration;
        StartCoroutine(DoStunRoutine(sourcePosition, force, duration));
    }

    IEnumerator DoStunRoutine(Vector3 sourcePosition, float force, float duration)
    {
        state = State.Stunned;
        stunTimer = duration;

        // disable nav control, enable physics
        if (agent != null) agent.enabled = false;
        rb.isKinematic = false;

        Vector3 dir = (transform.position - sourcePosition).normalized;
        dir.y = 0.3f;
        rb.AddForce(dir * force, ForceMode.Impulse);

        while (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            yield return null;
        }

        EndStun();
    }

    void EndStun()
    {
        // stop physics, return control to agent
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        if (agent != null) agent.enabled = true;

        state = State.Idle;
        idleTimer = idleTime;
    }

    // ----------------- UTILITIES -----------------

    // external forcing (debug or leader logic)
    public void ForceChase(Transform target)
    {
        player = target;
        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            lastSeenTime = Time.time;
            state = State.Chase;
        }
    }
}
