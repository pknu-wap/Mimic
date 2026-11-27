using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MimicCell.World;
using UnityEditor;
using UnityEngine;

namespace MimicCell.EditorTools
{
    public static class SeabedTerrainValidation
    {
        private const int PrimarySeed = 541000;
        private const float GridStep = 4f;

        [MenuItem("Mimic/Validate Seabed Terrain")]
        public static void Run()
        {
            var stamps = new HashSet<int>();
            for (int i = 0; i < 20; i++)
                Check(stamps.Add(ValidateSeed(PrimarySeed + i * 547, i == 0)), "Two seeds generated identical terrain.");
            Debug.Log("SHELF_TERRAIN_VALIDATION_PASS 20 seeds, main-route clearance, shoreline collision and streaming");
        }

        private static int ValidateSeed(int seed, bool checkStreaming)
        {
            var root = new GameObject("Terrain Validation");
            var focus = new GameObject("Terrain Focus");
            focus.transform.SetParent(root.transform, false);
            var oceanObject = new GameObject("Validation Ocean");
            oceanObject.transform.SetParent(root.transform, false);
            var terrainObject = new GameObject("Validation Seabed");
            terrainObject.transform.SetParent(root.transform, false);
            try
            {
                var ocean = oceanObject.AddComponent<CambrianOceanWorld>();
                var settings = new SerializedObject(ocean);
                settings.FindProperty("randomizeSeedEachRun").boolValue = false;
                settings.FindProperty("fixedSeed").intValue = seed;
                settings.ApplyModifiedPropertiesWithoutUndo();
                ocean.Configure(null, focus.transform);
                var terrain = terrainObject.AddComponent<SeabedTerrainWorld>();
                terrain.Configure(ocean, null, focus.transform);
                ocean.RegisterTerrain(terrain);

                int stamp = ValidateWater(seed, ocean, terrain);
                ValidateMainRoutes(terrain);
                ValidateLandmarks(ocean, terrain);
                int meshStamp = ValidateMeshes(ocean, terrain);
                if (checkStreaming)
                {
                    ValidateCollision(ocean, terrain);
                    focus.transform.position = new Vector3(384f, 0f, 0f);
                    terrain.Configure(ocean, null, focus.transform);
                    focus.transform.position = Vector3.zero;
                    terrain.Configure(ocean, null, focus.transform);
                    Check(ValidateMeshes(ocean, terrain) == meshStamp, "Terrain meshes changed after chunk eviction.");
                }

                return stamp;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int ValidateWater(int seed, CambrianOceanWorld ocean, SeabedTerrainWorld terrain)
        {
            int width = Mathf.FloorToInt((ocean.WorldMaxX - ocean.WorldMinX) / GridStep);
            int height = Mathf.FloorToInt((ocean.SurfaceY - ocean.WorldMinY) / GridStep);
            var wet = new bool[width, height];
            var connected = new bool[width, height];
            var queue = new Queue<Vector2Int>();
            int water = 0;
            int stamp = 17;
            int narrowSections = 0;
            for (int row = 0; row < height; row++)
            {
                int run = 0;
                for (int column = 0; column < width; column++)
                {
                    Vector2 point = GridPoint(ocean, column, row);
                    bool open = !terrain.IsSolidAt(point);
                    wet[column, row] = open;
                    stamp = unchecked(stamp * 31 + (open ? 1 : 0));
                    if (open)
                    {
                        water++;
                        run++;
                        if (row == 0)
                        {
                            connected[column, row] = true;
                            queue.Enqueue(new Vector2Int(column, row));
                        }
                    }
                    else
                    {
                        if (run >= 3 && run <= 8)
                            narrowSections++;
                        run = 0;
                    }
                }
            }

            int reached = 0;
            int deepRoutes = 0;
            while (queue.Count != 0)
            {
                Vector2Int cell = queue.Dequeue();
                reached++;
                if (cell.y % 4 == 0 && cell.x % 4 == 0 && ocean.GetZoneAt(GridPoint(ocean, cell.x, cell.y)) == OceanDepthZone.CC)
                    deepRoutes++;
                Enqueue(cell.x - 1, cell.y);
                Enqueue(cell.x + 1, cell.y);
                Enqueue(cell.x, cell.y - 1);
                Enqueue(cell.x, cell.y + 1);
            }

            Check(water > 0 && reached >= water * 0.985f,
                $"Seed {seed} has inaccessible water. Reached {reached} of {water} cells.");
            Check(deepRoutes >= 25, $"Seed {seed} provides too few accessible deep-water areas.");
            Check(narrowSections >= 12, $"Seed {seed} has too few narrow passages.");
            for (int x = -12; x <= 12; x += 3)
                for (int depth = 2; depth <= 25; depth += 3)
                    Check(ocean.IsInsidePlayableArea(new Vector2(x, ocean.SurfaceY - depth)),
                        "Rock intersects the starting water.");

            Debug.Log($"SHELF_LAYOUT seed={seed} waterConnected={(float)reached / water:P2} deepSamples={deepRoutes} narrowSections={narrowSections}");
            return stamp;

            void Enqueue(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height || connected[x, y] || !wet[x, y])
                    return;
                connected[x, y] = true;
                queue.Enqueue(new Vector2Int(x, y));
            }
        }

        private static Vector2 GridPoint(CambrianOceanWorld ocean, int column, int row) =>
            new Vector2(ocean.WorldMinX + (column + 0.5f) * GridStep, ocean.SurfaceY - (row + 0.5f) * GridStep);

        private static void ValidateMainRoutes(SeabedTerrainWorld terrain)
        {
            object layout = typeof(SeabedTerrainWorld).GetField("layout", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(terrain);
            Type layoutType = layout.GetType();
            var paths = (IList)layoutType.GetProperty("MainPaths").GetValue(layout);
            float radius = (float)layoutType.GetProperty("MainRadius").GetValue(layout);
            float clearance = radius - new SerializedObject(terrain).FindProperty("cellSize").floatValue;
            Check(paths.Count > 12, "Too few protected routes.");
            foreach (object path in paths)
            {
                Type type = path.GetType();
                Vector2 from = (Vector2)type.GetField("From").GetValue(path);
                Vector2 to = (Vector2)type.GetField("To").GetValue(path);
                int segments = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 8f));
                for (int segment = 0; segment <= segments; segment++)
                {
                    Vector2 point = Vector2.Lerp(from, to, segment / (float)segments);
                    for (int sample = 0; sample < 12; sample++)
                    {
                        float angle = sample * Mathf.PI / 6f;
                        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * clearance;
                        Check(!terrain.IsSolidAt(point + offset), "A main route loses body clearance at " + point);
                    }
                }
            }

            int pillars = 0;
            foreach (object section in (IList)layoutType.GetProperty("Sections").GetValue(layout))
            {
                Type type = section.GetType();
                var points = (Vector2[])type.GetField("Points").GetValue(section);
                if ((bool)type.GetField("Fork").GetValue(section) && terrain.IsSolidAt(points[2]))
                    pillars++;
            }
            Check(pillars > 0, "The run has no surviving pillars in branched chambers.");
        }

        private static void ValidateLandmarks(CambrianOceanWorld ocean, SeabedTerrainWorld terrain)
        {
            int[] counts = new int[4];
            foreach (SeabedLandmark landmark in terrain.Landmarks)
            {
                counts[(int)landmark.Kind]++;
                Vector2 point = landmark.Position + Vector2.up * 2f;
                OceanDepthZone zone = ocean.GetZoneAt(point);
                Check(ocean.IsInsidePlayableArea(point), $"Landmark {landmark.Kind} at {point} is inside rock.");
                if (landmark.Kind == SeabedLandmarkKind.HydrothermalVent)
                    Check(zone == OceanDepthZone.CC, "Vent is outside the deepest water.");
                if (landmark.Kind == SeabedLandmarkKind.SeafloorCliff)
                    Check(zone == OceanDepthZone.BB, "Cliff is outside the middle water.");
                if (landmark.Kind == SeabedLandmarkKind.Sinkhole)
                {
                    Check(zone == OceanDepthZone.AA, "Sinkhole is outside the shallow water.");
                    Check(!terrain.IsSolidAt(landmark.Position + Vector2.down * 4f), "Sinkhole opening is blocked.");
                }
            }

            Check(counts[(int)SeabedLandmarkKind.ArchaeocyathReef] > 0 &&
                counts[(int)SeabedLandmarkKind.HydrothermalVent] > 0, "Shallow reefs or deep-water sites are missing.");
            Transform markers = terrain.transform.Find("Landmark Sites (no visuals)");
            Check(markers != null && markers.childCount == terrain.Landmarks.Count, "Landmark markers are incomplete.");
        }

        private static int ValidateMeshes(CambrianOceanWorld ocean, SeabedTerrainWorld terrain)
        {
            Check(terrain.ActiveChunkCount == 2 * ocean.ActiveChunkRadius + 1, "Unexpected active chunk count.");
            int stamp = 0;
            int caves = 0;
            int backdrop = 0;
            foreach (MeshFilter filter in terrain.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Check(mesh != null && mesh.vertexCount > 0, "A terrain layer has an empty mesh.");
                bool cave = mesh.name.StartsWith("Cave Back Wall", StringComparison.Ordinal);
                bool distant = mesh.name.StartsWith("Distant Rock", StringComparison.Ordinal);
                if (cave || distant)
                {
                    Check(filter.GetComponent<Collider2D>() == null, "Background rock has a collider.");
                    Check(filter.GetComponent<MeshRenderer>().sortingOrder == (cave ? -95 : -97),
                        "Background rock has the wrong draw order.");
                }

                caves += cave ? 1 : 0;
                backdrop += distant ? 1 : 0;
                foreach (Vector3 point in mesh.vertices)
                {
                    Check(float.IsFinite(point.x) && float.IsFinite(point.y) && point.y >= ocean.WorldMinY - 0.01f &&
                        point.y <= ocean.SurfaceY + 0.01f, "Terrain vertex is outside the water mesh.");
                    stamp = unchecked(stamp + filter.transform.TransformPoint(point).GetHashCode());
                }
            }
            Check(caves > 0 && backdrop > 0, "Active chunks did not generate both background layers.");
            return stamp;
        }

        private static void ValidateCollision(CambrianOceanWorld ocean, SeabedTerrainWorld terrain)
        {
            Physics2D.SyncTransforms();
            for (float x = -51f; x <= 80f; x += 3f)
            {
                RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, ocean.SurfaceY - 1f),
                    Vector2.down, ocean.SurfaceY - ocean.WorldMinY);
                Collider2D boundary = null;
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider != null && hit.collider.GetComponentInParent<SeabedTerrainWorld>() == terrain)
                    {
                        boundary = hit.collider;
                        Check(HasTerrainBoundary(terrain, hit.point),
                            "Terrain collision and solid sample disagree at " + hit.point);
                        break;
                    }
                }

                Check(boundary != null, "Downward terrain ray missed at x=" + x);
            }
        }

        private static bool HasTerrainBoundary(SeabedTerrainWorld terrain, Vector2 point)
        {
            bool rock = false;
            bool water = false;
            // The contour interpolates a grid cell. On a steep or thin lip the rock
            // can be to the side of the hit rather than two units straight below it.
            for (int sample = 0; sample < 16; sample++)
            {
                float angle = sample * Mathf.PI / 8f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool solid = terrain.IsSolidAt(point + direction * 2f);
                rock |= solid;
                water |= !solid;
            }
            return rock && water;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
