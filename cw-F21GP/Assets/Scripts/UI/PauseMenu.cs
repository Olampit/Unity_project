using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.UI
{
    public class PauseMenu : MonoBehaviour
    {
        public bool IsPaused { get; private set; } = false;

        [SerializeField] private GameObject _pauseMenuUI;

        private void Start()
        {
            if (_pauseMenuUI != null && !IsPaused)
            {
                _pauseMenuUI.SetActive(false);
            }
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            if (_pauseMenuUI != null) _pauseMenuUI.SetActive(true);
            Time.timeScale = 0f;
            IsPaused = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Resume()
        {
            if (_pauseMenuUI != null) _pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            IsPaused = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void RestartButton()
        {
            Time.timeScale = 1f; 
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void MainMenuButton()
        {
            Time.timeScale = 1f; 
            SceneManager.LoadScene("MainMenu");
        }
    }
}
