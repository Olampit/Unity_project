// this script is used to manage the game state
using UnityEngine;

namespace F21GP.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Global References")]
        [SerializeField] private Transform _playerTransform;

        public Transform PlayerTransform => _playerTransform; // get player transform

        private void Awake() // Awake is called when the script is loaded
        {
            if (Instance != null && Instance != this) // Instance is needed here to make sure there is only one instance of the game manager
            {
                Destroy(gameObject); // destroy the game object
                return;
            }
            Instance = this;
        }

        public void GameOver() // GameOver is called when the game is over
        {
            Debug.Log("Game Over triggered via GameManager.");
        }
    }
}
