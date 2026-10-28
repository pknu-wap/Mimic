namespace MimicCell.World
{
    public enum OceanDepthZone
    {
        AA,
        AB,
        BB,
        BC,
        CC
    }

    public readonly struct OceanColumnProfile
    {
        public OceanColumnProfile(
            float surfaceY,
            float aaBottomY,
            float abBottomY,
            float bbBottomY,
            float bcBottomY,
            float seabedY)
        {
            SurfaceY = surfaceY;
            AABottomY = aaBottomY;
            ABBottomY = abBottomY;
            BBBottomY = bbBottomY;
            BCBottomY = bcBottomY;
            SeabedY = seabedY;
        }

        public float SurfaceY { get; }
        public float AABottomY { get; }
        public float ABBottomY { get; }
        public float BBBottomY { get; }
        public float BCBottomY { get; }
        public float SeabedY { get; }

        public OceanDepthZone GetZone(float worldY)
        {
            if (worldY >= AABottomY)
            {
                return OceanDepthZone.AA;
            }

            if (worldY >= ABBottomY)
            {
                return OceanDepthZone.AB;
            }

            if (worldY >= BBBottomY)
            {
                return OceanDepthZone.BB;
            }

            if (worldY >= BCBottomY)
            {
                return OceanDepthZone.BC;
            }

            return OceanDepthZone.CC;
        }

        public float GetTop(OceanDepthZone zone)
        {
            switch (zone)
            {
                case OceanDepthZone.AA:
                    return SurfaceY;
                case OceanDepthZone.AB:
                    return AABottomY;
                case OceanDepthZone.BB:
                    return ABBottomY;
                case OceanDepthZone.BC:
                    return BBBottomY;
                default:
                    return BCBottomY;
            }
        }

        public float GetBottom(OceanDepthZone zone)
        {
            switch (zone)
            {
                case OceanDepthZone.AA:
                    return AABottomY;
                case OceanDepthZone.AB:
                    return ABBottomY;
                case OceanDepthZone.BB:
                    return BBBottomY;
                case OceanDepthZone.BC:
                    return BCBottomY;
                default:
                    return SeabedY;
            }
        }
    }
}
