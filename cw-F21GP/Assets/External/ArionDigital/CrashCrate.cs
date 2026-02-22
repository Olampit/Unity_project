// CrashCrate.cs
// upgraded to apply horizontal-only explosion and call Enemy knockback
namespace ArionDigital
{
    using UnityEngine;
    using F21GP.Enemy;

    public class CrashCrate : MonoBehaviour
    {
        [Header("Whole Crate")]
        public MeshRenderer wholeCrate;
        public BoxCollider boxCollider;

        [Header("Fractured Crate")]
        public GameObject fracturedCrate;

        [Header("Audio")]
        public AudioSource crashAudioClip;

        [Header("Explosion Settings")]
        public float explosionRadius = 6f;
        public float explosionForce = 15f;
        public LayerMask explosionLayers = ~0; // set in inspector (include Enemy layer)
        public GameObject explosionEffect; // optional VFX prefab

        private bool hasExploded = false;

        public void Break()
        {
            if (hasExploded) return;
            hasExploded = true;

            Vector3 explosionPos = transform.position;

            // Visuals
            if (wholeCrate != null) wholeCrate.enabled = false;
            if (boxCollider != null) boxCollider.enabled = false;
            if (fracturedCrate != null) fracturedCrate.SetActive(true);
            if (crashAudioClip != null) crashAudioClip.Play();
            if (explosionEffect != null) Instantiate(explosionEffect, explosionPos, Quaternion.identity);

            // Apply explosion to objects in radius
            Collider[] hits = Physics.OverlapSphere(explosionPos, explosionRadius, explosionLayers);
            foreach (Collider col in hits)
            {
                // Apply explosion force to non-kinematic rigidbodies (but with NO upward modifier)
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // upwardModifier = 0 removes vertical lift
                    rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, 0f, ForceMode.Impulse);
                }

                // If it's an enemy, call its knockback API. We use a horizontal-only direction inside EnemyAI,
                // but we still pass a force which can be scaled by distance below if desired.
                EnemyAI ai = col.GetComponent<EnemyAI>();
                if (ai == null && col.attachedRigidbody != null)
                {
                    // sometimes the EnemyAI is on a parent, check parent
                    ai = col.attachedRigidbody.GetComponentInParent<EnemyAI>();
                }
                if (ai != null)
                {
                    // Optionally scale force by distance (stronger near center)
                    float dist = Vector3.Distance(explosionPos, ai.transform.position);
                    float t = Mathf.Clamp01(1f - (dist / explosionRadius)); // 1 at center -> 0 at edge
                    float scaledForce = Mathf.Lerp(0f, explosionForce, t);

                    // call knockback - EnemyAI will ensure knockback is horizontal and enables gravity
                    ai.ApplyKnockback(explosionPos, scaledForce, -1f);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Example: if bullet or player hits crate, break it.
            Break();
        }

        [ContextMenu("Test Explosion")]
        public void Test()
        {
            Break();
        }
    }
}
