using System;
using System.Collections.Generic;
using UnityEngine;

namespace MimicCell.Creatures
{
    [Serializable]
    public sealed class CreatureSpawnSettings
    {
        [SerializeField, Min(0f)]
        private float firstSpawnDelay = 2f;

        [SerializeField, Min(0.1f)]
        private float spawnInterval = 4f;

        [SerializeField, Min(1)]
        private int maxTotalAlive = 40;

        [SerializeField, Min(1)]
        private int candidateAttempts = 12;

        [SerializeField, Min(0f)]
        private float offscreenPadding = 2f;

        [SerializeField, Min(0f)]
        private float maxSpawnDistance = 30f;

        [SerializeField]
        private bool allowSpawnDuringCombat = true;

        public float FirstSpawnDelay => firstSpawnDelay;

        public float SpawnInterval => spawnInterval;

        public int MaxTotalAlive => maxTotalAlive;

        public int CandidateAttempts => candidateAttempts;

        public float OffscreenPadding => offscreenPadding;

        public float MaxSpawnDistance => maxSpawnDistance;

        public bool AllowSpawnDuringCombat =>
            allowSpawnDuringCombat;

        public void Validate()
        {
            firstSpawnDelay = Mathf.Max(0f, firstSpawnDelay);
            spawnInterval = Mathf.Max(0.1f, spawnInterval);
            maxTotalAlive = Mathf.Max(1, maxTotalAlive);
            candidateAttempts = Mathf.Max(1, candidateAttempts);
            offscreenPadding = Mathf.Max(0f, offscreenPadding);
            maxSpawnDistance = Mathf.Max(0f, maxSpawnDistance);
        }
    }

    [Serializable]
    public sealed class CreaturePerceptionSettings
    {
        [SerializeField, Min(0f)]
        private float commonPerceptionRadius = 5f;

        [SerializeField, Min(0.02f)]
        private float decisionInterval = 0.15f;

        [SerializeField, Min(0f)]
        private float tier1FleeDuration = 3f;

        [SerializeField, Min(0f)]
        private float tier2ChaseDuration = 3f;

        [SerializeField, Min(0f)]
        private float tier2FleeDuration = 4f;

        [SerializeField, Min(0f)]
        private float tier3ChaseDuration = 5f;

        [SerializeField, Min(0f)]
        private float tier3FleeDuration = 5f;

        [SerializeField, Min(1f)]
        private float sensingDurationMultiplier = 1.4f;

        public float CommonPerceptionRadius =>
            commonPerceptionRadius;

        public float DecisionInterval => decisionInterval;

        public float Tier1FleeDuration => tier1FleeDuration;

        public float Tier2ChaseDuration => tier2ChaseDuration;

        public float Tier2FleeDuration => tier2FleeDuration;

        public float Tier3ChaseDuration => tier3ChaseDuration;

        public float Tier3FleeDuration => tier3FleeDuration;

        public float SensingDurationMultiplier =>
            sensingDurationMultiplier;

        public void Validate()
        {
            commonPerceptionRadius =
                Mathf.Max(0f, commonPerceptionRadius);

            decisionInterval =
                Mathf.Max(0.02f, decisionInterval);

            tier1FleeDuration =
                Mathf.Max(0f, tier1FleeDuration);

            tier2ChaseDuration =
                Mathf.Max(0f, tier2ChaseDuration);

            tier2FleeDuration =
                Mathf.Max(0f, tier2FleeDuration);

            tier3ChaseDuration =
                Mathf.Max(0f, tier3ChaseDuration);

            tier3FleeDuration =
                Mathf.Max(0f, tier3FleeDuration);

            sensingDurationMultiplier =
                Mathf.Max(1f, sensingDurationMultiplier);
        }
    }

    [Serializable]
    public sealed class CreatureGroupSettings
    {
        [SerializeField, Min(0.1f)]
        private float targetSpacing = 1.5f;

        [SerializeField, Min(1f)]
        private float returnDistanceMultiplier = 3f;

        [SerializeField, Min(1f)]
        private float isolationDistanceMultiplier = 6f;

        [SerializeField, Min(0f)]
        private float groupSearchRadius = 5f;

        [SerializeField, Min(1f)]
        private float regroupCompletionMultiplier = 1.25f;

        [SerializeField, Range(0f, 180f)]
        private float scatterAngle = 35f;

        public float TargetSpacing => targetSpacing;

        public float ReturnDistance =>
            targetSpacing * returnDistanceMultiplier;

        public float IsolationDistance =>
            targetSpacing * isolationDistanceMultiplier;

        public float GroupSearchRadius => groupSearchRadius;

        public float RegroupCompletionDistance =>
            targetSpacing * regroupCompletionMultiplier;

        public float ScatterAngle => scatterAngle;

        public void Validate()
        {
            targetSpacing = Mathf.Max(0.1f, targetSpacing);

            returnDistanceMultiplier =
                Mathf.Max(1f, returnDistanceMultiplier);

            isolationDistanceMultiplier =
                Mathf.Max(1f, isolationDistanceMultiplier);

            groupSearchRadius =
                Mathf.Max(0f, groupSearchRadius);

            regroupCompletionMultiplier =
                Mathf.Max(1f, regroupCompletionMultiplier);

            scatterAngle = Mathf.Clamp(scatterAngle, 0f, 180f);
        }
    }

    [Serializable]
    public sealed class CreatureDespawnSettings
    {
        [SerializeField, Min(0f)]
        private float distance = 45f;

        [SerializeField, Min(0f)]
        private float conditionDuration = 3f;

        [SerializeField]
        private bool requireOffscreen = true;

        public float Distance => distance;

        public float ConditionDuration => conditionDuration;

        public bool RequireOffscreen => requireOffscreen;

        public void Validate()
        {
            distance = Mathf.Max(0f, distance);
            conditionDuration = Mathf.Max(0f, conditionDuration);
        }
    }

    [CreateAssetMenu(
        fileName = "NewEcosystem",
        menuName = "Mimic/Ecosystems/Ecosystem Definition")]
    public sealed class EcosystemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string ecosystemId =
            "NewEcosystem";

        [SerializeField]
        private string displayName =
            "새 생태계";

        [Header("Creatures")]
        [SerializeField]
        private List<CreatureDefinition> creatures =
            new List<CreatureDefinition>();

        [Header("Runtime Settings")]
        [SerializeField]
        private CreatureSpawnSettings spawnSettings =
            new CreatureSpawnSettings();

        [SerializeField]
        private CreaturePerceptionSettings perceptionSettings =
            new CreaturePerceptionSettings();

        [SerializeField]
        private CreatureGroupSettings groupSettings =
            new CreatureGroupSettings();

        [SerializeField]
        private CreatureDespawnSettings despawnSettings =
            new CreatureDespawnSettings();

        public string EcosystemId => ecosystemId;

        public string DisplayName => displayName;

        public IReadOnlyList<CreatureDefinition> Creatures =>
            creatures;

        public CreatureSpawnSettings SpawnSettings =>
            spawnSettings;

        public CreaturePerceptionSettings PerceptionSettings =>
            perceptionSettings;

        public CreatureGroupSettings GroupSettings =>
            groupSettings;

        public CreatureDespawnSettings DespawnSettings =>
            despawnSettings;

        private void OnValidate()
        {
            ecosystemId = string.IsNullOrWhiteSpace(ecosystemId)
                ? string.Empty
                : ecosystemId.Trim();

            displayName = string.IsNullOrWhiteSpace(displayName)
                ? ecosystemId
                : displayName.Trim();

            if (creatures == null)
            {
                creatures = new List<CreatureDefinition>();
            }

            if (spawnSettings == null)
            {
                spawnSettings = new CreatureSpawnSettings();
            }

            if (perceptionSettings == null)
            {
                perceptionSettings =
                    new CreaturePerceptionSettings();
            }

            if (groupSettings == null)
            {
                groupSettings = new CreatureGroupSettings();
            }

            if (despawnSettings == null)
            {
                despawnSettings =
                    new CreatureDespawnSettings();
            }

            spawnSettings.Validate();
            perceptionSettings.Validate();
            groupSettings.Validate();
            despawnSettings.Validate();
        }
    }
}