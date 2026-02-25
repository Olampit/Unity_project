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
            // Ensure pause menu is hidden at start
            // We check !IsPaused so that if the GameObject is disabled in the editor 
            // and first activated via the Escape key, it doesn't immediately hide itself!
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

            // Unlock cursor so player can click buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Resume()
        {
            if (_pauseMenuUI != null) _pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            IsPaused = false;

            // Lock cursor back
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void RestartButton()
        {
            Time.timeScale = 1f; // Resume time before reloading
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void MainMenuButton()
        {
            Time.timeScale = 1f; // Resume time
            SceneManager.LoadScene("MainMenu");
        }
    }
}
