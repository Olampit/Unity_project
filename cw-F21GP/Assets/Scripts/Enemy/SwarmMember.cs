using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using F21GP.Managers;

namespace F21GP.Enemy
{
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class SwarmMember : MonoBehaviour
    {
        private EnemyAI enemyAI;
        public NavMeshAgent Agent { get; private set; }
        
        [SerializeField] private EnemyStats _enemyStats;

        /// <summary>
        /// The specific swarm this drone belongs to. Assigned at spawn time by BossArenaManager.
        /// </summary>
        [HideInInspector] public DroneSwarmManager Swarm;

        private void Awake()
        {
            enemyAI = GetComponent<EnemyAI>();
            Agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            if (Swarm != null)
            {
                Swarm.RegisterMember(this);
            }
            enemyAI.OnEnemyDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (Swarm != null)
            {
                Swarm.UnregisterMember(this);
            }
            if (enemyAI != null)
            {
                enemyAI.OnEnemyDeath -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            if (Swarm != null)
            {
                Swarm.UnregisterMember(this);
            }
            this.enabled = false;
        }

        /// <summary>
        /// Called by BossArenaManager after instantiation to assign the swarm and register.
        /// </summary>
        public void AssignSwarm(DroneSwarmManager manager)
        {
            Swarm = manager;
            Swarm.RegisterMember(this);
        }

        private void LateUpdate()
        {
            if (Swarm == null || Swarm.SwarmCount <= 1) return;
            if (!Agent.enabled) return;

            bool isLeader = Swarm.Leader == this;
            enemyAI.OverridePathfinding = !isLeader;

            // The Leader applies separation from other leaders and the player
            if (isLeader)
            {
                ApplyLeaderSeparation();
                return;
            }

            // CRITICAL: Followers must always be moving. EnemyAI's Idle state
            // sets isStopped=true which freezes the agent even if we set a destination.
            Agent.isStopped = false;

            // Match the leader's speed so we keep up
            Agent.speed = Swarm.Leader.Agent.speed;
            
            // Get base pathfinding destination. Followers track their own Leader.
            Vector3 targetDestination = Swarm.Leader.transform.position;
            Vector3 currentPos = transform.position;

            float cohesionStr = _enemyStats != null ? _enemyStats.CohesionStrength : 1.0f;
            float alignStr = _enemyStats != null ? _enemyStats.AlignmentStrength : 1.0f;

            // --- 1. Cohesion ---
            Vector3 swarmCenter = Swarm.SwarmCenter;
            Vector3 cohesionVector = (swarmCenter - currentPos);
            cohesionVector.y = 0;
            
            if (cohesionVector.sqrMagnitude > 0.1f)
            {
               cohesionVector = cohesionVector.normalized * cohesionStr;
            }
            else
            {
                cohesionVector = Vector3.zero;
            }

            // --- 2. Alignment ---
            Vector3 alignmentVector = Swarm.SwarmHeading * alignStr;
            alignmentVector.y = 0;

            // Combine forces
            Vector3 desiredDirection = ((targetDestination - currentPos).normalized * 2f) + cohesionVector + alignmentVector;
            desiredDirection.y = 0;

            // Project destination forward to prevent NavMeshAgent from decelerating
            Vector3 newDest = currentPos + desiredDirection.normalized * 8f;
            
            if (Vector3.Distance(Agent.destination, newDest) > 1.5f)
            {
                if (NavMesh.SamplePosition(newDest, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                {
                    Agent.SetDestination(hit.position);
                }
            }
        }

        /// <summary>
        /// Leaders push away from other swarm leaders and maintain distance from the player.
        /// This prevents multiple swarms from merging into one blob.
        /// </summary>
        private void ApplyLeaderSeparation()
        {
            if (!Agent.enabled || Agent.pathPending) return;

            float leaderSepRadius = _enemyStats != null ? _enemyStats.LeaderSeparationRadius : 10f;
            float leaderSepStrength = _enemyStats != null ? _enemyStats.LeaderSeparationStrength : 3f;
            float playerSepRadius = _enemyStats != null ? _enemyStats.LeaderPlayerSeparationRadius : 6f;
            float playerSepStrength = _enemyStats != null ? _enemyStats.LeaderPlayerSeparationStrength : 2f;

            Vector3 separationForce = Vector3.zero;
            Vector3 currentPos = transform.position;

            // --- 1. Separation from other swarm leaders ---
            foreach (var manager in DroneSwarmManager.AllManagers)
            {
                if (manager == Swarm || manager.Leader == null) continue;

                Vector3 toOtherLeader = currentPos - manager.Leader.transform.position;
                toOtherLeader.y = 0;
                float dist = toOtherLeader.magnitude;

                if (dist < leaderSepRadius && dist > 0.01f)
                {
                    // Stronger push the closer they are
                    separationForce += toOtherLeader.normalized * (leaderSepStrength / dist);
                }
            }

            // --- 2. Separation from the player ---
            Transform playerTransform = GameManager.Instance != null ? GameManager.Instance.PlayerTransform : null;
            if (playerTransform != null)
            {
                Vector3 toPlayer = currentPos - playerTransform.position;
                toPlayer.y = 0;
                float playerDist = toPlayer.magnitude;

                if (playerDist < playerSepRadius && playerDist > 0.01f)
                {
                    separationForce += toPlayer.normalized * (playerSepStrength / playerDist);
                }
            }

            // Apply the separation offset to the current destination
            if (separationForce.sqrMagnitude > 0.1f)
            {
                Vector3 newDest = Agent.destination + separationForce;
                if (NavMesh.SamplePosition(newDest, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    Agent.SetDestination(hit.position);
                }
            }
        }
    }
}
