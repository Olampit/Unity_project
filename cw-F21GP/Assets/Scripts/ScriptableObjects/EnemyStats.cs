// reference: https://docs.unity3d.com/Manual/class-ScriptableObject.html
using UnityEngine;

namespace F21GP.Enemy
{
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "ScriptableObjects/EnemyStats")]
    public class EnemyStats : ScriptableObject
    {
        [Header("Health")]
        public int MaxHealth = 3;

        [Header("Movement Speeds")]
        public float PatrolSpeed = 2.2f; // speed of the enemy when patrolling
        public float ChaseSpeed = 3.8f; // speed of the enemy when chasing
        public float WanderRadius = 8f; // radius of the area where the enemy can wander

        [Header("Perception")]
        public float SightRange = 12f; // how far the enemy can see
        public float SightAngle = 120f; // how wide the enemy can see
        public float AttackRange = 8f; // how far the enemy can attack
        public float TimeToForgetPlayer = 4f; // how long the enemy remembers the player

        [Header("Attack")]
        public float AttackCooldown = 1f; // time between attacks
        public float AttackDamage = 3f; // damage of the attack
        
        [Header("Idle")]
        public float IdleTime = 2f; // time the enemy stays idle

        [Header("Crowd & Stun")]
        public float SeparationRadius = 5.0f; // radius of the area where the enemies can separate
        public float SeparationStrength = 1.2f; // strength of the separation
        public float CohesionStrength = 1.0f; // strength of moving towards the center of the swarm
        public float AlignmentStrength = 1.0f; // strength of aligning with the swarm's direction
        public float SwarmMemberAvoidanceRadius = 2.0f; // radius for swarm separation
        public float MinSeparationDistance = 0.8f; // minimum distance between enemies
        public float RetreatDistance = 1.2f; // distance the enemy retreats
        public float StuckVelocityThreshold = 0.05f; // velocity threshold for stuck detection
        
        public float StunDuration = 1.0f; // duration of the stun
        public float KnockbackForce = 600f; // force of the knockback
    }
}
