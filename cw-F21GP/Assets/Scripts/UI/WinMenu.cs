using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.UI
{
    public class WinMenu : MonoBehaviour
    {
        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        public void MainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
        {
            Application.Quit();
            Debug.Log("Quit Game"); // Does not happen in editor
        }
    }
}
