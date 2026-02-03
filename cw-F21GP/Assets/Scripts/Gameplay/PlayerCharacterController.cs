using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerCharacterController : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6.0f;
    public float runSpeed = 12.0f;
    public float jumpPower = 7.0f;
    public float gravity = 10.0f;

    public float lookXLimit = 45.0f;
    public float lookSpeed = 2.0f;

    Vector3 moveDirection = Vector3.zero;
    public bool canMove = true;

    float rotationX = 0;

    CharacterController characterController;

    [Header("Health")]
    [SerializeField] private float _maxHealth = 3;
    private float _currentHealth;
    [SerializeField] private GameObject _hitEffect;

    [SerializeField] private HealthBarController _healthBar;

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;

        _currentHealth = _maxHealth;
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
    }

    // Update is called once per frame
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
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }   
        

        #endregion
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            TakeDamage();
        }
    }

    private void TakeDamage()
    {
        _currentHealth -= Random.Range(0.5f, 1.5f);

        if (_currentHealth <= 0)
        {
            Destroy(gameObject);
        }
        else
        {   
            _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
            Instantiate(_hitEffect, transform.position, Quaternion.identity);
        }
    }
}
