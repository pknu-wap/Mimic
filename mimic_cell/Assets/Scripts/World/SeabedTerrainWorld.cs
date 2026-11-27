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

        private static readonly int[] TileTextureIds =
        {
            Shader.PropertyToID("_Tile0"),
            Shader.PropertyToID("_Tile1"),
            Shader.PropertyToID("_Tile2"),
            Shader.PropertyToID("_Tile3")
        };
        private static readonly int TileSeedId = Shader.PropertyToID("_TileSeed");

        [Header("Terrain Art")]
        [SerializeField] private Texture2D terrainTexture;
        [SerializeField] private Shader terrainTileShader;
        [Tooltip("Assign four textures to vary the terrain tile by tile.")]
        [SerializeField] private Texture2D[] terrainTiles = Array.Empty<Texture2D>();
        [SerializeField, Min(0.5f)] private float textureWorldSize = 4f;
        [SerializeField] private Texture2D backWallTexture;

        [Header("Geometry")]
        [SerializeField, Range(0.75f, 3f)] private float cellSize = 1.5f;
        [SerializeField, Min(0.05f)] private float streamingInterval = 0.2f;

        [Header("Shelf Terrain")]
        [SerializeField, Range(95f, 160f)] private float sectionSpacing = 119f;
        [SerializeField, Min(0.5f)] private float minimumBodyRadius = 11.5f;

        private readonly Dictionary<int, ChunkView> chunks = new Dictionary<int, ChunkView>();
        private readonly List<int> chunkToggleBuffer = new List<int>();
        private readonly List<int> chunkEvictionBuffer = new List<int>();

        private CambrianOceanWorld oceanWorld;
        private Transform streamingTarget;
        private Material terrainMaterial;
        private Material backWallMaterial;
        private Material backdropMaterial;
        private CambrianShelfTerrainLayout layout;
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
            DestroyGeneratedObject(backdropMaterial);
        }

        private void OnValidate()
        {
            textureWorldSize = Mathf.Max(0.5f, textureWorldSize);
            cellSize = Mathf.Clamp(cellSize, 0.75f, 3f);
            streamingInterval = Mathf.Max(0.05f, streamingInterval);
            sectionSpacing = Mathf.Clamp(sectionSpacing, 95f, 160f);
            minimumBodyRadius = Mathf.Max(0.5f, minimumBodyRadius);
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
                DestroyGeneratedObject(backdropMaterial);
                terrainMaterial = null;
                backWallMaterial = null;
                backdropMaterial = null;
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
                layout = new CambrianShelfTerrainLayout(world, cellSize, sectionSpacing, GetBodyRadius(world));
                BuildLandmarkLayout();
                CreateLandmarkMarkers();
                terrainMaterial = CreateMaterial();
                backWallMaterial = CreateBackWallMaterial();
                Texture2D backdropTexture = backWallTexture != null ? backWallTexture : terrainTexture;
                backdropMaterial = CreateTerrainMaterial("Distant Rock", backdropTexture,
                    new Color(0.34f, 0.49f, 0.57f, 0.58f), false);
                initialized = true;
                Debug.Log("Seabed terrain ready with seed " + resolvedSeed + ".", this);
            }

            RefreshStreaming(true);
        }

        public float GetSurfaceY(float worldX)
        {
            return initialized ? layout.FloorAt(worldX) : float.NaN;
        }

        public bool IsSolidAt(Vector2 worldPosition)
        {
            return initialized && layout.SolidAt(worldPosition.x, worldPosition.y) >= 0f;
        }

        public bool TryGetFloorBelow(Vector2 position, out float floorY)
        {
            floorY = 0f;
            if (!initialized || IsSolidAt(position))
                return false;

            const float nearbyDepth = 32f;
            float step = cellSize;
            for (float y = position.y - step; y >= Mathf.Max(worldMinY, position.y - nearbyDepth); y -= step)
            {
                float density = layout.SolidAt(position.x, y);
                if (density < 0f)
                    continue;

                float above = layout.SolidAt(position.x, y + step);
                floorY = Mathf.Lerp(y, y + step, density / Mathf.Max(density - above, 0.0001f));
                return true;
            }
            return false;
        }

        private float GetBodyRadius(CambrianOceanWorld world)
        {
            float radiusSquared = minimumBodyRadius * minimumBodyRadius;
            var spawner = world.GetComponent<MimicCell.Creatures.CreatureSpawner>();
            if (spawner == null || spawner.Ecosystem == null)
                return minimumBodyRadius;

            foreach (var species in spawner.Ecosystem.Creatures)
            {
                if (species == null || species.Prefab == null || species.PlayerForm == null || !species.PlayerForm.IsConfigured)
                    continue;
                // Replication copies these paths and the offset to the player collider.
                var source = species.Prefab.GetComponentInChildren<Collider2D>(true) as PolygonCollider2D;
                if (source == null)
                    continue;
                for (int path = 0; path < source.pathCount; path++)
                    foreach (Vector2 point in source.GetPath(path))
                        radiusSquared = Mathf.Max(radiusSquared, (point + source.offset).sqrMagnitude);
            }
            return Mathf.Sqrt(radiusSquared);
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
                ClearChunk(oldChunk);
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
            float[,] caveDensity = new float[columns + 3, rows + 1];
            float[,] backdropDensity = new float[columns + 3, rows + 1];

            for (int column = -1; column <= columns + 1; column++)
            {
                float worldX = startX + column * stepX;
                float floorY = layout.FloorAt(worldX);
                for (int row = 0; row <= rows; row++)
                {
                    float worldY = worldMinY + row * stepY;
                    layout.Sample(worldX, worldY, floorY, index, column >= 0 && column <= columns,
                        out caveDensity[column + 1, row], out density[column + 1, row],
                        out backdropDensity[column + 1, row]);
                }
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            List<Vector3> backVertices = new List<Vector3>();
            List<Vector2> backUvs = new List<Vector2>();
            List<int> backTriangles = new List<int>();
            List<Vector3> distantVertices = new List<Vector3>();
            List<Vector2> distantUvs = new List<Vector2>();
            List<int> distantTriangles = new List<int>();
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
                    if (density[column + 1, row] > 0f && density[column + 2, row] > 0f &&
                        density[column + 1, row + 1] > 0f && density[column + 2, row + 1] > 0f)
                    {
                        int last = row + 1;
                        while (last < rows && density[column + 1, last + 1] > 0f && density[column + 2, last + 1] > 0f)
                            last++;

                        // Interior rock has no collision contour. One strip covers the whole run.
                        if (drawCell)
                            AddSolidStrip(leftX, rightX, bottomY, worldMinY + last * stepY, startX, vertices, uvs, triangles);

                        row = last - 1;
                        continue;
                    }

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
                            bottomLeft, caveDensity[column + 1, row], density[column + 1, row],
                            topRight, caveDensity[column + 2, row + 1], density[column + 2, row + 1],
                            bottomRight, caveDensity[column + 2, row], density[column + 2, row],
                            startX, backClipA, backClipB, backVertices, backUvs, backTriangles);
                        ProcessBackWallTriangle(
                            bottomLeft, backdropDensity[column + 1, row], density[column + 1, row],
                            topRight, backdropDensity[column + 2, row + 1], density[column + 2, row + 1],
                            bottomRight, backdropDensity[column + 2, row], density[column + 2, row],
                            startX, backClipA, backClipB, distantVertices, distantUvs, distantTriangles);
                    }
                    ProcessTriangle(bottomLeft, density[column + 1, row],
                        topLeft, density[column + 1, row + 1],
                        topRight, density[column + 2, row + 1], drawCell, startX,
                        trianglePoints, triangleDensity, clipped, vertices, uvs, triangles, contourSegments);
                    if (drawCell)
                    {
                        ProcessBackWallTriangle(
                            bottomLeft, caveDensity[column + 1, row], density[column + 1, row],
                            topLeft, caveDensity[column + 1, row + 1], density[column + 1, row + 1],
                            topRight, caveDensity[column + 2, row + 1], density[column + 2, row + 1],
                            startX, backClipA, backClipB, backVertices, backUvs, backTriangles);
                        ProcessBackWallTriangle(
                            bottomLeft, backdropDensity[column + 1, row], density[column + 1, row],
                            topLeft, backdropDensity[column + 1, row + 1], density[column + 1, row + 1],
                            topRight, backdropDensity[column + 2, row + 1], density[column + 2, row + 1],
                            startX, backClipA, backClipB, distantVertices, distantUvs, distantTriangles);
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
            Mesh backMesh = BuildBackgroundMesh(root.transform, "Cave Back Wall", backWallMaterial, -95,
                backVertices, backUvs, backTriangles, index);
            Mesh backdrop = BuildBackgroundMesh(root.transform, "Distant Rock", backdropMaterial, -97,
                distantVertices, distantUvs, distantTriangles, index);
            AddContourColliders(root, contourSegments);
            return new ChunkView(root, mesh, backMesh, backdrop);
        }

        private void AddSolidStrip(float left, float right, float bottom, float top, float chunkStartX,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(left, bottom, 0f));
            vertices.Add(new Vector3(left, top, 0f));
            vertices.Add(new Vector3(right, top, 0f));
            vertices.Add(new Vector3(right, bottom, 0f));
            uvs.Add(new Vector2((chunkStartX + left) / textureWorldSize, bottom / textureWorldSize));
            uvs.Add(new Vector2((chunkStartX + left) / textureWorldSize, top / textureWorldSize));
            uvs.Add(new Vector2((chunkStartX + right) / textureWorldSize, top / textureWorldSize));
            uvs.Add(new Vector2((chunkStartX + right) / textureWorldSize, bottom / textureWorldSize));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
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
            if ((aRock < 0f && bRock < 0f && cRock < 0f) || (aSolid > 0f && bSolid > 0f && cSolid > 0f))
                return;

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

        private static Mesh BuildBackgroundMesh(Transform root, string name, Material material, int order,
            List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int index)
        {
            if (triangles.Count == 0)
            {
                return null;
            }

            Mesh mesh = new Mesh { name = name + " " + index.ToString("000") };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();

            GameObject back = new GameObject(name);
            back.transform.SetParent(root, false);
            MeshFilter filter = back.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = back.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
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

            return CreateTerrainMaterial("Seabed Terrain", terrainTexture, new Color(0.81f, 0.75f, 0.67f, 1f), true);
        }

        private Material CreateBackWallMaterial()
        {
            Texture2D texture = backWallTexture != null ? backWallTexture : terrainTexture;
            return CreateTerrainMaterial("Cave Back Wall", texture,
                new Color(0.46f, 0.54f, 0.55f, 0.88f), backWallTexture == null);
        }

        private Material CreateTerrainMaterial(string materialName, Texture2D fallbackTexture, Color tint, bool useTerrainTiles)
        {
            bool useVariants = useTerrainTiles && terrainTileShader != null && terrainTileShader.isSupported &&
                terrainTiles != null && terrainTiles.Length == TileTextureIds.Length;
            Shader shader = useVariants
                ? terrainTileShader
                : Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("No shader found for seabed terrain.", this);
                return null;
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = tint,
                hideFlags = HideFlags.DontSave
            };

            if (useVariants)
            {
                // Use only seed bits that fit exactly in the shader's float property.
                material.SetFloat(TileSeedId, resolvedSeed & 0x00ffffff);
                for (int i = 0; i < TileTextureIds.Length; i++)
                {
                    Texture2D tile = terrainTiles[i] != null ? terrainTiles[i] : fallbackTexture;
                    material.SetTexture(TileTextureIds[i], tile != null ? tile : Texture2D.whiteTexture);
                }
            }
            else
            {
                if (fallbackTexture != null)
                {
                    fallbackTexture.wrapMode = TextureWrapMode.Repeat;
                }

                material.mainTexture = fallbackTexture != null ? fallbackTexture : Texture2D.whiteTexture;
            }

            return material;
        }

        private void ClearChunks()
        {
            foreach (ChunkView chunk in chunks.Values)
            {
                ClearChunk(chunk);
            }

            chunks.Clear();
            lastCenterChunk = int.MinValue;
        }

        private static void ClearChunk(ChunkView chunk)
        {
            DestroyGeneratedObject(chunk.Mesh);
            DestroyGeneratedObject(chunk.BackWallMesh);
            DestroyGeneratedObject(chunk.BackdropMesh);
            DestroyGeneratedObject(chunk.Root);
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
            public readonly Mesh BackdropMesh;
            public ChunkView(GameObject root, Mesh mesh, Mesh backWallMesh, Mesh backdropMesh)
            {
                Root = root;
                Mesh = mesh;
                BackWallMesh = backWallMesh;
                BackdropMesh = backdropMesh;
            }
        }
    }
}
