using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.World
{
    [DisallowMultipleComponent]
    public sealed class CambrianOceanWorld : MonoBehaviour
    {
        public const float DefaultWorldHalfWidth = 1024f;
        public const float DefaultSurfaceY = 10f;
        public const float DefaultShallowShelfDepth = 64f;
        public const float DefaultDeepBasinDepth = 170f;
        public const float DefaultChunkWidth = 32f;

        private const int ZoneCount = 5;
        private const int SedimentColorIndex = 5;
        private const float MinimumAAThickness = 10f;
        private const float MinimumABThickness = 2f;
        private const float MaximumABThickness = 6f;
        private const float MinimumBBThickness = 10f;
        private const float MinimumBCThickness = 3f;
        private const float MaximumBCThickness = 7f;
        private const float MinimumCCThickness = 12f;

        private static readonly OceanDepthZone[] Zones =
        {
            OceanDepthZone.AA,
            OceanDepthZone.AB,
            OceanDepthZone.BB,
            OceanDepthZone.BC,
            OceanDepthZone.CC
        };

        [Header("Run Seed")]
        [Tooltip("Creates a different ocean layout whenever a new run scene starts.")]
        [SerializeField] private bool randomizeSeedEachRun = true;
        [Tooltip("Used when Randomize Seed Each Run is disabled.")]
        [SerializeField] private int fixedSeed = 541000;

        [Header("World Size")]
        [SerializeField, Min(128f)] private float worldHalfWidth = DefaultWorldHalfWidth;
        [SerializeField] private float surfaceY = DefaultSurfaceY;
        [SerializeField, Min(40f)] private float shallowShelfDepth = DefaultShallowShelfDepth;
        [SerializeField, Min(80f)] private float deepBasinDepth = DefaultDeepBasinDepth;

        [Header("Chunk Streaming")]
        [SerializeField, Min(8f)] private float chunkWidth = DefaultChunkWidth;
        [SerializeField, Range(4, 32)] private int samplesPerChunk = 16;
        [SerializeField, Range(1, 6)] private int activeChunkRadius = 2;
        [SerializeField, Min(0.05f)] private float streamingInterval = 0.2f;
        [SerializeField] private Transform streamingTarget;
        [SerializeField] private Camera streamingCamera;

        [Header("Zone Colors")]
        [SerializeField] private Color aaColor = new Color(0.13f, 0.58f, 0.75f, 0.92f);
        [SerializeField] private Color abColor = new Color(0.09f, 0.45f, 0.64f, 0.94f);
        [SerializeField] private Color bbColor = new Color(0.05f, 0.29f, 0.49f, 0.96f);
        [SerializeField] private Color bcColor = new Color(0.025f, 0.19f, 0.35f, 0.98f);
        [SerializeField] private Color ccColor = new Color(0.012f, 0.07f, 0.17f, 1f);
        [SerializeField] private Color sedimentColor = new Color(0.12f, 0.13f, 0.12f, 1f);

        private readonly Dictionary<int, OceanChunkData> chunkData = new Dictionary<int, OceanChunkData>();
        private readonly Dictionary<int, OceanChunkView> chunkViews = new Dictionary<int, OceanChunkView>();
        private readonly List<int> chunkToggleBuffer = new List<int>();

        private Material oceanMaterial;
        private Transform boundaryRoot;
        private bool initialized;
        private int resolvedSeed;
        private int chunkCount;
        private int lastCenterChunk = int.MinValue;
        private float worldMinX;
        private float worldMaxX;
        private float worldMinY;
        private float nextStreamingUpdate;

        public int Seed
        {
            get
            {
                InitializeIfNeeded();
                return resolvedSeed;
            }
        }

        public float WorldMinX
        {
            get
            {
                InitializeIfNeeded();
                return worldMinX;
            }
        }

        public float WorldMaxX
        {
            get
            {
                InitializeIfNeeded();
                return worldMaxX;
            }
        }

        public float WorldMinY
        {
            get
            {
                InitializeIfNeeded();
                return worldMinY;
            }
        }

        public float SurfaceY
        {
            get { return surfaceY; }
        }

        public int LoadedChunkCount
        {
            get { return chunkViews.Count; }
        }

        public int ActiveChunkCount
        {
            get
            {
                int count = 0;
                foreach (OceanChunkView view in chunkViews.Values)
                {
                    if (view.Root.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public event Action<int, int, Transform> ChunkActivated;
        public event Action<int> ChunkDeactivated;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Start()
        {
            ResolveStreamingTarget();
            RefreshStreaming(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextStreamingUpdate)
            {
                return;
            }

            nextStreamingUpdate = Time.unscaledTime + streamingInterval;
            ResolveStreamingTarget();
            RefreshStreaming(false);
        }

        private void OnDestroy()
        {
            foreach (OceanChunkView view in chunkViews.Values)
            {
                DestroyGeneratedObject(view.Mesh);
            }

            chunkViews.Clear();
            DestroyGeneratedObject(oceanMaterial);
        }

        private void OnValidate()
        {
            worldHalfWidth = Mathf.Max(128f, worldHalfWidth);
            shallowShelfDepth = Mathf.Max(40f, shallowShelfDepth);
            deepBasinDepth = Mathf.Max(shallowShelfDepth + 24f, deepBasinDepth);
            chunkWidth = Mathf.Max(8f, chunkWidth);
            samplesPerChunk = Mathf.Clamp(samplesPerChunk, 4, 32);
            activeChunkRadius = Mathf.Clamp(activeChunkRadius, 1, 6);
            streamingInterval = Mathf.Max(0.05f, streamingInterval);
        }

        public void Configure(Camera cameraForStreaming, Transform fallbackTarget)
        {
            streamingCamera = cameraForStreaming;
            streamingTarget = cameraForStreaming != null ? cameraForStreaming.transform : fallbackTarget;
            InitializeIfNeeded();
            RefreshStreaming(true);
        }

        public OceanColumnProfile GetColumnProfile(float worldX)
        {
            InitializeIfNeeded();
            float clampedX = Mathf.Clamp(worldX, worldMinX, worldMaxX);
            return GenerateColumnProfile(clampedX);
        }

        public OceanDepthZone GetZoneAt(Vector2 worldPosition)
        {
            return GetColumnProfile(worldPosition.x).GetZone(worldPosition.y);
        }

        public float GetSeabedY(float worldX)
        {
            return GetColumnProfile(worldX).SeabedY;
        }

        public int GetStableChunkSeed(float worldX)
        {
            InitializeIfNeeded();
            int chunkIndex = GetChunkIndex(worldX);
            return chunkData[chunkIndex].StableSeed;
        }

        public bool TryGetActiveChunkRoot(float worldX, out Transform chunkRoot)
        {
            InitializeIfNeeded();
            int chunkIndex = GetChunkIndex(worldX);
            if (chunkViews.TryGetValue(chunkIndex, out OceanChunkView view) && view.Root.activeSelf)
            {
                chunkRoot = view.Root.transform;
                return true;
            }

            chunkRoot = null;
            return false;
        }

        public void GetCameraBounds(Camera sceneCamera, out Vector2 minBounds, out Vector2 maxBounds)
        {
            InitializeIfNeeded();

            float verticalMargin = sceneCamera != null && sceneCamera.orthographic
                ? sceneCamera.orthographicSize
                : 5.5f;
            float horizontalMargin = sceneCamera != null
                ? verticalMargin * sceneCamera.aspect
                : verticalMargin * (16f / 9f);

            minBounds = new Vector2(worldMinX + horizontalMargin, worldMinY + verticalMargin);
            maxBounds = new Vector2(worldMaxX - horizontalMargin, surfaceY - verticalMargin);
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            OnValidate();
            resolvedSeed = randomizeSeedEachRun ? CreateRunSeed() : fixedSeed;
            worldMinX = -worldHalfWidth;
            chunkCount = Mathf.CeilToInt((worldHalfWidth * 2f) / chunkWidth);
            worldMaxX = worldMinX + (chunkCount * chunkWidth);
            worldMinY = surfaceY - deepBasinDepth - 16f;

            oceanMaterial = CreateOceanMaterial();
            BuildWorldData();
            CreateWorldBoundaries();
            initialized = true;

            Debug.Log(
                "Cambrian ocean generated with seed " + resolvedSeed +
                ", " + chunkCount + " chunks, bounds " +
                worldMinX.ToString("0") + ".." + worldMaxX.ToString("0") + ".");
        }

        private void BuildWorldData()
        {
            chunkData.Clear();

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                float startX = worldMinX + (chunkIndex * chunkWidth);
                OceanColumnProfile[] profiles = new OceanColumnProfile[samplesPerChunk + 1];

                for (int sample = 0; sample <= samplesPerChunk; sample++)
                {
                    float sampleX = startX + (chunkWidth * sample / samplesPerChunk);
                    profiles[sample] = GenerateColumnProfile(sampleX);
                }

                chunkData.Add(
                    chunkIndex,
                    new OceanChunkData(
                        chunkIndex,
                        startX,
                        StableHash(resolvedSeed, chunkIndex),
                        profiles));
            }
        }

        private OceanColumnProfile GenerateColumnProfile(float worldX)
        {
            float seedPhase = Hash01(resolvedSeed, 17) * Mathf.PI * 2f;
            float tectonicWave = 0.5f + (0.5f * Mathf.Sin((worldX / 260f) + seedPhase));
            float macroNoise = FractalNoise(worldX * 0.0017f, resolvedSeed ^ 0x2f6e2b1, 4);
            float basinSignal = (macroNoise * 0.67f) + (tectonicWave * 0.33f);
            float basinWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.72f, basinSignal));

            float spawnShelf = Mathf.Exp(-(worldX * worldX) / (2f * 180f * 180f));
            basinWeight *= Mathf.Lerp(1f, 0.16f, spawnShelf);

            float regionalRelief = (FractalNoise(worldX * 0.008f, resolvedSeed ^ 0x6c8e9cf, 3) - 0.5f) * 18f;
            float localRelief = (FractalNoise(worldX * 0.032f, resolvedSeed ^ 0x175ac4d, 2) - 0.5f) * 5f;
            float waterDepth = Mathf.Lerp(shallowShelfDepth, deepBasinDepth, basinWeight) + regionalRelief + localRelief;
            waterDepth = Mathf.Clamp(waterDepth, shallowShelfDepth, deepBasinDepth);

            float layerNoiseA = FractalNoise(worldX * 0.011f, resolvedSeed ^ 0x3a91d45, 2);
            float abNoise = FractalNoise(worldX * 0.021f, resolvedSeed ^ 0x51c77b9, 3);
            float bcNoise = FractalNoise(worldX * 0.027f, resolvedSeed ^ 0x2f846d1, 3);
            float transitionContrast = (FractalNoise(worldX * 0.014f, resolvedSeed ^ 0x6b204f3, 3) * 2f) - 1f;
            float separationNoise = FractalNoise(worldX * 0.009f, resolvedSeed ^ 0x14d83a7, 3);
            float depthRatio = Mathf.InverseLerp(shallowShelfDepth, deepBasinDepth, waterDepth);

            float maximumSafeAAThickness = waterDepth
                - MinimumABThickness
                - MinimumBBThickness
                - MinimumBCThickness
                - MinimumCCThickness;
            float aaThickness = Mathf.Clamp(
                Mathf.Lerp(12f, 18f, layerNoiseA),
                MinimumAAThickness,
                Mathf.Max(MinimumAAThickness, maximumSafeAAThickness));

            float desiredABThickness = Mathf.Clamp(
                Mathf.Lerp(MinimumABThickness, MaximumABThickness, abNoise)
                + (transitionContrast * 1.25f),
                MinimumABThickness,
                MaximumABThickness);
            float desiredBCThickness = Mathf.Clamp(
                Mathf.Lerp(MinimumBCThickness, MaximumBCThickness, bcNoise)
                - (transitionContrast * 1.5f),
                MinimumBCThickness,
                MaximumBCThickness);

            // Transition bands vary visibly, but their hard maxima stay below every primary zone minimum.
            float freeDepth = Mathf.Max(
                0f,
                waterDepth
                - aaThickness
                - MinimumABThickness
                - MinimumBBThickness
                - MinimumBCThickness
                - MinimumCCThickness);
            float transitionExtraBudget = freeDepth * Mathf.Lerp(0.42f, 0.24f, depthRatio);
            float desiredTransitionExtra =
                (desiredABThickness - MinimumABThickness) + (desiredBCThickness - MinimumBCThickness);
            float transitionScale = desiredTransitionExtra > 0f
                ? Mathf.Min(1f, transitionExtraBudget / desiredTransitionExtra)
                : 0f;

            float abThickness = MinimumABThickness
                + ((desiredABThickness - MinimumABThickness) * transitionScale);
            float bcThickness = MinimumBCThickness
                + ((desiredBCThickness - MinimumBCThickness) * transitionScale);

            float distributableDepth = Mathf.Max(
                0f,
                waterDepth
                - aaThickness
                - abThickness
                - MinimumBBThickness
                - bcThickness
                - MinimumCCThickness);
            float separationShape = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.22f, 0.78f, separationNoise));
            float bbShare = Mathf.Lerp(0.16f, 0.84f, separationShape);
            float bbThickness = MinimumBBThickness + (distributableDepth * bbShare);
            float ccThickness = MinimumCCThickness + (distributableDepth * (1f - bbShare));

            float aaBottomY = surfaceY - aaThickness;
            float abBottomY = aaBottomY - abThickness;
            float bbBottomY = abBottomY - bbThickness;
            float bcBottomY = bbBottomY - bcThickness;
            float seabedY = bcBottomY - ccThickness;

            return new OceanColumnProfile(
                surfaceY,
                aaBottomY,
                abBottomY,
                bbBottomY,
                bcBottomY,
                seabedY);
        }

        private void ResolveStreamingTarget()
        {
            if (streamingCamera == null)
            {
                streamingCamera = Camera.main;
            }

            if (streamingCamera != null)
            {
                streamingTarget = streamingCamera.transform;
            }
        }

        private void RefreshStreaming(bool force)
        {
            if (!initialized)
            {
                return;
            }

            float focusX = streamingTarget != null ? streamingTarget.position.x : 0f;
            int centerChunk = GetChunkIndex(focusX);
            if (!force && centerChunk == lastCenterChunk)
            {
                return;
            }

            lastCenterChunk = centerChunk;
            int firstActive = Mathf.Max(0, centerChunk - activeChunkRadius);
            int lastActive = Mathf.Min(chunkCount - 1, centerChunk + activeChunkRadius);

            chunkToggleBuffer.Clear();
            foreach (KeyValuePair<int, OceanChunkView> pair in chunkViews)
            {
                if (pair.Value.Root.activeSelf && (pair.Key < firstActive || pair.Key > lastActive))
                {
                    chunkToggleBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < chunkToggleBuffer.Count; i++)
            {
                int chunkIndex = chunkToggleBuffer[i];
                chunkViews[chunkIndex].Root.SetActive(false);
                ChunkDeactivated?.Invoke(chunkIndex);
            }

            for (int chunkIndex = firstActive; chunkIndex <= lastActive; chunkIndex++)
            {
                OceanChunkView view = EnsureChunkView(chunkIndex);
                if (!view.Root.activeSelf)
                {
                    view.Root.SetActive(true);
                    ChunkActivated?.Invoke(chunkIndex, chunkData[chunkIndex].StableSeed, view.Root.transform);
                }
            }
        }

        private OceanChunkView EnsureChunkView(int chunkIndex)
        {
            if (chunkViews.TryGetValue(chunkIndex, out OceanChunkView existingView))
            {
                return existingView;
            }

            OceanChunkData data = chunkData[chunkIndex];
            GameObject chunkObject = new GameObject("Ocean Chunk " + chunkIndex.ToString("000"));
            chunkObject.transform.SetParent(transform, false);
            chunkObject.transform.localPosition = new Vector3(data.StartX, 0f, 0f);

            Mesh mesh = BuildChunkMesh(data);
            MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = oceanMaterial;
            meshRenderer.sortingOrder = -100;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            EdgeCollider2D seabedCollider = chunkObject.AddComponent<EdgeCollider2D>();
            Vector2[] colliderPoints = new Vector2[data.Profiles.Length];
            float sampleStep = chunkWidth / samplesPerChunk;
            for (int sample = 0; sample < data.Profiles.Length; sample++)
            {
                colliderPoints[sample] = new Vector2(sample * sampleStep, data.Profiles[sample].SeabedY);
            }

            seabedCollider.points = colliderPoints;
            chunkObject.SetActive(false);

            OceanChunkView view = new OceanChunkView(chunkObject, mesh);
            chunkViews.Add(chunkIndex, view);
            return view;
        }

        private Mesh BuildChunkMesh(OceanChunkData data)
        {
            int segmentCount = data.Profiles.Length - 1;
            List<Vector3> vertices = new List<Vector3>(segmentCount * (ZoneCount + 1) * 4);
            List<int> triangles = new List<int>(segmentCount * (ZoneCount + 1) * 6);
            List<Color> colors = new List<Color>(segmentCount * (ZoneCount + 1) * 4);
            List<Vector2> uvs = new List<Vector2>(segmentCount * (ZoneCount + 1) * 4);
            float sampleStep = chunkWidth / samplesPerChunk;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                OceanColumnProfile left = data.Profiles[segment];
                OceanColumnProfile right = data.Profiles[segment + 1];
                float x0 = segment * sampleStep;
                float x1 = (segment + 1) * sampleStep;

                for (int zoneIndex = 0; zoneIndex < Zones.Length; zoneIndex++)
                {
                    OceanDepthZone zone = Zones[zoneIndex];
                    AddQuad(
                        vertices,
                        triangles,
                        colors,
                        uvs,
                        x0,
                        x1,
                        left.GetTop(zone),
                        right.GetTop(zone),
                        left.GetBottom(zone),
                        right.GetBottom(zone),
                        GetBandColor(zoneIndex));
                }

                AddQuad(
                    vertices,
                    triangles,
                    colors,
                    uvs,
                    x0,
                    x1,
                    left.SeabedY,
                    right.SeabedY,
                    worldMinY,
                    worldMinY,
                    GetBandColor(SedimentColorIndex));
            }

            Mesh mesh = new Mesh
            {
                name = "OceanChunk_" + data.Index.ToString("000") + "_Mesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            List<Vector2> uvs,
            float x0,
            float x1,
            float topLeft,
            float topRight,
            float bottomLeft,
            float bottomRight,
            Color color)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, topLeft, 0f));
            vertices.Add(new Vector3(x1, topRight, 0f));
            vertices.Add(new Vector3(x0, bottomLeft, 0f));
            vertices.Add(new Vector3(x1, bottomRight, 0f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 3);
            triangles.Add(start + 2);

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
        }

        private void CreateWorldBoundaries()
        {
            GameObject boundaries = new GameObject("Ocean World Boundaries");
            boundaries.transform.SetParent(transform, false);
            boundaryRoot = boundaries.transform;

            CreateBoundary(
                "Sea Surface",
                new[]
                {
                    new Vector2(worldMinX, surfaceY),
                    new Vector2(worldMaxX, surfaceY)
                });
            CreateBoundary(
                "West Limit",
                new[]
                {
                    new Vector2(worldMinX, worldMinY),
                    new Vector2(worldMinX, surfaceY)
                });
            CreateBoundary(
                "East Limit",
                new[]
                {
                    new Vector2(worldMaxX, surfaceY),
                    new Vector2(worldMaxX, worldMinY)
                });
        }

        private void CreateBoundary(string boundaryName, Vector2[] points)
        {
            GameObject boundary = new GameObject(boundaryName);
            boundary.transform.SetParent(boundaryRoot, false);
            EdgeCollider2D collider = boundary.AddComponent<EdgeCollider2D>();
            collider.points = points;
        }

        private Material CreateOceanMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                Debug.LogError("No unlit shader was found for the Cambrian ocean chunks.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Cambrian Ocean Runtime Material",
                color = Color.white,
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        private Color GetBandColor(int index)
        {
            switch (index)
            {
                case 0:
                    return aaColor;
                case 1:
                    return abColor;
                case 2:
                    return bbColor;
                case 3:
                    return bcColor;
                case 4:
                    return ccColor;
                default:
                    return sedimentColor;
            }
        }

        private int GetChunkIndex(float worldX)
        {
            int index = Mathf.FloorToInt((worldX - worldMinX) / chunkWidth);
            return Mathf.Clamp(index, 0, chunkCount - 1);
        }

        private static float FractalNoise(float value, int seed, int octaves)
        {
            float total = 0f;
            float amplitude = 1f;
            float amplitudeTotal = 0f;
            float frequency = 1f;

            for (int octave = 0; octave < octaves; octave++)
            {
                total += ValueNoise(value * frequency, seed + (octave * 1013)) * amplitude;
                amplitudeTotal += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return amplitudeTotal > 0f ? total / amplitudeTotal : 0f;
        }

        private static float ValueNoise(float value, int seed)
        {
            int left = Mathf.FloorToInt(value);
            float interpolation = value - left;
            interpolation = interpolation * interpolation * (3f - (2f * interpolation));
            return Mathf.Lerp(Hash01(left, seed), Hash01(left + 1, seed), interpolation);
        }

        private static float Hash01(int value, int seed)
        {
            uint hash = unchecked((uint)StableHash(value, seed));
            return (hash & 0x00ffffffu) / 16777215f;
        }

        private static int StableHash(int first, int second)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)first) * 16777619u;
                hash = (hash ^ (uint)second) * 16777619u;
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (int)hash;
            }
        }

        private static int CreateRunSeed()
        {
            unchecked
            {
                return Guid.NewGuid().GetHashCode() ^ Environment.TickCount;
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class OceanChunkData
        {
            public OceanChunkData(int index, float startX, int stableSeed, OceanColumnProfile[] profiles)
            {
                Index = index;
                StartX = startX;
                StableSeed = stableSeed;
                Profiles = profiles;
            }

            public int Index { get; }
            public float StartX { get; }
            public int StableSeed { get; }
            public OceanColumnProfile[] Profiles { get; }
        }

        private sealed class OceanChunkView
        {
            public OceanChunkView(GameObject root, Mesh mesh)
            {
                Root = root;
                Mesh = mesh;
            }

            public GameObject Root { get; }
            public Mesh Mesh { get; }
        }
    }
}
