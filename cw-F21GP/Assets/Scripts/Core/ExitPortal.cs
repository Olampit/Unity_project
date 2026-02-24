using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.Core
{
    [RequireComponent(typeof(Collider))]
    public class ExitPortal : MonoBehaviour
    {
        [Header("Scene Transition")]
        [SerializeField] private string bossSceneName = "BossDevScene";

        [Header("Audio")]
        [SerializeField] private AudioSource portalAudio;

        private bool isActivated = false;

        void OnEnable()
        {
            // Portal has been activated (SetActive(true) called by LayoutSpawner)
            isActivated = true;

            if (portalAudio != null)
                portalAudio.Play();

            Debug.Log("Exit Portal is now active and ready.");
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isActivated) return;

            // Only respond to the player
            if (other.CompareTag("Player"))
            {
                Debug.Log("Player entered the portal. Loading Boss Scene...");

                // Stop the level timer now that the player has committed to the transition
                LayoutSpawner spawner = FindObjectOfType<LayoutSpawner>();
                if (spawner != null)
                    spawner.StopTimer();

                SceneManager.LoadScene(bossSceneName);
            }
        }
    }
}
