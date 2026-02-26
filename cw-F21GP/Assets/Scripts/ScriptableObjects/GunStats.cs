// reference: https://docs.unity3d.com/Manual/class-ScriptableObject.html
using UnityEngine;

namespace F21GP.Player
{
    [CreateAssetMenu(fileName = "GunStats", menuName = "ScriptableObjects/GunStats")]
    public class GunStats : ScriptableObject
    {
        [Header("Gun Stats")]
        public int GunDamage = 1; // damage from the gun
        public float FireRate = 0.25f; // fire rate of the gun
        public float WeaponRange = 50f; // range of the gun
        public float HitForce = 100f; // force applied when the gun hits something
    }
}
