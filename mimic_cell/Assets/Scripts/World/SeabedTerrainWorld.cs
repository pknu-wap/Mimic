using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.World
{
    // Solid terrain is generated independently of the AA/AB/BB/BC/CC water bands.
    [DisallowMultipleComponent]
    public sealed partial class SeabedTerrainWorld : MonoBehaviour
    {
        private const string PlaceholderTexturePath = "Prototype/SeabedRockPlaceholder";
        private const float CaveRegionWidth = 260f;
        private const float SurfaceFeatureWidth = 240f;

        [Header("Temporary Art")]
        [SerializeField] private Texture2D terrainTexture;
        [SerializeField, Min(0.5f)] private float textureWorldSize = 4f;
        [SerializeField] private Texture2D backWallTexture;

        [Header("Geometry")]
        [SerializeField, Range(0.75f, 3f)] private float cellSize = 1.5f;
        [SerializeField, Min(0.05f)] private float streamingInterval = 0.2f;

        private readonly Dictionary<int, ChunkView> chunks = new Dictionary<int, ChunkView>();
        private readonly List<int> chunkToggleBuffer = new List<int>();
        private readonly List<int> chunkEvictionBuffer = new List<int>();

        private CambrianOceanWorld oceanWorld;
        private Transform streamingTarget;
        private Material terrainMaterial;
        private Material backWallMaterial;
        private int resolvedSeed;
        private int chunkCount;
        private int lastCenterChunk = int.MinValue;
        private float worldMinX;
        private float worldMaxX;
        private float worldMinY;
        private float waterSurfaceY;
        private float chunkWidth;
        private float nextStreamingUpdate;
        private bool initialized;

        public int Seed { get { return resolvedSeed; } }
        public int ActiveChunkCount
        {
            get
            {
                int count = 0;
                foreach (ChunkView chunk in chunks.Values)
                {
                    if (chunk.Root.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Update()
        {
            if (!initialized || Time.unscaledTime < nextStreamingUpdate)
            {
                return;
            }

            nextStreamingUpdate = Time.unscaledTime + streamingInterval;
            RefreshStreaming(false);
        }

        private void OnDestroy()
        {
            ClearChunks();
            ClearLandmarkMarkers();
            DestroyGeneratedObject(terrainMaterial);
            DestroyGeneratedObject(backWallMaterial);
        }

        private void OnValidate()
        {
            textureWorldSize = Mathf.Max(0.5f, textureWorldSize);
            cellSize = Mathf.Clamp(cellSize, 0.75f, 3f);
            streamingInterval = Mathf.Max(0.05f, streamingInterval);
        }

        public void Configure(CambrianOceanWorld world, Camera cameraForStreaming, Transform fallbackTarget)
        {
            if (world == null)
            {
                Debug.LogError("Seabed terrain needs a Cambrian ocean world for shared run bounds and seed.", this);
                return;
            }

            int runSeed = world.Seed;
            if (initialized && (world != oceanWorld || runSeed != resolvedSeed))
            {
                ClearChunks();
                ClearLandmarkMarkers();
                DestroyGeneratedObject(terrainMaterial);
                DestroyGeneratedObject(backWallMaterial);
                terrainMaterial = null;
                backWallMaterial = null;
                initialized = false;
            }

            oceanWorld = world;
            streamingTarget = cameraForStreaming != null ? cameraForStreaming.transform : fallbackTarget;
            if (!initialized)
            {
                OnValidate();
                resolvedSeed = runSeed;
                worldMinX = world.WorldMinX;
                worldMaxX = world.WorldMaxX;
                worldMinY = world.WorldMinY;
                waterSurfaceY = world.SurfaceY;
                chunkWidth = world.ChunkWidth;
                chunkCount = Mathf.CeilToInt((worldMaxX - worldMinX) / chunkWidth);
                BuildLandmarkLayout();
                CreateLandmarkMarkers();
                terrainMaterial = CreateMaterial();
                backWallMaterial = CreateBackWallMaterial();
                initialized = true;
                Debug.Log("Seabed terrain ready with seed " + resolvedSeed + ".", this);
            }

            RefreshStreaming(true);
        }

        public float GetSurfaceY(float worldX)
        {
            return initialized ? SampleSurfaceY(worldX) : float.NaN;
        }

        public bool IsSolidAt(Vector2 worldPosition)
        {
            if (!initialized)
            {
                return false;
            }

            List<CaveFeature> nearbyCaves = BuildCaveFeatures(worldPosition.x, worldPosition.x);
            return SampleDensity(worldPosition.x, worldPosition.y, SampleSurfaceY(worldPosition.x), nearbyCaves) >= 0f;
        }

        private void RefreshStreaming(bool force)
        {
            float focusX = streamingTarget != null ? streamingTarget.position.x : 0f;
            int center = GetChunkIndex(focusX);
            if (!force && center == lastCenterChunk)
            {
                return;
            }

            lastCenterChunk = center;
            int radius = oceanWorld.ActiveChunkRadius;
            int firstActive = Mathf.Max(0, center - radius);
            int lastActive = Mathf.Min(chunkCount - 1, center + radius);
            chunkToggleBuffer.Clear();
            chunkEvictionBuffer.Clear();

            foreach (KeyValuePair<int, ChunkView> pair in chunks)
            {
                if (pair.Key < firstActive || pair.Key > lastActive)
                {
                    chunkToggleBuffer.Add(pair.Key);
                    if (pair.Key < firstActive - 1 || pair.Key > lastActive + 1)
                    {
                        chunkEvictionBuffer.Add(pair.Key);
                    }
                }
            }

            for (int i = 0; i < chunkToggleBuffer.Count; i++)
            {
                chunks[chunkToggleBuffer[i]].Root.SetActive(false);
            }

            for (int i = 0; i < chunkEvictionBuffer.Count; i++)
            {
                int index = chunkEvictionBuffer[i];
                ChunkView oldChunk = chunks[index];
                chunks.Remove(index);
                DestroyGeneratedObject(oldChunk.Mesh);
                DestroyGeneratedObject(oldChunk.BackWallMesh);
                DestroyGeneratedObject(oldChunk.Root);
            }

            for (int index = firstActive; index <= lastActive; index++)
            {
                if (!chunks.TryGetValue(index, out ChunkView chunk))
                {
                    chunk = BuildChunk(index);
                    chunks.Add(index, chunk);
                }

                chunk.Root.SetActive(true);
            }

        }

        private ChunkView BuildChunk(int index)
        {
            float startX = worldMinX + index * chunkWidth;
            int columns = Mathf.Max(4, Mathf.CeilToInt(chunkWidth / cellSize));
            int rows = Mathf.Max(4, Mathf.CeilToInt((waterSurfaceY - worldMinY) / cellSize));
            float stepX = chunkWidth / columns;
            float stepY = (waterSurfaceY - worldMinY) / rows;
            float[,] density = new float[columns + 3, rows + 1];
            float[,] rockDensity = new float[columns + 3, rows + 1];
            List<CaveFeature> caves = BuildCaveFeatures(startX - stepX, startX + chunkWidth + stepX);

            for (int column = -1; column <= columns + 1; column++)
            {
                float worldX = startX + column * stepX;
                float surfaceHeight = SampleSurfaceY(worldX);
                for (int row = 0; row <= rows; row++)
                {
                    float worldY = worldMinY + row * stepY;
                    rockDensity[column + 1, row] = surfaceHeight - worldY;
                    density[column + 1, row] = SampleDensity(worldX, worldY, surfaceHeight, caves);
                }
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            List<Vector3> backVertices = new List<Vector3>();
            List<Vector2> backUvs = new List<Vector2>();
            List<int> backTriangles = new List<int>();
            List<Segment> contourSegments = new List<Segment>();
            Vector2[] trianglePoints = new Vector2[3];
            float[] triangleDensity = new float[3];
            Vector2[] clipped = new Vector2[6];
            BackWallVertex[] backClipA = new BackWallVertex[8];
            BackWallVertex[] backClipB = new BackWallVertex[8];

            // One-cell collider halo joins adjacent chunks without a physical gap.
            for (int column = -1; column <= columns; column++)
            {
                float leftX = column * stepX;
                float rightX = (column + 1) * stepX;
                bool drawCell = column >= 0 && column < columns;
                for (int row = 0; row < rows; row++)
                {
                    float bottomY = worldMinY + row * stepY;
                    float topY = bottomY + stepY;
                    Vector2 bottomLeft = new Vector2(leftX, bottomY);
                    Vector2 bottomRight = new Vector2(rightX, bottomY);
                    Vector2 topRight = new Vector2(rightX, topY);
                    Vector2 topLeft = new Vector2(leftX, topY);

                    // Split a cell consistently so ambiguous cave corners cannot change connectivity.
                    ProcessTriangle(bottomLeft, density[column + 1, row],
                        topRight, density[column + 2, row + 1],
                        bottomRight, density[column + 2, row], drawCell, startX,
                        trianglePoints, triangleDensity, clipped, vertices, uvs, triangles, contourSegments);
                    if (drawCell)
                    {
                        ProcessBackWallTriangle(
                            bottomLeft, rockDensity[column + 1, row], density[column + 1, row],
                            topRight, rockDensity[column + 2, row + 1], density[column + 2, row + 1],
                            bottomRight, rockDensity[column + 2, row], density[column + 2, row],
                            startX, backClipA, backClipB, backVertices, backUvs, backTriangles);
                    }
                    ProcessTriangle(bottomLeft, density[column + 1, row],
                        topLeft, density[column + 1, row + 1],
                        topRight, density[column + 2, row + 1], drawCell, startX,
                        trianglePoints, triangleDensity, clipped, vertices, uvs, triangles, contourSegments);
                    if (drawCell)
                    {
                        ProcessBackWallTriangle(
                            bottomLeft, rockDensity[column + 1, row], density[column + 1, row],
                            topLeft, rockDensity[column + 1, row + 1], density[column + 1, row + 1],
                            topRight, rockDensity[column + 2, row + 1], density[column + 2, row + 1],
                            startX, backClipA, backClipB, backVertices, backUvs, backTriangles);
                    }
                }
            }

            Mesh mesh = new Mesh { name = "Seabed Terrain " + index.ToString("000") };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();

            GameObject root = new GameObject("Terrain Chunk " + index.ToString("000"));
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(startX, 0f, 0f);
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            renderer.sortingOrder = -90;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Mesh backMesh = BuildBackWallMesh(root.transform,
                backVertices, backUvs, backTriangles, index);
            AddContourColliders(root, contourSegments);
            return new ChunkView(root, mesh, backMesh);
        }

        private void ProcessTriangle(
            Vector2 a, float aDensity, Vector2 b, float bDensity, Vector2 c, float cDensity,
            bool draw, float chunkStartX, Vector2[] points, float[] values, Vector2[] clipped,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, List<Segment> segments)
        {
            points[0] = a;
            points[1] = b;
            points[2] = c;
            values[0] = aDensity;
            values[1] = bDensity;
            values[2] = cDensity;
            int clippedCount = 0;
            int crossingCount = 0;
            Vector2 firstCrossing = Vector2.zero;

            for (int edge = 0; edge < 3; edge++)
            {
                int next = (edge + 1) % 3;
                bool inside = values[edge] >= 0f;
                bool nextInside = values[next] >= 0f;
                if (draw && inside)
                {
                    clipped[clippedCount++] = points[edge];
                }

                if (inside == nextInside)
                {
                    continue;
                }

                float fraction = values[edge] / (values[edge] - values[next]);
                Vector2 crossing = Vector2.LerpUnclamped(points[edge], points[next], fraction);
                if (draw)
                {
                    clipped[clippedCount++] = crossing;
                }

                if (crossingCount == 0)
                {
                    firstCrossing = crossing;
                }
                else if ((firstCrossing - crossing).sqrMagnitude > 0.00000001f)
                {
                    segments.Add(new Segment(firstCrossing, crossing));
                }

                crossingCount++;
            }

            if (!draw || clippedCount < 3)
            {
                return;
            }

            int firstVertex = vertices.Count;
            for (int i = 0; i < clippedCount; i++)
            {
                Vector2 point = clipped[i];
                vertices.Add(new Vector3(point.x, point.y, 0f));
                uvs.Add(new Vector2((chunkStartX + point.x) / textureWorldSize, point.y / textureWorldSize));
            }

            for (int i = 1; i < clippedCount - 1; i++)
            {
                triangles.Add(firstVertex);
                triangles.Add(firstVertex + i);
                triangles.Add(firstVertex + i + 1);
            }
        }

        private void ProcessBackWallTriangle(
            Vector2 a, float aRock, float aSolid,
            Vector2 b, float bRock, float bSolid,
            Vector2 c, float cRock, float cSolid,
            float chunkStartX, BackWallVertex[] clipA, BackWallVertex[] clipB,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            clipA[0] = new BackWallVertex(a, aRock, aSolid);
            clipA[1] = new BackWallVertex(b, bRock, bSolid);
            clipA[2] = new BackWallVertex(c, cRock, cSolid);
            int count = ClipBackWallPolygon(clipA, 3, clipB, true);
            if (count < 3)
            {
                return;
            }

            count = ClipBackWallPolygon(clipB, count, clipA, false);
            if (count < 3)
            {
                return;
            }

            int firstVertex = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 point = clipA[i].Position;
                vertices.Add(new Vector3(point.x, point.y, 0f));
                uvs.Add(new Vector2((chunkStartX + point.x) / textureWorldSize,
                    point.y / textureWorldSize));
            }

            for (int i = 1; i < count - 1; i++)
            {
                triangles.Add(firstVertex);
                triangles.Add(firstVertex + i);
                triangles.Add(firstVertex + i + 1);
            }
        }

        private static int ClipBackWallPolygon(BackWallVertex[] input, int count,
            BackWallVertex[] output, bool keepRock)
        {
            int outputCount = 0;
            for (int i = 0; i < count; i++)
            {
                BackWallVertex current = input[i];
                BackWallVertex next = input[(i + 1) % count];
                float currentValue = keepRock ? current.RockDensity : -current.SolidDensity;
                float nextValue = keepRock ? next.RockDensity : -next.SolidDensity;
                bool currentInside = currentValue >= 0f;
                bool nextInside = nextValue >= 0f;
                if (currentInside)
                {
                    output[outputCount++] = current;
                }

                if (currentInside != nextInside)
                {
                    float fraction = currentValue / (currentValue - nextValue);
                    output[outputCount++] = BackWallVertex.Lerp(current, next, fraction);
                }
            }

            return outputCount;
        }

        private Mesh BuildBackWallMesh(Transform root, List<Vector3> vertices,
            List<Vector2> uvs, List<int> triangles, int index)
        {
            if (triangles.Count == 0)
            {
                return null;
            }

            Mesh mesh = new Mesh { name = "Cave Back Wall " + index.ToString("000") };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();

            GameObject back = new GameObject("Cave Back Wall");
            back.transform.SetParent(root, false);
            MeshFilter filter = back.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = back.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = backWallMaterial;
            renderer.sortingOrder = -95;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mesh;
        }

        private static void AddContourColliders(GameObject root, List<Segment> segments)
        {
            Dictionary<Vector2Int, List<int>> adjacent = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < segments.Count; i++)
            {
                AddAdjacent(adjacent, PointKey(segments[i].A), i);
                AddAdjacent(adjacent, PointKey(segments[i].B), i);
            }

            bool[] visited = new bool[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                Vector2Int a = PointKey(segments[i].A);
                Vector2Int b = PointKey(segments[i].B);
                if (visited[i] || (adjacent[a].Count != 1 && adjacent[b].Count != 1))
                {
                    continue;
                }

                TraceContour(root, segments, adjacent, visited, i, adjacent[a].Count == 1 ? a : b);
            }

            // Remaining contours are closed cave walls.
            for (int i = 0; i < segments.Count; i++)
            {
                if (!visited[i])
                {
                    TraceContour(root, segments, adjacent, visited, i, PointKey(segments[i].A));
                }
            }
        }

        private static void TraceContour(
            GameObject root, List<Segment> segments, Dictionary<Vector2Int, List<int>> adjacent,
            bool[] visited, int firstSegment, Vector2Int firstKey)
        {
            List<Vector2> path = new List<Vector2>();
            Segment initial = segments[firstSegment];
            path.Add(PointKey(initial.A) == firstKey ? initial.A : initial.B);
            Vector2Int currentKey = firstKey;
            int segmentIndex = firstSegment;

            for (int limit = 0; limit < segments.Count && segmentIndex >= 0; limit++)
            {
                visited[segmentIndex] = true;
                Segment segment = segments[segmentIndex];
                Vector2 nextPoint = PointKey(segment.A) == currentKey ? segment.B : segment.A;
                if ((nextPoint - path[path.Count - 1]).sqrMagnitude > 0.00000001f)
                {
                    path.Add(nextPoint);
                }

                currentKey = PointKey(nextPoint);
                if (currentKey == firstKey)
                {
                    break;
                }

                segmentIndex = -1;
                List<int> choices = adjacent[currentKey];
                for (int i = 0; i < choices.Count; i++)
                {
                    if (!visited[choices[i]])
                    {
                        segmentIndex = choices[i];
                        break;
                    }
                }
            }

            if (path.Count >= 2)
            {
                EdgeCollider2D collider = root.AddComponent<EdgeCollider2D>();
                collider.points = path.ToArray();
                collider.edgeRadius = 0.015f;
            }
        }

        private static void AddAdjacent(Dictionary<Vector2Int, List<int>> adjacent, Vector2Int key, int index)
        {
            if (!adjacent.TryGetValue(key, out List<int> indices))
            {
                indices = new List<int>(2);
                adjacent.Add(key, indices);
            }

            indices.Add(index);
        }

        private static Vector2Int PointKey(Vector2 point)
        {
            return new Vector2Int(Mathf.RoundToInt(point.x * 10000f), Mathf.RoundToInt(point.y * 10000f));
        }

        private float SampleSurfaceY(float worldX)
        {
            return SampleBaseSurfaceY(worldX);
        }

        private float SampleBaseSurfaceY(float worldX)
        {
            float tectonic = 0.5f + 0.5f * Mathf.Sin(worldX / 310f + Hash01(0, 41) * Mathf.PI * 2f);
            float basin = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.30f, 0.66f, FractalNoise(worldX * 0.0015f, 51, 4) * 0.68f + tectonic * 0.32f));
            float shelf = Mathf.Exp(-worldX * worldX / (2f * 240f * 240f));
            basin *= Mathf.Lerp(1f, 0.08f, shelf);
            float depth = Mathf.Lerp(66f, 360f, basin)
                + (FractalNoise(worldX * 0.006f, 83, 3) - 0.5f) * 32f
                + (FractalNoise(worldX * 0.035f, 97, 2) - 0.5f) * 8f;

            int featureCell = Mathf.FloorToInt(worldX / SurfaceFeatureWidth);
            for (int cell = featureCell - 1; cell <= featureCell + 1; cell++)
            {
                float canyonX = (cell + 0.5f) * SurfaceFeatureWidth + (Hash01(cell, 107) - 0.5f) * 65f;
                float canyonWidth = Mathf.Lerp(28f, 65f, Hash01(cell, 109));
                float canyonDistance = (worldX - canyonX) / canyonWidth;
                float canyonSlope = Mathf.Clamp01(1f - Mathf.Abs(canyonDistance));
                float canyon = canyonSlope > 0.65f
                    ? 0.85f + (canyonSlope - 0.65f) * (0.15f / 0.35f)
                    : canyonSlope * (0.85f / 0.65f);
                depth += Mathf.Lerp(20f, 68f, basin) * canyon;

                // A broad, low-relief depositional fan at the basinward side of a canyon.
                float fanX = canyonX + Mathf.Lerp(75f, 125f, Hash01(cell, 113));
                float fanDistance = (worldX - fanX) / 75f;
                depth -= 17f * basin * Mathf.Exp(-fanDistance * fanDistance * 1.4f);
            }

            int reefRegion = Mathf.FloorToInt(worldX / CaveRegionWidth);
            for (int region = reefRegion - 1; region <= reefRegion + 1; region++)
            {
                float reefX = CaveMouthX(region);
                float reefDistance = (worldX - reefX) / Mathf.Lerp(22f, 38f, Hash01(region, 191));
                float reefStrength = Mathf.Clamp01(1f - basin * 1.5f);
                depth -= Mathf.Lerp(20f, 42f, Hash01(region, 193)) * reefStrength *
                    Mathf.Exp(-reefDistance * reefDistance * 1.7f);
                if (region == 0)
                {
                    float shallowEntrance = Mathf.Exp(-reefDistance * reefDistance * 2f);
                    depth = Mathf.Lerp(depth, Mathf.Min(depth, 28f), shallowEntrance);
                }
            }

            return waterSurfaceY - Mathf.Clamp(depth, 26f, 410f);
        }

        private List<CaveFeature> BuildCaveFeatures(float minX, float maxX)
        {
            List<CaveFeature> result = new List<CaveFeature>();
            int first = Mathf.FloorToInt(minX / CaveRegionWidth) - 1;
            int last = Mathf.FloorToInt(maxX / CaveRegionWidth) + 1;
            for (int region = first; region <= last; region++)
            {
                if (region != 0 && Hash01(region, 157) < 0.4f)
                {
                    continue;
                }

                float mouthX = CaveMouthX(region);
                float mouthY = SampleSurfaceY(mouthX);
                float exitX = (region + 0.73f) * CaveRegionWidth +
                    (Hash01(region, 165) - 0.5f) * 16f;
                float exitY = SampleSurfaceY(exitX);
                // Fossil cave analogues are tied to exposed carbonate shelves,
                // not repeated through every deep marine basin.
                if (region != 0 && (waterSurfaceY - mouthY > 155f ||
                    waterSurfaceY - exitY > 155f))
                {
                    continue;
                }

                Vector2 previous = new Vector2(mouthX, mouthY + 2f);
                float previousHalfWidth = PassageHalfWidth(region, 0);
                const int passageSegments = 12;
                for (int step = 1; step <= passageSegments; step++)
                {
                    float t = step / (float)passageSegments;
                    float x = Mathf.Lerp(mouthX, exitX, t);
                    float surface = SampleSurfaceY(x);
                    float burial = Mathf.Lerp(22f, 34f, Hash01(region, 167)) +
                        (Hash01(region * 31 + step, 169) - 0.5f) * 5f;
                    float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 7f));
                    float departure = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 7f));
                    float opening = Mathf.Min(entry, departure);
                    Vector2 next = new Vector2(x, Mathf.Lerp(surface + 2f,
                        surface - burial, opening));
                    float halfWidth = PassageHalfWidth(region, step);
                    float openingX = step == 1 ? mouthX : step == passageSegments ? exitX : 0f;
                    float openingRadius = step == 1 || step == passageSegments
                        ? Mathf.Max(9f, halfWidth * 1.6f) : 0f;
                    AddCaveFeature(result, minX, maxX, previous, next,
                        previousHalfWidth, halfWidth, openingX, openingRadius);

                    if (step == 6)
                    {
                        Vector2 fracture = next + new Vector2(8f, -36f);
                        Vector2 gallery = fracture + new Vector2(23f, -6f);
                        AddCaveFeature(result, minX, maxX, next, fracture, 3.8f, 3.5f);
                        AddCaveFeature(result, minX, maxX, fracture, gallery, 3.5f, 6f);
                        AddChamber(result, minX, maxX, gallery, region, 14, 8f, 6f);
                    }

                    previous = next;
                    previousHalfWidth = halfWidth;
                }
            }

            return result;
        }

        private float PassageHalfWidth(int region, int step)
        {
            float total = 0f;
            float weightSum = 0f;
            for (int offset = -2; offset <= 2; offset++)
            {
                float weight = 3f - Mathf.Abs(offset);
                total += RawPassageHalfWidth(region, Mathf.Clamp(step + offset, 0, 12)) * weight;
                weightSum += weight;
            }
            return total / weightSum;
        }

        private float RawPassageHalfWidth(int region, int step)
        {
            float t = step / 12f;
            float broadPhase = Hash01(region, 181) * Mathf.PI * 2f;
            float finePhase = Hash01(region, 183) * Mathf.PI * 2f;
            float broad = Mathf.Sin(t * Mathf.PI * 3.1f + broadPhase) * 1.25f;
            float fine = Mathf.Sin(t * Mathf.PI * 7.3f + finePhase) * 0.45f;
            float baseWidth = Mathf.Lerp(4.3f, 5.3f + broad + fine,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(step, 12 - step) / 2f)));
            float firstCenter = Mathf.Lerp(3.5f, 4.7f, Hash01(region, 185));
            float secondCenter = Mathf.Lerp(7.3f, 8.8f, Hash01(region, 187));
            float firstRadius = Mathf.Lerp(1.8f, 2.7f, Hash01(region, 189));
            float secondRadius = Mathf.Lerp(1.7f, 2.8f, Hash01(region, 191));
            float firstRoom = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(1f - Mathf.Abs(step - firstCenter) / firstRadius));
            float secondRoom = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(1f - Mathf.Abs(step - secondCenter) / secondRadius));
            float roomWidth = firstRoom * Mathf.Lerp(3.2f, 5.4f, Hash01(region, 193)) +
                secondRoom * Mathf.Lerp(3.5f, 6f, Hash01(region, 195));
            return Mathf.Clamp(baseWidth + roomWidth, 3.6f, 13f);
        }

        private float CaveMouthX(int region)
        {
            return (region + 0.15f) * CaveRegionWidth + (Hash01(region, 163) - 0.5f) * 36f;
        }

        private static void AddCaveFeature(List<CaveFeature> result, float minX, float maxX,
            Vector2 start, Vector2 end, float startWidth, float endWidth,
            float openingX = 0f, float openingRadius = 0f)
        {
            Vector2 along = (end - start).normalized;
            Vector2 side = new Vector2(-along.y, along.x);
            Vector2 overlap = along * 1.5f;
            CaveFeature feature = new CaveFeature(start, end, new[]
            {
                start - overlap + side * startWidth,
                end + overlap + side * endWidth,
                end + overlap - side * endWidth,
                start - overlap - side * startWidth
            }, openingX, openingRadius);
            if (feature.MaxX >= minX && feature.MinX <= maxX)
            {
                result.Add(feature);
            }
        }

        private void AddChamber(List<CaveFeature> result, float minX, float maxX,
            Vector2 center, int region, int salt, float halfWidth, float halfHeight)
        {
            const int corners = 8;
            Vector2[] points = new Vector2[corners];
            for (int i = 0; i < corners; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / corners;
                float roughness = Mathf.Lerp(0.82f, 1.12f,
                    Hash01(region * 37 + i, salt * 19 + 211));
                points[i] = center + new Vector2(Mathf.Cos(angle) * halfWidth,
                    Mathf.Sin(angle) * halfHeight) * roughness;
            }

            CaveFeature feature = new CaveFeature(center, center, points);
            if (feature.MaxX >= minX && feature.MinX <= maxX)
            {
                result.Add(feature);
            }
        }

        private static float SampleDensity(float x, float y, float surfaceY, List<CaveFeature> caves)
        {
            float rockDepth = surfaceY - y;
            float density = rockDepth;
            for (int i = 0; i < caves.Count; i++)
            {
                CaveFeature cave = caves[i];
                if (x < cave.MinX - 3f || x > cave.MaxX + 3f ||
                    y < cave.MinY - 3f || y > cave.MaxY + 3f)
                {
                    continue;
                }

                // Only a designated mouth may break the surface. Everywhere else
                // the cavity keeps a minimum rock roof, including on steep hillsides.
                float roofThickness = 5f;
                if (cave.OpeningRadius > 0f)
                {
                    float distance = Mathf.Clamp01(Mathf.Abs(x - cave.OpeningX) /
                        cave.OpeningRadius);
                    roofThickness = Mathf.Lerp(-8f, 5f,
                        Mathf.SmoothStep(0f, 1f, distance));
                }
                float caveDensity = Mathf.Max(PolygonDistance(x, y, cave.Points),
                    roofThickness - rockDepth);
                density = Mathf.Min(density, caveDensity);
            }

            return density;
        }

        private static float PolygonDistance(float x, float y, Vector2[] points)
        {
            Vector2 point = new Vector2(x, y);
            float distanceSquared = float.PositiveInfinity;
            bool inside = false;
            for (int i = 0, previous = points.Length - 1; i < points.Length; previous = i++)
            {
                Vector2 a = points[previous];
                Vector2 b = points[i];
                Vector2 edge = b - a;
                float along = Mathf.Clamp01(Vector2.Dot(point - a, edge) /
                    Mathf.Max(0.000001f, edge.sqrMagnitude));
                distanceSquared = Mathf.Min(distanceSquared,
                    (point - (a + edge * along)).sqrMagnitude);
                if ((a.y > y) != (b.y > y) &&
                    x < a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y))
                {
                    inside = !inside;
                }
            }

            float distance = Mathf.Sqrt(distanceSquared);
            return inside ? -distance : distance;
        }

        private float FractalNoise(float position, int salt, int octaves)
        {
            float total = 0f;
            float weight = 1f;
            float weightSum = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += ValueNoise(position, salt + i * 1013) * weight;
                weightSum += weight;
                weight *= 0.5f;
                position *= 2f;
            }

            return total / weightSum;
        }

        private float ValueNoise(float position, int salt)
        {
            int left = Mathf.FloorToInt(position);
            float blend = position - left;
            blend = blend * blend * (3f - 2f * blend);
            return Mathf.Lerp(Hash01(left, salt), Hash01(left + 1, salt), blend);
        }

        private float Hash01(int value, int salt)
        {
            unchecked
            {
                uint hash = (uint)resolvedSeed ^ ((uint)value * 0x9e3779b9u) ^ ((uint)salt * 0x85ebca6bu);
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        private int GetChunkIndex(float worldX)
        {
            return Mathf.Clamp(Mathf.FloorToInt((worldX - worldMinX) / chunkWidth), 0, chunkCount - 1);
        }

        private Material CreateMaterial()
        {
            if (terrainTexture == null)
            {
                terrainTexture = Resources.Load<Texture2D>(PlaceholderTexturePath);
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                Debug.LogError("No shader found for seabed terrain.", this);
                return null;
            }

            if (terrainTexture != null)
            {
                terrainTexture.wrapMode = TextureWrapMode.Repeat;
            }

            Material material = new Material(shader)
            {
                name = "Seabed Terrain Temporary Material",
                color = Color.white,
                mainTexture = terrainTexture != null ? terrainTexture : Texture2D.whiteTexture,
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        private Material CreateBackWallMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            Texture2D texture = backWallTexture != null ? backWallTexture : terrainTexture;
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
            }

            return new Material(shader)
            {
                name = "Cave Back Wall Temporary Material",
                mainTexture = texture != null ? texture : Texture2D.whiteTexture,
                color = new Color(0.66f, 0.73f, 0.76f, 1f),
                hideFlags = HideFlags.DontSave
            };
        }

        private void ClearChunks()
        {
            foreach (ChunkView chunk in chunks.Values)
            {
                DestroyGeneratedObject(chunk.Mesh);
                DestroyGeneratedObject(chunk.BackWallMesh);
                DestroyGeneratedObject(chunk.Root);
            }

            chunks.Clear();
            lastCenterChunk = int.MinValue;
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

        private readonly struct BackWallVertex
        {
            public readonly Vector2 Position;
            public readonly float RockDensity;
            public readonly float SolidDensity;

            public BackWallVertex(Vector2 position, float rockDensity, float solidDensity)
            {
                Position = position;
                RockDensity = rockDensity;
                SolidDensity = solidDensity;
            }

            public static BackWallVertex Lerp(BackWallVertex a, BackWallVertex b, float t)
            {
                return new BackWallVertex(
                    Vector2.LerpUnclamped(a.Position, b.Position, t),
                    Mathf.LerpUnclamped(a.RockDensity, b.RockDensity, t),
                    Mathf.LerpUnclamped(a.SolidDensity, b.SolidDensity, t));
            }
        }

        private readonly struct CaveFeature
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly Vector2[] Points;
            public readonly float MinX, MaxX, MinY, MaxY;
            public readonly float OpeningX, OpeningRadius;

            public CaveFeature(Vector2 start, Vector2 end, Vector2[] points,
                float openingX = 0f, float openingRadius = 0f)
            {
                Start = start;
                End = end;
                Points = points;
                OpeningX = openingX;
                OpeningRadius = openingRadius;
                MinX = MaxX = points[0].x;
                MinY = MaxY = points[0].y;
                for (int i = 1; i < points.Length; i++)
                {
                    MinX = Mathf.Min(MinX, points[i].x);
                    MaxX = Mathf.Max(MaxX, points[i].x);
                    MinY = Mathf.Min(MinY, points[i].y);
                    MaxY = Mathf.Max(MaxY, points[i].y);
                }
            }
        }

        private readonly struct Segment
        {
            public readonly Vector2 A, B;
            public Segment(Vector2 a, Vector2 b) { A = a; B = b; }
        }

        private sealed class ChunkView
        {
            public readonly GameObject Root;
            public readonly Mesh Mesh;
            public readonly Mesh BackWallMesh;
            public ChunkView(GameObject root, Mesh mesh, Mesh backWallMesh)
            {
                Root = root;
                Mesh = mesh;
                BackWallMesh = backWallMesh;
            }
        }
    }
}
