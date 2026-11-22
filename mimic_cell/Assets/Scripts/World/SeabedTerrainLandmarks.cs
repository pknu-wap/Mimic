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
                float y = SampleTopSurfaceY(x);
                float depth = waterSurfaceY - y;
                if (depth < 25f) continue;
                int size = Mathf.FloorToInt(Hash01(cell, 317) * 3f);
                float opening = size == 0 ? 14f : size == 1 ? 28f : 56f;
                if (opening + 10f > depth) opening = depth >= 38f ? 28f : 14f;
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.ArchaeocyathReef,
                    new Vector2(x, y), opening, 0f, opening + 24f, 0f,
                    SinkholeCurrent.Weak));
            }

            // Raised carbonate faces provide attachment sites above the deep floor.
            for (int region = 0; region < upperRocks.Count; region++)
            {
                CaveFeature rock = upperRocks[region];
                float centerX = (rock.MinX + rock.MaxX) * 0.5f;
                OceanColumnProfile column = oceanWorld.GetColumnProfile(centerX);
                for (int band = 0; band < 4; band++)
                {
                    OceanDepthZone zone = (OceanDepthZone)band;
                    float y = (column.GetTop(zone) + column.GetBottom(zone)) * 0.5f;
                    float outsideX = rock.MinX - 6f;
                    bool wasWater = SampleDensity(outsideX, y,
                        SampleRockDensity(outsideX, y, SampleSurfaceY(outsideX))) < 0f;
                    for (float x = rock.MinX - 4f; x <= centerX; x += 2f)
                    {
                        bool solid = SampleDensity(x, y,
                            SampleRockDensity(x, y, SampleSurfaceY(x))) >= 0f;
                        if (solid && wasWater && x > worldMinX + 30f &&
                            Mathf.Abs(x) > 30f)
                        {
                            float opening = band == 0 ? 14f : band == 1 ? 28f : 56f;
                            landmarks.Add(new SeabedLandmark(
                                SeabedLandmarkKind.ArchaeocyathReef,
                                new Vector2(x - 2f, y), opening, 0f,
                                opening + 24f, 0f, SinkholeCurrent.Weak));
                            break;
                        }
                        wasWater = !solid;
                    }
                }
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
                    oceanWorld.GetZoneAt(new Vector2(x, SampleSurfaceY(x) + 2f)) !=
                    OceanDepthZone.CC) continue;
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.HydrothermalVent,
                    new Vector2(x, SampleSurfaceY(x)), 0f, 0f, 8f,
                    Mathf.Lerp(0.55f, 1f, Hash01(cell, 323)), SinkholeCurrent.Weak));
            }
        }

        private void AddCliffSites()
        {
            foreach (CaveFeature rock in upperRocks)
            {
                float centerX = (rock.MinX + rock.MaxX) * 0.5f;
                OceanColumnProfile profile = oceanWorld.GetColumnProfile(centerX);
                float upperB = (profile.ABBottomY + profile.BBBottomY) * 0.5f;
                if (rock.MaxY < upperB || rock.MaxX > worldMaxX - 30f ||
                    SampleSurfaceY(rock.MaxX + 5f) >= profile.BCBottomY - 15f)
                {
                    continue;
                }
                for (float x = rock.MaxX + 5f; x >= centerX; x -= 2f)
                {
                    if (SampleDensity(x, upperB,
                        SampleRockDensity(x, upperB, SampleSurfaceY(x))) < 0f)
                    {
                        continue;
                    }
                    landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.SeafloorCliff,
                        new Vector2(x + 2f, upperB), 0f,
                        profile.BCBottomY - 12f, 50f,
                        profile.BBBottomY - profile.BCBottomY, SinkholeCurrent.Weak));
                    break;
                }
            }
        }

        private void AddSinkholeSites()
        {
            foreach (SinkholeGeometry sinkhole in sinkholeGeometries)
            {
                if (sinkhole.MouthX < worldMinX + 30f ||
                    sinkhole.MouthX > worldMaxX - 30f) continue;
                int size = sinkhole.Opening <= 16f ? 0 : sinkhole.Opening <= 28f ? 1 : 2;
                float radius = size == 0 ? Mathf.Lerp(20f, 30f, Hash01(sinkhole.Salt, 339)) :
                    size == 1 ? Mathf.Lerp(30f, 40f, Hash01(sinkhole.Salt, 339)) :
                    Mathf.Lerp(40f, 50f, Hash01(sinkhole.Salt, 339));
                landmarks.Add(new SeabedLandmark(SeabedLandmarkKind.Sinkhole,
                    new Vector2(sinkhole.MouthX, SampleTopSurfaceY(sinkhole.MouthX)),
                    sinkhole.Opening, sinkhole.ExitY, radius, 0f,
                    (SinkholeCurrent)Mathf.FloorToInt(Hash01(sinkhole.Salt, 337) * 3f)));
            }
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
