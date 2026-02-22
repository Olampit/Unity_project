using System.Collections;
using UnityEngine;
using F21GP.Enemy;

namespace F21GP.Player
{
    public class RayCastShoot : MonoBehaviour
    {
        [Header("Gun Stats")]
        [SerializeField] private int _gunDamage = 1;
        [SerializeField] private float _fireRate = 0.25f;
        [SerializeField] private float _weaponRange = 50f;
        [SerializeField] private float _hitForce = 100f;    
        [SerializeField] private Transform _gunEnd;

        [Header("Components")]
        [SerializeField] private Camera _fpsCam;                                                
        [SerializeField] private AudioSource _gunAudio;
        [SerializeField] private LineRenderer _laserLine;
        
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
                _nextFire = Time.time + _fireRate;

                StartCoroutine(ShotEffect());

                Vector3 rayOrigin = _fpsCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));

                RaycastHit hit;

                _laserLine.SetPosition(0, _gunEnd.position);

                if (Physics.Raycast(rayOrigin, _fpsCam.transform.forward, out hit, _weaponRange))
                {
                    _laserLine.SetPosition(1, hit.point);
                    
                    EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(_gunDamage);
                        enemy.OnNoiseHeard(_gunEnd.position);
                    }

                    ArionDigital.CrashCrate crate = hit.collider.GetComponent<ArionDigital.CrashCrate>();
                    if (crate != null)
                    {
                        crate.Break();
                    }

                    if (hit.rigidbody != null)
                    {
                        hit.rigidbody.AddForce(-hit.normal * _hitForce);
                    }
                }
                else
                {
                    _laserLine.SetPosition(1, rayOrigin + (_fpsCam.transform.forward * _weaponRange));
                }  
            }
        }

        private IEnumerator ShotEffect()
        {
            _gunAudio.Play();

            _laserLine.enabled = true;

            yield return _shotDuration;

            _laserLine.enabled = false;
        }
    }
}
