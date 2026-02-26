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
        [SerializeField] private GameObject portalGrid; 
        [SerializeField] private GameObject portalClosedDoor;
        [SerializeField] private GameObject portalOpenDoor;

        [Header("Loading Screen")]
        [SerializeField] private GameObject loadingScreenUI;

        private bool isActivated = false;

        void Start()
        {
            if (portalClosedDoor != null)
                portalClosedDoor.SetActive(false);
            if (portalOpenDoor != null)
                portalOpenDoor.SetActive(true);
            if (portalGrid != null)
                portalGrid.SetActive(false);
        }

        void OnEnable()
        {
            isActivated = true;

            if (portalGrid != null)
                portalGrid.SetActive(true);
            if (portalClosedDoor != null)
                portalClosedDoor.SetActive(true);
            if (portalOpenDoor != null)
                portalOpenDoor.SetActive(false);

            if (portalAudio != null)
                portalAudio.Play();

        }

        void OnTriggerEnter(Collider other)
        {
            if (!isActivated) return;

            if (other.CompareTag("Player"))
            {

                LayoutSpawner spawner = FindObjectOfType<LayoutSpawner>();
                if (spawner != null)
                    spawner.StopTimer();

                StartCoroutine(LoadBossSceneAsync());
            }
        }

        private IEnumerator LoadBossSceneAsync()
        {
            if (loadingScreenUI != null)
            {
                loadingScreenUI.SetActive(true);
            }

            yield return new WaitForSeconds(1.5f);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(bossSceneName);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }
    }
}
