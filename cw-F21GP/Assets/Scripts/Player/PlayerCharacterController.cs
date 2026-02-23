using UnityEngine;
using F21GP.UI;
using F21GP.Enemy;

namespace F21GP.Player
{
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PlayerStats _playerStats;

        [Header("Components")]
        [SerializeField] private CharacterController _characterController;

        [Header("Health & UI")]
        [SerializeField] private float _currentHealth;
        [SerializeField] private GameObject _hitEffect;

        [SerializeField] private HealthBarController _healthBar;
        [SerializeField] private GameOverScreen _gameOverScreen;
        [SerializeField] private PauseMenu _pauseMenu;

        [Header("Abilities")]
        [SerializeField] private GameObject _crashCratePrefab;

        private Vector3 _moveDirection = Vector3.zero;
        public bool CanMove = true;

        private float _rotationX = 0;
        private float _rotationY = 0;

        void Start()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();

            Cursor.lockState = CursorLockMode.Locked; // lock the cursor to the center of the screen
            Cursor.visible = false; // hide the cursor

            if (_playerStats != null)
            {
                _currentHealth = _playerStats.MaxHealth;
                if (_healthBar != null)
                    _healthBar.UpdateHealthBar(_currentHealth, _playerStats.MaxHealth); // update the health bar initially
            }
        }

        void Update()
        {
            if (_currentHealth > 0 && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_pauseMenu != null)
                {
                    _pauseMenu.TogglePause(); // toggle the pause menu
                }
            }

            if (_pauseMenu != null && _pauseMenu.IsPaused) return; // because we don't want to move the player when the pause menu is open

            if (Input.GetKeyDown(KeyCode.C) && _playerStats != null && _playerStats.CanDropCrashCrate)
            {
                DropCrashCrate();
            }

            CalculateMovement(); // calculate the movement of the player
            CalculateJump(); // calculate the jump of the player
            CalculateRotation(); // calculate the rotation of the player
        }

        private void CalculateMovement()
        {
            if (_playerStats == null) return;

            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);

            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float curSpeedX = CanMove ? (isRunning ? _playerStats.RunSpeed : _playerStats.WalkSpeed) * Input.GetAxis("Vertical") : 0;
            float curSpeedY = CanMove ? (isRunning ? _playerStats.RunSpeed : _playerStats.WalkSpeed) * Input.GetAxis("Horizontal") : 0;
            float movementDirectionY = _moveDirection.y;
            
            _moveDirection = (forward * curSpeedX) + (right * curSpeedY);
            _moveDirection.y = movementDirectionY;
        }

        private void CalculateJump()
        {
            if (_playerStats == null) return;

            if (Input.GetAxis("Jump") > 0 && CanMove && _characterController.isGrounded)
            {
                _moveDirection.y = _playerStats.JumpPower;
            }

            if (!_characterController.isGrounded)
            {
                _moveDirection.y -= _playerStats.Gravity * Time.deltaTime;
            }
        }

        private void CalculateRotation()
        {
            if (_playerStats == null) return;

            _characterController.Move(_moveDirection * Time.deltaTime);
            if (CanMove)
            {
                _rotationX += -Input.GetAxis("Mouse Y") * _playerStats.LookSpeed;
                _rotationX = Mathf.Clamp(_rotationX, -_playerStats.LookXLimit, _playerStats.LookXLimit);
                _rotationY += Input.GetAxis("Mouse X") * _playerStats.LookSpeed;
                transform.localRotation = Quaternion.Euler(_rotationX, _rotationY, 0);
            }
        }

        private void LateUpdate()
        {
            // quick test damage on H
            if (Input.GetKeyDown(KeyCode.H))
            {
                TakeDamage(Random.Range(0.5f, 1.5f));
            }
        }

        private void DropCrashCrate()
        {
            if (_crashCratePrefab != null)
            {
                // Instantiate crate slightly in front and above the player
                Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.7f - transform.right * 0.5f;
                GameObject crate = Instantiate(_crashCratePrefab, dropPosition, transform.rotation);

                // If the player adds a Rigidbody to the crate prefab, throw it forward
                Rigidbody rb = crate.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Calculate a throw direction: mostly forward, slightly up
                    Vector3 throwDirection = transform.forward + (Vector3.up * 0.3f);
                    float throwForce = 15f; 
                    
                    rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);
                }
            }
        }

        // Public method enemies can call
        public void TakeDamage(float amount)
        {
            if (_playerStats == null) return;

            _currentHealth -= amount;
            if (_currentHealth < 0) _currentHealth = 0;

            if (_healthBar != null)
                _healthBar.UpdateHealthBar(_currentHealth, _playerStats.MaxHealth);

            if (_currentHealth <= 0)
            {
                if (_gameOverScreen != null)
                {
                    _gameOverScreen.Setup();
                }
                
                CanMove = false;
            }
            else
            {
                if (_hitEffect != null)
                    Instantiate(_hitEffect, transform.position, Quaternion.identity);
            }
        }
    }
}
