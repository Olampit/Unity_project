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
        }

        public void GameOver() 
        {
        }
    }
}
