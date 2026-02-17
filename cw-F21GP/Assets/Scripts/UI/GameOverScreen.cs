using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public void Setup()
    {
        gameObject.SetActive(true);
        // Unlock cursor so player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Stop the game
        Time.timeScale = 0f;
    }

    public void RestartButton()
    {
        Time.timeScale = 1f; // Resume time before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenuButton()
    {
        Time.timeScale = 1f; // Resume time
        SceneManager.LoadScene("MainMenu"); // Make sure to name your start scene "MainMenu"
    }
}
