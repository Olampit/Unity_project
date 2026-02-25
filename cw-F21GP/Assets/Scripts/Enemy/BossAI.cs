using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System;
using F21GP.Managers;
using F21GP.Player;

namespace F21GP.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class BossAI : MonoBehaviour
    {
        private enum State { Idle, Patrol, Wander, Chase, Attack, Stunned }
        private State state = State.Idle;

        [Header("Data")]
        [SerializeField] private EnemyStats _enemyStats;

        [Header("Components")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Rigidbody rb;
        private Transform player;

        private float idleTimer;
        private float yieldTimer;

        [Header("Movement")]
        public Transform[] patrolPoints;
        private int patrolIndex = 0;

        [Header("Perception")]
        public LayerMask sightLayerMask = ~0;
        private Vector3 lastKnownPlayerPosition;
        private float lastSeenTime;

        [Header("Attack")]
        private float lastAttackTime = -999f;
        public Transform[] gunTips;
        public LineRenderer[] laserLines;
        public float shotDuration = 0.07f;
        public AudioSource gunAudio;

        private float currentHealth;

        [Header("Crowd")]
        public LayerMask enemyLayerMask;

        public event Action OnEnemyDeath;
        [HideInInspector] public bool OverridePathfinding = false;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;

            if (_enemyStats != null)
                currentHealth = _enemyStats.MaxHealth;
        }

        void Start()
        {
            if (GameManager.Instance != null)
                player = GameManager.Instance.PlayerTransform;

            if (laserLines == null || laserLines.Length == 0)
            {
                var lr = GetComponent<LineRenderer>();
                if (lr != null) laserLines = new LineRenderer[] { lr };
            }
            if (gunAudio == null) gunAudio = GetComponent<AudioSource>();

            if (_enemyStats != null)
                idleTimer = _enemyStats.IdleTime;

            if (patrolPoints == null || patrolPoints.Length == 0)
                state = State.Wander;
            else
                state = State.Idle;

            agent.stoppingDistance = 0.8f;
        }

        void Update()
        {
            if (yieldTimer > 0f && state != State.Stunned)
            {
                yieldTimer -= Time.deltaTime;
                ResolveAgentStuck();
            }
            else
            {
                switch (state)
                {
                    case State.Idle: UpdateIdle(); break;
                    case State.Patrol: UpdatePatrol(); break;
                    case State.Wander: UpdateWander(); break;
                    case State.Chase: UpdateChase(); break;
                    case State.Attack: UpdateAttack(); break;
                }

                ResolveAgentStuck();
            }

            if (agent.remainingDistance == Mathf.Infinity)
                agent.ResetPath();

            if (_enemyStats != null && agent.stoppingDistance != _enemyStats.AttackRange - 1.0f)
                agent.stoppingDistance = Mathf.Max(_enemyStats.AttackRange - 1.0f, 0.5f);

            if (state != State.Stunned)
                TryDetectPlayer();
        }

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
            agent.speed = _enemyStats.PatrolSpeed;

            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                state = State.Wander;
                return;
            }

            if (!OverridePathfinding && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }

            if (!agent.pathPending && agent.remainingDistance <= 0.8f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                state = State.Idle;
                idleTimer = _enemyStats.IdleTime;
            }

            ApplySeparation();
        }

        void UpdateWander()
        {
            agent.speed = _enemyStats.PatrolSpeed;

            if (agent.pathPending) return;

            if (!OverridePathfinding && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f))
            {
                Vector3 rnd = transform.position + UnityEngine.Random.insideUnitSphere * _enemyStats.WanderRadius;
                if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, _enemyStats.WanderRadius, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }

            ApplySeparation();
        }

        void UpdateChase()
        {
            agent.speed = _enemyStats.ChaseSpeed;

            if (!OverridePathfinding)
                agent.SetDestination(lastKnownPlayerPosition);

            if (player != null &&
                Vector3.Distance(transform.position, player.position) <= _enemyStats.AttackRange &&
                IsPlayerVisible())
            {
                state = State.Attack;
                agent.isStopped = true;
                return;
            }

            if (Time.time - lastSeenTime > _enemyStats.TimeToForgetPlayer)
            {
                state = State.Wander;
                agent.isStopped = false;
            }

            ApplySeparation();
        }

        void UpdateAttack()
        {
            if (player != null)
                FaceTarget(player.position);

            if (Time.time - lastAttackTime >= _enemyStats.AttackCooldown)
            {
                lastAttackTime = Time.time;
                PerformAttack();
            }

            if (player == null ||
                Vector3.Distance(transform.position, player.position) > _enemyStats.AttackRange + 0.5f)
            {
                agent.isStopped = false;
                state = State.Chase;
            }
        }

        void FaceTarget(Vector3 target)
        {
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0f;
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }

        void TryDetectPlayer()
        {
            if (player == null || state == State.Stunned) return;

            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 toPlayer = player.position - eye;

            if (toPlayer.magnitude > _enemyStats.SightRange) return;
            if (Vector3.Angle(transform.forward, toPlayer) > _enemyStats.SightAngle * 0.5f) return;

            if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, _enemyStats.SightRange, sightLayerMask))
            {
                if (hit.transform == player || hit.collider.CompareTag("Player"))
                {
                    lastKnownPlayerPosition = player.position;
                    lastSeenTime = Time.time;
                    state = State.Chase;
                    agent.isStopped = false;
                }
            }
        }

        bool IsPlayerVisible()
        {
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 dir = (player.position - eye).normalized;
            if (Physics.Raycast(eye, dir, out RaycastHit hit, _enemyStats.SightRange, sightLayerMask))
                return hit.transform == player || hit.collider.CompareTag("Player");
            return false;
        }

        void PerformAttack()
        {
            StartCoroutine(ShotEffect());

            Vector3 origin = (gunTips != null && gunTips.Length > 0 && gunTips[0] != null)
                ? gunTips[0].position
                : transform.position + Vector3.up * 1.5f;

            Vector3 target = player.position + Vector3.up * -0.7f;
            Vector3 dir = (target - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, 100f, sightLayerMask))
            {
                var pcc = hit.collider.GetComponentInParent<PlayerCharacterController>();
                if (pcc != null)
                    pcc.TakeDamage(_enemyStats.AttackDamage);
            }
        }

        IEnumerator ShotEffect()
        {
            if (gunAudio != null) gunAudio.Play();

            if (laserLines != null)
                foreach (var lr in laserLines) if (lr != null) lr.enabled = true;

            yield return new WaitForSeconds(shotDuration);

            if (laserLines != null)
                foreach (var lr in laserLines) if (lr != null) lr.enabled = false;
        }

        public void TakeDamage(float amount)
        {
            currentHealth -= amount;
            if (currentHealth <= 0) Die();
            else
            {
                lastKnownPlayerPosition = player.position;
                lastSeenTime = Time.time;
                state = State.Chase;
            }
        }

        void Die()
        {
            OnEnemyDeath?.Invoke();
            agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
        }

        public void OnNoiseHeard(Vector3 pos)
        {
            lastKnownPlayerPosition = pos;
            lastSeenTime = Time.time;
            state = State.Chase;
        }

        public void PlaceAt(Transform spawnPoint)
        {
            if (spawnPoint == null) return;
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
            if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        Vector3 ComputeSeparationOffset()
        {
            Vector3 offset = Vector3.zero;
            float sepRad = _enemyStats != null ? _enemyStats.SeparationRadius : 2.0f;
            float sepStr = _enemyStats != null ? _enemyStats.SeparationStrength : 1.2f;

            Collider[] hits = Physics.OverlapSphere(transform.position, sepRad, enemyLayerMask);
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
                offset = (offset / count) * sepStr;
                offset.y = 0f;
            }
            return offset;
        }
        void ApplySeparation()
        {
            if (OverridePathfinding) return;
            if (state == State.Stunned) return;
            if (agent.pathPending) return;
            if (!agent.hasPath) return;

            Vector3 sep = ComputeSeparationOffset();

            // Only apply if meaningful
            if (sep.sqrMagnitude < 0.3f * 0.3f)
                return;

            Vector3 newDest = agent.destination + sep;

            // Only update if destination actually changed significantly
            if (Vector3.Distance(newDest, agent.destination) > 0.5f)
            {
                agent.SetDestination(newDest);
            }
        }

        void ResolveAgentStuck()
            {
                if (OverridePathfinding) return;
                if (!agent.enabled || !agent.hasPath) return;
                // Only reconsider yielding if we aren't currently deeply into a yield (allow slight chaining)
                if (yieldTimer > 0.5f) return; 

                if (_enemyStats != null && agent.velocity.magnitude >= _enemyStats.StuckVelocityThreshold) return;

                float minSepDist = _enemyStats != null ? _enemyStats.MinSeparationDistance : 0.8f;
                Collider[] nearby = Physics.OverlapSphere(transform.position, minSepDist, enemyLayerMask);
                if (nearby.Length <= 1) return;

                Vector3 retreatDir = Vector3.zero;
                foreach (var c in nearby)
                {
                    if (c.gameObject == gameObject) continue;

                    // ASYMMETRY: Only the drone with the HIGHER ID will yield and retreat.
                    // This prevents both drones from endlessly reversing and pushing into each other.
                    if (gameObject.GetInstanceID() < c.gameObject.GetInstanceID()) return;

                    Vector3 away = transform.position - c.transform.position;
                    // Emphasize moving backwards
                    away += -transform.forward * 1.5f; 

                    if (away.sqrMagnitude > 0.001f)
                        retreatDir += away.normalized;
                }

                if (retreatDir.sqrMagnitude < 0.0001f) return;

                retreatDir.Normalize();
                float retDist = _enemyStats != null ? _enemyStats.RetreatDistance : 1.2f;
                Vector3 retreatTarget = transform.position + retreatDir * retDist;
                if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, retDist, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    yieldTimer = 1.0f; // Pause normal AI for 1 second to actually back up
                }
            }

        public void SetPlayer(Transform target)
        {
            player = target;
        }
    }
}