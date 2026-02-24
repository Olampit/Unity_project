using System.Collections;
using UnityEngine;
using F21GP.Enemy;

namespace F21GP.Player
{
    public class RayCastShoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GunStats _gunStats; // gun stats

        [Header("Components")]
        [SerializeField] private Transform _gunEnd; // location of ray cast origin
        [SerializeField] private Camera _fpsCam; // camera
        [SerializeField] private AudioSource _gunAudio; // audio source
        [SerializeField] private LineRenderer _laserLine; // line renderer
        
        private WaitForSeconds _shotDuration = new WaitForSeconds(0.07f);
        
        private float _nextFire;

        void Start()
        {
            if (_laserLine == null) _laserLine = GetComponent<LineRenderer>();
            if (_gunAudio == null) _gunAudio = GetComponent<AudioSource>();
            if (_fpsCam == null) _fpsCam = GetComponentInParent<Camera>();
        }

        void Update()
        {
            if (Input.GetButtonDown("Fire1") && Time.time > _nextFire) 
            {
                _nextFire = Time.time + _gunStats.FireRate;

                StartCoroutine(ShotEffect()); // coroutine is used to create a laser effect (or shot effect)

                Vector3 rayOrigin = _fpsCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));

                RaycastHit hit;

                _laserLine.SetPosition(0, _gunEnd.position);

                if (Physics.Raycast(rayOrigin, _fpsCam.transform.forward, out hit, _gunStats.WeaponRange))
                {
                    _laserLine.SetPosition(1, hit.point);
                    
                    // check if the hit object is an enemy
                    EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(_gunStats.GunDamage);
                        enemy.OnNoiseHeard(_gunEnd.position);
                    }

                    // check if the hit object is a crash crate
                    Interactions.CrashCrate crate = hit.collider.GetComponent<Interactions.CrashCrate>();
                    if (crate != null)
                    {
                        crate.Break();
                    }

                    // check if the hit object has a rigidbody
                    if (hit.rigidbody != null)
                    {
                        hit.rigidbody.AddForce(-hit.normal * _gunStats.HitForce);
                    }
                }
                else
                {
                    _laserLine.SetPosition(1, rayOrigin + (_fpsCam.transform.forward * _gunStats.WeaponRange));
                }  
            }
        }

        private IEnumerator ShotEffect() // would be called when the player shoots the gun from coroutine
        {
            _gunAudio.Play();

            _laserLine.enabled = true;

            yield return _shotDuration;

            _laserLine.enabled = false;
        }
    }
}
