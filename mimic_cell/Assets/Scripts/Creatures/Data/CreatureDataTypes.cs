using System;
using UnityEngine;

namespace MimicCell.Creatures
{
    public enum CreatureSpawnKind
    {
        Individual,
        Group
    }

    public enum CreatureFoodTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    public enum CreatureBodySize
    {
        Small,
        Medium,
        Large
    }

    [Flags]
    public enum CreatureHabitatMask
    {
        None = 0,

        OceanAA = 1 << 0,
        OceanAB = 1 << 1,
        OceanBB = 1 << 2,
        OceanBC = 1 << 3,
        OceanCC = 1 << 4,

        AllOcean =
            OceanAA |
            OceanAB |
            OceanBB |
            OceanBC |
            OceanCC
    }

    [Flags]
    public enum CreatureTraits
    {
        None = 0,

        Swimming = 1 << 0,
        Sprinting = 1 << 1,
        Sensing = 1 << 2,
        VerticalMovement = 1 << 3,
        Capturing = 1 << 4,
        Spiked = 1 << 5,
        Armored = 1 << 6,
        Benthic = 1 << 7,
        Floating = 1 << 8
    }

    [Serializable]
    public struct CreatureIntRange
    {
        [SerializeField] private int min;
        [SerializeField] private int max;

        public int Min => min;
        public int Max => max;

        public int Evaluate(float normalizedVariation)
        {
            float variation = Mathf.Clamp01(normalizedVariation);
            return Mathf.RoundToInt(Mathf.Lerp(min, max, variation));
        }

        public void Validate(int minimumAllowed)
        {
            min = Mathf.Max(minimumAllowed, min);
            max = Mathf.Max(min, max);
        }
    }

    [Serializable]
    public struct CreatureDesignSizeRange
    {
        [SerializeField] private Vector2 min;
        [SerializeField] private Vector2 max;

        public Vector2 Min => min;
        public Vector2 Max => max;

        public Vector2 Evaluate(float normalizedVariation)
        {
            float variation = Mathf.Clamp01(normalizedVariation);
            return Vector2.Lerp(min, max, variation);
        }

        public void Validate()
        {
            min.x = Mathf.Max(0f, min.x);
            min.y = Mathf.Max(0f, min.y);

            max.x = Mathf.Max(min.x, max.x);
            max.y = Mathf.Max(min.y, max.y);
        }
    }

    public readonly struct CreatureSpawnContext
    {
        public CreatureSpawnContext(
            Vector2 position,
            float variation,
            int regionId,
            int stableSeed)
        {
            Position = position;
            Variation = Mathf.Clamp01(variation);
            RegionId = regionId;
            StableSeed = stableSeed;
        }

        public Vector2 Position { get; }

        public float Variation { get; }

        public int RegionId { get; }

        public int StableSeed { get; }
    }
}