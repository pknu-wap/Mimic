using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ExtinctionColdHunting : MonoBehaviour
    {
        [SerializeField] private CreatureController testPredator;
        [SerializeField, Min(0.1f)] private float testRadius = 5f;

        public CreatureController FindPrey(CreatureController predator, float radius)
        {
            if (predator == null || predator.CurrentState == CreatureState.Dead ||
                predator.Definition == null || radius <= 0f)
            {
                return null;
            }

            var predatorDefinition = predator.Definition;
            if (predatorDefinition.Disposition != CreatureDisposition.Aggressive ||
                predatorDefinition.FoodTier < CreatureFoodTier.Tier2)
            {
                return null;
            }

            Vector2 origin = predator.transform.position;
            CreatureController nearestPrey = null;
            float nearestDistanceSquared = radius * radius;

            foreach (Collider2D hit in Physics2D.OverlapCircleAll(origin, radius))
            {
                CreatureController candidate = hit.GetComponentInParent<CreatureController>();
                if (candidate == null || candidate == predator ||
                    candidate.CurrentState == CreatureState.Dead || candidate.Definition == null)
                {
                    continue;
                }

                var candidateDefinition = candidate.Definition;
                bool isLowerTier = candidateDefinition.FoodTier < predatorDefinition.FoodTier;
                bool isSmallerSameTier =
                    candidateDefinition.FoodTier == predatorDefinition.FoodTier &&
                    candidateDefinition.BodySize < predatorDefinition.BodySize;

                if (!isLowerTier && !isSmallerSameTier)
                    continue;

                float distanceSquared =
                    ((Vector2)candidate.transform.position - origin).sqrMagnitude;

                if (distanceSquared > nearestDistanceSquared)
                    continue;

                nearestPrey = candidate;
                nearestDistanceSquared = distanceSquared;
            }

            return nearestPrey;
        }

        [ContextMenu("Test Cold Hunting")]
        private void TestColdHunting()
        {
            if (testPredator == null)
            {
                Debug.LogWarning("Test Predator 칸에 포식자를 넣어줘.", this);
                return;
            }

            CreatureController prey = FindPrey(testPredator, testRadius);
            if (prey == null)
            {
                Debug.Log($"[빙하기 사냥] {testPredator.name} 주변에 조건을 충족하는 먹이가 없어.", this);
                return;
            }

            Debug.Log(
                $"[빙하기 사냥] {testPredator.name} → {prey.name} " +
                $"({prey.Definition.FoodTier}, {prey.Definition.BodySize})",
                prey);
        }
    }
}