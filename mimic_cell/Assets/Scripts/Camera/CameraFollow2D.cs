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
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds = new Vector2(-20f, -8f);
        [SerializeField] private Vector2 maxBounds = new Vector2(20f, 8f);

        private Vector3 dampVelocity;

        private void Reset()
        {
            Camera attachedCamera = GetComponent<Camera>();
            attachedCamera.orthographic = true;
            attachedCamera.orthographicSize = 5.5f;
            attachedCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
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

        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;
            useBounds = true;
        }
    }
}
