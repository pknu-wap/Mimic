using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(-5)]
    public class PlayerJellyBodyContact : MonoBehaviour
    {
        private const float ReferenceSpeed = 3f;
        private const float ShapeSpring = 14f;
        private const float ShapeDamping = 3.4f;
        private const float IdlePulseAmplitude = 0.014f;
        private const float IdlePulseFrequency = 0.46f;
        private const float FullImpactSpeed = 3.5f;
        private const float ImpactKick = 2.8f;
        private const float ImpactSpring = 22f;
        private const float ImpactDamping = 3.4f;
        private const float ContactRecoverySpeed = 2.4f;

        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform core;

        private Rigidbody2D playerBody;
        private PlayerJellyBodyMesh bodyMesh;
        private Vector3 baseBodyPosition;
        private Vector3 baseBodyScale;
        private Vector3 baseCorePosition;
        private Vector3 baseCoreScale;
        private Vector2 pressureShape;
        private Vector2 pressureVelocity;
        private Vector2 previousVelocity;
        private Vector2 impactDirection = Vector2.up;
        private float impactAmount;
        private float impactVelocity;
        private float sustainedImpact;
        private float visualTime;
        private bool showingBody;

        private void Awake()
        {
            if (bodyRenderer == null || bodyRenderer.sprite == null)
            {
                enabled = false;
                return;
            }

            playerBody = GetComponent<Rigidbody2D>();
            baseBodyPosition = bodyRenderer.transform.localPosition;
            baseBodyScale = bodyRenderer.transform.localScale;
            if (core != null)
            {
                baseCorePosition = core.localPosition;
                baseCoreScale = core.localScale;
            }
            bodyMesh = new PlayerJellyBodyMesh(bodyRenderer);
            enabled = bodyMesh.IsValid;
        }

        private void LateUpdate()
        {
            if (!bodyRenderer.gameObject.activeInHierarchy)
            {
                showingBody = false;
                bodyMesh.SetVisible(false);
                return;
            }

            Transform body = bodyRenderer.transform;
            Vector2 velocity = body.InverseTransformDirection(playerBody.linearVelocity);
            if (!showingBody)
            {
                ResetMotion(velocity);
                showingBody = true;
            }
            bodyMesh.SetVisible(true);

            float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (deltaTime <= 0f)
            {
                return;
            }
            visualTime += deltaTime;
            Vector2 acceleration = Vector2.ClampMagnitude((velocity - previousVelocity) / deltaTime, 18f);
            previousVelocity = velocity;

            Vector2 pressureTarget = Vector2.ClampMagnitude(velocity / ReferenceSpeed, 1f);
            pressureVelocity += ((pressureTarget - pressureShape) * ShapeSpring - pressureVelocity * ShapeDamping) * deltaTime;
            pressureShape += pressureVelocity * deltaTime;
            impactVelocity += ((sustainedImpact - impactAmount) * ImpactSpring - impactVelocity * ImpactDamping) * deltaTime;
            impactAmount = Mathf.Clamp(impactAmount + impactVelocity * deltaTime, -0.5f, 1.25f);
            sustainedImpact = Mathf.MoveTowards(sustainedImpact, 0f, deltaTime * ContactRecoverySpeed);

            float pulse = Mathf.Sin(visualTime * IdlePulseFrequency * Mathf.PI * 2f) * IdlePulseAmplitude;
            bodyMesh.Simulate(pressureShape, acceleration, visualTime, pulse, deltaTime);
            UpdateBodyTransforms(body, pulse);
        }

        private void ResetMotion(Vector2 velocity)
        {
            pressureShape = Vector2.zero;
            pressureVelocity = Vector2.zero;
            previousVelocity = velocity;
            impactAmount = 0f;
            impactVelocity = 0f;
            sustainedImpact = 0f;
            impactDirection = Vector2.up;
            visualTime = 0f;
            bodyMesh.Reset();
        }

        private void UpdateBodyTransforms(Transform body, float pulse)
        {
            Vector2 bodyLag = -pressureShape * 0.05f - impactDirection * (impactAmount * 0.022f);
            body.localPosition = baseBodyPosition + (Vector3)bodyLag;
            body.localScale = new Vector3(
                baseBodyScale.x * (1f + pulse * 0.52f),
                baseBodyScale.y * (1f - pulse * 0.38f),
                baseBodyScale.z);

            if (core != null)
            {
                Vector2 coreLag = -pressureShape * 0.075f - impactDirection * (impactAmount * 0.075f);
                float corePulse = 1f - pulse * 0.8f + Mathf.Abs(impactAmount) * 0.025f;
                core.localPosition = baseCorePosition + (Vector3)(bodyLag + coreLag);
                core.localScale = new Vector3(baseCoreScale.x * corePulse, baseCoreScale.y * corePulse, baseCoreScale.z);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            float strength = Mathf.Max(0.2f, Mathf.Clamp01(collision.relativeVelocity.magnitude / FullImpactSpeed));
            RegisterCollision(collision, strength, true);
        }

        private void OnCollisionStay2D(Collision2D collision) => RegisterCollision(collision, 0.32f, false);

        private void RegisterCollision(Collision2D collision, float strength, bool kick)
        {
            if (!showingBody || collision.contactCount == 0)
            {
                return;
            }

            Collider2D obstacle = collision.collider != null && collision.collider.attachedRigidbody != playerBody
                ? collision.collider
                : collision.otherCollider;
            bool wideContact = IsFlatCollider(obstacle);
            Vector2 center = playerBody.worldCenterOfMass;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                Vector2 inward = Vector2.Dot(contact.normal, center - contact.point) < 0f ? -contact.normal : contact.normal;
                RegisterBodyContact(contact.point, inward, strength, kick && i == 0, wideContact, true);
            }
            RegisterImpact(collision.GetContact(0).point - center, strength, kick);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!showingBody || !CreatureColliderRole.IsBodyCollider(other))
            {
                return;
            }

            Vector2 center = playerBody.worldCenterOfMass;
            Vector2 point = other.ClosestPoint(center);
            if ((point - center).sqrMagnitude < 0.0001f)
            {
                point = center - (center - (Vector2)other.bounds.center).normalized * 0.35f;
            }
            RegisterImpact(point - center, 0.16f, true);
            RegisterBodyContact(point, center - point, 0.16f, true, IsFlatCollider(other), false);
        }

        private void RegisterBodyContact(Vector2 point, Vector2 inward, float strength, bool kick, bool wide, bool sustained)
        {
            Transform body = bodyRenderer.transform;
            Vector2 localNormal = body.InverseTransformDirection(inward.normalized);
            bodyMesh.AddContact(body.InverseTransformPoint(point), localNormal, strength, kick, wide, ImpactKick, sustained);
        }

        private void RegisterImpact(Vector2 outward, float strength, bool kick)
        {
            if (outward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 localOutward = bodyRenderer.transform.InverseTransformDirection(outward.normalized);
            impactDirection = Vector2.Lerp(impactDirection, localOutward, 0.82f).normalized;
            sustainedImpact = Mathf.Max(sustainedImpact, strength);
            if (kick)
            {
                impactVelocity += strength * ImpactKick;
            }
        }

        private static bool IsFlatCollider(Collider2D collider) =>
            collider is BoxCollider2D || collider is EdgeCollider2D ||
            collider is PolygonCollider2D || collider is CompositeCollider2D;

        private void OnDisable()
        {
            showingBody = false;
            if (bodyMesh == null)
            {
                return;
            }
            bodyMesh.SetVisible(false);
            bodyRenderer.transform.localPosition = baseBodyPosition;
            bodyRenderer.transform.localScale = baseBodyScale;
            if (core != null)
            {
                core.localPosition = baseCorePosition;
                core.localScale = baseCoreScale;
            }
        }

        private void OnDestroy() => bodyMesh?.Dispose();
    }
}
