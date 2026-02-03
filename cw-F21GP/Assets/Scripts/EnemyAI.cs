using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    // Finite State Machine states
    private enum State { Patrol, Wander, Chase, Attack, Stunned }
    private State state = State.Patrol;

    // Main components
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Transform player;

    // Patrol / wander settings
    [Header("Patrol & Wander")]
    public Transform[] patrolPoints;   // points to move between
    public float wanderRadius = 8f;    // random movement range
    public float patrolPointTolerance = 1f;
    private int currentPatrolIndex = 0;
    private Vector3 wanderTarget;

    // Vision and detection
    [Header("Perception")]
    public float sightRange = 12f;
    public float sightAngle = 120f;    // field of view
    public float attackRange = 1.6f;
    public LayerMask sightLayerMask;   // walls + environment
    public float timeToLoseInterest = 4f;
    private float lostSightTimer = 0f;

    // Attack timing
    [Header("Attack")]
    public float attackCooldown = 1.0f;
    private float lastAttackTime = -999f;

    // Crowd behaviour
    [Header("Crowd Separation")]
    public float separationRadius = 2.0f;
    public float separationStrength = 1.2f;
    public LayerMask enemyLayerMask;

    // Prevent agents getting stuck together
    [Header("Anti-Stuck Behaviour")]
    public float minSeparationDistance = 0.8f;
    public float retreatDistance = 1.2f;
    public float stuckVelocityThreshold = 0.05f;

    // Physics stun / knockback
    [Header("Stun / Physics")]
    public float stunDuration = 1.0f;
    private float stunTimer = 0f;
    public float knockbackForce = 6f;
    private bool isPhysicallyStunned = false;

    // Movement speeds
    [Header("Misc")]
    public float chaseSpeed = 3.8f;
    public float patrolSpeed = 2.2f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // NavMesh controls movement by default
        rb.isKinematic = true;
    }

    void Start()
    {
        // Find player using tag
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        agent.speed = patrolSpeed;
        agent.stoppingDistance = attackRange * 0.9f;

        SetNextWanderTarget();
    }

    void Update()
    {
        // Run behaviour based on current state
        switch (state)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Wander:
                UpdateWander();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
            case State.Stunned:
                UpdateStunned();
                break;
        }

        // Fix agents that stop moving when too close
        ResolveAgentStuck();

        // Try to spot the player unless stunned
        if (state != State.Stunned)
        {
            TryDetectPlayer();
        }
    }

    #region STATE_UPDATES

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        // Move between patrol points if they exist
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            if (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }

            ApplySeparation();
        }
        else
        {
            // No patrol points → switch to wandering
            state = State.Wander;
            SetNextWanderTarget();
        }
    }

    void UpdateWander()
    {
        agent.speed = patrolSpeed;

        // Pick a new random destination when close enough
        if (!agent.pathPending && agent.remainingDistance <= 0.9f)
        {
            SetNextWanderTarget();
        }

        ApplySeparation();
    }

    void UpdateChase()
    {
        if (player == null)
        {
            state = State.Wander;
            return;
        }

        agent.speed = chaseSpeed;

        // Move toward player with separation offset
        Vector3 dest = player.position;
        Vector3 offset = ComputeSeparationOffset();
        agent.SetDestination(dest + offset);

        float dist = Vector3.Distance(transform.position, player.position);

        // Close enough → attack
        if (dist <= attackRange)
        {
            state = State.Attack;
            agent.isStopped = true;
            return;
        }

        // Lose interest if player not visible for some time
        if (!IsPlayerVisible())
        {
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer >= timeToLoseInterest)
            {
                lostSightTimer = 0f;
                state = State.Wander;
                agent.isStopped = false;
                SetNextWanderTarget();
            }
        }
        else
        {
            lostSightTimer = 0f;
        }
    }

    void UpdateAttack()
    {
        if (player == null)
        {
            state = State.Wander;
            agent.isStopped = false;
            return;
        }

        // Simple timed attack
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformAttack();
        }

        // Too far → chase again
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackRange + 0.2f)
        {
            agent.isStopped = false;
            state = State.Chase;
        }
    }

    void UpdateStunned()
    {
        stunTimer -= Time.deltaTime;

        if (stunTimer <= 0f)
        {
            EndStun();
        }
    }

    #endregion

    #region PERCEPTION

    void TryDetectPlayer()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > sightRange) return;

        // Check if player is inside field of view
        Vector3 toPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, toPlayer);

        if (angle <= sightAngle * 0.5f)
        {
            // Raycast to check if something blocks vision
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 dir = (player.position - eye).normalized;

            if (Physics.Raycast(eye, dir, out RaycastHit hit, sightRange, sightLayerMask))
            {
                if (hit.transform == player || hit.collider.CompareTag("Player"))
                {
                    OnPlayerSpotted();
                }
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
        {
            return hit.transform == player || hit.collider.CompareTag("Player");
        }

        return false;
    }

    void OnPlayerSpotted()
    {
        if (state == State.Stunned) return;

        state = State.Chase;
        agent.isStopped = false;
        lostSightTimer = 0f;
    }

    #endregion

    #region ATTACK

    void PerformAttack()
    {
        // Placeholder attack logic
        Debug.Log($"{name} attacks player at {Time.time}");
    }

    #endregion

    #region SEPARATION

    // Calculates offset away from nearby enemies
    Vector3 ComputeSeparationOffset()
    {
        Vector3 offset = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(transform.position, separationRadius, enemyLayerMask);

        int count = 0;
        foreach (var c in hits)
        {
            if (c.gameObject == gameObject) continue;

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
            Vector3 baseDestination = agent.destination;
            Vector3 sep = ComputeSeparationOffset();
            agent.SetDestination(baseDestination + sep);
        }
    }

    // Forces agent to back away if stuck with others
    void ResolveAgentStuck()
    {
        if (!agent.enabled || !agent.hasPath) return;
        if (agent.velocity.magnitude >= stuckVelocityThreshold) return;

        Collider[] nearby = Physics.OverlapSphere(
            transform.position,
            minSeparationDistance,
            enemyLayerMask
        );

        if (nearby.Length <= 1) return;

        Vector3 retreatDir = Vector3.zero;

        foreach (Collider c in nearby)
        {
            if (c.gameObject == gameObject) continue;

            Vector3 away = transform.position - c.transform.position;
            if (away.sqrMagnitude > 0.001f)
            {
                retreatDir += away.normalized;
            }
        }

        if (retreatDir == Vector3.zero) return;

        retreatDir.Normalize();
        Vector3 retreatTarget = transform.position + retreatDir * retreatDistance;

        if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, retreatDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    #endregion

    #region WANDER_HELPERS

    void SetNextWanderTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            wanderTarget = hit.position;
            agent.SetDestination(wanderTarget);
        }
        else
        {
            agent.SetDestination(transform.position + transform.forward * 2f);
        }
    }

    #endregion

    #region STUN / PHYSICS

    // Applies knockback and temporary stun
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

        agent.enabled = false;
        rb.isKinematic = false;

        Vector3 direction = (transform.position - sourcePosition).normalized;
        direction.y = 0.3f;

        rb.AddForce(direction * force, ForceMode.Impulse);
        isPhysicallyStunned = true;

        while (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            yield return null;
        }

        EndStun();
    }

    void EndStun()
    {
        isPhysicallyStunned = false;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        agent.enabled = true;

        state = State.Wander;
        SetNextWanderTarget();
    }

    #endregion

    // Debug helper
    public void ForceChase(Transform target)
    {
        player = target;
        OnPlayerSpotted();
    }
}
