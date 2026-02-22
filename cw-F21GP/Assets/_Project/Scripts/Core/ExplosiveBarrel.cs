using UnityEngine;
using F21GP.Enemy;

public class ExplosiveBarrel : MonoBehaviour
{
    public float explosionRadius = 5f;
    public float explosionForce = 10f;
    public float upwardModifier = 1.2f;

    public LayerMask affectedLayers;

    public GameObject intactModel;
    public GameObject destroyedModel;
    public GameObject explosionEffect;

    private bool exploded = false;

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 explosionPos = transform.position;

        // Spawn VFX
        if (explosionEffect != null)
            Instantiate(explosionEffect, explosionPos, Quaternion.identity);

        // Apply physics explosion
        Collider[] hits = Physics.OverlapSphere(explosionPos, explosionRadius, affectedLayers);

        foreach (Collider col in hits)
        {
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

            EnemyAI ai = col.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.ApplyKnockback(explosionPos, explosionForce, 1.2f);
            }
        }

        // Switch model
        if (intactModel != null) intactModel.SetActive(false);
        if (destroyedModel != null) destroyedModel.SetActive(true);
    }
}
