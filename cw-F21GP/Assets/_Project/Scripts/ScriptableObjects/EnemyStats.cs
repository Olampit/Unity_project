
using UnityEngine;

namespace F21GP.Enemy
{
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "ScriptableObjects/EnemyStats")]
    public class EnemyStats : ScriptableObject
    {
        [Header("Health")]
        public int MaxHealth = 3;

        [Header("Movement Speeds")]
        public float PatrolSpeed = 2.2f;
        public float ChaseSpeed = 3.8f;
        public float WanderRadius = 8f;

        [Header("Perception")]
        public float SightRange = 12f;
        public float SightAngle = 120f;
        public float AttackRange = 8f;
        public float TimeToForgetPlayer = 4f;

        [Header("Attack")]
        public float AttackCooldown = 3f;
        public float AttackDamage = 0.3f;
        
        [Header("Idle")]
        public float IdleTime = 2f;

        [Header("Crowd & Stun")]
        public float SeparationRadius = 2.0f;
        public float SeparationStrength = 1.2f;
        public float MinSeparationDistance = 0.8f;
        public float RetreatDistance = 1.2f;
        public float StuckVelocityThreshold = 0.05f;
        
        public float StunDuration = 1.0f;
        public float KnockbackForce = 600f;
    }
}
