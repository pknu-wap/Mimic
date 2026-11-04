using UnityEngine;

namespace MimicCell.Creatures
{
    [CreateAssetMenu(
        fileName = "NewCreatureData",
        menuName = "Mimic/Creatures/Creature Definition")]
    public sealed class CreatureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string creatureId = "NewCreature";
        [SerializeField] private string displayName = "새 생물";
        [SerializeField] private GameObject prefab;

        [Header("Ecology")]
        [SerializeField]
        private CreatureSpawnKind spawnKind =
            CreatureSpawnKind.Individual;

        [SerializeField]
        private CreatureFoodTier foodTier =
            CreatureFoodTier.Tier1;

        [SerializeField]
        private CreatureBodySize bodySize =
            CreatureBodySize.Small;

        [SerializeField]
        private CreatureHabitatMask allowedHabitats =
            CreatureHabitatMask.None;

        [SerializeField]
        private CreatureTraits traits =
            CreatureTraits.None;

        [Header("Behaviour")]
        [SerializeField]
        private CreatureDisposition disposition =
            CreatureDisposition.Passive;

        [Header("Stats")]
        [SerializeField] private CreatureDesignSizeRange designSizeRange;
        [SerializeField] private CreatureIntRange healthRange;
        [SerializeField] private CreatureIntRange attackRange;
        [SerializeField] private CreatureIntRange reflectionDamageRange;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float swimSpeed = 1f;
        [SerializeField, Min(0f)] private float sprintSpeed = 1f;
        [SerializeField, Min(0.1f)] private float wanderRadius = 4f;

        [Header("Perception")]
        [SerializeField] private bool usePerceptionRadiusOverride;

        [SerializeField, Min(0f)]
        private float perceptionRadius = 5f;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;
        [SerializeField, Min(1)] private int maxAlive = 4;
        [SerializeField] private CreatureIntRange groupSizeRange;
        [SerializeField, Min(0)] private int maxGroupCount;

        public string CreatureId => creatureId;

        public string DisplayName => displayName;

        public GameObject Prefab => prefab;

        public CreatureSpawnKind SpawnKind => spawnKind;

        public CreatureFoodTier FoodTier => foodTier;

        public CreatureBodySize BodySize => bodySize;

        public CreatureHabitatMask AllowedHabitats =>
            allowedHabitats;

        public CreatureTraits Traits => traits;

        public CreatureDisposition Disposition => disposition;

        public CreatureDesignSizeRange DesignSizeRange =>
            designSizeRange;

        public CreatureIntRange HealthRange => healthRange;

        public CreatureIntRange AttackRange => attackRange;

        public CreatureIntRange ReflectionDamageRange =>
            reflectionDamageRange;

        public float SwimSpeed => swimSpeed;

        public float SprintSpeed => sprintSpeed;

        public float WanderRadius => wanderRadius;

        public float SpawnWeight => spawnWeight;

        public int MaxAlive => maxAlive;

        public CreatureIntRange GroupSizeRange => groupSizeRange;

        public int MaxGroupCount => maxGroupCount;

        public float GetPerceptionRadius(
            float commonPerceptionRadius)
        {
            return usePerceptionRadiusOverride
                ? perceptionRadius
                : commonPerceptionRadius;
        }

        private void OnValidate()
        {
            creatureId = string.IsNullOrWhiteSpace(creatureId)
                ? string.Empty
                : creatureId.Trim();

            displayName = string.IsNullOrWhiteSpace(displayName)
                ? creatureId
                : displayName.Trim();

            designSizeRange.Validate();
            healthRange.Validate(1);
            attackRange.Validate(0);
            reflectionDamageRange.Validate(0);

            swimSpeed = Mathf.Max(0f, swimSpeed);
            sprintSpeed = Mathf.Max(swimSpeed, sprintSpeed);
            wanderRadius = Mathf.Max(0.1f, wanderRadius);
            perceptionRadius = Mathf.Max(0f, perceptionRadius);

            spawnWeight = Mathf.Max(0f, spawnWeight);
            maxAlive = Mathf.Max(1, maxAlive);

            if (spawnKind == CreatureSpawnKind.Group)
            {
                groupSizeRange.Validate(2);
                maxGroupCount = Mathf.Max(1, maxGroupCount);
            }
            else
            {
                maxGroupCount = 0;
            }
        }
    }
}