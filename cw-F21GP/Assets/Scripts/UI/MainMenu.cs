using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        // Assumes the gameplay scene is at build index 1 or named "Scene1" based on previous file listing
        // Ideally we use build index +1 from current, or a specific name.
        // Given file list showed Scene1.unity, let's try loading that.
        // But for safety, SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); is common.
        // Let's stick to a specific scene name if possible or just next index.
        // Let's use "Scene1" as it was seen in the file list.
        Time.timeScale = 1f;
        SceneManager.LoadScene("EnemyDevScene");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game"); // Does not happen in editor
    }
}
