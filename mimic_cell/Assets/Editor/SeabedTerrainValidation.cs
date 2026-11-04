using System;
using System.Collections;
using System.Reflection;
using MimicCell.World;
using UnityEditor;
using UnityEngine;

namespace MimicCell.EditorTools
{
    public static class SeabedTerrainValidation
    {
        [MenuItem("Mimic/Validate Seabed Terrain")]
        public static void Run()
        {
            GameObject worldObject = null;
            GameObject terrainObject = null;
            GameObject targetObject = null;
            GameObject alternateWorldObject = null;
            GameObject alternateTerrainObject = null;
            try
            {
                targetObject = new GameObject("Validation Target");
                worldObject = new GameObject("Validation Ocean");
                CambrianOceanWorld world = worldObject.AddComponent<CambrianOceanWorld>();
                SerializedObject serializedWorld = new SerializedObject(world);
                serializedWorld.FindProperty("randomizeSeedEachRun").boolValue = false;
                serializedWorld.FindProperty("fixedSeed").intValue = 541000;
                serializedWorld.ApplyModifiedPropertiesWithoutUndo();
                world.Configure(null, targetObject.transform);

                terrainObject = new GameObject("Validation Terrain");
                SeabedTerrainWorld terrain = terrainObject.AddComponent<SeabedTerrainWorld>();
                terrain.Configure(world, null, targetObject.transform);
                world.RegisterTerrain(terrain);

                float minimumDepth = float.MaxValue;
                float maximumDepth = float.MinValue;
                int sampledColumns = 0;
                int accessibleABColumns = 0;
                int accessibleBCColumns = 0;
                float[] zoneShareTotals = new float[5];
                for (float x = -960f; x <= 960f; x += 20f)
                {
                    float terrainY = terrain.GetSurfaceY(x);
                    float depth = world.SurfaceY - terrainY;
                    minimumDepth = Mathf.Min(minimumDepth, depth);
                    maximumDepth = Mathf.Max(maximumDepth, depth);
                    OceanColumnProfile column = world.GetColumnProfile(x);
                    sampledColumns++;
                    if (terrainY < column.AABottomY) accessibleABColumns++;
                    if (terrainY < column.BBBottomY) accessibleBCColumns++;
                    Check(column.SurfaceY > column.AABottomY &&
                        column.AABottomY > column.ABBottomY &&
                        column.ABBottomY > column.BBBottomY &&
                        column.BBBottomY > column.BCBottomY &&
                        column.BCBottomY > column.DepthReferenceY,
                        "Water band order failed at x=" + x);
                    float aaThickness = column.SurfaceY - column.AABottomY;
                    float abThickness = column.AABottomY - column.ABBottomY;
                    float bbThickness = column.ABBottomY - column.BBBottomY;
                    float bcThickness = column.BBBottomY - column.BCBottomY;
                    float ccThickness = column.BCBottomY - column.DepthReferenceY;
                    float columnDepth = column.SurfaceY - column.DepthReferenceY;
                    zoneShareTotals[0] += aaThickness / columnDepth;
                    zoneShareTotals[1] += abThickness / columnDepth;
                    zoneShareTotals[2] += bbThickness / columnDepth;
                    zoneShareTotals[3] += bcThickness / columnDepth;
                    zoneShareTotals[4] += ccThickness / columnDepth;
                }

                float[] expectedShares = { 0.11f, 0.15f, 0.26f, 0.18f, 0.30f };
                for (int zone = 0; zone < expectedShares.Length; zone++)
                    Check(Mathf.Abs(zoneShareTotals[zone] / sampledColumns - expectedShares[zone]) < 0.03f,
                        "Mean water band share differs from design at zone " + zone);

                int[] landmarkCounts = new int[4];
                foreach (SeabedLandmark landmark in terrain.Landmarks)
                {
                    landmarkCounts[(int)landmark.Kind]++;
                    Check(landmark.Position.x >= world.WorldMinX &&
                        landmark.Position.x <= world.WorldMaxX,
                        "Landmark site is outside the map.");
                    OceanDepthZone zone = world.GetZoneAt(landmark.Position + Vector2.up * 2f);
                    if (landmark.Kind == SeabedLandmarkKind.HydrothermalVent)
                        Check(zone == OceanDepthZone.CC, "Vent outside C zone.");
                    if (landmark.Kind == SeabedLandmarkKind.Sinkhole)
                        Check(zone == OceanDepthZone.AA, "Sinkhole outside A zone.");
                    if (landmark.Kind == SeabedLandmarkKind.SeafloorCliff)
                    {
                        float upperX = landmark.Position.x - 32f;
                        Check(world.GetZoneAt(new Vector2(upperX,
                            terrain.GetSurfaceY(upperX) + 2f)) == OceanDepthZone.BB,
                            "Planned cliff site is outside B.");
                    }
                }
                Check(landmarkCounts[(int)SeabedLandmarkKind.ArchaeocyathReef] > 0 &&
                    landmarkCounts[(int)SeabedLandmarkKind.Sinkhole] > 0,
                    "Expected landmark sites were not recorded.");
                Transform siteRoot = terrain.transform.Find("Landmark Sites (no visuals)");
                Check(siteRoot != null && siteRoot.childCount == terrain.Landmarks.Count,
                    "Landmark position markers are missing.");
                Check(terrain.GetComponentsInChildren<PolygonCollider2D>(true).Length == 0 &&
                    terrain.GetComponentsInChildren<LineRenderer>(true).Length == 0,
                    "Placeholder interaction geometry is still active.");

                Check(maximumDepth - minimumDepth > 120f,
                    "Regional terrain relief too small: " + (maximumDepth - minimumDepth));
                Check(terrain.ActiveChunkCount == 5, "Unexpected terrain active chunk count.");
                Check(terrain.GetComponentsInChildren<EdgeCollider2D>().Length > 0,
                    "Terrain generated no edge colliders.");
                Check(terrain.GetComponentsInChildren<MeshRenderer>().Length >
                    terrain.ActiveChunkCount, "Passage back wall was not generated.");
                int backWallVertices = 0;
                foreach (MeshFilter filter in terrain.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null ||
                        !filter.sharedMesh.name.StartsWith("Cave Back Wall"))
                    {
                        continue;
                    }

                    foreach (Vector3 localVertex in filter.sharedMesh.vertices)
                    {
                        Vector3 vertex = filter.transform.TransformPoint(localVertex);
                        Check(vertex.y <= terrain.GetSurfaceY(vertex.x) + 2f,
                            "Back wall protrudes above seabed at " + vertex);
                        backWallVertices++;
                    }
                }

                Check(backWallVertices > 0, "Back wall has no visible geometry.");
                Physics2D.SyncTransforms();
                float[] collisionProbeX = { -62f, -33f, -1f, 0f, 1f, 31f, 33f, 62f, 94f };
                foreach (float probeX in collisionProbeX)
                {
                    bool hitTerrain = false;
                    RaycastHit2D[] hits = Physics2D.RaycastAll(
                        new Vector2(probeX, world.SurfaceY - 1f), Vector2.down, 500f);
                    foreach (RaycastHit2D hit in hits)
                    {
                        if (hit.collider != null && hit.collider.transform.IsChildOf(terrain.transform))
                        {
                            hitTerrain = true;
                            break;
                        }
                    }

                    Check(hitTerrain, "Downward ray missed terrain at x=" + probeX);
                }

                Type terrainType = typeof(SeabedTerrainWorld);
                BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                IList tunnels = (IList)terrainType.GetField("tunnelSegments", hidden)
                    .GetValue(terrain);
                IList boulders = (IList)terrainType.GetField("chamberBoulders", hidden)
                    .GetValue(terrain);
                Check(tunnels.Count > 10 && boulders.Count > 0,
                    "Expected branching caves and chamber obstacles were not generated.");
                for (int i = 0; i < tunnels.Count; i++)
                {
                    object tunnel = tunnels[i];
                    Type tunnelType = tunnel.GetType();
                    Vector2 start = (Vector2)tunnelType.GetField("Start").GetValue(tunnel);
                    Vector2 end = (Vector2)tunnelType.GetField("End").GetValue(tunnel);
                    for (int sample = 0; sample <= 10; sample++)
                        Check(!terrain.IsSolidAt(Vector2.Lerp(start, end, sample / 10f)),
                            "Tunnel center is blocked at segment " + i +
                            " sample " + sample);
                }
                SeabedLandmark firstSinkhole = default;
                foreach (SeabedLandmark landmark in terrain.Landmarks)
                    if (landmark.Kind == SeabedLandmarkKind.Sinkhole)
                    {
                        firstSinkhole = landmark;
                        break;
                    }
                Check(HasSurfaceOpening(terrain, firstSinkhole.Position.x),
                    "Sinkhole mouth is sealed.");
                float originalSurface = terrain.GetSurfaceY(0f);
                int originalLandmarkCount = terrain.Landmarks.Count;
                Vector2 originalLandmark = terrain.Landmarks[0].Position;
                targetObject.transform.position = new Vector3(320f, 0f, 0f);
                terrain.Configure(world, null, targetObject.transform);
                targetObject.transform.position = Vector3.zero;
                terrain.Configure(world, null, targetObject.transform);
                Check(Mathf.Abs(originalSurface - terrain.GetSurfaceY(0f)) < 0.0001f,
                    "Terrain changed after chunk streaming.");
                Check(terrain.Landmarks.Count == originalLandmarkCount &&
                    terrain.Landmarks[0].Position == originalLandmark,
                    "Landmark layout changed after chunk streaming.");

                alternateWorldObject = new GameObject("Alternate Seed Ocean");
                CambrianOceanWorld alternateWorld = alternateWorldObject.AddComponent<CambrianOceanWorld>();
                SerializedObject alternateSettings = new SerializedObject(alternateWorld);
                alternateSettings.FindProperty("randomizeSeedEachRun").boolValue = false;
                alternateSettings.FindProperty("fixedSeed").intValue = 541001;
                alternateSettings.ApplyModifiedPropertiesWithoutUndo();
                alternateWorld.Configure(null, targetObject.transform);
                alternateTerrainObject = new GameObject("Alternate Seed Terrain");
                SeabedTerrainWorld alternateTerrain = alternateTerrainObject.AddComponent<SeabedTerrainWorld>();
                alternateTerrain.Configure(alternateWorld, null, targetObject.transform);
                Check(Mathf.Abs(terrain.GetSurfaceY(500f) - alternateTerrain.GetSurfaceY(500f)) > 0.5f ||
                    Mathf.Abs(terrain.GetSurfaceY(-500f) - alternateTerrain.GetSurfaceY(-500f)) > 0.5f,
                    "Different run seeds generated the same terrain.");

                Debug.Log("TERRAIN_VALIDATION_PASS relief=" +
                    (maximumDepth - minimumDepth).ToString("0.0") +
                    " caveSegments=" + tunnels.Count +
                    " chamberObstacles=" + boulders.Count +
                    " openAB=" + accessibleABColumns + "/" + sampledColumns +
                    " openBC=" + accessibleBCColumns + "/" + sampledColumns +
                    " seed=" + world.Seed);
            }
            finally
            {
                if (alternateTerrainObject != null) UnityEngine.Object.DestroyImmediate(alternateTerrainObject);
                if (alternateWorldObject != null) UnityEngine.Object.DestroyImmediate(alternateWorldObject);
                if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
                if (worldObject != null) UnityEngine.Object.DestroyImmediate(worldObject);
                if (targetObject != null) UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool HasSurfaceOpening(SeabedTerrainWorld terrain, float centerX)
        {
            for (float x = centerX - 10f; x <= centerX + 10f; x += 1f)
                if (!terrain.IsSolidAt(new Vector2(x, terrain.GetSurfaceY(x) - 1f)))
                    return true;
            return false;
        }
    }
}
