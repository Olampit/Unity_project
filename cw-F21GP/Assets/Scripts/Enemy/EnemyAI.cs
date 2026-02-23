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
    public class EnemyAI : MonoBehaviour
    {
        #region Init
        // FSM
        private enum State { Idle, Patrol, Wander, Chase, Attack, Stunned }
        private State state = State.Idle;

        [Header("Data")]
        [SerializeField] private EnemyStats _enemyStats;

        // Components
        [Header("Components")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Rigidbody rb;
        private Transform player;

        // Idle
        private float idleTimer;
        private float yieldTimer; // Used for anti-stuck yielding

        // Patrol / wander
        [Header("Movement")]
        public Transform[] patrolPoints;
        private int patrolIndex = 0;

        // Perception
        [Header("Perception")]
        public LayerMask sightLayerMask = ~0; // default everything
        private Vector3 lastKnownPlayerPosition;
        private float lastSeenTime;

        // Attack
        [Header("Attack")]
        private float lastAttackTime = -999f;
        public Transform[] gunTips;
        public LineRenderer[] laserLines;
        public float shotDuration = 0.07f;
        public AudioSource gunAudio;

        // Health
        private float currentHealth;

        // Separation / crowd avoidance
        [Header("Crowd")]
        public LayerMask enemyLayerMask;


        public event Action OnEnemyDeath;


        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;   // navmesh controls vertical normally

            if (_enemyStats != null) currentHealth = _enemyStats.MaxHealth;
        }


        void Start()
        {
            if (GameManager.Instance != null)
                player = GameManager.Instance.PlayerTransform;

            // Auto-assign if empty (legacy backup)
            if (laserLines == null || laserLines.Length == 0)
            {
                var lr = GetComponent<LineRenderer>();
                if (lr != null) laserLines = new LineRenderer[] { lr };
            }
            if (gunAudio == null) gunAudio = GetComponent<AudioSource>();

            if (_enemyStats != null) idleTimer = _enemyStats.IdleTime;

            // if no patrol points, start wandering
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
                // Still allow perception and basic animation while yielding, but skip movement state updates
                // resolve local deadlocks between agents
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

                // resolve local deadlocks between agents
                ResolveAgentStuck();
            }

                if (agent.remainingDistance == Mathf.Infinity)
                {
                    agent.ResetPath();
                }

                if (_enemyStats != null && agent.stoppingDistance != _enemyStats.AttackRange - 1.0f)
                    agent.stoppingDistance = Mathf.Max(_enemyStats.AttackRange - 1.0f, 0.5f);
            
                // perception only when not stunned
            if (state != State.Stunned)
                TryDetectPlayer();
                if (Time.frameCount % 10 == 0)
            
            Debug.Log($"{name} state={state} pos={transform.position:F2} dest={agent.destination:F2} remDist={agent.remainingDistance:F2} hasPath={agent.hasPath} pathPending={agent.pathPending}");
        }

        #endregion

        #region States

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
                if (_enemyStats != null) agent.speed = _enemyStats.PatrolSpeed;
                if (patrolPoints == null || patrolPoints.Length == 0)
                {
                    state = State.Wander;
                    return;
                }

                // go to current patrol point if we don't have a path
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                }

                // reached point -> idle and go to next
                if (!agent.pathPending && agent.remainingDistance <= 0.8f)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    state = State.Idle;
                    if (_enemyStats != null) idleTimer = _enemyStats.IdleTime;
                }

                Debug.Log($"{name} next patrol index = {patrolIndex}");

                ApplySeparation();
            }

            void UpdateWander()
            {
                if (_enemyStats != null) agent.speed = _enemyStats.PatrolSpeed;

                if (agent.pathPending)
                    return;

                if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                {
                    float radius = _enemyStats != null ? _enemyStats.WanderRadius : 8f;
                    Vector3 rnd = transform.position + UnityEngine.Random.insideUnitSphere * radius;

                    if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, radius, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }

                ApplySeparation();
            }

            void UpdateChase()
            {
                if (_enemyStats != null) agent.speed = _enemyStats.ChaseSpeed;

                // pathfind to last known player position
                agent.SetDestination(lastKnownPlayerPosition);

                // if we have direct vision and are within attack range, attack
                if (player != null && _enemyStats != null && Vector3.Distance(transform.position, player.position) <= _enemyStats.AttackRange && IsPlayerVisible())
                {
                    state = State.Attack;
                    agent.isStopped = true;
                    return;
                }

                // no recent sight -> give up and wander
                if (_enemyStats != null && Time.time - lastSeenTime > _enemyStats.TimeToForgetPlayer)
                {
                    state = State.Wander;
                    agent.isStopped = false;
                }

                ApplySeparation();
            }

            void UpdateAttack()
            {
                // rotate towards player even while attacking
                if (player != null) FaceTarget(player.position);

                // simple timed attack, deals damage if player has PlayerHealth
                if (_enemyStats != null && Time.time - lastAttackTime >= _enemyStats.AttackCooldown)
                {
                    lastAttackTime = Time.time;
                    PerformAttack();
                }

                // if player moved away, resume chase
                if (player == null || (_enemyStats != null && Vector3.Distance(transform.position, player.position) > _enemyStats.AttackRange + 0.5f))
                {
                    agent.isStopped = false;
                    state = State.Chase;
                }
            }

        void FaceTarget(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            direction.y = 0; // maintain upright orientation
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

            void OnDrawGizmosSelected()
            {
                if (_enemyStats == null) return;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, _enemyStats.AttackRange);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, _enemyStats.SightRange);
            }

        #endregion

        #region Perception

        void TryDetectPlayer()
        {
            if (player == null || state == State.Stunned)
                return;

            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 toPlayer = player.position - eye;

            float sRange = _enemyStats != null ? _enemyStats.SightRange : 12f;
            float sAngle = _enemyStats != null ? _enemyStats.SightAngle : 120f;

            if (toPlayer.magnitude > sRange)
                return;

            if (Vector3.Angle(transform.forward, toPlayer) > sAngle * 0.5f)
                return;

            if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, sRange, sightLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == player || hit.collider.CompareTag("Player"))
                {
                    lastKnownPlayerPosition = player.position;
                    lastSeenTime = Time.time;

                    if (state != State.Chase && state != State.Attack)
                        state = State.Chase;
                    agent.isStopped = false;
                }
            }
        }
        bool IsPlayerVisible()
        {
            if (player == null) return false;
            float sRange = _enemyStats != null ? _enemyStats.SightRange : 12f;
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > sRange) return false;

            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 dir = (player.position - eye).normalized;
            if (Physics.Raycast(eye, dir, out RaycastHit hit, sRange, sightLayerMask))
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

        #endregion

        #region Attack / Damage

        void PerformAttack()
        {
            // try to damage player if they have PlayerCharacterController
            if (player != null)
            {
                StartCoroutine(ShotEffect());

                // Use first gun tip for logic, or enemy center if none
                Vector3 logicalOrigin = (gunTips != null && gunTips.Length > 0 && gunTips[0] != null) 
                                        ? gunTips[0].position 
                                        : transform.position + Vector3.up * 1.5f;

                // Aim for chest/head instead of feet
                Vector3 targetPoint = player.position + Vector3.up * -0.7f + transform.right * -0.3f; 
                Vector3 direction = (targetPoint - logicalOrigin).normalized;

                // 1. VISUALS: Set all lasers
                if (laserLines != null && gunTips != null)
                {
                    for (int i = 0; i < laserLines.Length; i++)
                    {
                        if (i >= gunTips.Length) break; // safety
                        if (laserLines[i] == null || gunTips[i] == null) continue;

                        laserLines[i].SetPosition(0, gunTips[i].position);
                        
                        // We can simply draw the line to where the logic raycast hits (or misses)
                        // but relative to this gun tip's perspective, it might look slightly off if perfectly parallel.
                        // For simplicity, we'll draw to the same 'hit point' or 'target direction'
                    }
                }

                // 2. LOGIC: Single raycast for damage
                float shootDist = 100f; 
                Vector3 hitPoint = logicalOrigin + (direction * shootDist); // default miss pos

                if (Physics.Raycast(logicalOrigin, direction, out RaycastHit hit, shootDist, sightLayerMask)) 
                {
                    hitPoint = hit.point;

                    // Check for player
                    var hitPCC = hit.collider.GetComponentInParent<PlayerCharacterController>();
                    
                    if (hit.transform == player || hit.collider.CompareTag("Player") || hitPCC != null)
                    {
                        if (hitPCC == null && player != null) 
                            hitPCC = player.GetComponent<PlayerCharacterController>();

                        if (hitPCC != null)
                        {
                            float dmg = _enemyStats != null ? _enemyStats.AttackDamage : 0.3f;
                            hitPCC.TakeDamage(dmg);
                        }
                    }
                    
                    // physics push
                    if (hit.rigidbody != null)
                    {
                        
                        hit.rigidbody.AddForce(-hit.normal * 100f); 
                    }
                }
                
                // Update visual end positions to the hit point
                if (laserLines != null)
                {
                    foreach (var lr in laserLines)
                    {
                        if (lr != null) lr.SetPosition(1, hitPoint);
                    }
                }
            }
        }

        private IEnumerator ShotEffect()
        {
            if (gunAudio != null) gunAudio.Play();

            if (laserLines != null)
            {
                foreach (var lr in laserLines)
                {
                    if (lr != null) lr.enabled = true;
                }
            }

            yield return new WaitForSeconds(shotDuration);

            if (laserLines != null)
            {
                foreach (var lr in laserLines)
                {
                    if (lr != null) lr.enabled = false;
                }
            }
        }


        // simple health on the enemy itself
        public void TakeDamage(float amount)
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
            OnEnemyDeath?.Invoke();

            if (agent != null) agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            gameObject.SetActive(false);
        }

        #endregion

        #region Separation / Anti-Stuck

        // small offset away from nearby enemies so they don't bunch up
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

            // If agents are jammed (low velocity and close together) retreat a bit
            void ResolveAgentStuck()
            {
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

        #endregion

        #region Stun / Knockback

        private Coroutine stunRoutine;

            public void ApplyKnockback(Vector3 sourcePosition, float force = -1f, float duration = -1f)
            {
                if (force <= 0f) force = _enemyStats != null ? _enemyStats.KnockbackForce : 600f;
                if (duration <= 0f) duration = _enemyStats != null ? _enemyStats.StunDuration : 1.0f;

            if (stunRoutine != null)
                StopCoroutine(stunRoutine);

            stunRoutine = StartCoroutine(StunRoutine(sourcePosition, force, duration));
        }

        IEnumerator StunRoutine(Vector3 sourcePosition, float force, float duration)
        {
            state = State.Stunned;

            // Stop agent safely
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.ResetPath();   // CRITICAL: clear internal path memory

            // Small lift to avoid ground depenetration pop
            transform.position += Vector3.up * 0.05f;

            // Enable physics
            rb.isKinematic = false;
            rb.useGravity = false;   // we control Y manually
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            RigidbodyConstraints originalConstraints = rb.constraints;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // ---- HORIZONTAL BLAST ONLY ----
            Vector3 dir = transform.position - sourcePosition;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
                dir.Normalize();
            else
                dir = transform.forward;

            rb.AddForce(dir * force * rb.mass, ForceMode.Impulse);

            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;

                // HARD LOCK Y to prevent hovering
                Vector3 pos = transform.position;
                pos.y = 1.9f;   
                transform.position = pos;

                yield return null;
            }

            // ---- STOP PHYSICS ----
            rb.constraints = originalConstraints;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            // ---- CRITICAL SYNC ORDER ----

            Vector3 finalPos = transform.position;

            // Snap to NavMesh
            if (NavMesh.SamplePosition(finalPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                finalPos = hit.position;
            }

            // Move transform FIRST
            transform.position = finalPos;

            // Sync agent internal position BEFORE enabling updatePosition
            agent.nextPosition = finalPos;

            // Warp agent (keeps internal + transform consistent)
            agent.Warp(finalPos);

            // Re-enable agent control
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;

            // Resume behavior
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                state = State.Patrol;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
            else
            {
                state = State.Wander;
            }

            stunRoutine = null;
        }

        #endregion

        #region Utilities

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
        #endregion
    }
}