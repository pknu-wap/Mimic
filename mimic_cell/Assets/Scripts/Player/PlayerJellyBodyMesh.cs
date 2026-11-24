using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.Player
{
    internal class PlayerJellyBodyMesh : IDisposable
    {
        private const int MaximumContacts = 12;
        private const int BodyMeshColumns = 16;
        private const int BodyMeshRows = 18;
        private const float SurfaceSpring = 27f;
        private const float SurfaceDamping = 5.3f;
        private const float SurfaceCohesion = 22f;
        private const float PressureStretch = 0.16f;
        private const float PressureTaper = 0.3f;
        private const float FrontCompression = 0.13f;
        private const float RearElongation = 0.19f;
        private const float BodyWaveAmplitude = 0.045f;
        private const float BodyWaveFrequency = 0.92f;
        private const float BodyWaveCycles = 1.45f;
        private const float AccelerationInertia = 0.055f;
        private const float MaximumBodyOffset = 0.78f;
        private const float ImpactCompression = 0.48f;
        private const float DisplacedFluidBulge = 0.34f;
        private const float ContactInfluence = 0.82f;
        private const float ContactRecoverySpeed = 2.4f;

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
        private readonly float minimumExtent;

        public PlayerJellyBodyMesh(SpriteRenderer source)
        {
            grid = new SpriteGridMesh(source, BodyMeshColumns, BodyMeshRows, "Fluid Pudding Mesh");
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

        public void SetVisible(bool visible) => grid.SetVisible(visible);

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
            float deltaTime)
        {
            if (!grid.IsValid)
            {
                return;
            }

            BuildTargets(pressureVector, localAcceleration, time, pulse);

            const int substeps = 2;
            float stepTime = deltaTime / substeps;
            for (int step = 0; step < substeps; step++)
            {
                BuildForces();
                for (int i = 0; i < positions.Length; i++)
                {
                    velocities[i] += forces[i] * stepTime;
                    velocities[i] *= 1f / (1f + SurfaceDamping * stepTime);
                    velocities[i] = Vector2.ClampMagnitude(velocities[i], 4.5f);
                    positions[i] += velocities[i] * stepTime;
                    if (contactPins[i] > 0f)
                    {
                        positions[i] = Vector2.Lerp(positions[i], targets[i], contactPins[i]);
                        velocities[i] *= 1f - contactPins[i];
                    }

                    Vector2 basePosition = grid.BaseVertices[i];
                    Vector2 displacement = positions[i] - basePosition;
                    float maximumOffset = minimumExtent * MaximumBodyOffset;
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
            DecayContacts(deltaTime);
        }

        public void Dispose()
        {
            grid.Dispose();
        }

        private void BuildTargets(
            Vector2 pressureVector,
            Vector2 localAcceleration,
            float time,
            float pulse)
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
                along *= 1f + pressure * PressureStretch;
                along *= 1f - pressure * FrontCompression * front * front;
                along *= 1f + pressure * RearElongation * rear * rear;
                perpendicular *= 1f - pressure * PressureTaper * rear * rear;

                Vector2 result = center + direction * along + perpendicular;
                Vector2 normalizedRadius = new Vector2(
                    relative.x / Mathf.Max(0.001f, extents.x),
                    relative.y / Mathf.Max(0.001f, extents.y));
                float radialLength = normalizedRadius.magnitude;
                Vector2 radialDirection = radialLength > 0.001f ? normalizedRadius / radialLength : Vector2.zero;
                float surfaceWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, radialLength));
                float contactPin = GetContactPin(basePosition);
                contactPins[i] = contactPin;

                float wavePhase =
                    time * BodyWaveFrequency * Mathf.PI * 2f -
                    normalizedAlong * BodyWaveCycles * Mathf.PI * 2f +
                    Mathf.Atan2(normalizedRadius.y, normalizedRadius.x) * 0.22f;
                float resistance = 0.16f + pressure * 0.84f;
                float wave = Mathf.Sin(wavePhase) * BodyWaveAmplitude * minimumExtent * resistance * surfaceWeight;
                result += radialDirection * wave * (1f - contactPin);
                result -= normalizedAcceleration * (AccelerationInertia * minimumExtent * surfaceWeight * (1f - contactPin));

                result -= center;
                result.x *= 1f + pulse * (1f - contactPin);
                result.y *= 1f - pulse * 0.72f * (1f - contactPin);
                result += center;
                ApplyContactFields(ref result, radialDirection);

                Vector2 targetOffset = result - basePosition;
                float maximumTargetOffset = minimumExtent * MaximumBodyOffset;
                targets[i] = targetOffset.sqrMagnitude > maximumTargetOffset * maximumTargetOffset
                    ? basePosition + targetOffset.normalized * maximumTargetOffset
                    : result;
            }
        }

        private float GetContactPin(Vector2 position)
        {
            float radius = minimumExtent * ContactInfluence;
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
            Vector2 radialDirection)
        {
            float radius = minimumExtent * ContactInfluence;
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
                float depth = minimumExtent * ImpactCompression * contact.Strength;
                position += contact.Inward * (depth * dent);

                float ringDistance = (normalizedDistance - 0.82f) / 0.23f;
                float ring = Mathf.Exp(-(ringDistance * ringDistance));
                Vector2 tangent = new Vector2(-contact.Inward.y, contact.Inward.x);
                float side = Mathf.Sign(Vector2.Dot(position - contact.Point, tangent));
                position += radialDirection * (depth * DisplacedFluidBulge * ring);
                position += tangent * (side * depth * DisplacedFluidBulge * ring * 0.28f);
            }
        }

        private void BuildForces()
        {
            const int vertexColumns = BodyMeshColumns + 1;
            const int vertexRows = BodyMeshRows + 1;
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
                        ? ((neighborOffset / neighborCount) - offset) * SurfaceCohesion
                        : Vector2.zero;
                    forces[index] = (targets[index] - positions[index]) * SurfaceSpring + cohesion;
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

        private void DecayContacts(float deltaTime)
        {
            for (int i = contacts.Count - 1; i >= 0; i--)
            {
                if (!contacts[i].Sustained || Time.time - contacts[i].LastRefreshTime > 0.12f)
                {
                    contacts[i].Strength = Mathf.MoveTowards(contacts[i].Strength, 0f, ContactRecoverySpeed * deltaTime);
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

        private sealed class SpriteGridMesh : IDisposable
        {
            private readonly SpriteRenderer source;
            private readonly GameObject meshObject;
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly MeshRenderer renderer;
            private readonly bool sourceWasHidden;
            private static readonly int Tint = Shader.PropertyToID("_Color");
            private Color lastTint;

            public SpriteGridMesh(SpriteRenderer sourceRenderer, int horizontalSegments, int verticalSegments, string meshName)
            {
                source = sourceRenderer;
                sourceWasHidden = source != null && source.forceRenderingOff;
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

                var colors = new Color32[BaseVertices.Length];
                Array.Fill(colors, new Color32(255, 255, 255, 255));
                mesh = new Mesh
                {
                    name = meshName,
                    hideFlags = HideFlags.DontSave,
                    vertices = BaseVertices,
                    uv = uv,
                    colors32 = colors,
                    triangles = triangles
                };
                mesh.MarkDynamic();
                mesh.RecalculateBounds();
                Bounds = mesh.bounds;
                Bounds renderBounds = Bounds;
                renderBounds.Expand(2f * MaximumBodyOffset * Mathf.Min(Bounds.extents.x, Bounds.extents.y));
                mesh.bounds = renderBounds;

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
                    lastTint = source.color;
                    material.SetColor(Tint, lastTint);
                }

                meshObject = new GameObject(meshName, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    layer = source.gameObject.layer,
                    hideFlags = HideFlags.DontSave
                };
                meshObject.transform.SetParent(source.transform, false);

                MeshFilter filter = meshObject.GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                renderer = meshObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder;
                renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            public Vector3[] BaseVertices { get; }
            public Bounds Bounds { get; }
            public bool IsValid => mesh != null && material != null && meshObject != null;

            public void ApplyVertices(Vector3[] vertices)
            {
                if (!IsValid || vertices == null || vertices.Length != BaseVertices.Length)
                {
                    return;
                }

                mesh.SetVertices(vertices, 0, vertices.Length, MeshUpdateFlags.DontRecalculateBounds);
            }

            public void SetVisible(bool visible)
            {
                if (!IsValid)
                {
                    return;
                }

                source.forceRenderingOff = visible || sourceWasHidden;
                renderer.enabled = visible && source.enabled && !sourceWasHidden;
                if (renderer.enabled && material.HasProperty(Tint) && lastTint != source.color)
                {
                    lastTint = source.color;
                    material.SetColor(Tint, lastTint);
                }
            }

            public void Dispose()
            {
                if (source != null)
                {
                    source.forceRenderingOff = sourceWasHidden;
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
