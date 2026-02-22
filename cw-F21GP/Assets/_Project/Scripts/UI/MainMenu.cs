using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.UI
{
    public class MainMenu : MonoBehaviour
    {
        public void PlayGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("EnemyDevScene");
        }

        public void QuitGame()
        {
            Application.Quit();
            Debug.Log("Quit Game"); // Does not happen in editor
        }
    }
}
