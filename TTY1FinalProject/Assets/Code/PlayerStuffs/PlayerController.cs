using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace TTY1
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float MoveSpeed = 4f;
        public float MouseSensitivity = 1f;
        public float Gravity = -9.81f;

        [Header("Acceleration")]
        [Tooltip("How quickly the player reaches MoveSpeed (units/sec^2)")]
        public float Acceleration = 40f;
        [Tooltip("How quickly the player slows to a stop (units/sec^2)")]
        public float Deceleration = 50f;
        [Tooltip("Multiplier applied to deceleration when there's no input to create a slidey stop (0..1). Lower = more slide.")]
        [Range(0f, 1f)]
        public float StopSlideFactor = 0.35f;

        [Header("Turn resistance")]
        [Tooltip("When changing direction, how much to reduce acceleration (0..1). Lower = harder to turn into opposite direction.")]
        [Range(0f, 1f)]
        public float TurnAccelMultiplier = 0.35f;
        [Tooltip("When changing direction, how much to increase deceleration. (>1 increases braking when reversing)")]
        public float TurnDecelMultiplier = 1.6f;
        [Tooltip("Alignment dot threshold below which turning penalties start to apply (-1..1). Lower allows sharper turns before penalty.")]
        [Range(-1f, 1f)]
        public float TurnPenaltyThreshold = 0.2f;

        [Header("Crouch")]
        [Tooltip("How far the camera moves down when crouching (meters).")]
        public float CrouchHeight = 0.5f;
        [Tooltip("How quickly the camera moves when toggling crouch.")]
        public float CrouchLerpSpeed = 10f;

        [Header("Camera")]
        public Transform PlayerCamera; // assign your camera (child of player)

        [Header("Health")]
        public int MaxHealth = 100;
        public int CurrentHealth;
        public Image healthBar; // optional, assign in Inspector

        [Header("Input (optional - Unity Input System)")]
        [Tooltip("2D Vector: x = horizontal (A/D), y = vertical (W/S)")]
        public InputActionReference moveAction;
        [Tooltip("2D Vector: x = look X, y = look Y (typically Pointer/delta)")]
        public InputActionReference lookAction;
        [Tooltip("Optional crouch action (button)")]
        public InputActionReference crouchAction;

        private CharacterController _controller;
        private Vector3 _verticalVelocity;
        private float _cameraPitch = 0f;
        private Vector3 _cameraOriginalLocalPos;
        private Vector3 _cameraCrouchLocalPos;
        private bool _isDead;

        // horizontal movement state used for accel/decel smoothing
        private Vector3 _currentHorizontalVelocity = Vector3.zero;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (PlayerCamera == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    PlayerCamera = cam.transform;
            }

            if (PlayerCamera != null)
            {
                _cameraOriginalLocalPos = PlayerCamera.localPosition;
                _cameraCrouchLocalPos = _cameraOriginalLocalPos + Vector3.down * Mathf.Abs(CrouchHeight);
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
            if (lookAction != null && lookAction.action != null) lookAction.action.Enable();
            if (crouchAction != null && crouchAction.action != null) crouchAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
            if (lookAction != null && lookAction.action != null) lookAction.action.Disable();
            if (crouchAction != null && crouchAction.action != null) crouchAction.action.Disable();
        }

        private void Start()
        {
            CurrentHealth = MaxHealth;
            UpdateHealthUI();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (_isDead) return;

            HandleMouseLook();
            HandleMovement();
            HandleCrouch();
        }

        private void HandleMouseLook()
        {
            // Read look input from Input System if available, otherwise fall back to legacy axes.
            Vector2 look = Vector2.zero;
            if (lookAction != null && lookAction.action != null)
            {
                look = lookAction.action.ReadValue<Vector2>();
            }
            else
            {
                look.x = Input.GetAxis("Mouse X");
                look.y = Input.GetAxis("Mouse Y");
            }

            float mx = look.x * MouseSensitivity;
            float my = look.y * MouseSensitivity;

            // Y (pitch) affects camera, X (yaw) rotates player
            _cameraPitch -= my;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -89f, 89f);

            if (PlayerCamera != null)
                PlayerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

            transform.Rotate(Vector3.up * mx);
        }

        private void HandleMovement()
        {
            // Read movement input from Input System if available, otherwise fall back to legacy axes.
            Vector2 moveInput = Vector2.zero;
            if (moveAction != null && moveAction.action != null)
            {
                moveInput = moveAction.action.ReadValue<Vector2>();
            }
            else
            {
                moveInput.x = Input.GetAxis("Horizontal");
                moveInput.y = Input.GetAxis("Vertical");
            }

            float horizontal = moveInput.x;
            float vertical = moveInput.y;

            // Build world-space desired direction using the player's transform
            Vector3 desiredDirection = transform.right * horizontal + transform.forward * vertical;
            // preserve analog magnitude (so stick pressure scales speed)
            float inputMagnitude = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);

            Vector3 desiredVelocity = (desiredDirection.sqrMagnitude > 0.0001f)
                ? desiredDirection.normalized * MoveSpeed * inputMagnitude
                : Vector3.zero;

            // choose acceleration vs deceleration depending on whether target speed is higher
            float currentSpeed = _currentHorizontalVelocity.magnitude;
            float targetSpeed = desiredVelocity.magnitude;

            // base rate before turn adjustments
            float rate;
            if (targetSpeed > currentSpeed + 0.001f)
            {
                rate = Acceleration;
            }
            else
            {
                // When there's no input (targetSpeed ~= 0) reduce deceleration to create a slidey stop.
                if (targetSpeed <= 0.01f)
                    rate = Deceleration * StopSlideFactor;
                else
                    rate = Deceleration;
            }

            // Apply turn-resistance: if current velocity points sufficiently away from desired direction,
            // make it harder to accelerate into that direction and increase braking when reversing.
            if (_currentHorizontalVelocity.sqrMagnitude > 0.0001f && desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 curDir = _currentHorizontalVelocity.normalized;
                Vector3 desDir = desiredVelocity.normalized;
                float alignment = Vector3.Dot(curDir, desDir); // 1 = same, -1 = opposite

                if (alignment < TurnPenaltyThreshold)
                {
                    // map alignment from [TurnPenaltyThreshold..-1] to [1..0] range for interpolation
                    float t = Mathf.InverseLerp(TurnPenaltyThreshold, -1f, alignment);

                    if (targetSpeed > currentSpeed + 0.001f)
                    {
                        // reduce acceleration smoothly based on t
                        float accelMultiplier = Mathf.Lerp(1f, TurnAccelMultiplier, t);
                        rate = Mathf.Max(0.001f, rate * accelMultiplier);
                    }
                    else
                    {
                        // increase deceleration smoothly when reversing/turning
                        float decelMultiplier = Mathf.Lerp(1f, TurnDecelMultiplier, t);
                        rate = Mathf.Max(0.001f, rate * decelMultiplier);
                    }
                }
            }

            // smoothly move current velocity towards desired velocity
            _currentHorizontalVelocity = Vector3.MoveTowards(_currentHorizontalVelocity, desiredVelocity, rate * Time.deltaTime);

            // apply horizontal movement (velocity is units/sec)
            _controller.Move(_currentHorizontalVelocity * Time.deltaTime);

            // gravity
            if (_controller.isGrounded && _verticalVelocity.y < 0f)
                _verticalVelocity.y = -2f; // small negative to keep grounded

            _verticalVelocity.y += Gravity * Time.deltaTime;
            _controller.Move(_verticalVelocity * Time.deltaTime);
        }

        private void HandleCrouch()
        {
            if (PlayerCamera == null) return;

            bool isCrouching = false;

            if (crouchAction != null && crouchAction.action != null)
            {
                isCrouching = crouchAction.action.IsPressed();
            }
            else
            {
                isCrouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            }

            Vector3 target = isCrouching ? _cameraCrouchLocalPos : _cameraOriginalLocalPos;
            PlayerCamera.localPosition = Vector3.Lerp(PlayerCamera.localPosition, target, Time.deltaTime * CrouchLerpSpeed);
        }

        // Public health API
        public void TakeDamage(int damage)
        {
            if (_isDead || damage <= 0) return;

            CurrentHealth -= damage;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);
            UpdateHealthUI();

            if (CurrentHealth == 0) Die();
        }

        public void Heal(int amount)
        {
            if (_isDead || amount <= 0) return;

            CurrentHealth += amount;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);
            UpdateHealthUI();
        }

        private void UpdateHealthUI()
        {
            if (healthBar != null)
                healthBar.fillAmount = (float)CurrentHealth / MaxHealth;
        }

        private void Die()
        {
            _isDead = true;
            // freeze player movement and show cursor; any death handling can be added by the user
            MoveSpeed = 0f;
            _verticalVelocity = Vector3.zero;
            _currentHorizontalVelocity = Vector3.zero;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}