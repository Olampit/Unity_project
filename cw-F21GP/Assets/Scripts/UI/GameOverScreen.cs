using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.UI
{
    public class GameOverScreen : MonoBehaviour
    {
        public void Setup()
        {
            gameObject.SetActive(true);
            // unlock cursor so player can click buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // pause the game
            Time.timeScale = 0f;
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
