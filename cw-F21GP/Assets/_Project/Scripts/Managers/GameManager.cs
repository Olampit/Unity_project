using UnityEngine;

namespace F21GP.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Global References")]
        [SerializeField] private Transform _playerTransform;

        public Transform PlayerTransform => _playerTransform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject);
        }

        public void GameOver()
        {
            // Handle global Game Over state
            Debug.Log("Game Over triggered via GameManager.");
        }
    }
}
