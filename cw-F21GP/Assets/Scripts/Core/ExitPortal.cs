using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace F21GP.Core
{
    [RequireComponent(typeof(Collider))]
    public class ExitPortal : MonoBehaviour
    {
        [Header("Scene Transition")]
        [SerializeField] private string bossSceneName = "BossDevScene";

        [Header("Audio")]
        [SerializeField] private AudioSource portalAudio;

        [Header("Visuals")]
        [SerializeField] private GameObject portalGrid; // The visual effect of the portal
        [SerializeField] private GameObject portalClosedDoor; // The visual effect of the portal
        [SerializeField] private GameObject portalOpenDoor; // The visual effect of the portal

        [Header("Loading Screen")]
        [SerializeField] private GameObject loadingScreenUI;

        private bool isActivated = false;

        void Start()
        {
            // Ensure the correct door is visible at the start
            if (portalClosedDoor != null)
                portalClosedDoor.SetActive(false);
            if (portalOpenDoor != null)
                portalOpenDoor.SetActive(true);
            if (portalGrid != null)
                portalGrid.SetActive(false);
        }

        void OnEnable()
        {
            // Portal has been activated (SetActive(true) called by LayoutSpawner)
            isActivated = true;

            // Activate the visual effect of the portal
            if (portalGrid != null)
                portalGrid.SetActive(true);
            if (portalClosedDoor != null)
                portalClosedDoor.SetActive(true);
            if (portalOpenDoor != null)
                portalOpenDoor.SetActive(false);

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

                StartCoroutine(LoadBossSceneAsync());
            }
        }

        private IEnumerator LoadBossSceneAsync()
        {
            // Activate loading screen UI if assigned
            if (loadingScreenUI != null)
            {
                loadingScreenUI.SetActive(true);
            }

            // Wait for 5 seconds on the loading screen
            yield return new WaitForSeconds(5f);

            // Start loading the scene asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(bossSceneName);

            // Wait until the asynchronous scene fully loads
            while (!asyncLoad.isDone)
            {
                // You could update a progress bar here using asyncLoad.progress (0 to 1)
                yield return null;
            }
        }
    }
}
