using UnityEngine;
using UnityEngine.AI;

namespace F21GP.Enemy
{
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class SwarmMember : MonoBehaviour
    {
        private EnemyAI enemyAI;
        public NavMeshAgent Agent { get; private set; }
        
        [SerializeField] private EnemyStats _enemyStats;

        private void Awake()
        {
            enemyAI = GetComponent<EnemyAI>();
            Agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            if (DroneSwarmManager.Instance != null)
            {
                DroneSwarmManager.Instance.RegisterMember(this);
            }
            enemyAI.OnEnemyDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (DroneSwarmManager.Instance != null)
            {
                DroneSwarmManager.Instance.UnregisterMember(this);
            }
            if (enemyAI != null)
            {
                enemyAI.OnEnemyDeath -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            if (DroneSwarmManager.Instance != null)
            {
                DroneSwarmManager.Instance.UnregisterMember(this);
            }
            this.enabled = false;
        }

        private void LateUpdate()
        {
            if (DroneSwarmManager.Instance == null || DroneSwarmManager.Instance.SwarmCount <= 1) return;
            if (!Agent.enabled) return;

            bool isLeader = DroneSwarmManager.Instance.Leader == this;
            enemyAI.OverridePathfinding = !isLeader;

            // The Leader does not need swarm forces, it leads the pack
            if (isLeader) return;

            // CRITICAL: Followers must always be moving. EnemyAI's Idle state
            // sets isStopped=true which freezes the agent even if we set a destination.
            Agent.isStopped = false;

            // Match the leader's speed so we keep up
            Agent.speed = DroneSwarmManager.Instance.Leader.Agent.speed;
            
            // Get base pathfinding destination. Followers track the Leader.
            Vector3 targetDestination = DroneSwarmManager.Instance.Leader.transform.position;
            Vector3 currentPos = transform.position;

            float cohesionStr = _enemyStats != null ? _enemyStats.CohesionStrength : 1.0f;
            float alignStr = _enemyStats != null ? _enemyStats.AlignmentStrength : 1.0f;

            // --- 1. Cohesion ---
            // Move towards the center of mass
            Vector3 swarmCenter = DroneSwarmManager.Instance.SwarmCenter;
            Vector3 cohesionVector = (swarmCenter - currentPos);
            cohesionVector.y = 0; // keep it horizontal
            
            // Distance check, if already very close to center, reduce cohesion pull
            if (cohesionVector.sqrMagnitude > 0.1f)
            {
               cohesionVector = cohesionVector.normalized * cohesionStr;
            }
            else
            {
                cohesionVector = Vector3.zero;
            }

            // --- 2. Alignment ---
            // Try to move in the same direction as the swarm
            Vector3 alignmentVector = DroneSwarmManager.Instance.SwarmHeading * alignStr;
            alignmentVector.y = 0;

            // Combine forces with the current intended path direction
            Vector3 desiredDirection = ((targetDestination - currentPos).normalized * 2f) + cohesionVector + alignmentVector;
            desiredDirection.y = 0;

            // Calculate new offset destination by projecting it forward to prevent the NavMeshAgent from decelerating
            Vector3 newDest = currentPos + desiredDirection.normalized * 8f;
            
            // Apply only if significantly different to save NavMesh rebuilds
            if (Vector3.Distance(Agent.destination, newDest) > 1.5f)
            {
                if (NavMesh.SamplePosition(newDest, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
                {
                    Agent.SetDestination(hit.position);
                }
            }
        }
    }
}
