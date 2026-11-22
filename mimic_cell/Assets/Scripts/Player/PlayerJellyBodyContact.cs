using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(-50)]
    public class PlayerJellyBodyContact : MonoBehaviour
    {
        [SerializeField] private Transform contactShape;
        [SerializeField] private LayerMask terrainLayers = ~0;
        [SerializeField, Range(0f, 0.45f)] private float compression = 0.22f;
        [SerializeField, Min(0.01f)] private float recoverySeconds = 0.16f;

        private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        private Rigidbody2D playerBody;
        private Vector2 contactNormal;
        private Vector2 pressure;

        private void Awake()
        {
            playerBody = GetComponent<Rigidbody2D>();
            if (contactShape == null)
            {
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            contactNormal = Vector2.zero;
            int count = playerBody.GetContacts(contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = contacts[i];
                Collider2D terrain = contact.collider.attachedRigidbody == playerBody
                    ? contact.otherCollider
                    : contact.collider;
                if (terrain == null || (terrainLayers.value & (1 << terrain.gameObject.layer)) == 0 ||
                    (terrain.attachedRigidbody != null && terrain.attachedRigidbody.bodyType != RigidbodyType2D.Static))
                {
                    continue;
                }

                Vector2 normal = contact.normal;
                if (Vector2.Dot(normal, playerBody.worldCenterOfMass - contact.point) < 0f)
                {
                    normal = -normal;
                }
                contactNormal += normal;
            }
            contactNormal = Vector2.ClampMagnitude(contactNormal, 1f);
        }

        private void LateUpdate()
        {
            float response = contactNormal.sqrMagnitude > 0f ? 0.045f : recoverySeconds;
            pressure = Vector2.Lerp(pressure, contactNormal * compression, 1f - Mathf.Exp(-Time.deltaTime / response));
            Vector2 localPressure = contactShape.parent.InverseTransformDirection(pressure);
            float horizontal = Mathf.Abs(localPressure.x);
            float vertical = Mathf.Abs(localPressure.y);
            contactShape.localScale = new Vector3(1f - horizontal + vertical * 0.35f, 1f - vertical + horizontal * 0.35f, 1f);
            contactShape.localPosition = localPressure * 0.3f;
        }

        private void OnDisable()
        {
            contactNormal = Vector2.zero;
            pressure = Vector2.zero;
            if (contactShape != null)
            {
                contactShape.localPosition = Vector3.zero;
                contactShape.localScale = Vector3.one;
            }
        }
    }
}
