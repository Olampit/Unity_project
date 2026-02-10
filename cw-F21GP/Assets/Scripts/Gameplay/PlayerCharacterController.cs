using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerCharacterController : MonoBehaviour
{
    public float walkSpeed = 6.0f;
    public float runSpeed = 12.0f;
    public float jumpPower = 7.0f;
    public float gravity = 10.0f;

    public float lookXLimit = 45.0f;
    public float lookSpeed = 2.0f;

    Vector3 moveDirection = Vector3.zero;
    public bool canMove = true;

    float rotationX = 0;
    float rotationY = 0;

    CharacterController characterController;

    [Header("Health")]
    [SerializeField] private float _maxHealth = 3;
    private float _currentHealth;
    [SerializeField] private GameObject _hitEffect;

    [SerializeField] private HealthBarController _healthBar;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _currentHealth = _maxHealth;
        if (_healthBar != null)
            _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
    }

    void Update()
    {
        #region Player Movement
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);
        #endregion

        #region Player Jump
        if (Input.GetAxis("Jump") > 0 && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        #endregion

        #region Player Rotation
        characterController.Move(moveDirection * Time.deltaTime);
        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            rotationY += Input.GetAxis("Mouse X") * lookSpeed;
            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
        }
        #endregion
    }

    private void LateUpdate()
    {
        // quick test damage on H
        if (Input.GetKeyDown(KeyCode.H))
        {
            TakeDamage(Random.Range(0.5f, 1.5f));
        }
    }

    // Public method enemies can call
    public void TakeDamage(float amount)
    {
        _currentHealth -= amount;

        if (_currentHealth <= 0)
        {
            // player death 
            Destroy(gameObject);
        }
        else
        {
            if (_healthBar != null)
                _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);

            if (_hitEffect != null)
                Instantiate(_hitEffect, transform.position, Quaternion.identity);
        }
    }
}
