namespace ArionDigital
{
    using UnityEngine;

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
        public float upwardModifier = 0f;
        public LayerMask explosionLayers;

        private bool hasExploded = false;

        public void Break()
        {
            if (hasExploded) return;
            hasExploded = true;

            Vector3 explosionPos = transform.position;

            // --- VISUAL BREAK ---
            wholeCrate.enabled = false;
            boxCollider.enabled = false;
            fracturedCrate.SetActive(true);

            Rigidbody[] debris = fracturedCrate.GetComponentsInChildren<Rigidbody>();
            foreach (var piece in debris)
            {
                piece.AddExplosionForce(explosionForce * 0.5f, explosionPos, explosionRadius);
            }


            if (crashAudioClip != null)
                crashAudioClip.Play();

            // --- EXPLOSION LOGIC ---
            Collider[] hits = Physics.OverlapSphere(
                explosionPos,
                explosionRadius,
                explosionLayers
            );

            foreach (Collider col in hits)
            {
                // Apply physics force
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(
                        explosionForce,
                        explosionPos,
                        explosionRadius,
                        upwardModifier,
                        ForceMode.Impulse
                    );
                }

                // Apply drone knockback
                EnemyAI ai = col.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    ai.ApplyKnockback(explosionPos, explosionForce, 1.2f);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Break();
        }

        [ContextMenu("Test Explosion")]
        public void Test()
        {
            Break();
        }
    }
}
