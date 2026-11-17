using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ExtinctionWarningMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float directionInfluence = 4f;

        public Vector2 GetScatteredDirection(CreatureController creature, float warningTime)
        {
            float variation = Mathf.Repeat(
                (creature.GetInstanceID() % 1024) * 0.618034f, 1f);

            float turnInterval = Mathf.Lerp(0.4f, 0.65f, variation);
            int turns = Mathf.FloorToInt(warningTime / turnInterval + variation);

            float turnAngle = variation < 0.5f ? 137.5f : -137.5f;
            float angle = (variation * 360f + turns * turnAngle) * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                * directionInfluence;
        }
    }
}