using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MouseHoldMovement : MonoBehaviour
    {
        [Header("Mouse Hold Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [Tooltip("Acceleration applied as thrust while the movement button is held.")]
        [SerializeField, Min(0f)] private float moveAcceleration = 6f;
        [Tooltip("Speed lost per second after releasing the movement button. Lower values create a longer glide.")]
        [SerializeField, Min(0f)] private float moveDeceleration = 2f;
        [SerializeField, Min(0f)] private float pointerDeadZone = 0.1f;
        [SerializeField, Min(0f)] private float directionPreviewLength = 2.6f;
        [SerializeField, Range(0, 2)] private int movementMouseButton = 0;

        [Header("Aim")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LineRenderer aimLine;

        private Rigidbody2D attachedRigidbody;
        private Vector2 aimDirection = Vector2.right;
        private float pointerDistance;
        private bool movementButtonHeld;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
            ResolveVisualRoot();
        }

        private void OnEnable()
        {
            movementButtonHeld = false;
            SetAimLineVisible(aimLine != null);
        }

        private void OnDisable()
        {
            movementButtonHeld = false;
            SetAimLineVisible(false);
            StopMovement();
        }

        private void Update()
        {
            aimDirection = ReadMouseWorldDirection(out pointerDistance);
            movementButtonHeld = pointerDistance > pointerDeadZone && IsMovementButtonHeld();

            UpdateFacing();
            UpdateAimLine();
        }

        private void FixedUpdate()
        {
            Vector2 velocity = attachedRigidbody.linearVelocity;

            if (movementButtonHeld)
            {
                velocity += aimDirection * (moveAcceleration * Time.fixedDeltaTime);
                velocity = Vector2.ClampMagnitude(velocity, moveSpeed);
            }
            else
            {
                velocity = Vector2.MoveTowards(
                    velocity,
                    Vector2.zero,
                    moveDeceleration * Time.fixedDeltaTime);
            }

            attachedRigidbody.linearVelocity = velocity;
        }

        public void Configure(Camera newAimCamera, Transform newVisualRoot, LineRenderer newAimLine)
        {
            aimCamera = newAimCamera;
            visualRoot = newVisualRoot;
            aimLine = newAimLine;
            ResolveVisualRoot();
            SetAimLineVisible(enabled);
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

        private void UpdateFacing()
        {
            if (visualRoot == null || aimDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            visualRoot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateAimLine()
        {
            if (aimLine == null)
            {
                return;
            }

            bool hasDirection = pointerDistance > pointerDeadZone;
            SetAimLineVisible(hasDirection);
            if (!hasDirection)
            {
                return;
            }

            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(aimDirection * directionPreviewLength);
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);
        }

        private Vector2 ReadMouseWorldDirection(out float distance)
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (aimCamera == null)
            {
                distance = 0f;
                return aimDirection;
            }

            Vector2 pointerPosition = ReadPointerScreenPosition();
            float cameraDepth = Mathf.Abs(aimCamera.transform.position.z - transform.position.z);
            Vector3 pointerWorld = aimCamera.ScreenToWorldPoint(
                new Vector3(pointerPosition.x, pointerPosition.y, cameraDepth));
            Vector2 direction = (Vector2)pointerWorld - (Vector2)transform.position;
            distance = direction.magnitude;
            return distance > 0.001f ? direction / distance : aimDirection;
        }

        private static Vector2 ReadPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private bool IsMovementButtonHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (movementMouseButton == 0 && mouse.leftButton.isPressed) return true;
                if (movementMouseButton == 1 && mouse.rightButton.isPressed) return true;
                if (movementMouseButton == 2 && mouse.middleButton.isPressed) return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(movementMouseButton);
#else
            return false;
#endif
        }

        private void SetAimLineVisible(bool isVisible)
        {
            if (aimLine != null)
            {
                aimLine.enabled = isVisible;
            }
        }

        private void StopMovement()
        {
            if (attachedRigidbody != null)
            {
                attachedRigidbody.linearVelocity = Vector2.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(aimDirection.normalized * directionPreviewLength);
            Gizmos.DrawLine(start, end);
        }
    }
}
