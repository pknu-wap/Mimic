using System.Collections.Generic;
using UnityEngine;

namespace MimicCell.World
{
    // The sections describe swimming routes before the rock outlines are sampled.
    internal class CambrianShelfTerrainLayout
    {
        private enum Setting { Shelf, Slope, Basin }

        private readonly CambrianOceanWorld ocean;
        private readonly int seed;
        private readonly float left;
        private readonly float right;
        private readonly float surface;
        private readonly float bottom;
        private readonly float chunkWidth;
        private readonly float cell;
        private readonly float floorStep;
        private readonly float surfacePassageBottom;
        private readonly float[] floor;
        private readonly TerrainChunk[] chunks;
        private readonly List<Section> sections = new List<Section>();
        private readonly List<Sinkhole> sinkholes = new List<Sinkhole>();

        public float MainRadius { get; }
        public IReadOnlyList<TerrainPath> MainPaths => mainPaths;
        public IReadOnlyList<Sinkhole> Sinkholes => sinkholes;
        public IReadOnlyList<Section> Sections => sections;

        private readonly List<TerrainPath> mainPaths = new List<TerrainPath>();

        public CambrianShelfTerrainLayout(CambrianOceanWorld ocean, float cellSize, float sectionSpacing, float bodyRadius)
        {
            this.ocean = ocean;
            seed = ocean.Seed;
            left = ocean.WorldMinX;
            right = ocean.WorldMaxX;
            surface = ocean.SurfaceY;
            bottom = ocean.WorldMinY;
            chunkWidth = ocean.ChunkWidth;
            cell = cellSize;
            MainRadius = bodyRadius + cell * 1.6f;
            surfacePassageBottom = surface - 2f * MainRadius - cell;
            int samples = Mathf.CeilToInt((right - left) / cell);
            floor = new float[samples + 1];
            floorStep = (right - left) / samples;
            chunks = new TerrainChunk[Mathf.CeilToInt((right - left) / chunkWidth)];
            for (int i = 0; i < chunks.Length; i++)
                chunks[i] = new TerrainChunk();

            for (int i = 0; i < floor.Length; i++)
            {
                float x = left + i * floorStep;
                OceanColumnProfile column = ocean.GetColumnProfile(x);
                Setting setting = SettingAt(x);
                float depthRatio = Mathf.Lerp(0.91f, 0.98f, Noise(x / 150f, 3));
                float relief = setting == Setting.Basin ? 5f : 16f;
                float height = column.SurfaceY - (column.SurfaceY - column.SeabedY) * depthRatio;
                height += (Noise(x / 38f, 5) - 0.5f) * relief + (Noise(x / 14f, 7) - 0.5f) * 3f;
                floor[i] = Mathf.Clamp(height, bottom + 10f, column.BCBottomY - 2f * MainRadius);
            }

            BuildSections(sectionSpacing);
            foreach (Section section in sections)
                BuildWaterways(section);
            for (int i = 0; i < sections.Count - 1; i++)
            {
                Section a = sections[i];
                Section b = sections[i + 1];
                if (Hash(a.Key, 131) < 0.36f)
                {
                    Vector2 entry = a.Points[3];
                    Vector2 exit = b.Points[3];
                    Vector2 bend = Vector2.Lerp(entry, exit, 0.54f) + Vector2.down * 10f;
                    AddWater(entry, bend, MainRadius, MainRadius + 2f, true);
                    AddWater(bend, exit, MainRadius + 2f, MainRadius, true);
                }
            }
            foreach (Section section in sections)
                BuildRock(section);

            foreach (Section section in sections)
                if (SettingAt(section.X) == Setting.Shelf && (Mathf.Abs(section.X) < 0.1f || Hash(section.Key, 229) < 0.39f))
                    BuildShelfGrotto(section);
        }

        public float FloorAt(float x)
        {
            float sample = Mathf.Clamp((x - left) / floorStep, 0f, floor.Length - 1);
            int index = Mathf.Min((int)sample, floor.Length - 2);
            return Mathf.Lerp(floor[index], floor[index + 1], sample - index);
        }

        public float SolidAt(float x, float y)
        {
            Sample(x, y, FloorAt(x), ChunkAt(x), false, out _, out float solid, out _);
            return solid;
        }

        public void Sample(float x, float y, float floorY, int chunkIndex, bool render,
            out float caveWall, out float solid, out float backdrop)
        {
            TerrainChunk chunk = chunks[chunkIndex];
            var point = new Vector2(x, y);
            float rock = floorY - y;
            float empty = -cell * 4f;
            caveWall = backdrop = empty;
            foreach (RockShape shape in chunk.Rock)
            {
                if (shape.Bounds.Contains(point))
                    rock = Mathf.Max(rock, shape.Density(point));
            }

            // A surface lane connects the separate descents, including in the largest form.
            solid = Mathf.Min(rock, surfacePassageBottom - y);
            if (solid > empty)
            {
                foreach (TerrainPath path in chunk.Water)
                {
                    if (!path.Bounds.Contains(point))
                        continue;

                    float distance = path.Distance(point);
                    solid = Mathf.Min(solid, distance);
                    if (render && path.IsGrotto)
                        caveWall = Mathf.Max(caveWall, Mathf.Min(rock - cell, -distance));
                }
            }

            if (!render || solid > cell * 3f)
                return;

            foreach (RockShape shape in chunk.RearRock)
            {
                if (shape.Bounds.Contains(point))
                    backdrop = Mathf.Max(backdrop, shape.Density(point));
            }
            backdrop = Mathf.Min(backdrop, surface - 5f - y);
        }

        private void BuildSections(float spacing)
        {
            var centers = new List<float> { 0f };
            for (int direction = -1; direction <= 1; direction += 2)
            {
                float boundary = direction < 0 ? -left : right;
                float x = 0f;
                for (int index = 1; x < boundary; index++)
                {
                    x += spacing * Mathf.Lerp(0.77f, 1.31f, Hash(index * direction, 17));
                    if (x < boundary - spacing * 0.32f)
                        centers.Add(x * direction);
                }
            }
            centers.Sort();
            for (int i = 0; i < centers.Count; i++)
            {
                float x = centers[i];
                float minX = i == 0 ? left : (centers[i - 1] + x) * 0.5f;
                float maxX = i == centers.Count - 1 ? right : (centers[i + 1] + x) * 0.5f;
                float width = maxX - minX;
                OceanColumnProfile profile = ocean.GetColumnProfile(x);
                float floorY = FloorAt(x);
                int key = i + 37;
                float drift = Hash(key, 19) < 0.5f ? -1f : 1f;
                float shift = Mathf.Min(29f, width * 0.23f);
                var route = new[]
                {
                    new Vector2(x, surface - MainRadius),
                    new Vector2(x + drift * shift * 0.35f, Mathf.Lerp(profile.AABottomY, profile.ABBottomY, 0.35f)),
                    new Vector2(x - drift * shift * Mathf.Lerp(0.38f, 0.95f, Hash(key, 23)),
                        Mathf.Lerp(profile.ABBottomY, profile.BBBottomY, Mathf.Lerp(0.32f, 0.68f, Hash(key, 29)))),
                    new Vector2(x + drift * shift * Mathf.Lerp(0.20f, 1f, Hash(key, 31)),
                        Mathf.Lerp(profile.BBBottomY, profile.BCBottomY, 0.58f)),
                    new Vector2(x - drift * shift * 0.25f, floorY + MainRadius + 6f)
                };
                bool fork = width >= 94f && (Mathf.Abs(x) < 0.1f || Hash(key, 37) < 0.56f);
                sections.Add(new Section(key, x, width, drift, route, fork));
            }
        }

        private void BuildWaterways(Section section)
        {
            Vector2[] points = section.Points;
            AddWater(points[0], points[1], MainRadius + 5f, MainRadius, true);
            AddWater(points[3], points[4], MainRadius, MainRadius + 3f, true);
            if (section.Fork)
            {
                Vector2 room = points[2];
                float distance = MainRadius + section.PillarHalfWidth + 5f;
                var shoulder = new Vector2(section.Drift * distance, 6f);
                float forkHeight = Mathf.Max(39f, MainRadius * 3.1f);
                var inlet = room + Vector2.up * forkHeight;
                var outlet = room + Vector2.down * forkHeight;
                Vector2 primary = room + shoulder;
                Vector2 secondary = room - shoulder + Vector2.down * 5f;
                AddWater(points[1], inlet, MainRadius, MainRadius + 2f, true);
                AddWater(inlet, primary, MainRadius + 2f, MainRadius + 2f, true);
                AddWater(primary, outlet, MainRadius + 2f, MainRadius + 2f, true);
                AddWater(inlet, secondary, MainRadius - 1f, MainRadius * 0.78f, false);
                AddWater(secondary, outlet, MainRadius * 0.78f, MainRadius, false);
                AddWater(outlet, points[3], MainRadius + 2f, MainRadius, true);
            }
            else
            {
                float roomRadius = MainRadius + Mathf.Lerp(7f, 16f, Hash(section.Key, 41));
                AddWater(points[1], points[2], MainRadius, roomRadius, true);
                AddWater(points[2], points[3], roomRadius, MainRadius, true);
            }
        }

        private void BuildRock(Section section)
        {
            int key = section.Key;
            Setting setting = SettingAt(section.X);
            Vector2[] points = section.Points;
            float preferredSide = section.Drift;
            var sockets = new Vector2[2];
            var hasSocket = new bool[2];
            Vector2? scarp = null;
            // Low, wide deposits sit on the basin floor. Steeper packages accumulate above them.
            float toeX = section.X + preferredSide * section.Width * 0.27f;
            AddRock(new Vector2(toeX, FloorAt(toeX) + 3f), section.Width * 0.51f,
                setting == Setting.Basin ? 18f : 29f, -preferredSide * 0.47f, key, 47, false);

            for (int band = 1; band <= 3; band++)
            {
                Vector2 node = points[band];
                if (section.Fork && band == 2)
                {
                    float chamberOffset = MainRadius + section.PillarHalfWidth + 12f;
                    Vector2 leftWall = node + new Vector2(-chamberOffset - 15f, -9f);
                    Vector2 rightWall = node + new Vector2(chamberOffset + 15f, 7f);
                    AddRock(leftWall, 29f, 59f, 0.32f, key, 291, false);
                    AddRock(rightWall, 36f, 74f, -0.23f, key, 293, false);
                    AddRearConnection(sockets, hasSocket, 0, leftWall, key, 296);
                    AddRearConnection(sockets, hasSocket, 1, rightWall, key, 301);
                    AddScarp(ref scarp, preferredSide < 0f ? leftWall : rightWall, preferredSide, key, 303, setting);
                    continue;
                }

                for (int bank = 0; bank < 2; bank++)
                {
                    float sign = bank == 0 ? preferredSide : -preferredSide;
                    if (bank == 1 && Hash(key, 59 + band) < (setting == Setting.Basin ? 0.66f : 0.35f))
                        continue;

                    int salt = 61 + bank * 71 + band * 11;
                    float width = Mathf.Lerp(23f, 45f, Hash(key, salt));
                    float height = Mathf.Lerp(43f, 86f, Hash(key, salt + 1));
                    if (setting == Setting.Basin && band == 3)
                        height *= 0.65f;
                    float level = Mathf.Lerp(-21f, 16f, Hash(key, salt + 2)) + (bank == 0 ? 1f : -13f);
                    var center = node + new Vector2(sign * (MainRadius + width * 0.42f), level);
                    float dip = sign * Mathf.Lerp(-0.49f, 0.42f, Hash(key, salt + 3));
                    AddRock(center, width, height, dip, key, salt + 4, false);
                    int socket = sign < 0f ? 0 : 1;
                    AddRearConnection(sockets, hasSocket, socket, center, key, salt + 8);
                    if (bank == 0)
                        AddScarp(ref scarp, center, sign, key, salt + 10, setting);

                    if (Hash(key, salt + 5) < 0.73f)
                    {
                        Vector2 ledge = center + new Vector2(-sign * width * 0.31f, Mathf.Lerp(-10f, 17f, Hash(key, salt + 6)));
                        AddRock(ledge, width * 1.04f, Mathf.Lerp(12f, 23f, Hash(key, salt + 7)),
                            dip * 0.32f, key, salt + 9, false);
                    }
                }
            }

            if (scarp.HasValue)
            {
                float footX = Mathf.Lerp(scarp.Value.x, toeX, 0.65f) + preferredSide * 7f;
                var foot = new Vector2(footX, FloorAt(footX) - 8f);
                AddRibbon(scarp.Value, foot, 13f, 26f, key, 203, false);
            }
            for (int side = 0; side < sockets.Length; side++)
            {
                if (!hasSocket[side])
                    continue;
                Vector2 anchor = sockets[side];
                float footX = anchor.x + (side == 0 ? -1f : 1f) * 18f;
                AddRibbon(anchor, new Vector2(footX, FloorAt(footX) - 4f), 20f, 28f, key, 205 + side, true);
            }

            if (section.Fork)
            {
                Vector2 pillar = points[2];
                AddRock(pillar, section.PillarHalfWidth * 2f, 43f, preferredSide * 0.36f, key, 211, false);
                Vector2 rearAnchor = hasSocket[0] ? sockets[0] : sockets[1];
                AddRibbon(pillar + Vector2.down * 4f, rearAnchor, 10f, 24f, key, 213, true);
            }

        }

        private void AddScarp(ref Vector2? previous, Vector2 center, float outward, int key, int salt, Setting setting)
        {
            Vector2 anchor = center + Vector2.right * outward * 9f;
            if (previous.HasValue && setting != Setting.Basin)
            {
                float width = Mathf.Lerp(12f, 20f, Hash(key, salt));
                AddRibbon(previous.Value, anchor, width, width + 4f, key, salt + 1, false);
            }
            previous = anchor;
        }

        private void AddRearConnection(Vector2[] sockets, bool[] hasSocket, int index, Vector2 point, int key, int salt)
        {
            float outward = index == 0 ? -1f : 1f;
            var anchor = point + new Vector2(outward * 7f, 8f);
            AddRock(anchor, 44f, 77f, outward * 0.18f, key, salt, true);
            if (hasSocket[index])
            {
                AddRibbon(sockets[index], anchor, 18f, 24f, key, salt + 1, true);
            }
            sockets[index] = anchor;
            hasSocket[index] = true;
        }

        private void BuildShelfGrotto(Section section)
        {
            Vector2 shallow = section.Points[1];
            Vector2 deep = Vector2.Lerp(shallow, section.Points[2], 0.35f);
            for (int sample = 0; sample < 6; sample++)
            {
                Vector2 water = Vector2.Lerp(shallow + Vector2.up * 10f, deep, sample / 5f);
                if (TryCutGrotto(section, water, section.Drift) || TryCutGrotto(section, water, -section.Drift))
                    return;
            }
        }

        private bool TryCutGrotto(Section section, Vector2 water, float direction)
        {
            if (SolidAt(water.x, water.y) >= 0f)
                return false;

            int key = section.Key;
            for (float distance = 3f; distance <= 42f; distance += cell)
            {
                var face = new Vector2(water.x + distance * direction, water.y);
                if (face.x <= left + 18f || face.x >= right - 18f)
                    break;
                if (SolidAt(face.x, face.y) < 0f)
                    continue;

                Vector2 pocket = face + new Vector2(12f * direction, Mathf.Lerp(-5f, 2f, Hash(key, 233)));
                float pocketRadius = Mathf.Lerp(5f, 7f, Hash(key, 237));
                if (!HasRockCover(pocket, pocketRadius + cell))
                    return false;

                Vector2 mouth = face - Vector2.right * (direction * cell * 2.5f);
                Vector2 throat = Vector2.Lerp(face, pocket, 0.42f) + Vector2.down * 1.4f;
                AddWater(mouth, throat, 3.2f, 3.7f, false, true);
                AddWater(throat, pocket, 3.7f, pocketRadius, false, true);
                AddShelfSinkhole(key, pocket, direction);
                return true;
            }
            return false;
        }

        private bool HasRockCover(Vector2 position, float radius)
        {
            if (SolidAt(position.x, position.y) <= 0f)
                return false;

            for (int sample = 0; sample < 8; sample++)
            {
                float angle = sample * Mathf.PI * 0.25f;
                Vector2 shell = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (SolidAt(shell.x, shell.y) < cell * 0.5f)
                    return false;
            }
            return true;
        }

        private void AddShelfSinkhole(int key, Vector2 pocket, float direction)
        {
            float mouthY = surfacePassageBottom;
            float x = pocket.x + direction * 2f;
            if (Hash(key, 241) < 0.55f || ocean.GetZoneAt(new Vector2(x, mouthY + 2f)) != OceanDepthZone.AA ||
                SolidAt(x, mouthY - 6f) < cell || SolidAt(x, mouthY - 12f) < cell)
                return;

            var mouth = new Vector2(x, mouthY);
            var joint = Vector2.Lerp(mouth, pocket, 0.52f) + Vector2.left * direction * 2f;
            AddWater(mouth + Vector2.up * 4f, joint, 6f, 5f, false, true);
            AddWater(joint, pocket, 5f, 8f, false, true);
            sinkholes.Add(new Sinkhole(mouth, pocket.y));
        }

        private void AddRock(Vector2 center, float width, float height, float dip, int key, int salt, bool behind)
        {
            const int rows = 9;
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float head = Mathf.Lerp(0.37f, 0.81f, Hash(key, salt));
            var outline = new Vector2[rows * 2];
            for (int i = 0; i < rows; i++)
            {
                float t = i / (rows - 1f);
                float belly = Mathf.Sin(t * Mathf.PI);
                float broadness = Mathf.Lerp(0.70f, head, t) + belly * 0.34f;
                float offset = (t - 0.5f) * width * dip;
                float y = center.y + (2f * t - 1f) * halfHeight;
                float leftEdge = halfWidth * (broadness + (Hash(key, salt + 2 + i) - 0.5f) * 0.35f);
                float rightEdge = halfWidth * (broadness + (Hash(key, salt + 13 + i) - 0.5f) * 0.35f);
                outline[i] = new Vector2(center.x + offset - leftEdge, y + (Hash(key, salt + 23 + i) - 0.5f) * 3f);
                outline[outline.Length - 1 - i] = new Vector2(center.x + offset + rightEdge,
                    y + (Hash(key, salt + 33 + i) - 0.5f) * 3f);
            }

            AddOutline(outline, behind);
        }

        private void AddOutline(Vector2[] outline, bool behind)
        {
            var rock = new RockShape(outline, cell * 3f);
            for (int i = ChunkAt(rock.Bounds.xMin - cell); i <= ChunkAt(rock.Bounds.xMax + cell); i++)
            {
                List<RockShape> bucket = behind ? chunks[i].RearRock : chunks[i].Rock;
                bucket.Add(rock);
            }
        }

        private void AddWater(Vector2 from, Vector2 to, float startRadius, float endRadius, bool main, bool grotto = false)
        {
            var path = new TerrainPath(from, to, startRadius, endRadius, cell * 3f, grotto);
            if (main)
                mainPaths.Add(path);
            for (int i = ChunkAt(path.Bounds.xMin - cell); i <= ChunkAt(path.Bounds.xMax + cell); i++)
                chunks[i].Water.Add(path);
        }

        private void AddRibbon(Vector2 a, Vector2 b, float widthA, float widthB, int key, int salt, bool behind)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 16f)
                return;

            int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / 9f), 8, 20);
            Vector2 normal = Vector2.Perpendicular(delta).normalized;
            float phase = 64f * Hash(key, salt);
            var outline = new Vector2[steps * 2];
            for (int i = 0; i < steps; i++)
            {
                float t = i / (steps - 1f);
                float swell = Mathf.Sin(t * Mathf.PI);
                float bend = (Noise(t * 3.3f + phase, salt + 1) - 0.5f) * 22f * swell;
                float width = Mathf.Lerp(widthA, widthB, t) * Mathf.Lerp(0.73f, 1.22f, Noise(t * 5.6f + phase, salt + 3));
                Vector2 center = a + delta * t + normal * bend;
                outline[i] = center + normal * (width + swell * 7f * Hash(key, salt + i + 5));
                outline[outline.Length - 1 - i] = center - normal * (width + swell * 5f * Hash(key, salt + i + 37));
            }
            AddOutline(outline, behind);
        }

        private Setting SettingAt(float x)
        {
            float across = Mathf.Abs(x) / Mathf.Max(1f, (right - left) * 0.5f);
            float edge = across + (Noise(x / 230f, 251) - 0.5f) * 0.32f;
            return edge < 0.30f ? Setting.Shelf : edge < 0.71f ? Setting.Slope : Setting.Basin;
        }

        private int ChunkAt(float x) => Mathf.Clamp(Mathf.FloorToInt((x - left) / chunkWidth), 0, chunks.Length - 1);

        private float Noise(float x, int salt)
        {
            int index = Mathf.FloorToInt(x);
            return Mathf.SmoothStep(Hash(index, salt), Hash(index + 1, salt), x - index);
        }

        public float Hash(int id, int salt)
        {
            unchecked
            {
                uint hash = (uint)seed ^ (uint)id * 0x9e3779b9u ^ (uint)salt * 0x85ebca6bu;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777216f;
            }
        }

        internal readonly struct Section
        {
            public readonly int Key;
            public readonly float X, Width, Drift, PillarHalfWidth;
            public readonly Vector2[] Points;
            public readonly bool Fork;

            public Section(int key, float x, float width, float drift, Vector2[] points, bool fork)
            {
                Key = key;
                X = x;
                Width = width;
                Drift = drift;
                Points = points;
                Fork = fork;
                PillarHalfWidth = Mathf.Clamp(width * 0.072f, 6f, 10f);
            }
        }

        internal readonly struct Sinkhole
        {
            public readonly Vector2 Mouth;
            public readonly float ExitY;

            public Sinkhole(Vector2 mouth, float exitY)
            {
                Mouth = mouth;
                ExitY = exitY;
            }
        }

        private class TerrainChunk
        {
            public readonly List<RockShape> Rock = new List<RockShape>();
            public readonly List<RockShape> RearRock = new List<RockShape>();
            public readonly List<TerrainPath> Water = new List<TerrainPath>();
        }

        private readonly struct RockShape
        {
            private readonly Vector2[] outline;
            public readonly Rect Bounds;

            public RockShape(Vector2[] outline, float padding)
            {
                this.outline = outline;
                Vector2 min = outline[0];
                Vector2 max = min;
                for (int i = 1; i < outline.Length; i++)
                {
                    min = Vector2.Min(min, outline[i]);
                    max = Vector2.Max(max, outline[i]);
                }
                Bounds = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
            }

            public float Density(Vector2 point)
            {
                bool inside = false;
                float nearest = float.PositiveInfinity;
                Vector2 a = outline[outline.Length - 1];
                foreach (Vector2 b in outline)
                {
                    Vector2 edge = b - a;
                    Vector2 from = point - a;
                    float t = Mathf.Clamp01(Vector2.Dot(edge, from) / Mathf.Max(edge.sqrMagnitude, 0.0001f));
                    nearest = Mathf.Min(nearest, (from - edge * t).sqrMagnitude);
                    if ((a.y > point.y) != (b.y > point.y) && point.x < a.x + from.y * edge.x / edge.y)
                        inside = !inside;
                    a = b;
                }
                float distance = Mathf.Sqrt(nearest);
                return inside ? distance : -distance;
            }
        }

        internal readonly struct TerrainPath
        {
            public readonly Vector2 From, To;
            public readonly float RadiusA, RadiusB;
            public readonly Rect Bounds;
            public readonly bool IsGrotto;
            private readonly Vector2 delta;
            private readonly float inverseLengthSquared;

            public TerrainPath(Vector2 a, Vector2 b, float radiusA, float radiusB, float padding, bool grotto)
            {
                From = a;
                To = b;
                RadiusA = radiusA;
                RadiusB = radiusB;
                IsGrotto = grotto;
                delta = b - a;
                inverseLengthSquared = 1f / Mathf.Max(delta.sqrMagnitude, 0.001f);
                float reach = Mathf.Max(radiusA, radiusB) + padding;
                Bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - reach, Mathf.Min(a.y, b.y) - reach,
                    Mathf.Max(a.x, b.x) + reach, Mathf.Max(a.y, b.y) + reach);
            }

            public float Distance(Vector2 point)
            {
                Vector2 offset = point - From;
                float t = Mathf.Clamp01(Vector2.Dot(offset, delta) * inverseLengthSquared);
                return (offset - delta * t).magnitude - Mathf.Lerp(RadiusA, RadiusB, t);
            }
        }
    }
}
