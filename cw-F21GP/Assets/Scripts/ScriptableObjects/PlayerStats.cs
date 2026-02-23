// reference: https://docs.unity3d.com/Manual/class-ScriptableObject.html
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
        public float LookXLimit = 45.0f; // how far the player can look up and down
        public float LookSpeed = 2.0f; // how fast the player can look

        [Header("Health")]
        public float MaxHealth = 100f;

        [Header("Abilities")]
        public bool CanDropCrashCrate = true;
    }
}
