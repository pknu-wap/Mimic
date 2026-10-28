using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class WasdSwimMovement : MonoBehaviour
    {
        [Header("WASD Swim")]
        [SerializeField, Min(0f)] private float swimSpeed = 3f;
        [Tooltip("Acceleration applied as thrust while movement input is held.")]
        [SerializeField, Min(0f)] private float swimAcceleration = 6f;
        [Tooltip("Speed lost per second after releasing movement input. Lower values create a longer glide.")]
        [SerializeField, Min(0f)] private float swimDeceleration = 2f;

        [Header("Facing")]
        [Tooltip("Time used by the body to settle toward a new 8-direction movement heading.")]
        [SerializeField, Min(0.01f)] private float facingSmoothTime = 0.18f;
        [SerializeField, Min(0f)] private float maximumFacingSpeed = 360f;
        [Tooltip("When enabled, WASD selects the target heading while thrust follows the body's current forward direction.")]
        [SerializeField] private bool thrustAlongFacing = true;
        [SerializeField] private Transform visualRoot;

        private Rigidbody2D attachedRigidbody;
        private Vector2 moveInput;
        private float headingAngle;
        private float headingVelocity;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
            ResolveVisualRoot();
        }

        private void OnEnable()
        {
            moveInput = Vector2.zero;
            headingVelocity = 0f;
            headingAngle = visualRoot != null ? visualRoot.localEulerAngles.z : 0f;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            headingVelocity = 0f;
            StopMovement();
        }

        private void Update()
        {
            moveInput = ReadKeyboardMove();
            UpdateFacing(moveInput);
        }

        private void FixedUpdate()
        {
            Vector2 velocity = attachedRigidbody.linearVelocity;

            if (moveInput.sqrMagnitude > 0.001f)
            {
                Vector2 thrustDirection = thrustAlongFacing && visualRoot != null
                    ? (Vector2)visualRoot.up
                    : moveInput;
                velocity += thrustDirection.normalized * (swimAcceleration * Time.fixedDeltaTime);
                velocity = Vector2.ClampMagnitude(velocity, swimSpeed);
            }
            else
            {
                velocity = Vector2.MoveTowards(
                    velocity,
                    Vector2.zero,
                    swimDeceleration * Time.fixedDeltaTime);
            }

            attachedRigidbody.linearVelocity = velocity;
        }

        public void Configure(Transform newVisualRoot)
        {
            visualRoot = newVisualRoot;
            ResolveVisualRoot();
        }

        private void ConfigureRigidbody()
        {
            attachedRigidbody.gravityScale = 0f;
            attachedRigidbody.freezeRotation = true;
            attachedRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform visualChild = transform.Find("Visual");
            visualRoot = visualChild != null ? visualChild : transform;
        }

        private void UpdateFacing(Vector2 direction)
        {
            if (visualRoot == null || direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float targetAngle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f;
            headingAngle = Mathf.SmoothDampAngle(
                headingAngle,
                targetAngle,
                ref headingVelocity,
                facingSmoothTime,
                maximumFacingSpeed,
                Time.deltaTime);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, headingAngle);
        }

        private void StopMovement()
        {
            if (attachedRigidbody != null)
            {
                attachedRigidbody.linearVelocity = Vector2.zero;
            }
        }

        private static Vector2 ReadKeyboardMove()
        {
            Vector2 move = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed) move.y += 1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            move.x += Input.GetAxisRaw("Horizontal");
            move.y += Input.GetAxisRaw("Vertical");
#endif

            return Vector2.ClampMagnitude(move, 1f);
        }
    }
}
