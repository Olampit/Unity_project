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

            if (isLeader)
            {
                ApplyLeaderSeparation();
                return;
            }

            Agent.isStopped = false;

            Agent.speed = Swarm.Leader.Agent.speed;
            
            Vector3 targetDestination = Swarm.Leader.transform.position;
            Vector3 currentPos = transform.position;

            float cohesionStr = _enemyStats != null ? _enemyStats.CohesionStrength : 1.0f;
            float alignStr = _enemyStats != null ? _enemyStats.AlignmentStrength : 1.0f;

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

            Vector3 alignmentVector = Swarm.SwarmHeading * alignStr;
            alignmentVector.y = 0;

            Vector3 desiredDirection = ((targetDestination - currentPos).normalized * 2f) + cohesionVector + alignmentVector;
            desiredDirection.y = 0;

            Vector3 newDest = currentPos + desiredDirection.normalized * 8f;
            
            if (Vector3.Distance(Agent.destination, newDest) > 1.5f)
            {
                if (NavMesh.SamplePosition(newDest, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                {
                    Agent.SetDestination(hit.position);
                }
            }
        }

        private void ApplyLeaderSeparation()
        {
            if (!Agent.enabled || Agent.pathPending) return;

            float leaderSepRadius = _enemyStats != null ? _enemyStats.LeaderSeparationRadius : 10f;
            float leaderSepStrength = _enemyStats != null ? _enemyStats.LeaderSeparationStrength : 3f;
            float playerSepRadius = _enemyStats != null ? _enemyStats.LeaderPlayerSeparationRadius : 6f;
            float playerSepStrength = _enemyStats != null ? _enemyStats.LeaderPlayerSeparationStrength : 2f;

            Vector3 separationForce = Vector3.zero;
            Vector3 currentPos = transform.position;

            foreach (var manager in DroneSwarmManager.AllManagers)
            {
                if (manager == Swarm || manager.Leader == null) continue;

                Vector3 toOtherLeader = currentPos - manager.Leader.transform.position;
                toOtherLeader.y = 0;
                float dist = toOtherLeader.magnitude;

                if (dist < leaderSepRadius && dist > 0.01f)
                {
                    separationForce += toOtherLeader.normalized * (leaderSepStrength / dist);
                }
            }

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
