using System.Collections.Generic;
using UnityEngine;

namespace MimicCell.World
{
    public enum SeabedLandmarkKind { HydrothermalVent, ArchaeocyathReef, SeafloorCliff, Sinkhole }
    public enum SinkholeCurrent { Weak, Normal, Strong }

    // Planned interaction sites. These records do not add renderers, colliders, or cave cuts.
    public readonly struct SeabedLandmark
    {
        public readonly SeabedLandmarkKind Kind;
        public readonly Vector2 Position;
        public readonly float OpeningWidth;
        public readonly float DestinationY;
        public readonly float InfluenceRadius;
        public readonly float Strength;
        public readonly SinkholeCurrent Current;

        public SeabedLandmark(SeabedLandmarkKind kind, Vector2 position, float openingWidth,
            float destinationY, float influenceRadius, float strength, SinkholeCurrent current)
        {
            Kind = kind;
            Position = position;
            OpeningWidth = openingWidth;
            DestinationY = destinationY;
            InfluenceRadius = influenceRadius;
            Strength = strength;
            Current = current;
        }
    }

    public sealed partial class SeabedTerrainWorld
    {
        private readonly List<SeabedLandmark> landmarks = new List<SeabedLandmark>();
        private Transform landmarkMarkerRoot;

        public IReadOnlyList<SeabedLandmark> Landmarks { get { return landmarks; } }

        private void BuildLandmarkLayout()
        {
            landmarks.Clear();
            AddReefSites();
            AddVentSites();
            AddCliffSites();
            AddSinkholeSites();
        }

        private void AddReefSites()
        {
            const float spacing = 170f;
            for (int cell = Mathf.FloorToInt(worldMinX / spacing);
                cell <= Mathf.FloorToInt(worldMaxX / spacing); cell++)
            {
                float x = (cell + 0.52f) * spacing + (Hash01(cell, 311) - 0.5f) * 32f;
                if (x < worldMinX + 55f || x > worldMaxX - 55f ||
                    Mathf.Abs(x) < 65f || Hash01(cell, 313) < 0.23f) continue;
                float y = SampleSurfaceY(x);
                float depth = waterSurfaceY - y;
                if (depth < 25f) continue;
                int size = Mathf.FloorToInt(Hash01(cell, 317) * 3f);
                float opening = size == 0 ? 14f : size == 1 ? 28f : 56f;
                if (opening + 10f > depth) opening = depth >= 38f ? 28f : 14f;
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.ArchaeocyathReef,
                    new Vector2(x, y), opening, 0f, opening + 24f, 0f,
                    SinkholeCurrent.Weak));
            }
        }

        private void AddVentSites()
        {
            const float spacing = 270f;
            for (int cell = Mathf.FloorToInt(worldMinX / spacing);
                cell <= Mathf.FloorToInt(worldMaxX / spacing); cell++)
            {
                float x = (cell + 0.37f) * spacing + (Hash01(cell, 321) - 0.5f) * 46f;
                if (x < worldMinX + 35f || x > worldMaxX - 35f ||
                    SurfaceZone(x) != OceanDepthZone.CC) continue;
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.HydrothermalVent,
                    new Vector2(x, SampleSurfaceY(x)), 0f, 0f, 8f,
                    Mathf.Lerp(0.55f, 1f, Hash01(cell, 323)), SinkholeCurrent.Weak));
            }
        }

        private void AddCliffSites()
        {
            const float spacing = 560f;
            for (int cell = Mathf.FloorToInt(worldMinX / spacing);
                cell <= Mathf.FloorToInt(worldMaxX / spacing); cell++)
            {
                float center = (cell + 0.45f) * spacing +
                    (Hash01(cell, 301) - 0.5f) * 70f;
                float bestX = float.NaN;
                float bestSlope = 0f;
                for (int sample = -5; sample <= 5; sample++)
                {
                    float x = center + sample * 38f;
                    if (x < worldMinX + 80f || x > worldMaxX - 80f ||
                        Mathf.Abs(x) < 220f || SurfaceZone(x - 32f) != OceanDepthZone.BB)
                        continue;
                    float slope = SampleSurfaceY(x - 32f) - SampleSurfaceY(x + 32f);
                    if (slope <= bestSlope) continue;
                    bestX = x;
                    bestSlope = slope;
                }
                if (float.IsNaN(bestX)) continue;
                OceanColumnProfile profile = oceanWorld.GetColumnProfile(bestX + 32f);
                float targetY = profile.BCBottomY - 12f;
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.SeafloorCliff,
                    new Vector2(bestX, SampleSurfaceY(bestX - 32f)), 0f,
                    targetY, 50f, Mathf.Max(0f, bestSlope), SinkholeCurrent.Weak));
            }
        }

        private void AddSinkholeSites()
        {
            const float spacing = 210f;
            bool foundShallowSite = false;
            for (int cell = Mathf.FloorToInt(worldMinX / spacing);
                cell <= Mathf.FloorToInt(worldMaxX / spacing); cell++)
            {
                float x = (cell + 0.47f) * spacing +
                    (Hash01(cell, 331) - 0.5f) * 34f;
                if (x < worldMinX + 30f || x > worldMaxX - 30f ||
                    Mathf.Abs(x) < 70f || SurfaceZone(x) != OceanDepthZone.AA)
                    continue;
                AddSinkholeSite(x, cell);
                foundShallowSite = true;
            }

            if (foundShallowSite) return;
            // The starting carbonate rise is shallow even when other shelves are not.
            float mouth = CaveMouthX(0);
            float bestX = mouth;
            float bestDistance = float.MaxValue;
            for (float x = mouth - 45f; x <= mouth + 45f; x += 5f)
            {
                if (SurfaceZone(x) != OceanDepthZone.AA) continue;
                float distance = Mathf.Abs(x - (mouth - 30f));
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestX = x;
            }
            AddSinkholeSite(bestX, 0);
        }

        private void AddSinkholeSite(float x, int salt)
        {
            OceanColumnProfile profile = oceanWorld.GetColumnProfile(x);
            int size = Mathf.FloorToInt(Hash01(salt, 333) * 3f);
            float opening = size == 0 ? 16f : size == 1 ? 28f : 44f;
            int destination = Mathf.FloorToInt(Hash01(salt, 335) * 3f);
            float endY = destination == 0 ? (profile.ABBottomY + profile.BBBottomY) * 0.5f :
                destination == 1 ? (profile.BBBottomY + profile.BCBottomY) * 0.5f :
                (profile.BCBottomY + profile.SeabedY) * 0.5f;
            float radius = size == 0 ? Mathf.Lerp(20f, 30f, Hash01(salt, 339)) :
                size == 1 ? Mathf.Lerp(30f, 40f, Hash01(salt, 339)) :
                Mathf.Lerp(40f, 50f, Hash01(salt, 339));
            landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.Sinkhole,
                new Vector2(x, SampleSurfaceY(x)), opening, endY, radius, 0f,
                (SinkholeCurrent)Mathf.FloorToInt(Hash01(salt, 337) * 3f)));
        }

        private OceanDepthZone SurfaceZone(float x)
        {
            return oceanWorld.GetZoneAt(new Vector2(x, SampleSurfaceY(x) + 2f));
        }

        private void CreateLandmarkMarkers()
        {
            GameObject root = new GameObject("Landmark Sites (no visuals)");
            root.transform.SetParent(transform, false);
            landmarkMarkerRoot = root.transform;
            for (int i = 0; i < landmarks.Count; i++)
            {
                SeabedLandmark site = landmarks[i];
                GameObject marker = new GameObject(site.Kind + " Site " + i.ToString("00"));
                marker.transform.SetParent(landmarkMarkerRoot, false);
                marker.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
            }
        }

        private void ClearLandmarkMarkers()
        {
            if (landmarkMarkerRoot == null) return;
            DestroyGeneratedObject(landmarkMarkerRoot.gameObject);
            landmarkMarkerRoot = null;
        }
    }
}
