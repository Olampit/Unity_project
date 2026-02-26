using UnityEngine;
using UnityEngine.SceneManagement;

namespace F21GP.UI
{
    public class ControlsMenu : MonoBehaviour
    {
        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        public void BackButton()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
