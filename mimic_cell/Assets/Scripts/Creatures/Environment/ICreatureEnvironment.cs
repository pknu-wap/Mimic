using System;
using UnityEngine;

namespace MimicCell.Creatures
{
    public interface ICreatureEnvironment
    {
        event Action<int, int, Transform> RegionActivated;

        event Action<int> RegionDeactivated;

        CreatureHabitatMask GetHabitatAt(
            Vector2 worldPosition);

        bool IsInsidePlayableArea(
            Vector2 worldPosition);

        int GetRegionId(
            Vector2 worldPosition);

        int GetStableRegionSeed(
            Vector2 worldPosition);

        bool TryGetActiveRegionRoot(
            Vector2 worldPosition,
            out Transform regionRoot);
    }
}