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
        [SerializeField] private Transform visualRoot;

        private Rigidbody2D attachedRigidbody;
        private Vector2 moveInput;
        private Vector2 lastFacingDirection = Vector2.right;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
            ResolveVisualRoot();
        }

        private void OnEnable()
        {
            moveInput = Vector2.zero;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
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
                velocity += moveInput * (swimAcceleration * Time.fixedDeltaTime);
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
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            lastFacingDirection = direction.normalized;
            float angle = Mathf.Atan2(lastFacingDirection.y, lastFacingDirection.x) * Mathf.Rad2Deg;
            visualRoot.rotation = Quaternion.Euler(0f, 0f, angle);
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
