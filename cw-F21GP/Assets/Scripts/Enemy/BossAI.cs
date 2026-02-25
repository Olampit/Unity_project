using System;
using UnityEngine;
using UnityEngine.AI;
using F21GP.Managers;
using F21GP.Player;

namespace F21GP.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class BossAI : MonoBehaviour
    {
        enum State { Wander, Chase, Attack }
        State state = State.Wander;

        [SerializeField] private EnemyStats _enemyStats;

        NavMeshAgent agent;
        Rigidbody rb;
        Transform player;
        Animator animator;

        float currentHealth;
        float lastAttackTime;

        Vector3 lastKnownPlayerPosition;
        float lastSeenTime;

        public event Action OnEnemyDeath;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>();

            rb.isKinematic = true;
            rb.useGravity = false;

            if (animator != null)
                animator.applyRootMotion = false;

            if (_enemyStats != null)
                currentHealth = _enemyStats.MaxHealth;
        }

        void Start()
        {
            if (GameManager.Instance != null)
                player = GameManager.Instance.PlayerTransform;

            agent.stoppingDistance = Mathf.Max(_enemyStats.AttackRange - 0.2f, 0.6f);
            agent.speed = _enemyStats.PatrolSpeed;
        }

        void Update()
        {
            if (_enemyStats == null || player == null)
                return;

            TryDetectPlayer();

            switch (state)
            {
                case State.Wander: UpdateWander(); break;
                case State.Chase: UpdateChase(); break;
                case State.Attack: UpdateAttack(); break;
            }
        }

        void UpdateWander()
        {
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
            {
                Vector3 rnd = transform.position + UnityEngine.Random.insideUnitSphere * _enemyStats.WanderRadius;
                rnd.y = transform.position.y;

                if (NavMesh.SamplePosition(rnd, out NavMeshHit hit, _enemyStats.WanderRadius, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }
        }

        void UpdateChase()
        {
            agent.speed = _enemyStats.ChaseSpeed;
            agent.SetDestination(lastKnownPlayerPosition);

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= _enemyStats.AttackRange)
            {
                agent.isStopped = true;
                state = State.Attack;
            }

            if (Time.time - lastSeenTime > _enemyStats.TimeToForgetPlayer)
            {
                agent.isStopped = false;
                state = State.Wander;
            }
        }

        void UpdateAttack()
        {
            FaceTarget(player.position);

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist > _enemyStats.AttackRange + 0.4f)
            {
                agent.isStopped = false;
                state = State.Chase;
                return;
            }

            if (Time.time - lastAttackTime >= _enemyStats.AttackCooldown)
            {
                lastAttackTime = Time.time;
                DealMeleeDamage();
            }
        }

        void DealMeleeDamage()
        {
            var pcc = player.GetComponent<PlayerCharacterController>();
            if (pcc != null)
                pcc.TakeDamage(_enemyStats.AttackDamage);
        }

        void TryDetectPlayer()
        {
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            Vector3 toPlayer = player.position - eye;

            if (toPlayer.magnitude > _enemyStats.SightRange)
                return;

            if (Vector3.Angle(transform.forward, toPlayer) > _enemyStats.SightAngle * 0.5f)
                return;

            if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, _enemyStats.SightRange))
            {
                if (hit.transform == player || hit.collider.CompareTag("Player"))
                {
                    lastKnownPlayerPosition = player.position;
                    lastSeenTime = Time.time;
                    agent.isStopped = false;
                    state = State.Chase;
                }
            }
        }

        void FaceTarget(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f)
                return;

            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 6f);
        }

        public void TakeDamage(float dmg)
        {
            currentHealth -= dmg;
            lastKnownPlayerPosition = player.position;
            lastSeenTime = Time.time;
            state = State.Chase;

            if (currentHealth <= 0f)
                Die();
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
        public void SetPlayer(Transform target)
        {
            player = target;
        }

        void Die()
        {
            OnEnemyDeath?.Invoke();
            agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
        }
    }
}