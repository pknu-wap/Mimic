using UnityEngine;

namespace MimicCell.CameraSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [Tooltip("World-space distance the target can move from the camera center before the camera follows.")]
        [SerializeField] private Vector2 deadZone = new Vector2(1.8f, 1.1f);
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds = new Vector2(-20f, -8f);
        [SerializeField] private Vector2 maxBounds = new Vector2(20f, 8f);

        private Vector3 dampVelocity;

        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
            deadZone = new Vector2(Mathf.Max(0f, deadZone.x), Mathf.Max(0f, deadZone.y));
        }

        private void Reset()
        {
            Camera attachedCamera = GetComponent<Camera>();
            attachedCamera.orthographic = true;
            attachedCamera.orthographicSize = 7.5f;
            attachedCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = transform.position;
            Vector2 cameraFocus = (Vector2)transform.position - (Vector2)offset;
            Vector2 targetDelta = (Vector2)target.position - cameraFocus;

            if (Mathf.Abs(targetDelta.x) > deadZone.x)
            {
                desiredPosition.x = target.position.x + offset.x - Mathf.Sign(targetDelta.x) * deadZone.x;
            }

            if (Mathf.Abs(targetDelta.y) > deadZone.y)
            {
                desiredPosition.y = target.position.y + offset.y - Mathf.Sign(targetDelta.y) * deadZone.y;
            }

            desiredPosition.z = target.position.z + offset.z;
            if (useBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref dampVelocity, smoothTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        public void SetDeadZone(Vector2 newDeadZone)
        {
            deadZone = new Vector2(
                Mathf.Max(0f, newDeadZone.x),
                Mathf.Max(0f, newDeadZone.y));
        }

        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
            useBounds = true;
        }
    }
}
