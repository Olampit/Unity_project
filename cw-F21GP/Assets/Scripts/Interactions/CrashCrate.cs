namespace F21GP.Interactions
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

        [Header("Damage")]
        public int damage = 1;

        [Header("Explosion Settings")]
        public float explosionRadius = 6f;
        public float explosionForce = 15f;
        public LayerMask explosionLayers = ~0; 
        public GameObject explosionEffect; 
        public float destroyDelay = 300f;

        private bool hasExploded = false;

        public void Break()
        {
            if (hasExploded) return;
            hasExploded = true;

            Vector3 explosionPos = transform.position;

            if (wholeCrate != null) wholeCrate.enabled = false;
            if (boxCollider != null) boxCollider.enabled = false;
            if (fracturedCrate != null) fracturedCrate.SetActive(true);
            if (crashAudioClip != null) crashAudioClip.Play();
            if (explosionEffect != null) Instantiate(explosionEffect, explosionPos, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(explosionPos, explosionRadius, explosionLayers);
            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, 0f, ForceMode.Impulse);
                }

            
                EnemyAI ai = col.GetComponent<EnemyAI>();
                if (ai == null && col.attachedRigidbody != null)
                {
                    ai = col.attachedRigidbody.GetComponentInParent<EnemyAI>();
                }
                if (ai != null)
                {
                    float dist = Vector3.Distance(explosionPos, ai.transform.position);
                    float t = Mathf.Clamp01(1f - (dist / explosionRadius));
                    float scaledForce = Mathf.Lerp(0f, explosionForce, t);

                    ai.ApplyKnockback(explosionPos, scaledForce, -1f);
                    
                    ai.TakeDamage(damage);
                    ai.OnNoiseHeard(explosionPos);
                }
            }

            Destroy(gameObject, destroyDelay);
        }

        private void OnTriggerEnter(Collider other)
        {
            Break();
        }
    }
}
