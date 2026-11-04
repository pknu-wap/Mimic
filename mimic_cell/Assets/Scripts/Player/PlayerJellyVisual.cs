using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerJellyVisual : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Transform coreRoot;
        [SerializeField] private SpriteRenderer[] legRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private SpriteRenderer extractionTentacleRenderer;
        [SerializeField] private Rigidbody2D targetRigidbody;

        [Header("Fluid Pudding Body")]
        [SerializeField, Range(5, 20)] private int bodyMeshColumns = 16;
        [SerializeField, Range(5, 24)] private int bodyMeshRows = 18;
        [SerializeField, Min(0f)] private float idlePulseAmplitude = 0.014f;
        [SerializeField, Min(0f)] private float idlePulseFrequency = 0.46f;
        [SerializeField, Min(0f)] private float shapeSpring = 14f;
        [SerializeField, Min(0f)] private float shapeDamping = 3.4f;
        [SerializeField, Min(0f)] private float surfaceSpring = 27f;
        [SerializeField, Min(0f)] private float surfaceDamping = 5.3f;
        [SerializeField, Min(0f)] private float surfaceCohesion = 22f;
        [SerializeField, Min(0.1f)] private float referenceSpeed = 3f;
        [SerializeField, Range(0f, 0.5f)] private float pressureStretch = 0.16f;
        [SerializeField, Range(0f, 0.65f)] private float pressureTaper = 0.3f;
        [SerializeField, Range(0f, 0.4f)] private float frontCompression = 0.13f;
        [SerializeField, Range(0f, 0.5f)] private float rearElongation = 0.19f;
        [SerializeField, Range(0f, 0.25f)] private float bodyWaveAmplitude = 0.045f;
        [SerializeField, Min(0f)] private float bodyWaveFrequency = 0.92f;
        [SerializeField, Min(0f)] private float bodyWaveCycles = 1.45f;
        [SerializeField, Range(0f, 0.2f)] private float accelerationInertia = 0.055f;
        [SerializeField, Range(0.1f, 1.5f)] private float maximumBodyOffset = 0.78f;

        [Header("Contact Shape")]
        [SerializeField, Range(0f, 0.85f)] private float impactCompression = 0.48f;
        [SerializeField, Range(0f, 0.75f)] private float displacedFluidBulge = 0.34f;
        [SerializeField, Range(0.25f, 1.5f)] private float contactInfluence = 0.82f;
        [SerializeField, Min(0.1f)] private float fullImpactSpeed = 3.5f;
        [SerializeField, Min(0f)] private float impactKick = 2.8f;
        [SerializeField, Min(0f)] private float impactSpring = 22f;
        [SerializeField, Min(0f)] private float impactDamping = 3.4f;
        [SerializeField, Range(0f, 1f)] private float collisionStayCompression = 0.32f;
        [SerializeField, Min(0f)] private float contactRecoverySpeed = 2.4f;

        [Header("Rope Appendages")]
        [SerializeField, Range(8, 40)] private int tentacleRows = 28;
        [SerializeField, Min(0f)] private float tentacleWaveForce = 2.4f;
        [SerializeField, Min(0f)] private float tentacleWaveFrequency = 0.42f;
        [SerializeField, Min(0f)] private float waterFlowDrag = 2f;
        [SerializeField, Min(0f)] private float wakeGatherForce = 2.4f;
        [SerializeField, Range(0f, 0.7f)] private float idleWaveScale = 0.55f;
        [Tooltip("Inner fraction that follows the body without rope motion.")]
        [SerializeField, Range(0.03f, 0.45f)] private float rootRigidFraction = 0.07f;
        [Tooltip("Distance in body-local units that hides each rope root inside the body sprite.")]
        [SerializeField, Range(0f, 0.3f)] private float rootAnchorInset = 0.02f;

        private readonly List<TentacleSpriteMesh> tentacles = new List<TentacleSpriteMesh>();

        private SoftBodySpriteMesh bodyMesh;
        private Vector3 baseBodyPosition;
        private Vector3 baseBodyScale;
        private Vector3 baseCorePosition;
        private Vector3 baseCoreScale;
        private Vector2 pressureShape;
        private Vector2 pressureShapeVelocity;
        private Vector2 previousLocalVelocity;
        private Vector2 impactDirection = Vector2.up;
        private float impactAmount;
        private float impactVelocity;
        private float sustainedImpact;
        private float visualTime;
        private float lastVisualAngle;
        private bool initialized;

        public void Configure(
            Transform newVisualRoot,
            Transform newBodyRoot,
            SpriteRenderer newBodyRenderer,
            Transform newCoreRoot,
            SpriteRenderer[] newLegRenderers,
            SpriteRenderer newExtractionTentacleRenderer)
        {
            visualRoot = newVisualRoot;
            bodyRoot = newBodyRoot;
            bodyRenderer = newBodyRenderer;
            coreRoot = newCoreRoot;
            legRenderers = newLegRenderers ?? Array.Empty<SpriteRenderer>();
            extractionTentacleRenderer = newExtractionTentacleRenderer;
            targetRigidbody = GetComponent<Rigidbody2D>();
            initialized = false;
        }

        public void ApplyImpact(Vector2 worldPoint, float normalizedStrength)
        {
            InitializeIfNeeded();
            if (!initialized)
            {
                return;
            }

            float strength = Mathf.Clamp01(normalizedStrength);
            Vector2 outward = worldPoint - targetRigidbody.worldCenterOfMass;
            RegisterImpact(outward, strength, true);
            RegisterBodyContact(
                worldPoint,
                targetRigidbody.worldCenterOfMass - worldPoint,
                strength,
                true,
                false);
        }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            ResetMotionState();
        }

        private void OnValidate()
        {
            bodyMeshColumns = Mathf.Clamp(bodyMeshColumns, 5, 20);
            bodyMeshRows = Mathf.Clamp(bodyMeshRows, 5, 24);
            idlePulseAmplitude = Mathf.Max(0f, idlePulseAmplitude);
            idlePulseFrequency = Mathf.Max(0f, idlePulseFrequency);
            shapeSpring = Mathf.Max(0f, shapeSpring);
            shapeDamping = Mathf.Max(0f, shapeDamping);
            surfaceSpring = Mathf.Max(0f, surfaceSpring);
            surfaceDamping = Mathf.Max(0f, surfaceDamping);
            surfaceCohesion = Mathf.Max(0f, surfaceCohesion);
            referenceSpeed = Mathf.Max(0.1f, referenceSpeed);
            bodyWaveFrequency = Mathf.Max(0f, bodyWaveFrequency);
            bodyWaveCycles = Mathf.Max(0f, bodyWaveCycles);
            fullImpactSpeed = Mathf.Max(0.1f, fullImpactSpeed);
            impactKick = Mathf.Max(0f, impactKick);
            impactSpring = Mathf.Max(0f, impactSpring);
            impactDamping = Mathf.Max(0f, impactDamping);
            contactRecoverySpeed = Mathf.Max(0f, contactRecoverySpeed);
            tentacleRows = Mathf.Clamp(tentacleRows, 8, 40);
            tentacleWaveForce = Mathf.Max(0f, tentacleWaveForce);
            tentacleWaveFrequency = Mathf.Max(0f, tentacleWaveFrequency);
            waterFlowDrag = Mathf.Max(0f, waterFlowDrag);
            wakeGatherForce = Mathf.Max(0f, wakeGatherForce);
            rootRigidFraction = Mathf.Clamp(rootRigidFraction, 0.03f, 0.45f);
            rootAnchorInset = Mathf.Clamp(rootAnchorInset, 0f, 0.3f);
        }

        private void LateUpdate()
        {
            InitializeIfNeeded();
            if (!initialized)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (deltaTime <= 0f)
            {
                return;
            }

            visualTime += deltaTime;
            Vector2 localVelocity = visualRoot.InverseTransformDirection(targetRigidbody.linearVelocity);
            Vector2 localAcceleration = Vector2.ClampMagnitude(
                (localVelocity - previousLocalVelocity) / deltaTime,
                18f);
            previousLocalVelocity = localVelocity;

            Vector2 pressureTarget = Vector2.ClampMagnitude(localVelocity / referenceSpeed, 1f);
            StepSpring(ref pressureShape, ref pressureShapeVelocity, pressureTarget, shapeSpring, shapeDamping, deltaTime);

            StepSpring(ref impactAmount, ref impactVelocity, sustainedImpact, impactSpring, impactDamping, deltaTime);
            impactAmount = Mathf.Clamp(impactAmount, -0.5f, 1.25f);
            sustainedImpact = Mathf.MoveTowards(sustainedImpact, 0f, deltaTime * contactRecoverySpeed);

            float pulse = Mathf.Sin(visualTime * idlePulseFrequency * Mathf.PI * 2f) * idlePulseAmplitude;
            BodySimulationSettings bodySettings = new BodySimulationSettings
            {
                SurfaceSpring = surfaceSpring,
                SurfaceDamping = surfaceDamping,
                SurfaceCohesion = surfaceCohesion,
                PressureStretch = pressureStretch,
                PressureTaper = pressureTaper,
                FrontCompression = frontCompression,
                RearElongation = rearElongation,
                WaveAmplitude = bodyWaveAmplitude,
                WaveFrequency = bodyWaveFrequency,
                WaveCycles = bodyWaveCycles,
                AccelerationInertia = accelerationInertia,
                MaximumOffset = maximumBodyOffset,
                ImpactCompression = impactCompression,
                DisplacedFluidBulge = displacedFluidBulge,
                ContactInfluence = contactInfluence,
                ContactRecoverySpeed = contactRecoverySpeed
            };
            bodyMesh.Simulate(
                pressureShape,
                localAcceleration,
                visualTime,
                pulse,
                bodySettings,
                deltaTime);

            UpdateBodyTransforms(pulse);

            float visualAngle = visualRoot.eulerAngles.z;
            float visualAngleDelta = Mathf.DeltaAngle(lastVisualAngle, visualAngle);
            lastVisualAngle = visualAngle;

            for (int i = 0; i < tentacles.Count; i++)
            {
                tentacles[i].Simulate(
                    localVelocity,
                    visualAngleDelta,
                    visualTime,
                    waterFlowDrag,
                    wakeGatherForce,
                    idleWaveScale,
                    tentacleWaveForce,
                    tentacleWaveFrequency,
                    referenceSpeed,
                    deltaTime);
            }

            for (int i = 0; i < tentacles.Count; i++)
            {
                tentacles[i].FinishSimulation();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.contactCount == 0)
            {
                return;
            }

            float strength = Mathf.Max(0.2f, Mathf.Clamp01(collision.relativeVelocity.magnitude / fullImpactSpeed));
            RegisterCollision(collision, strength, true);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision.contactCount == 0)
            {
                return;
            }

            RegisterCollision(collision, collisionStayCompression, false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            InitializeIfNeeded();
            if (!initialized)
            {
                return;
            }

            Vector2 center = targetRigidbody.worldCenterOfMass;
            Vector2 point = other.ClosestPoint(center);
            if ((point - center).sqrMagnitude < 0.0001f)
            {
                Vector2 fallbackDirection = center - (Vector2)other.bounds.center;
                point = center - fallbackDirection.normalized * 0.35f;
            }

            RegisterImpact(point - center, 0.16f, true);
            RegisterBodyContact(point, center - point, 0.16f, true, IsFlatCollider(other));
        }

        private void OnDestroy()
        {
            bodyMesh?.Dispose();
            bodyMesh = null;

            for (int i = 0; i < tentacles.Count; i++)
            {
                tentacles[i].Dispose();
            }

            tentacles.Clear();
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            targetRigidbody ??= GetComponent<Rigidbody2D>();
            visualRoot ??= transform;
            bodyRenderer ??= bodyRoot != null ? bodyRoot.GetComponentInChildren<SpriteRenderer>() : null;
            bodyRoot ??= bodyRenderer != null ? bodyRenderer.transform : null;

            if (targetRigidbody == null || visualRoot == null || bodyRoot == null || bodyRenderer == null || bodyRenderer.sprite == null)
            {
                return;
            }

            baseBodyPosition = bodyRoot.localPosition;
            baseBodyScale = bodyRoot.localScale;
            if (coreRoot != null)
            {
                baseCorePosition = coreRoot.localPosition;
                baseCoreScale = coreRoot.localScale;
            }

            bodyMesh = new SoftBodySpriteMesh(bodyRenderer, bodyMeshColumns, bodyMeshRows);
            AddTentacles(legRenderers, 0f);
            if (extractionTentacleRenderer != null && extractionTentacleRenderer.sprite != null)
            {
                tentacles.Add(new TentacleSpriteMesh(
                    extractionTentacleRenderer,
                    bodyRenderer,
                    transform,
                    Mathf.Max(48, tentacleRows),
                    rootRigidFraction,
                    rootAnchorInset,
                    2.55f,
                    true));
            }

            initialized = bodyMesh.IsValid;
            ResetMotionState();
        }

        private void AddTentacles(SpriteRenderer[] renderers, float phaseOffset)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sprite != null)
                {
                    tentacles.Add(new TentacleSpriteMesh(
                        renderers[i],
                        bodyRenderer,
                        transform,
                        tentacleRows,
                        rootRigidFraction,
                        rootAnchorInset,
                        phaseOffset + i * 0.85f));
                }
            }
        }

        private void ResetMotionState()
        {
            pressureShape = Vector2.zero;
            pressureShapeVelocity = Vector2.zero;
            impactAmount = 0f;
            impactVelocity = 0f;
            sustainedImpact = 0f;
            visualTime = 0f;
            lastVisualAngle = visualRoot != null ? visualRoot.eulerAngles.z : 0f;
            previousLocalVelocity = targetRigidbody != null && visualRoot != null
                ? (Vector2)visualRoot.InverseTransformDirection(targetRigidbody.linearVelocity)
                : Vector2.zero;
            bodyMesh?.Reset();

            for (int i = 0; i < tentacles.Count; i++)
            {
                tentacles[i].Reset();
            }
        }

        private void RegisterCollision(Collision2D collision, float strength, bool addKick)
        {
            InitializeIfNeeded();
            if (!initialized)
            {
                return;
            }

            Collider2D obstacle = ResolveObstacleCollider(collision);
            bool wideContact = IsFlatCollider(obstacle);
            Vector2 center = targetRigidbody.worldCenterOfMass;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                Vector2 expectedInward = center - contact.point;
                Vector2 contactInward = contact.normal;
                if (Vector2.Dot(contactInward, expectedInward) < 0f)
                {
                    contactInward = -contactInward;
                }

                RegisterBodyContact(
                    contact.point,
                    contactInward,
                    strength,
                    addKick && i == 0,
                    wideContact,
                    true);
            }

            Vector2 firstPoint = collision.GetContact(0).point;
            RegisterImpact(firstPoint - center, strength, addKick);
        }

        private void RegisterBodyContact(
            Vector2 worldPoint,
            Vector2 worldInward,
            float strength,
            bool addImpulse,
            bool wideContact,
            bool sustained = false)
        {
            if (bodyMesh == null || bodyRenderer == null)
            {
                return;
            }

            if (worldInward.sqrMagnitude < 0.0001f)
            {
                worldInward = visualRoot.up;
            }

            Vector2 localPoint = bodyRenderer.transform.InverseTransformPoint(worldPoint);
            Vector2 localInward = bodyRenderer.transform.InverseTransformDirection(worldInward.normalized);
            bodyMesh.AddContact(
                localPoint,
                localInward.normalized,
                Mathf.Clamp01(strength),
                addImpulse,
                wideContact,
                impactKick,
                sustained);
        }

        private void RegisterImpact(Vector2 worldOutward, float strength, bool addKick)
        {
            if (worldOutward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 localOutward = visualRoot.InverseTransformDirection(worldOutward.normalized);
            impactDirection = Vector2.Lerp(impactDirection, localOutward.normalized, 0.82f).normalized;
            sustainedImpact = Mathf.Max(sustainedImpact, Mathf.Clamp01(strength));
            if (addKick)
            {
                impactVelocity += Mathf.Clamp01(strength) * impactKick;
            }
        }

        private void UpdateBodyTransforms(float pulse)
        {
            Vector2 bodyLag = -pressureShape * 0.05f - impactDirection * (impactAmount * 0.022f);
            bodyRoot.localPosition = baseBodyPosition + (Vector3)bodyLag;
            bodyRoot.localScale = new Vector3(
                baseBodyScale.x * (1f + pulse * 0.52f),
                baseBodyScale.y * (1f - pulse * 0.38f),
                baseBodyScale.z);

            if (coreRoot == null)
            {
                return;
            }

            Vector2 coreLag = -pressureShape * 0.075f - impactDirection * (impactAmount * 0.075f);
            float corePulse = 1f - pulse * 0.8f + Mathf.Abs(impactAmount) * 0.025f;
            coreRoot.localPosition = baseCorePosition + (Vector3)coreLag;
            coreRoot.localScale = new Vector3(baseCoreScale.x * corePulse, baseCoreScale.y * corePulse, baseCoreScale.z);
        }

        private Collider2D ResolveObstacleCollider(Collision2D collision)
        {
            if (collision.collider != null && collision.collider.attachedRigidbody != targetRigidbody)
            {
                return collision.collider;
            }

            if (collision.otherCollider != null && collision.otherCollider.attachedRigidbody != targetRigidbody)
            {
                return collision.otherCollider;
            }

            return collision.collider != null ? collision.collider : collision.otherCollider;
        }

        private static bool IsFlatCollider(Collider2D collider)
        {
            return collider is BoxCollider2D ||
                   collider is EdgeCollider2D ||
                   collider is PolygonCollider2D ||
                   collider is CompositeCollider2D;
        }

        private static void StepSpring(
            ref Vector2 value,
            ref Vector2 velocity,
            Vector2 target,
            float spring,
            float damping,
            float deltaTime)
        {
            velocity += ((target - value) * spring - velocity * damping) * deltaTime;
            value += velocity * deltaTime;
        }

        private static void StepSpring(
            ref float value,
            ref float velocity,
            float target,
            float spring,
            float damping,
            float deltaTime)
        {
            velocity += ((target - value) * spring - velocity * damping) * deltaTime;
            value += velocity * deltaTime;
        }

        private struct BodySimulationSettings
        {
            public float SurfaceSpring;
            public float SurfaceDamping;
            public float SurfaceCohesion;
            public float PressureStretch;
            public float PressureTaper;
            public float FrontCompression;
            public float RearElongation;
            public float WaveAmplitude;
            public float WaveFrequency;
            public float WaveCycles;
            public float AccelerationInertia;
            public float MaximumOffset;
            public float ImpactCompression;
            public float DisplacedFluidBulge;
            public float ContactInfluence;
            public float ContactRecoverySpeed;
        }

        private sealed class SoftBodySpriteMesh : IDisposable
        {
            private const int MaximumContacts = 12;

            private readonly SpriteGridMesh grid;
            private readonly Vector2[] positions;
            private readonly Vector2[] velocities;
            private readonly Vector2[] targets;
            private readonly Vector2[] forces;
            private readonly float[] contactPins;
            private readonly Vector3[] renderedVertices;
            private readonly List<ContactSample> contacts = new List<ContactSample>(MaximumContacts);
            private readonly Vector2 center;
            private readonly Vector2 extents;
            private readonly int columns;
            private readonly int rows;
            private readonly float minimumExtent;

            public SoftBodySpriteMesh(SpriteRenderer source, int meshColumns, int meshRows)
            {
                columns = Mathf.Max(1, meshColumns);
                rows = Mathf.Max(1, meshRows);
                grid = new SpriteGridMesh(source, columns, rows, "Fluid Pudding Mesh");
                int vertexCount = grid.IsValid ? grid.BaseVertices.Length : 0;
                positions = new Vector2[vertexCount];
                velocities = new Vector2[vertexCount];
                targets = new Vector2[vertexCount];
                forces = new Vector2[vertexCount];
                contactPins = new float[vertexCount];
                renderedVertices = new Vector3[vertexCount];
                center = grid.Bounds.center;
                extents = grid.Bounds.extents;
                minimumExtent = Mathf.Max(0.001f, Mathf.Min(extents.x, extents.y));
                Reset();
            }

            public bool IsValid => grid.IsValid;

            public void Reset()
            {
                contacts.Clear();
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = grid.BaseVertices[i];
                    targets[i] = positions[i];
                    velocities[i] = Vector2.zero;
                    contactPins[i] = 0f;
                    renderedVertices[i] = grid.BaseVertices[i];
                }

                if (grid.IsValid)
                {
                    grid.ApplyVertices(grid.BaseVertices);
                }
            }

            public void AddContact(
                Vector2 point,
                Vector2 inward,
                float strength,
                bool addImpulse,
                bool wideContact,
                float impulseMagnitude,
                bool sustained)
            {
                if (!grid.IsValid || inward.sqrMagnitude < 0.0001f)
                {
                    return;
                }

                inward.Normalize();
                AddSingleContact(point, inward, strength, sustained);

                if (wideContact)
                {
                    Vector2 tangent = new Vector2(-inward.y, inward.x);
                    float spread = minimumExtent * 0.34f;
                    AddSingleContact(point + tangent * spread, inward, strength * 0.72f, sustained);
                    AddSingleContact(point - tangent * spread, inward, strength * 0.72f, sustained);
                }

                if (addImpulse)
                {
                    ApplyContactImpulse(point, inward, strength, impulseMagnitude);
                }
            }

            public void Simulate(
                Vector2 pressureVector,
                Vector2 localAcceleration,
                float time,
                float pulse,
                BodySimulationSettings settings,
                float deltaTime)
            {
                if (!grid.IsValid)
                {
                    return;
                }

                BuildTargets(pressureVector, localAcceleration, time, pulse, settings);

                const int substeps = 2;
                float stepTime = deltaTime / substeps;
                for (int step = 0; step < substeps; step++)
                {
                    BuildForces(settings);
                    for (int i = 0; i < positions.Length; i++)
                    {
                        velocities[i] += forces[i] * stepTime;
                        velocities[i] *= 1f / (1f + settings.SurfaceDamping * stepTime);
                        velocities[i] = Vector2.ClampMagnitude(velocities[i], 4.5f);
                        positions[i] += velocities[i] * stepTime;
                        if (contactPins[i] > 0f)
                        {
                            positions[i] = Vector2.Lerp(positions[i], targets[i], contactPins[i]);
                            velocities[i] *= 1f - contactPins[i];
                        }

                        Vector2 basePosition = grid.BaseVertices[i];
                        Vector2 displacement = positions[i] - basePosition;
                        float maximumOffset = minimumExtent * settings.MaximumOffset;
                        if (displacement.sqrMagnitude > maximumOffset * maximumOffset)
                        {
                            positions[i] = basePosition + displacement.normalized * maximumOffset;
                            velocities[i] *= 0.55f;
                        }
                    }
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    renderedVertices[i] = new Vector3(positions[i].x, positions[i].y, grid.BaseVertices[i].z);
                }

                grid.ApplyVertices(renderedVertices);
                DecayContacts(settings.ContactRecoverySpeed, deltaTime);
            }

            public void Dispose()
            {
                grid.Dispose();
            }

            private void BuildTargets(
                Vector2 pressureVector,
                Vector2 localAcceleration,
                float time,
                float pulse,
                BodySimulationSettings settings)
            {
                float pressure = Mathf.Clamp01(pressureVector.magnitude);
                Vector2 direction = pressure > 0.0001f ? pressureVector / pressure : Vector2.up;
                float halfAxis = Mathf.Max(0.001f, Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y);
                Vector2 normalizedAcceleration = Vector2.ClampMagnitude(localAcceleration / 18f, 1f);

                for (int i = 0; i < grid.BaseVertices.Length; i++)
                {
                    Vector2 basePosition = grid.BaseVertices[i];
                    Vector2 relative = basePosition - center;
                    float along = Vector2.Dot(relative, direction);
                    float normalizedAlong = Mathf.Clamp(along / halfAxis, -1f, 1f);
                    Vector2 perpendicular = relative - direction * along;

                    float front = Mathf.Max(0f, normalizedAlong);
                    float rear = Mathf.Max(0f, -normalizedAlong);
                    along *= 1f + pressure * settings.PressureStretch;
                    along *= 1f - pressure * settings.FrontCompression * front * front;
                    along *= 1f + pressure * settings.RearElongation * rear * rear;
                    perpendicular *= 1f - pressure * settings.PressureTaper * rear * rear;

                    Vector2 result = center + direction * along + perpendicular;
                    Vector2 normalizedRadius = new Vector2(
                        relative.x / Mathf.Max(0.001f, extents.x),
                        relative.y / Mathf.Max(0.001f, extents.y));
                    float radialLength = normalizedRadius.magnitude;
                    Vector2 radialDirection = radialLength > 0.001f ? normalizedRadius / radialLength : Vector2.zero;
                    float surfaceWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, radialLength));
                    float contactPin = GetContactPin(basePosition, settings.ContactInfluence);
                    contactPins[i] = contactPin;

                    float wavePhase =
                        time * settings.WaveFrequency * Mathf.PI * 2f -
                        normalizedAlong * settings.WaveCycles * Mathf.PI * 2f +
                        Mathf.Atan2(normalizedRadius.y, normalizedRadius.x) * 0.22f;
                    float resistance = 0.16f + pressure * 0.84f;
                    float wave = Mathf.Sin(wavePhase) * settings.WaveAmplitude * minimumExtent * resistance * surfaceWeight;
                    result += radialDirection * wave * (1f - contactPin);
                    result -= normalizedAcceleration * (settings.AccelerationInertia * minimumExtent * surfaceWeight * (1f - contactPin));

                    result -= center;
                    result.x *= 1f + pulse * (1f - contactPin);
                    result.y *= 1f - pulse * 0.72f * (1f - contactPin);
                    result += center;
                    ApplyContactFields(ref result, radialDirection, settings);

                    Vector2 targetOffset = result - basePosition;
                    float maximumTargetOffset = minimumExtent * settings.MaximumOffset;
                    targets[i] = targetOffset.sqrMagnitude > maximumTargetOffset * maximumTargetOffset
                        ? basePosition + targetOffset.normalized * maximumTargetOffset
                        : result;
                }
            }

            private float GetContactPin(Vector2 position, float influence)
            {
                float radius = minimumExtent * influence;
                float pin = 0f;
                for (int i = 0; i < contacts.Count; i++)
                {
                    ContactSample contact = contacts[i];
                    if (!contact.Sustained || Time.time - contact.LastRefreshTime > 0.12f)
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(position, contact.Point) / Mathf.Max(0.001f, radius);
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance));
                    pin = Mathf.Max(pin, weight * Mathf.Clamp01(contact.Strength * 4f));
                }

                return pin;
            }

            private void ApplyContactFields(
                ref Vector2 position,
                Vector2 radialDirection,
                BodySimulationSettings settings)
            {
                float radius = minimumExtent * settings.ContactInfluence;
                for (int i = 0; i < contacts.Count; i++)
                {
                    ContactSample contact = contacts[i];
                    float normalizedDistance = Vector2.Distance(position, contact.Point) / Mathf.Max(0.001f, radius);
                    if (normalizedDistance >= 1.35f)
                    {
                        continue;
                    }

                    if (contact.Sustained)
                    {
                        if (Time.time - contact.LastRefreshTime <= 0.12f)
                        {
                            float contactWeight = 1f - Mathf.SmoothStep(0f, 1f,
                                Mathf.Clamp01(normalizedDistance));
                            float planeDistance = Vector2.Dot(position - contact.Point, contact.Inward);
                            float adjustment = Mathf.Clamp(0.01f - planeDistance,
                                -minimumExtent * 0.2f, minimumExtent * 0.4f);
                            position += contact.Inward * (adjustment * contactWeight);
                        }

                        continue;
                    }

                    float dent = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedDistance));
                    dent *= dent;
                    float depth = minimumExtent * settings.ImpactCompression * contact.Strength;
                    position += contact.Inward * (depth * dent);

                    float ringDistance = (normalizedDistance - 0.82f) / 0.23f;
                    float ring = Mathf.Exp(-(ringDistance * ringDistance));
                    Vector2 tangent = new Vector2(-contact.Inward.y, contact.Inward.x);
                    float side = Mathf.Sign(Vector2.Dot(position - contact.Point, tangent));
                    position += radialDirection * (depth * settings.DisplacedFluidBulge * ring);
                    position += tangent * (side * depth * settings.DisplacedFluidBulge * ring * 0.28f);
                }
            }

            private void BuildForces(BodySimulationSettings settings)
            {
                int vertexColumns = columns + 1;
                int vertexRows = rows + 1;
                for (int row = 0; row < vertexRows; row++)
                {
                    for (int column = 0; column < vertexColumns; column++)
                    {
                        int index = row * vertexColumns + column;
                        Vector2 offset = positions[index] - (Vector2)grid.BaseVertices[index];
                        Vector2 neighborOffset = Vector2.zero;
                        int neighborCount = 0;

                        AddNeighborOffset(column - 1, row, vertexColumns, vertexRows, ref neighborOffset, ref neighborCount);
                        AddNeighborOffset(column + 1, row, vertexColumns, vertexRows, ref neighborOffset, ref neighborCount);
                        AddNeighborOffset(column, row - 1, vertexColumns, vertexRows, ref neighborOffset, ref neighborCount);
                        AddNeighborOffset(column, row + 1, vertexColumns, vertexRows, ref neighborOffset, ref neighborCount);

                        Vector2 cohesion = neighborCount > 0
                            ? ((neighborOffset / neighborCount) - offset) * settings.SurfaceCohesion
                            : Vector2.zero;
                        forces[index] = (targets[index] - positions[index]) * settings.SurfaceSpring + cohesion;
                    }
                }
            }

            private void AddNeighborOffset(
                int column,
                int row,
                int vertexColumns,
                int vertexRows,
                ref Vector2 sum,
                ref int count)
            {
                if (column < 0 || column >= vertexColumns || row < 0 || row >= vertexRows)
                {
                    return;
                }

                int index = row * vertexColumns + column;
                sum += positions[index] - (Vector2)grid.BaseVertices[index];
                count++;
            }

            private void AddSingleContact(Vector2 point, Vector2 inward, float strength, bool sustained)
            {
                float mergeDistance = minimumExtent * 0.14f;
                float mergeDistanceSquared = mergeDistance * mergeDistance;
                ContactSample match = null;
                for (int i = 0; i < contacts.Count; i++)
                {
                    ContactSample candidate = contacts[i];
                    if ((candidate.Point - point).sqrMagnitude <= mergeDistanceSquared &&
                        Vector2.Dot(candidate.Inward, inward) > 0.72f)
                    {
                        match = candidate;
                        break;
                    }
                }

                if (match == null)
                {
                    if (contacts.Count >= MaximumContacts)
                    {
                        match = contacts[0];
                        for (int i = 1; i < contacts.Count; i++)
                        {
                            if (contacts[i].Strength < match.Strength)
                            {
                                match = contacts[i];
                            }
                        }
                    }
                    else
                    {
                        match = new ContactSample();
                        contacts.Add(match);
                    }
                }

                match.Point = Vector2.Lerp(match.Point, point, match.Strength > 0f ? 0.55f : 1f);
                match.Inward = Vector2.Lerp(match.Inward, inward, 0.72f).normalized;
                match.Strength = Mathf.Max(match.Strength, Mathf.Clamp01(strength));
                match.Sustained = sustained;
                match.LastRefreshTime = Time.time;
            }

            private void ApplyContactImpulse(Vector2 point, Vector2 inward, float strength, float magnitude)
            {
                float radius = minimumExtent * 0.9f;
                for (int i = 0; i < positions.Length; i++)
                {
                    float normalizedDistance = Vector2.Distance(positions[i], point) / radius;
                    if (normalizedDistance >= 1f)
                    {
                        continue;
                    }

                    float weight = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    velocities[i] += inward * (magnitude * strength * weight);
                }
            }

            private void DecayContacts(float recoverySpeed, float deltaTime)
            {
                for (int i = contacts.Count - 1; i >= 0; i--)
                {
                    if (!contacts[i].Sustained || Time.time - contacts[i].LastRefreshTime > 0.12f)
                    {
                        contacts[i].Strength = Mathf.MoveTowards(contacts[i].Strength, 0f, recoverySpeed * deltaTime);
                    }
                    if (contacts[i].Strength <= 0.001f)
                    {
                        contacts.RemoveAt(i);
                    }
                }
            }

            private sealed class ContactSample
            {
                public Vector2 Point;
                public Vector2 Inward = Vector2.up;
                public float Strength;
                public bool Sustained;
                public float LastRefreshTime;
            }
        }

        private sealed class TentacleSpriteMesh : IDisposable
        {
            private readonly SpriteGridMesh grid;
            private readonly Vector3[] renderedVertices;
            private readonly Vector2[] restNodes;
            private readonly Vector2[] nodes;
            private readonly Vector2[] renderNodes;
            private readonly Vector2[] worldNodes;
            private readonly float[] worldSegmentLengths;
            private readonly float[] worldRestAngles;
            private readonly float[] guideAngles;
            private readonly float[] guideAngularVelocities;
            private readonly float[] restSegmentLengths;
            private readonly Vector2[] renderedNormals;
            private readonly float[] segmentAngles;
            private readonly float[] segmentAngularSpeeds;
            private readonly RaycastHit2D[] environmentHits = new RaycastHit2D[12];
            private readonly Collider2D[] nearbyColliders = new Collider2D[64];
            private readonly List<EdgeCollider2D> nearbyEdges = new List<EdgeCollider2D>(8);
            private readonly List<BoxCollider2D> nearbyBoxes = new List<BoxCollider2D>(8);
            private readonly Dictionary<EdgeCollider2D, Vector2[]> edgePoints =
                new Dictionary<EdgeCollider2D, Vector2[]>();
            private readonly LineRenderer line;
            private readonly Material lineMaterial;
            private readonly Transform bodyAnchorTransform;
            private readonly Transform collisionOwner;
            private readonly Vector2 bodyAnchorLocal;
            private readonly Vector2 bodyCenterLocal;
            private readonly float rootRigidFraction;
            private readonly int rows;
            private readonly float phase;
            private readonly float top;
            private readonly float height;
            private readonly float centerX;
            private Vector2 rootAnchor;
            private Vector2 rootOffset;
            private float initialBodyAngle;
            private float bodyAngle;
            private readonly float totalWorldLength;

            public TentacleSpriteMesh(
                SpriteRenderer source,
                SpriteRenderer body,
                Transform owner,
                int meshRows,
                float rigidFraction,
                float anchorInset,
                float wavePhase,
                bool useLine = false)
            {
                rows = Mathf.Max(2, meshRows);
                rootRigidFraction = Mathf.Clamp(rigidFraction, 0.03f, 0.45f);
                phase = wavePhase;
                grid = new SpriteGridMesh(source, 1, rows, "Rope Physics Mesh", !useLine);
                if (useLine && grid.IsValid)
                {
                    line = source.gameObject.AddComponent<LineRenderer>();
                    Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader != null)
                    {
                        lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                        line.sharedMaterial = lineMaterial;
                    }

                    line.useWorldSpace = false;
                    line.positionCount = rows + 1;
                    line.numCapVertices = 5;
                    line.numCornerVertices = 5;
                    line.widthMultiplier = 0.13f;
                    line.widthCurve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(0.7f, 0.65f),
                        new Keyframe(1f, 0.1f));
                    line.startColor = new Color(0.96f, 0.32f, 0.1f, 1f);
                    line.endColor = new Color(1f, 0.39f, 0.18f, 0.9f);
                    line.sortingLayerID = source.sortingLayerID;
                    line.sortingOrder = source.sortingOrder;
                }
                renderedVertices = grid.IsValid ? new Vector3[grid.BaseVertices.Length] : Array.Empty<Vector3>();
                restNodes = new Vector2[rows + 1];
                nodes = new Vector2[rows + 1];
                renderNodes = new Vector2[rows + 1];
                worldNodes = new Vector2[rows + 1];
                worldSegmentLengths = new float[rows + 1];
                worldRestAngles = new float[rows + 1];
                guideAngles = new float[rows + 1];
                guideAngularVelocities = new float[rows + 1];
                restSegmentLengths = new float[rows + 1];
                renderedNormals = new Vector2[rows + 1];
                segmentAngles = new float[rows + 1];
                segmentAngularSpeeds = new float[rows + 1];
                top = grid.Bounds.max.y;
                height = Mathf.Max(0.001f, grid.Bounds.size.y);
                centerX = grid.Bounds.center.x;
                bodyAnchorTransform = body != null ? body.transform : null;
                collisionOwner = owner;
                bodyCenterLocal = body != null && body.sprite != null
                    ? (Vector2)body.sprite.bounds.center
                    : Vector2.zero;

                for (int i = 0; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    restNodes[i] = new Vector2(centerX, top - height * t);
                }

                TraceSpriteCenterline(source.sprite);
                for (int i = 1; i <= rows; i++)
                {
                    Vector2 restStep = restNodes[i] - restNodes[i - 1];
                    restSegmentLengths[i] = restStep.magnitude;
                    worldSegmentLengths[i] = Vector2.Distance(
                        source.transform.TransformPoint(restNodes[i - 1]),
                        source.transform.TransformPoint(restNodes[i]));
                    totalWorldLength += worldSegmentLengths[i];
                }

                Vector2 rootWorld = source != null
                    ? (Vector2)source.transform.TransformPoint(restNodes[0])
                    : Vector2.zero;
                if (bodyAnchorTransform != null)
                {
                    Vector2 rawAnchor = bodyAnchorTransform.InverseTransformPoint(rootWorld);
                    bodyAnchorLocal = Vector2.MoveTowards(rawAnchor, bodyCenterLocal, Mathf.Max(0f, anchorInset));
                }
                else
                {
                    bodyAnchorLocal = restNodes[0];
                }
                rootAnchor = restNodes[0];

                Reset();
            }

            public void Reset()
            {
                UpdateRootAnchor();
                initialBodyAngle = grid.SourceTransform.eulerAngles.z;
                bodyAngle = initialBodyAngle;
                nearbyEdges.Clear();
                nearbyBoxes.Clear();
                for (int i = 0; i <= rows; i++)
                {
                    Vector2 anchoredRest = GetAnchoredRestNode(i);
                    nodes[i] = anchoredRest;
                    worldNodes[i] = grid.SourceTransform.TransformPoint(anchoredRest);
                    renderedNormals[i] = Vector2.right;
                    segmentAngles[i] = 0f;
                    segmentAngularSpeeds[i] = 0f;
                    guideAngles[i] = bodyAngle;
                    guideAngularVelocities[i] = 0f;
                    if (i > 0)
                    {
                        Vector2 restDirection = grid.SourceTransform.TransformDirection(
                            restNodes[i] - restNodes[i - 1]);
                        worldRestAngles[i] = Vector2.SignedAngle(Vector2.down, restDirection);
                        segmentAngles[i] = worldRestAngles[i];
                    }
                }

                if (grid.IsValid)
                {
                    FinishSimulation();
                }
            }

            public void Simulate(
                Vector2 localVelocity,
                float visualAngleDelta,
                float time,
                float flowDrag,
                float gatherForce,
                float stationaryWaveScale,
                float waveForce,
                float waveFrequency,
                float referenceSpeed,
                float deltaTime)
            {
                if (!grid.IsValid)
                {
                    return;
                }

                UpdateRootAnchor();
                Transform source = grid.SourceTransform;
                bodyAngle += visualAngleDelta;
                float speedRatio = Mathf.Clamp01(localVelocity.magnitude /
                    Mathf.Max(0.1f, referenceSpeed));
                Vector2 worldVelocity = source.TransformDirection(localVelocity);
                float flowAngle = worldVelocity.sqrMagnitude > 0.0001f
                    ? Vector2.SignedAngle(Vector2.down, -worldVelocity.normalized)
                    : 0f;
                int pinnedCount = Mathf.Clamp(Mathf.RoundToInt(rows * rootRigidFraction),
                    1, rows - 2);
                worldNodes[0] = source.TransformPoint(GetAnchoredRestNode(0));
                nodes[0] = source.InverseTransformPoint(worldNodes[0]);
                CollectNearbySurfaces();

                for (int i = 1; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    if (i <= pinnedCount)
                    {
                        segmentAngles[i] = worldRestAngles[i] +
                            (bodyAngle - initialBodyAngle);
                        segmentAngularSpeeds[i] = 0f;
                        guideAngles[i] = bodyAngle;
                        guideAngularVelocities[i] = 0f;
                    }
                    else
                    {
                        float guideTime = Mathf.Lerp(0.012f, 0.042f,
                            Mathf.Pow(t, 1.35f));
                        guideAngles[i] = Mathf.SmoothDamp(
                            guideAngles[i],
                            guideAngles[i - 1],
                            ref guideAngularVelocities[i],
                            guideTime,
                            450f,
                            deltaTime);

                        float desiredAngle = worldRestAngles[i] +
                            (guideAngles[i] - initialBodyAngle);
                        if (speedRatio > 0.001f)
                        {
                            float flowError = Mathf.Clamp(
                                Mathf.DeltaAngle(desiredAngle, flowAngle), -90f, 90f);
                            desiredAngle += flowError * speedRatio * t * t *
                                Mathf.Clamp01(flowDrag * gatherForce * 0.1f);
                        }

                        float wave = Mathf.Sin(time * waveFrequency * Mathf.PI * 2f -
                            t * Mathf.PI * 2.2f + phase);
                        desiredAngle += wave * waveForce * 2.3f * t * t *
                            Mathf.Lerp(stationaryWaveScale, 1f, speedRatio);
                        float responseTime = Mathf.Lerp(0.045f, 0.13f,
                            Mathf.Pow(t, 1.3f));
                        segmentAngles[i] = Mathf.SmoothDamp(
                            segmentAngles[i],
                            desiredAngle,
                            ref segmentAngularSpeeds[i],
                            responseTime,
                            450f,
                            deltaTime);

                        float allowedBend = Mathf.Lerp(16f, 23f, t);
                        float bend = Mathf.DeltaAngle(segmentAngles[i - 1],
                            segmentAngles[i]);
                        segmentAngles[i] = segmentAngles[i - 1] +
                            Mathf.Clamp(bend, -allowedBend, allowedBend);
                    }

                    float radians = segmentAngles[i] * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Sin(radians),
                        -Mathf.Cos(radians));
                    direction = ResolveWorldCollision(i, direction);
                    worldNodes[i] = worldNodes[i - 1] +
                        direction * worldSegmentLengths[i];
                    nodes[i] = source.InverseTransformPoint(worldNodes[i]);
                }

                KeepChainOutsideSurfaces(pinnedCount, source);
            }

            private void CollectNearbySurfaces()
            {
                nearbyEdges.Clear();
                nearbyBoxes.Clear();
                ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
                int count = Physics2D.OverlapCircle(worldNodes[0],
                    totalWorldLength + 0.3f, filter, nearbyColliders);
                for (int i = 0; i < count; i++)
                {
                    if (nearbyColliders[i] is EdgeCollider2D edge &&
                        !edge.transform.IsChildOf(collisionOwner))
                    {
                        nearbyEdges.Add(edge);
                    }
                    else if (nearbyColliders[i] is BoxCollider2D box &&
                        !box.transform.IsChildOf(collisionOwner))
                    {
                        nearbyBoxes.Add(box);
                    }
                }
            }

            private void KeepChainOutsideSurfaces(int pinnedCount, Transform source)
            {
                const float radius = 0.1f;
                for (int i = pinnedCount + 1; i <= rows; i++)
                {
                    Vector2 start = worldNodes[i - 1];
                    float radians = segmentAngles[i] * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Sin(radians),
                        -Mathf.Cos(radians));
                    float length = worldSegmentLengths[i];
                    Vector2 candidate = start + direction * length;

                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int edgeIndex = 0; edgeIndex < nearbyEdges.Count; edgeIndex++)
                        {
                            GetEdgeSurface(nearbyEdges[edgeIndex], candidate,
                                worldNodes[0], out Vector2 surface, out Vector2 normal);
                            ConstrainToSurface(ref candidate, start, direction, length,
                                surface, normal, radius);
                        }

                        for (int boxIndex = 0; boxIndex < nearbyBoxes.Count; boxIndex++)
                        {
                            if (GetBoxSurface(nearbyBoxes[boxIndex], candidate,
                                worldNodes[0], length + radius,
                                out Vector2 surface, out Vector2 normal))
                            {
                                ConstrainToSurface(ref candidate, start, direction, length,
                                    surface, normal, radius);
                            }
                        }
                    }

                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int edgeIndex = 0; edgeIndex < nearbyEdges.Count; edgeIndex++)
                        {
                            GetEdgeSurface(nearbyEdges[edgeIndex], candidate,
                                worldNodes[0], out Vector2 surface, out Vector2 normal);
                            Vector2 tangent = new Vector2(-normal.y, normal.x);
                            if (Mathf.Abs(Vector2.Dot(candidate - surface, tangent)) <=
                                length * 2f + radius)
                            {
                                PushOutside(ref candidate, surface, normal);
                            }
                        }

                        for (int boxIndex = 0; boxIndex < nearbyBoxes.Count; boxIndex++)
                        {
                            if (GetBoxSurface(nearbyBoxes[boxIndex], candidate,
                                worldNodes[0], length + radius,
                                out Vector2 surface, out Vector2 normal))
                            {
                                PushOutside(ref candidate, surface, normal);
                            }
                        }
                    }

                    worldNodes[i] = candidate;
                    nodes[i] = source.InverseTransformPoint(candidate);
                    Vector2 finalDirection = candidate - start;
                    if (finalDirection.sqrMagnitude > 0.0001f)
                    {
                        float worldAngle = Vector2.SignedAngle(Vector2.down,
                            finalDirection);
                        segmentAngles[i] += Mathf.DeltaAngle(segmentAngles[i], worldAngle);
                    }
                }
            }

            private static void PushOutside(ref Vector2 candidate, Vector2 surface,
                Vector2 normal)
            {
                if (normal.sqrMagnitude < 0.5f)
                {
                    return;
                }

                float correction = 0.02f - Vector2.Dot(candidate - surface, normal);
                if (correction > 0f)
                {
                    candidate += normal * Mathf.Min(correction, 0.05f);
                }
            }

            private void ConstrainToSurface(ref Vector2 candidate, Vector2 start,
                Vector2 direction, float length, Vector2 surface, Vector2 normal,
                float radius)
            {
                if (normal.sqrMagnitude < 0.5f ||
                    Vector2.Dot(candidate - surface, normal) >= radius)
                {
                    return;
                }

                Vector2 tangent = new Vector2(-normal.y, normal.x);
                float normalLength = Mathf.Clamp(radius -
                    Vector2.Dot(start - surface, normal), -length, length);
                float tangentSide = Vector2.Dot(direction, tangent);
                if (Mathf.Abs(tangentSide) < 0.05f)
                {
                    tangentSide = phase < 1.3f ? 1f : -1f;
                }

                float tangentLength = Mathf.Sqrt(Mathf.Max(0f,
                    length * length - normalLength * normalLength));
                candidate = start + normal * normalLength +
                    tangent * Mathf.Sign(tangentSide) * tangentLength;
            }

            private bool GetBoxSurface(BoxCollider2D box, Vector2 candidate,
                Vector2 root, float margin, out Vector2 surface, out Vector2 normal)
            {
                Vector2 half = box.size * 0.5f;
                Vector2 localRoot = box.transform.InverseTransformPoint(root);
                Vector2 localCandidate = box.transform.InverseTransformPoint(candidate);
                localRoot -= box.offset;
                localCandidate -= box.offset;

                Vector2 faceNormal;
                Vector2 facePoint;
                if (localRoot.x < -half.x &&
                    (localRoot.y >= -half.y && localRoot.y <= half.y ||
                     -half.x - localRoot.x >= Mathf.Abs(localRoot.y) - half.y))
                {
                    faceNormal = Vector2.left;
                    facePoint = new Vector2(-half.x, Mathf.Clamp(localCandidate.y, -half.y, half.y));
                    if (Mathf.Abs(localCandidate.y) > half.y + margin)
                    {
                        surface = normal = Vector2.zero;
                        return false;
                    }
                }
                else if (localRoot.x > half.x &&
                    (localRoot.y >= -half.y && localRoot.y <= half.y ||
                     localRoot.x - half.x >= Mathf.Abs(localRoot.y) - half.y))
                {
                    faceNormal = Vector2.right;
                    facePoint = new Vector2(half.x, Mathf.Clamp(localCandidate.y, -half.y, half.y));
                    if (Mathf.Abs(localCandidate.y) > half.y + margin)
                    {
                        surface = normal = Vector2.zero;
                        return false;
                    }
                }
                else
                {
                    faceNormal = localRoot.y >= 0f ? Vector2.up : Vector2.down;
                    facePoint = new Vector2(Mathf.Clamp(localCandidate.x, -half.x, half.x),
                        faceNormal.y * half.y);
                    if (Mathf.Abs(localCandidate.x) > half.x + margin)
                    {
                        surface = normal = Vector2.zero;
                        return false;
                    }
                }

                surface = box.transform.TransformPoint(facePoint + box.offset);
                normal = box.transform.TransformDirection(faceNormal).normalized;
                return true;
            }

            private Vector2 ResolveWorldCollision(int index, Vector2 direction)
            {
                const float radius = 0.1f;
                Vector2 start = worldNodes[index - 1];
                float length = worldSegmentLengths[index];
                RaycastHit2D hit = FindEnvironmentHit(start, direction, length,
                    radius);
                Vector2 candidate = start + direction * length;
                Vector2 fromRoot = candidate - worldNodes[0];
                if (fromRoot.sqrMagnitude > 0.0001f)
                {
                    RaycastHit2D edgeHit = FindEnvironmentHit(worldNodes[0],
                        fromRoot.normalized, fromRoot.magnitude,
                        radius, true);
                    if (edgeHit.collider != null)
                    {
                        hit = edgeHit;
                        EdgeCollider2D crossedEdge = (EdgeCollider2D)edgeHit.collider;
                        if (!nearbyEdges.Contains(crossedEdge))
                        {
                            nearbyEdges.Add(crossedEdge);
                        }
                    }
                    else if (hit.collider == null)
                    {
                        hit = FindEnvironmentHit(worldNodes[0],
                            fromRoot.normalized, fromRoot.magnitude,
                            radius);
                    }
                }

                if (hit.collider == null)
                {
                    return direction;
                }

                Vector2 candidatePosition = start + direction * length;
                Vector2 surface;
                Vector2 normal;
                if (hit.collider is EdgeCollider2D edge)
                {
                    GetEdgeSurface(edge, candidatePosition, worldNodes[0],
                        out surface, out normal);
                }
                else
                {
                    surface = hit.point;
                    normal = hit.normal.normalized;
                }

                if (normal.sqrMagnitude < 0.5f ||
                    Vector2.Dot(candidatePosition - surface, normal) >= radius)
                {
                    return direction;
                }

                Vector2 tangent = new Vector2(-normal.y, normal.x);
                float tangentSide = Vector2.Dot(direction, tangent);
                if (Mathf.Abs(tangentSide) < 0.08f)
                {
                    float restRadians = worldRestAngles[index] * Mathf.Deg2Rad;
                    Vector2 restDirection = new Vector2(
                        Mathf.Sin(restRadians), -Mathf.Cos(restRadians));
                    tangentSide = Vector2.Dot(restDirection, tangent);
                    if (Mathf.Abs(tangentSide) < 0.08f)
                    {
                        tangentSide = phase < 1.3f ? 1f : -1f;
                    }
                }

                float startDistance = Vector2.Dot(start - surface, normal);
                float normalShare = Mathf.Clamp((radius - startDistance) /
                    Mathf.Max(0.001f, length), -0.96f, 0.96f);
                Vector2 redirected = tangent * Mathf.Sign(tangentSide) *
                    Mathf.Sqrt(1f - normalShare * normalShare) +
                    normal * normalShare;
                float newAngle = Vector2.SignedAngle(Vector2.down, redirected);
                segmentAngles[index] += Mathf.DeltaAngle(segmentAngles[index], newAngle);
                segmentAngularSpeeds[index] = 0f;
                return redirected;
            }

            private void GetEdgeSurface(
                EdgeCollider2D edge, Vector2 position, Vector2 root,
                out Vector2 surface, out Vector2 normal)
            {
                if (!edgePoints.TryGetValue(edge, out Vector2[] points))
                {
                    points = edge.points;
                    if (edgePoints.Count > 48)
                    {
                        edgePoints.Clear();
                    }

                    edgePoints.Add(edge, points);
                }

                surface = position;
                normal = Vector2.zero;
                float closestDistance = float.PositiveInfinity;
                for (int i = 1; i < points.Length; i++)
                {
                    Vector2 first = edge.transform.TransformPoint(points[i - 1]);
                    Vector2 second = edge.transform.TransformPoint(points[i]);
                    Vector2 segment = second - first;
                    float lengthSquared = segment.sqrMagnitude;
                    if (lengthSquared < 0.000001f)
                    {
                        continue;
                    }

                    float fraction = Mathf.Clamp01(Vector2.Dot(position - first, segment) / lengthSquared);
                    Vector2 point = first + segment * fraction;
                    float distance = (position - point).sqrMagnitude;
                    if (distance >= closestDistance)
                    {
                        continue;
                    }

                    closestDistance = distance;
                    surface = point;
                    normal = new Vector2(-segment.y, segment.x).normalized;
                    if (Vector2.Dot(normal, root - point) < 0f)
                    {
                        normal = -normal;
                    }
                }
            }

            private RaycastHit2D FindEnvironmentHit(
                Vector2 start, Vector2 direction, float distance, float radius,
                bool edgeOnly = false)
            {
                ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
                int count = Physics2D.CircleCast(start, radius, direction, filter,
                    environmentHits, distance);
                RaycastHit2D nearest = default;
                float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit2D hit = environmentHits[i];
                    if (hit.collider == null || hit.collider.isTrigger ||
                        (edgeOnly && !(hit.collider is EdgeCollider2D)) ||
                        hit.collider.transform.IsChildOf(collisionOwner) ||
                        (!(hit.collider is EdgeCollider2D) &&
                            Vector2.Dot(hit.normal, direction) > -0.025f) ||
                        hit.distance >= nearestDistance)
                    {
                        continue;
                    }

                    nearest = hit;
                    nearestDistance = hit.distance;
                }

                return nearest;
            }

            public void FinishSimulation()
            {
                if (line != null)
                {
                    for (int i = 0; i <= rows; i++)
                    {
                        line.SetPosition(i, nodes[i]);
                    }
                }
                else
                {
                    UpdateRibbonVertices();
                }
            }

            public void Dispose()
            {
                if (line != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(line);
                    else UnityEngine.Object.DestroyImmediate(line);
                }
                if (lineMaterial != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(lineMaterial);
                    else UnityEngine.Object.DestroyImmediate(lineMaterial);
                }
                grid.Dispose();
            }

            private void UpdateRootAnchor()
            {
                rootAnchor = restNodes[0];
                if (bodyAnchorTransform != null && grid.SourceTransform != null)
                {
                    Vector2 worldAnchor = bodyAnchorTransform.TransformPoint(bodyAnchorLocal);
                    Vector2 localAnchor = grid.SourceTransform.InverseTransformPoint(worldAnchor);
                    if (float.IsFinite(localAnchor.x) && float.IsFinite(localAnchor.y))
                    {
                        rootAnchor = localAnchor;
                    }
                }

                rootOffset = rootAnchor - restNodes[0];
            }

            private void TraceSpriteCenterline(Sprite sprite)
            {
                if (sprite == null || sprite.texture == null || !sprite.texture.isReadable)
                {
                    Debug.LogWarning("Rope sprite must have Read/Write enabled to trace its centerline.");
                    return;
                }

                Texture2D texture = sprite.texture;
                Color32[] pixels = texture.GetPixels32();
                Rect textureRect = sprite.textureRect;
                int firstX = Mathf.Clamp(Mathf.FloorToInt(textureRect.xMin), 0, texture.width - 1);
                int lastX = Mathf.Clamp(Mathf.CeilToInt(textureRect.xMax) - 1, firstX, texture.width - 1);
                float[] tracedX = new float[rows + 1];

                for (int i = 0; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    int centerY = Mathf.RoundToInt(textureRect.yMin + textureRect.height * (1f - t));
                    float weightedX = 0f;
                    float totalAlpha = 0f;
                    for (int y = centerY - 2; y <= centerY + 2; y++)
                    {
                        if (y < textureRect.yMin || y >= textureRect.yMax || y < 0 || y >= texture.height)
                        {
                            continue;
                        }

                        int rowStart = y * texture.width;
                        for (int x = firstX; x <= lastX; x++)
                        {
                            float alpha = pixels[rowStart + x].a;
                            if (alpha < 24f)
                            {
                                continue;
                            }

                            weightedX += x * alpha;
                            totalAlpha += alpha;
                        }
                    }

                    tracedX[i] = totalAlpha > 0f
                        ? grid.Bounds.min.x + grid.Bounds.size.x *
                            ((weightedX / totalAlpha - textureRect.xMin) / textureRect.width)
                        : float.NaN;
                }

                for (int i = 0; i <= rows; i++)
                {
                    if (float.IsFinite(tracedX[i]))
                    {
                        continue;
                    }

                    int before = i - 1;
                    int after = i + 1;
                    while (before >= 0 && !float.IsFinite(tracedX[before])) before--;
                    while (after <= rows && !float.IsFinite(tracedX[after])) after++;
                    tracedX[i] = before >= 0 && after <= rows
                        ? Mathf.Lerp(tracedX[before], tracedX[after], (i - before) / (float)(after - before))
                        : before >= 0 ? tracedX[before]
                        : after <= rows ? tracedX[after]
                        : centerX;
                }

                for (int i = 0; i <= rows; i++)
                {
                    float smoothedX = (tracedX[Mathf.Max(0, i - 2)] +
                        2f * tracedX[Mathf.Max(0, i - 1)] +
                        3f * tracedX[i] +
                        2f * tracedX[Mathf.Min(rows, i + 1)] +
                        tracedX[Mathf.Min(rows, i + 2)]) / 9f;
                    restNodes[i].x = smoothedX;
                }
            }

            private Vector2 GetAnchoredRestNode(int index)
            {
                return restNodes[index] + rootOffset;
            }

            private void UpdateRibbonVertices()
            {
                renderNodes[0] = nodes[0];
                renderNodes[rows] = nodes[rows];
                for (int i = 1; i < rows; i++)
                {
                    Vector2 neighborhood = (nodes[i - 1] + 2f * nodes[i] + nodes[i + 1]) * 0.25f;
                    float smoothness = Mathf.SmoothStep(0f, 0.8f, i / (float)rows) * 0.8f;
                    renderNodes[i] = Vector2.Lerp(nodes[i], neighborhood, smoothness);
                }

                float tangentStep = 2f / rows;
                for (int row = 0; row <= rows; row++)
                {
                    float t = row / (float)rows;
                    Vector2 before = SampleNode(Mathf.Max(0f, t - tangentStep));
                    Vector2 after = SampleNode(Mathf.Min(1f, t + tangentStep));
                    Vector2 restBefore = SampleRestNode(Mathf.Max(0f, t - tangentStep));
                    Vector2 restAfter = SampleRestNode(Mathf.Min(1f, t + tangentStep));
                    float rotation = Vector2.SignedAngle(restAfter - restBefore, after - before) * Mathf.Deg2Rad;
                    Vector2 normal = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation));

                    renderedNormals[row] = normal;
                }

                for (int i = 0; i < grid.BaseVertices.Length; i++)
                {
                    Vector3 baseVertex = grid.BaseVertices[i];
                    float t = Mathf.Clamp01((top - baseVertex.y) / height);
                    Vector2 center = SampleNode(t);
                    Vector2 normal = SampleNormal(t);
                    float across = baseVertex.x - SampleRestNode(t).x;
                    Vector2 position = center + normal * across;
                    if (t > rootRigidFraction)
                    {
                        position = KeepRenderedVertexOutside(position);
                    }
                    renderedVertices[i] = new Vector3(position.x, position.y, baseVertex.z);
                }

                grid.ApplyVertices(renderedVertices);
            }

            private Vector2 KeepRenderedVertexOutside(Vector2 localPosition)
            {
                Transform source = grid.SourceTransform;
                Vector2 world = source.TransformPoint(localPosition);
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < nearbyEdges.Count; i++)
                    {
                        GetEdgeSurface(nearbyEdges[i], world, worldNodes[0],
                            out Vector2 surface, out Vector2 normal);
                        Vector2 tangent = new Vector2(-normal.y, normal.x);
                        if (Mathf.Abs(Vector2.Dot(world - surface, tangent)) > 0.15f)
                        {
                            continue;
                        }

                        float depth = 0.01f - Vector2.Dot(world - surface, normal);
                        if (depth > 0f) world += normal * depth;
                    }

                    for (int i = 0; i < nearbyBoxes.Count; i++)
                    {
                        if (!GetBoxSurface(nearbyBoxes[i], world, worldNodes[0],
                            0.15f, out Vector2 surface, out Vector2 normal))
                        {
                            continue;
                        }

                        float depth = 0.01f - Vector2.Dot(world - surface, normal);
                        if (depth > 0f) world += normal * depth;
                    }
                }

                return source.InverseTransformPoint(world);
            }

            private Vector2 SampleNormal(float t)
            {
                float rowPosition = Mathf.Clamp01(t) * rows;
                int first = Mathf.Min(rows - 1, Mathf.FloorToInt(rowPosition));
                int second = Mathf.Min(rows, first + 1);
                Vector2 normal = Vector2.Lerp(
                    renderedNormals[first],
                    renderedNormals[second],
                    rowPosition - first);
                return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.right;
            }

            private Vector2 SampleNode(float t)
            {
                float rowPosition = Mathf.Clamp01(t) * rows;
                int first = Mathf.Min(rows - 1, Mathf.FloorToInt(rowPosition));
                int second = Mathf.Min(rows, first + 1);
                return Vector2.Lerp(renderNodes[first], renderNodes[second], rowPosition - first);
            }

            private Vector2 SampleRestNode(float t)
            {
                float rowPosition = Mathf.Clamp01(t) * rows;
                int first = Mathf.Min(rows - 1, Mathf.FloorToInt(rowPosition));
                int second = Mathf.Min(rows, first + 1);
                return Vector2.Lerp(restNodes[first], restNodes[second], rowPosition - first);
            }
        }

        private sealed class SpriteGridMesh : IDisposable
        {
            private readonly SpriteRenderer source;
            private readonly GameObject meshObject;
            private readonly Mesh mesh;
            private readonly Material material;

            public SpriteGridMesh(SpriteRenderer sourceRenderer, int horizontalSegments, int verticalSegments, string meshName, bool visible = true)
            {
                source = sourceRenderer;
                if (source == null || source.sprite == null)
                {
                    BaseVertices = Array.Empty<Vector3>();
                    Bounds = new Bounds();
                    return;
                }

                Sprite sprite = source.sprite;
                horizontalSegments = Mathf.Max(1, horizontalSegments);
                verticalSegments = Mathf.Max(1, verticalSegments);
                int vertexColumns = horizontalSegments + 1;
                int vertexRows = verticalSegments + 1;
                BaseVertices = new Vector3[vertexColumns * vertexRows];
                Vector2[] uv = new Vector2[BaseVertices.Length];
                int[] triangles = new int[horizontalSegments * verticalSegments * 6];

                Rect spriteRect = sprite.rect;
                Rect textureRect = sprite.textureRect;
                float unitsPerPixel = 1f / sprite.pixelsPerUnit;
                float left = -sprite.pivot.x * unitsPerPixel;
                float bottom = -sprite.pivot.y * unitsPerPixel;
                float width = spriteRect.width * unitsPerPixel;
                float height = spriteRect.height * unitsPerPixel;

                for (int y = 0; y < vertexRows; y++)
                {
                    float v = y / (float)verticalSegments;
                    for (int x = 0; x < vertexColumns; x++)
                    {
                        float u = x / (float)horizontalSegments;
                        int index = y * vertexColumns + x;
                        BaseVertices[index] = new Vector3(left + width * u, bottom + height * v, 0f);
                        uv[index] = new Vector2(
                            (textureRect.x + textureRect.width * u) / sprite.texture.width,
                            (textureRect.y + textureRect.height * v) / sprite.texture.height);
                    }
                }

                int triangleIndex = 0;
                for (int y = 0; y < verticalSegments; y++)
                {
                    for (int x = 0; x < horizontalSegments; x++)
                    {
                        int lowerLeft = y * vertexColumns + x;
                        int lowerRight = lowerLeft + 1;
                        int upperLeft = lowerLeft + vertexColumns;
                        int upperRight = upperLeft + 1;
                        triangles[triangleIndex++] = lowerLeft;
                        triangles[triangleIndex++] = upperLeft;
                        triangles[triangleIndex++] = upperRight;
                        triangles[triangleIndex++] = lowerLeft;
                        triangles[triangleIndex++] = upperRight;
                        triangles[triangleIndex++] = lowerRight;
                    }
                }

                mesh = new Mesh
                {
                    name = meshName,
                    hideFlags = HideFlags.DontSave,
                    vertices = BaseVertices,
                    uv = uv,
                    triangles = triangles
                };
                mesh.MarkDynamic();
                mesh.RecalculateBounds();
                Bounds = mesh.bounds;

                Material sourceMaterial = source.sharedMaterial;
                if (sourceMaterial != null)
                {
                    material = new Material(sourceMaterial);
                }
                else
                {
                    Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null)
                    {
                        return;
                    }

                    material = new Material(shader);
                }

                material.name = meshName + " Material";
                material.hideFlags = HideFlags.DontSave;
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", sprite.texture);
                }
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", sprite.texture);
                }
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", source.color);
                }

                meshObject = new GameObject(meshName, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    layer = source.gameObject.layer,
                    hideFlags = HideFlags.DontSave
                };
                meshObject.transform.SetParent(source.transform, false);

                MeshFilter filter = meshObject.GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = meshObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder;
                renderer.enabled = visible;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                source.enabled = false;
            }

            public Vector3[] BaseVertices { get; }
            public Bounds Bounds { get; }
            public Transform SourceTransform => source.transform;
            public bool IsValid => mesh != null && material != null && meshObject != null;

            public void ApplyVertices(Vector3[] vertices)
            {
                if (!IsValid || vertices == null || vertices.Length != BaseVertices.Length)
                {
                    return;
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }

            public void Dispose()
            {
                if (source != null)
                {
                    source.enabled = true;
                }

                DestroyRuntimeObject(meshObject);
                DestroyRuntimeObject(material);
                DestroyRuntimeObject(mesh);
            }

            private static void DestroyRuntimeObject(UnityEngine.Object target)
            {
                if (target == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(target);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }
    }
}
