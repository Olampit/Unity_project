// EnemyAI.cs
// Overwrite your previous EnemyAI.cs with this file.
// Main change: robust CreateSquad(...) that assigns runtime state & patrol points to members.

using System.Collections;
using System.Collections.Generic;
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
        // FSM states
        private enum State { Idle, Patrol, Wander, SquadPatrol, Regroup, Chase, Attack, Stunned }
        private State state = State.Idle;

        [Header("Data")]
        [SerializeField] private EnemyStats _enemyStats;

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

        // Squad-related fields
        [Header("Squad")]
        [Tooltip("Unique ID of the squad; 0 means no squad")]
        public int squadId = 0;
        [Tooltip("Is this enemy the leader of its squad?")]
        public bool isLeader = false;

        [Header("Squad Behavior")]
        [Tooltip("Maximum distance a follower can be from the leader before trying to regroup")]
        public float maxFollowDistance = 5f;
        [Tooltip("Spacing between squad members in formation")]
        public float formationSpacing = 2f;
        [Tooltip("Minimum time before formation change (leader only)")]
        public float formationChangeMin = 5f;
        [Tooltip("Maximum time before formation change (leader only)")]
        public float formationChangeMax = 15f;
        private float formationChangeTimer;

        // Static data for managing squads
        private static Dictionary<int, List<EnemyAI>> squads;
        private static Dictionary<int, EnemyAI> squadLeaders;
        private static Dictionary<int, FormationType> squadFormations;
        private static int nextSquadId = 1;

        // Enum for formation types
        public enum FormationType { Line, Triangle }

        // Event for death notification
        public event Action OnEnemyDeath;


        [Header("Squad Patrol (Level 2)")]
        public Transform[] squadPatrolPoints;
        private int squadPatrolIndex = 0;

        [Tooltip("Average squad distance before forcing regroup")]
        public float regroupThreshold = 8f;

        /// <summary>
        /// Registers a new squad with specified members, leader index, and formation.
        /// Returns the squad ID assigned (0 on failure).
        /// This method now also:
        ///  - assigns squadId/isLeader on members
        ///  - sets their runtime state to SquadPatrol
        ///  - tries to auto-assign squadPatrolPoints from a GameObject named "SquadPatrolPoints" if present
        ///  - sets the leader's initial destination to the first patrol point (if available)
        /// </summary>
        public static int CreateSquad(List<EnemyAI> members, int leaderIndex, FormationType formation)
        {
            Debug.Log("squad created");
            if (members == null || members.Count == 0)
            {
                Debug.LogWarning("[EnemyAI.CreateSquad] No members supplied - squad not created.");
                return 0;
            }

            if (leaderIndex < 0 || leaderIndex >= members.Count)
            {
                leaderIndex = 0;
            }

            if (squads == null)
            {
                squads = new Dictionary<int, List<EnemyAI>>();
                squadLeaders = new Dictionary<int, EnemyAI>();
                squadFormations = new Dictionary<int, FormationType>();
                nextSquadId = 1;
            }

            int id = nextSquadId++;
            squads[id] = new List<EnemyAI>(members);
            squadFormations[id] = formation;

            // Assign leader and flags
            EnemyAI leader = members[leaderIndex];
            squadLeaders[id] = leader;

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null)
                    continue;

                m.squadId = id;
                m.isLeader = (i == leaderIndex);

                // Set runtime state to squad patrol and un-pause agent so they begin following formation
                m.state = State.SquadPatrol;
                if (m.agent != null)
                {
                    m.agent.isStopped = false;
                    // ensure agent is allowed to move
                    m.agent.updatePosition = true;
                    m.agent.updateRotation = true;
                }
            }

            // Try to auto-assign squad patrol points from a parent object named "SquadPatrolPoints"
            var container = GameObject.Find("SquadPatrolPoints");
            Transform[] assignedPoints = null;
            if (container != null)
            {
                var children = container.GetComponentsInChildren<Transform>(includeInactive: false);
                var points = new List<Transform>();
                foreach (var t in children)
                {
                    if (t == container.transform) continue;
                    points.Add(t);
                }
                if (points.Count > 0)
                    assignedPoints = points.ToArray();
            }

            // If container not found or no points, leave member.squadPatrolPoints as-is (can be set in inspector)
            if (assignedPoints != null)
            {
                foreach (var m in members)
                {
                    if (m == null) continue;
                    m.squadPatrolPoints = assignedPoints;
                }
            }

            // Set leader initial destination (first patrol point if available)
            if (leader != null && leader.agent != null)
            {
                if (leader.squadPatrolPoints != null && leader.squadPatrolPoints.Length > 0)
                {
                    leader.squadPatrolIndex = 0;
                    leader.agent.SetDestination(leader.squadPatrolPoints[leader.squadPatrolIndex].position);
                    Debug.Log($"[EnemyAI.CreateSquad] Squad {id} leader set to first patrol point.");
                }
                else
                {
                    // fallback: leader will roam/wait until detection; still valid
                    Debug.Log($"[EnemyAI.CreateSquad] Squad {id} created but no SquadPatrolPoints assigned.");
                }
            }

            Debug.Log($"[EnemyAI.CreateSquad] Created squad {id} with {members.Count} members. Formation={formation}");
            return id;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;   // navmesh controls vertical normally

            if (_enemyStats != null) currentHealth = _enemyStats.MaxHealth;

            // Initialize static squad data if needed
            if (squads == null)
            {
                squads = new Dictionary<int, List<EnemyAI>>();
                squadLeaders = new Dictionary<int, EnemyAI>();
                squadFormations = new Dictionary<int, FormationType>();
                nextSquadId = 1;
            }
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

            // if squadId already assigned before Start (rare), start in SquadPatrol
            if (squadId != 0 && squadPatrolPoints != null && squadPatrolPoints.Length > 0)
            {
                state = State.SquadPatrol;
            }
            else
            {
                if (patrolPoints == null || patrolPoints.Length == 0)
                    state = State.Wander;
                else
                    state = State.Idle;
            }

            agent.stoppingDistance = 0.8f;
            // Initialize formation change timer for leader
            formationChangeTimer = UnityEngine.Random.Range(formationChangeMin, formationChangeMax);
        }

        void Update()
        {
            if (yieldTimer > 0f && state != State.Stunned)
            {
                yieldTimer -= Time.deltaTime;
                // Still allow perception and animation while yielding, skip movement updates
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
                    case State.SquadPatrol: UpdateSquadPatrol(); break;
                    case State.Regroup: UpdateRegroup(); break;
                }
                ResolveAgentStuck();
            }

            if (agent.remainingDistance == Mathf.Infinity)
            {
                agent.ResetPath();
            }
            if (_enemyStats != null && agent.stoppingDistance != _enemyStats.AttackRange - 1.0f)
                agent.stoppingDistance = Mathf.Max(_enemyStats.AttackRange - 1.0f, 0.5f);

            // Perception only when not stunned
            if (state != State.Stunned)
                TryDetectPlayer();

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
            // Move to next patrol point when reached
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
            // After reaching point, go idle then advance
            if (!agent.pathPending && agent.remainingDistance <= 0.8f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                state = State.Idle;
                if (_enemyStats != null) idleTimer = _enemyStats.IdleTime;
            }
            ApplySeparation();
        }

        void UpdateWander()
        {
            if (_enemyStats != null) agent.speed = _enemyStats.PatrolSpeed;
            if (agent.pathPending) return;
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

            // Squad behavior: followers maintain formation
            if (squadId != 0 && !isLeader)
            {
                // Follow the leader of this squad
                if (squadLeaders.TryGetValue(squadId, out EnemyAI leader) && leader != null)
                {
                    float distToLeader = Vector3.Distance(transform.position, leader.transform.position);
                    if (distToLeader > maxFollowDistance)
                    {
                        // Regroup: move directly towards leader
                        agent.SetDestination(leader.transform.position);
                    }
                    else
                    {
                        // Compute formation offset relative to leader
                        FormationType formation = squadFormations.ContainsKey(squadId) ? squadFormations[squadId] : FormationType.Line;
                        Vector3 offset = Vector3.zero;
                        List<EnemyAI> members = squads.ContainsKey(squadId) ? squads[squadId] : new List<EnemyAI>{ leader };
                        int idx = members.IndexOf(this) - 1; // index among followers
                        float s = formationSpacing;
                        if (formation == FormationType.Line)
                        {
                            // Line: each behind leader in a line
                            offset = new Vector3(0, 0, -(s * (idx + 1)));
                        }
                        else // Triangle
                        {
                            // Triangle pattern for first few followers
                            switch (idx)
                            {
                                case 0: offset = new Vector3(-s, 0, -s); break;
                                case 1: offset = new Vector3(s, 0, -s); break;
                                case 2: offset = new Vector3(0, 0, -2 * s); break;
                                case 3: offset = new Vector3(-s, 0, -2 * s); break;
                                case 4: offset = new Vector3(s, 0, -2 * s); break;
                                default: offset = new Vector3(0, 0, -(s * (idx + 1))); break;
                            }
                        }
                        Vector3 worldOffset = leader.transform.TransformDirection(offset);
                        Vector3 targetPos = leader.transform.position + worldOffset;
                        agent.SetDestination(targetPos);
                    }
                }
                ApplySeparation();
                return;
            }

            // Leader occasionally changes formation
            if (squadId != 0 && isLeader)
            {
                formationChangeTimer -= Time.deltaTime;
                if (formationChangeTimer <= 0f)
                {
                    FormationType newFormation = (UnityEngine.Random.value > 0.5f) ? FormationType.Line : FormationType.Triangle;
                    if (newFormation == FormationType.Triangle && squads.ContainsKey(squadId) && squads[squadId].Count < 3)
                        newFormation = FormationType.Line;
                    squadFormations[squadId] = newFormation;
                    formationChangeTimer = UnityEngine.Random.Range(formationChangeMin, formationChangeMax);
                }
            }

            // Default chase: head to player's last known position
            agent.SetDestination(lastKnownPlayerPosition);
            // If player in sight and in range, attack
            if (player != null && _enemyStats != null &&
                Vector3.Distance(transform.position, player.position) <= _enemyStats.AttackRange &&
                IsPlayerVisible())
            {
                state = State.Attack;
                agent.isStopped = true;
                return;
            }
            // If lost sight, go back to wandering (or squad patrol)
            if (_enemyStats != null && Time.time - lastSeenTime > _enemyStats.TimeToForgetPlayer)
            {
                if (squadId != 0)
                {
                    state = State.SquadPatrol;
                    agent.isStopped = false;
                }
                else
                {
                    state = State.Wander;
                    agent.isStopped = false;
                }
            }

            ApplySeparation();
        }

        void UpdateAttack()
        {
            // Rotate towards player
            if (player != null) FaceTarget(player.position);
            // Attack cooldown and damage
            if (_enemyStats != null && Time.time - lastAttackTime >= _enemyStats.AttackCooldown)
            {
                lastAttackTime = Time.time;
                PerformAttack();
            }
            // If player moves out of range, resume chase
            if (player == null || (_enemyStats != null &&
                Vector3.Distance(transform.position, player.position) > _enemyStats.AttackRange + 0.5f))
            {
                agent.isStopped = false;
                state = State.Chase;
            }
        }

        void FaceTarget(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            direction.y = 0;
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        void UpdateSquadPatrol()
        {
            if (squadId == 0)
            {
                state = State.Wander;
                return;
            }

            if (!squadLeaders.TryGetValue(squadId, out EnemyAI leader))
                return;

            if (isLeader)
            {
                if (squadPatrolPoints == null || squadPatrolPoints.Length == 0)
                    return;

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    squadPatrolIndex = (squadPatrolIndex + 1) % squadPatrolPoints.Length;
                    agent.SetDestination(squadPatrolPoints[squadPatrolIndex].position);
                }

                // Check squad cohesion
                if (IsSquadScattered())
                {
                    state = State.Regroup;
                }
            }
            else
            {
                MaintainFormation(leader);
            }

            ApplySeparation();
        }


        void UpdateRegroup()
        {
            if (squadId == 0)
            {
                state = State.Wander;
                return;
            }

            if (!squadLeaders.TryGetValue(squadId, out EnemyAI leader))
                return;

            float dist = Vector3.Distance(transform.position, leader.transform.position);

            if (dist > 2f)
            {
                agent.SetDestination(leader.transform.position);
            }
            else
            {
                state = State.SquadPatrol;
            }

            ApplySeparation();
        }


        void MaintainFormation(EnemyAI leader)
        {
            float distToLeader = Vector3.Distance(transform.position, leader.transform.position);

            if (distToLeader > maxFollowDistance)
            {
                state = State.Regroup;
                return;
            }

            FormationType formation = squadFormations.ContainsKey(squadId) ? squadFormations[squadId] : FormationType.Line;
            List<EnemyAI> members = squads.ContainsKey(squadId) ? squads[squadId] : new List<EnemyAI>{ leader };

            int idx = members.IndexOf(this) - 1;
            if (idx < 0) return;

            float s = formationSpacing;
            Vector3 offset = Vector3.zero;

            if (formation == FormationType.Line)
            {
                offset = new Vector3(0, 0, -(s * (idx + 1)));
            }
            else
            {
                switch (idx)
                {
                    case 0: offset = new Vector3(-s, 0, -s); break;
                    case 1: offset = new Vector3(s, 0, -s); break;
                    case 2: offset = new Vector3(0, 0, -2 * s); break;
                    case 3: offset = new Vector3(-s, 0, -2 * s); break;
                    case 4: offset = new Vector3(s, 0, -2 * s); break;
                    default: offset = new Vector3(0, 0, -(s * (idx + 1))); break;
                }
            }

            Vector3 worldOffset = leader.transform.TransformDirection(offset);
            Vector3 targetPos = leader.transform.position + worldOffset;

            agent.SetDestination(targetPos);
        }

        bool IsSquadScattered()
        {
            if (!squads.ContainsKey(squadId)) return false;

            List<EnemyAI> members = squads[squadId];
            if (members.Count == 0) return false;

            Vector3 center = Vector3.zero;

            foreach (var m in members)
                center += m.transform.position;

            center /= members.Count;

            float total = 0f;
            foreach (var m in members)
                total += Vector3.Distance(center, m.transform.position);

            float average = total / members.Count;

            return average > regroupThreshold;
        }

        #endregion

        #region Perception

        void TryDetectPlayer()
        {
            if (player == null || state == State.Stunned) return;

            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 toPlayer = player.position - eye;
            float sRange = _enemyStats != null ? _enemyStats.SightRange : 12f;
            float sAngle = _enemyStats != null ? _enemyStats.SightAngle : 120f;

            if (toPlayer.magnitude > sRange) return;
            if (Vector3.Angle(transform.forward, toPlayer) > sAngle * 0.5f) return;

            if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, sRange, sightLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == player || hit.collider.CompareTag("Player"))
                {
                    // Player seen: update and chase
                    lastKnownPlayerPosition = player.position;
                    lastSeenTime = Time.time;
                    if (state != State.Chase && state != State.Attack)
                        state = State.Chase;
                    agent.isStopped = false;

                    // Alert squad members
                    if (squadId != 0 && squads.ContainsKey(squadId))
                    {
                        foreach (EnemyAI member in squads[squadId])
                        {
                            if (member == this) continue;
                            member.lastKnownPlayerPosition = lastKnownPlayerPosition;
                            member.lastSeenTime = Time.time;
                            if (member.state != State.Chase && member.state != State.Attack && member.state != State.Stunned)
                            {
                                member.state = State.Chase;
                                member.agent.isStopped = false;
                            }
                        }
                    }
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

        // Called by player's weapon for noise
        public void OnNoiseHeard(Vector3 noisePosition)
        {
            lastKnownPlayerPosition = noisePosition;
            lastSeenTime = Time.time;
            if (state != State.Stunned) state = State.Chase;

            // Alert squad members to the noise
            if (squadId != 0 && squads.ContainsKey(squadId))
            {
                foreach (EnemyAI member in squads[squadId])
                {
                    if (member == this) continue;
                    member.lastKnownPlayerPosition = noisePosition;
                    member.lastSeenTime = Time.time;
                    if (member.state != State.Stunned)
                    {
                        member.state = State.Chase;
                    }
                }
            }
        }
        #endregion

        #region Attack / Damage

        void PerformAttack()
        {
            if (player != null)
            {
                StartCoroutine(ShotEffect());

                // Aim logic origin
                Vector3 logicalOrigin = (gunTips != null && gunTips.Length > 0 && gunTips[0] != null)
                                        ? gunTips[0].position
                                        : transform.position + Vector3.up * 1.5f;
                // Aim offset
                Vector3 targetPoint = player.position + Vector3.up * -0.7f + transform.right * -0.3f;
                Vector3 direction = (targetPoint - logicalOrigin).normalized;

                // Visual lines
                if (laserLines != null && gunTips != null)
                {
                    for (int i = 0; i < laserLines.Length; i++)
                    {
                        if (i >= gunTips.Length) break;
                        if (laserLines[i] == null || gunTips[i] == null) continue;
                        laserLines[i].SetPosition(0, gunTips[i].position);
                    }
                }

                // Raycast for damage
                float shootDist = 100f;
                Vector3 hitPoint = logicalOrigin + (direction * shootDist);
                if (Physics.Raycast(logicalOrigin, direction, out RaycastHit hit, shootDist, sightLayerMask))
                {
                    hitPoint = hit.point;
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
                    // Physics push effect
                    if (hit.rigidbody != null)
                    {
                        hit.rigidbody.AddForce(-hit.normal * 100f);
                    }
                }

                // Update laser end points
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
            if (player != null)
            {
                lastKnownPlayerPosition = player.position;
                lastSeenTime = Time.time;
                state = State.Chase;
            }
        }

        void Die()
        {
            // Remove from squad if applicable
            if (squadId != 0 && squads.ContainsKey(squadId))
            {
                squads[squadId].Remove(this);
                // If this was leader, assign new leader or dissolve squad
                if (isLeader)
                {
                    if (squads[squadId].Count > 0)
                    {
                        EnemyAI newLeader = squads[squadId][0];
                        newLeader.isLeader = true;
                        squadLeaders[squadId] = newLeader;
                    }
                    else
                    {
                        squads.Remove(squadId);
                        squadLeaders.Remove(squadId);
                        squadFormations.Remove(squadId);
                    }
                }
                // If only one left after removal, that one becomes leader
                else if (squads.ContainsKey(squadId) && squads[squadId].Count == 1)
                {
                    EnemyAI remaining = squads[squadId][0];
                    remaining.isLeader = true;
                    squadLeaders[squadId] = remaining;
                }
            }

            OnEnemyDeath?.Invoke();
            // Disable enemy
            if (agent != null) agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
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

                // HARD LOCK Y to prevent hovering (tweak as needed)
                Vector3 pos = transform.position;
                pos.y = 1.9f;
                transform.position = pos;

                yield return null;
            }

            rb.constraints = originalConstraints;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;


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

        #region Separation / Anti-Stuck

        // Compute a small offset to avoid crowding
        Vector3 ComputeSeparationOffset()
        {
            Vector3 offset = Vector3.zero;
            Collider[] others = Physics.OverlapSphere(transform.position, 1f, enemyLayerMask);
            foreach (Collider col in others)
            {
                if (col == null || col.transform == transform) continue;
                Vector3 toOther = transform.position - col.transform.position;
                if (toOther.magnitude > 0)
                    offset += toOther.normalized / toOther.magnitude;
            }
            return offset;
        }

        void ApplySeparation()
        {
            Vector3 sep = ComputeSeparationOffset();
            // move using NavMeshAgent where possible
            if (agent != null && agent.isOnNavMesh)
                agent.Move(sep * 0.5f * Time.deltaTime);
            else
                transform.position += sep * 0.5f * Time.deltaTime;
        }

        void ResolveAgentStuck()
        {
            if (yieldTimer <= 0 && agent.hasPath && agent.remainingDistance < 0.1f)
            {
                yieldTimer = 0.2f;
                float angle = UnityEngine.Random.Range(30f, 150f);
                transform.Rotate(0, angle, 0);
            }
        }

        #endregion
    }
}