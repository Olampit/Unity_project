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

            Cursor.lockState = CursorLockMode.Locked; 
            Cursor.visible = false; 

            if (_playerStats != null)
            {
                _currentHealth = _playerStats.MaxHealth;
                if (_healthBar != null)
                    _healthBar.UpdateHealthBar(_currentHealth, _playerStats.MaxHealth);
            }
        }

        void Update()
        {
            if (_currentHealth > 0 && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_pauseMenu != null)
                {
                    _pauseMenu.TogglePause(); 
                }
            }

            if (_pauseMenu != null && _pauseMenu.IsPaused) return; 

            if (Input.GetKeyDown(KeyCode.C) && _playerStats != null && _playerStats.CanDropCrashCrate)
            {
                DropCrashCrate();
            }

            CalculateMovement(); 
            CalculateJump(); 
            CalculateRotation(); 
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

        private void DropCrashCrate()
        {
            if (_crashCratePrefab != null)
            {
                Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.7f - transform.right * 0.5f;
                GameObject crate = Instantiate(_crashCratePrefab, dropPosition, transform.rotation);

                Rigidbody rb = crate.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 throwDirection = transform.forward + (Vector3.up * 0.3f);
                    float throwForce = 15f; 
                    
                    rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);
                }
            }
        }

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
