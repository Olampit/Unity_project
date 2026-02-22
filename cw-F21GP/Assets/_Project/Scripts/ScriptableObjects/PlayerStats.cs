
using UnityEngine;

namespace F21GP.Player
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "ScriptableObjects/PlayerStats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("Movement")]
        public float WalkSpeed = 6.0f;
        public float RunSpeed = 12.0f;
        public float JumpPower = 4.0f;
        public float Gravity = 10.0f;

        [Header("Look")]
        public float LookXLimit = 45.0f;
        public float LookSpeed = 2.0f;

        [Header("Health")]
        public float MaxHealth = 100f;
    }
}
