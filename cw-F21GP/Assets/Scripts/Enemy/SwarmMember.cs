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

            // The Leader does not need swarm forces, it leads the pack
            if (isLeader) return;

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
    }
}
