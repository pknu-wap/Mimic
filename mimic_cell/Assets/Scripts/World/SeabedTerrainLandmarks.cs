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
            foreach (CambrianShelfTerrainLayout.Section section in layout.Sections)
            {
                Vector2 bottomWater = section.Points[4];
                float floor = layout.FloorAt(bottomWater.x);
                var vent = new Vector2(bottomWater.x, floor);
                if (layout.Hash(section.Key, 321) > 0.40f &&
                    oceanWorld.GetZoneAt(vent + Vector2.up * 2f) == OceanDepthZone.CC &&
                    layout.SolidAt(vent.x, vent.y + 2f) < 0f)
                {
                    landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.HydrothermalVent, vent,
                        0f, 0f, 8f, Mathf.Lerp(0.55f, 1f, layout.Hash(section.Key, 323)), SinkholeCurrent.Weak));
                }

                if (Mathf.Abs(section.X) < worldMaxX * 0.43f &&
                    (TryFindWallFace(section.Points[1], OceanDepthZone.AB, section.Drift, out Vector2 reef) ||
                     TryFindWallFace(section.Points[1], OceanDepthZone.AB, -section.Drift, out reef)))
                {
                    landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.ArchaeocyathReef, reef,
                        14f, 0f, 38f, 0f, SinkholeCurrent.Weak));
                }

                Vector2 chamber = section.Points[2];
                if (section.Fork)
                    chamber.x += section.Drift * (layout.MainRadius + section.PillarHalfWidth + 5f);
                if (layout.Hash(section.Key, 327) < 0.58f &&
                    TryFindWallFace(chamber, OceanDepthZone.BB, section.Drift, out Vector2 cliff))
                {
                    OceanColumnProfile column = oceanWorld.GetColumnProfile(cliff.x);
                    landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.SeafloorCliff, cliff,
                        0f, column.BCBottomY - 12f, 50f, column.BBBottomY - column.BCBottomY, SinkholeCurrent.Weak));
                }
            }

            for (int i = 0; i < layout.Sinkholes.Count; i++)
            {
                CambrianShelfTerrainLayout.Sinkhole shaft = layout.Sinkholes[i];
                float strength = layout.Hash(i, 337);
                var current = (SinkholeCurrent)Mathf.Min(2, (int)(strength * 3f));
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.Sinkhole, shaft.Mouth, 12f,
                    shaft.ExitY, Mathf.Lerp(20f, 50f, strength), strength, current));
            }
        }

        private bool TryFindWallFace(Vector2 water, OceanDepthZone zone, float side, out Vector2 position)
        {
            position = default;
            if (layout.SolidAt(water.x, water.y) >= 0f)
                return false;

            for (float distance = 2f; distance <= 52f; distance += 2f)
            {
                float x = water.x + distance * side;
                if (x < worldMinX + 8f || x > worldMaxX - 8f)
                    break;
                if (layout.SolidAt(x, water.y) < 0f)
                    continue;

                for (float offset = 2f; offset <= Mathf.Min(distance, 8f); offset += 2f)
                {
                    float markerX = x - offset * side;
                    var candidate = new Vector2(markerX, water.y);
                    if (layout.SolidAt(markerX, water.y) < 0f && layout.SolidAt(markerX, water.y + 2f) < 0f &&
                        oceanWorld.GetZoneAt(candidate + Vector2.up * 2f) == zone)
                    {
                        position = candidate;
                        return true;
                    }
                }
                break;
            }
            return false;
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
