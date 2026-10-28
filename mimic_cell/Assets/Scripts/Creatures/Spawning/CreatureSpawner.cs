using System;
using System.Collections.Generic;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    public sealed class CreatureSpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private EcosystemDefinition ecosystem;

        [SerializeField]
        private MonoBehaviour environmentSource;

        [Header("References")]
        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        private PrototypePlayer player;

        [Header("Testing")]
        [SerializeField]
        private bool useFixedSeedForTesting;

        [SerializeField]
        private int fixedTestSeed = 12345;

        [SerializeField]
        private bool logSpawnEvents = true;

        [SerializeField]
        private bool logDespawnEvents = true;

        private readonly List<CreatureController>
            aliveCreatures =
                new List<CreatureController>();

        private readonly Dictionary<CreatureController, float> 
            despawnTimers =
                new Dictionary<CreatureController, float>();

        private ICreatureEnvironment environment;
        private System.Random random;
        private float spawnTimer;
        private bool initialized;

        private void Start()
        {
            if (!TryResolveReferences())
            {
                enabled = false;
                return;
            }

            int seed =
                useFixedSeedForTesting
                    ? fixedTestSeed
                    : unchecked(
                        System.Environment.TickCount ^
                        GetInstanceID());

            random = new System.Random(seed);

            spawnTimer =
                ecosystem.SpawnSettings.FirstSpawnDelay;

            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            CleanupAliveCreatures();
            UpdateDespawnTimers();

            spawnTimer -= Time.deltaTime;

            if (spawnTimer > 0f)
            {
                return;
            }

            spawnTimer =
                ecosystem.SpawnSettings.SpawnInterval;

            TrySpawnOnce();
        }

        private void CleanupAliveCreatures()
        {
            for (
                int index = aliveCreatures.Count - 1;
                index >= 0;
                index--)
            {
                CreatureController creature =
                    aliveCreatures[index];

                if (creature == null || creature.CurrentState == CreatureState.Dead)
                {
                    if (!object.ReferenceEquals(creature, null))
                    {
                        despawnTimers.Remove(creature);
                    }

                    aliveCreatures.RemoveAt(index);
                }
            }
        }

        private bool MeetsDespawnCondition(CreatureController creature)
        {
            float distance = Vector2.Distance(
                player.transform.position,
                creature.transform.position);

            if (distance < ecosystem.DespawnSettings.Distance)
            {
                return false;
            }

            bool mustBeOffscreen =
                ecosystem.DespawnSettings.RequireOffscreen;

            bool isOffscreen =
                IsOutsideCamera(creature.transform.position, 0f);

            if (mustBeOffscreen && !isOffscreen)
            {
                return false;
            }

            return true;
        }

        private void UpdateDespawnTimers()
        {
            for (int index = aliveCreatures.Count - 1; index >= 0; index--)
            {
                CreatureController creature = aliveCreatures[index];

                if (creature == null ||
                    creature.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                if (!MeetsDespawnCondition(creature))
                {
                    despawnTimers.Remove(creature);
                    continue;
                }

                despawnTimers.TryGetValue(creature, out float elapsedTime);
                elapsedTime += Time.deltaTime;

                if (elapsedTime < ecosystem.DespawnSettings.ConditionDuration)
                {
                    despawnTimers[creature] = elapsedTime;
                    continue;
                }

                Vector2 despawnPosition = creature.transform.position;

                despawnTimers.Remove(creature);
                aliveCreatures.RemoveAt(index);

                LogDespawnEvent(creature, despawnPosition);
                Destroy(creature.gameObject);
            }
        }

        private void TrySpawnOnce()
        {
            if (aliveCreatures.Count >=
                ecosystem.SpawnSettings.MaxTotalAlive)
            {
                return;
            }

            if (!TryFindSpawnPosition(
                    out Vector2 spawnPosition))
            {
                return;
            }

            if (!TryChooseDefinition(
                    spawnPosition,
                    out CreatureDefinition definition))
            {
                return;
            }

            if (!environment.TryGetActiveRegionRoot(
                    spawnPosition,
                    out Transform regionRoot))
            {
                return;
            }

            int spawnedCount =
                SpawnDefinition(
                    definition,
                    spawnPosition,
                    regionRoot);

            LogSpawnEvent(
                definition,
                spawnedCount,
                spawnPosition);
        }

        private int GetSpawnCount(
            CreatureDefinition definition)
        {
            if (definition.SpawnKind ==
                CreatureSpawnKind.Individual)
            {
                return 1;
            }

            int totalRemaining =
                ecosystem.SpawnSettings.MaxTotalAlive -
                aliveCreatures.Count;

            int speciesRemaining =
                definition.MaxAlive -
                CountAlive(definition);

            int maximum =
                Mathf.Min(
                    definition.GroupSizeRange.Max,
                    Mathf.Min(
                        totalRemaining,
                        speciesRemaining));

            int minimum =
                definition.GroupSizeRange.Min;

            if (maximum < minimum)
            {
                return 0;
            }

            return random.Next(
                minimum,
                maximum + 1);
        }

        private Vector2 GetGroupMemberPosition(
            Vector2 groupCenter,
            Transform regionRoot)
        {
            float targetSpacing =
                ecosystem.GroupSettings.TargetSpacing;

            const int attemptCount = 6;

            for (int attempt = 0;
                attempt < attemptCount;
                attempt++)
            {
                float angle =
                    (float)random.NextDouble() *
                    Mathf.PI *
                    2f;

                float distance =
                    targetSpacing *
                    Mathf.Lerp(
                        0.65f,
                        1.25f,
                        (float)random.NextDouble());

                Vector2 direction =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle));

                Vector2 candidate =
                    groupCenter +
                    direction * distance;

                if (!IsOutsideCamera(
                        candidate,
                        ecosystem.SpawnSettings
                            .OffscreenPadding))
                {
                    continue;
                }

                if (!environment.IsInsidePlayableArea(
                        candidate))
                {
                    continue;
                }

                if (!environment.TryGetActiveRegionRoot(
                        candidate,
                        out Transform candidateRegionRoot))
                {
                    continue;
                }

                if (candidateRegionRoot != regionRoot)
                {
                    continue;
                }

                return candidate;
            }

            return groupCenter;
        }

        private bool IsOutsideCamera(
            Vector2 worldPosition,
            float padding)
        {
            Vector3 viewportPosition =
                targetCamera.WorldToViewportPoint(
                    worldPosition);

            if (viewportPosition.z <= 0f)
            {
                return false;
            }

            float verticalWorldSize =
                targetCamera.orthographicSize * 2f;

            float horizontalWorldSize =
                verticalWorldSize * targetCamera.aspect;

            float horizontalPadding =
                horizontalWorldSize > 0f
                    ? padding / horizontalWorldSize
                    : 0f;

            float verticalPadding =
                verticalWorldSize > 0f
                    ? padding / verticalWorldSize
                    : 0f;

            return
                viewportPosition.x < -horizontalPadding ||
                viewportPosition.x > 1f + horizontalPadding ||
                viewportPosition.y < -verticalPadding ||
                viewportPosition.y > 1f + verticalPadding;
        }

        private bool TryFindSpawnPosition(
            out Vector2 spawnPosition)
        {
            spawnPosition = default;

            CreatureSpawnSettings settings =
                ecosystem.SpawnSettings;

            float cameraHalfHeight =
                targetCamera.orthographicSize;

            float cameraHalfWidth =
                cameraHalfHeight * targetCamera.aspect;

            float minimumDistance =
                Mathf.Max(
                    cameraHalfWidth,
                    cameraHalfHeight) +
                settings.OffscreenPadding;

            float maximumDistance =
                settings.MaxSpawnDistance;

            if (maximumDistance < minimumDistance)
            {
                return false;
            }

            Vector2 center =
                player.transform.position;

            for (
                int attempt = 0;
                attempt < settings.CandidateAttempts;
                attempt++)
            {
                float angle =
                    (float)random.NextDouble() *
                    Mathf.PI *
                    2f;

                float distance =
                    Mathf.Lerp(
                        minimumDistance,
                        maximumDistance,
                        (float)random.NextDouble());

                Vector2 direction =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle));

                Vector2 candidate =
                    center + direction * distance;

                if (!IsOutsideCamera(
                        candidate,
                        settings.OffscreenPadding))
                {
                    continue;
                }

                if (!environment.IsInsidePlayableArea(
                        candidate))
                {
                    continue;
                }

                if (!environment.TryGetActiveRegionRoot(
                        candidate,
                        out _))
                {
                    continue;
                }

                spawnPosition = candidate;
                return true;
            }

            return false;
        }

        private int CountAlive(
            CreatureDefinition definition)
        {
            int count = 0;

            for (
                int index = 0;
                index < aliveCreatures.Count;
                index++)
            {
                CreatureController creature =
                    aliveCreatures[index];

                if (creature == null ||
                    creature.CurrentState ==
                    CreatureState.Dead)
                {
                    continue;
                }

                if (creature.Definition == definition)
                {
                    count++;
                }
            }

            return count;
        }

        private bool CanSpawnDefinition(
            CreatureDefinition definition,
            CreatureHabitatMask habitat)
        {
            if (definition == null ||
                definition.Prefab == null ||
                definition.SpawnWeight <= 0f)
            {
                return false;
            }

            if ((definition.AllowedHabitats & habitat) == 0)
            {
                return false;
            }

            int requiredCount =
                definition.SpawnKind ==
                CreatureSpawnKind.Group
                    ? definition.GroupSizeRange.Min
                    : 1;

            int totalRemaining =
                ecosystem.SpawnSettings.MaxTotalAlive -
                aliveCreatures.Count;

            if (totalRemaining < requiredCount)
            {
                return false;
            }

            int speciesRemaining =
                definition.MaxAlive -
                CountAlive(definition);

            if (speciesRemaining < requiredCount)
            {
                return false;
            }

            return true;
        }

        private bool TryChooseDefinition(
            Vector2 spawnPosition,
            out CreatureDefinition selected)
        {
            selected = null;

            CreatureHabitatMask habitat =
                environment.GetHabitatAt(
                    spawnPosition);

            float totalWeight = 0f;

            for (
                int index = 0;
                index < ecosystem.Creatures.Count;
                index++)
            {
                CreatureDefinition candidate =
                    ecosystem.Creatures[index];

                if (CanSpawnDefinition(
                        candidate,
                        habitat))
                {
                    totalWeight +=
                        candidate.SpawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float roll =
                (float)random.NextDouble() *
                totalWeight;

            CreatureDefinition lastEligible = null;

            for (
                int index = 0;
                index < ecosystem.Creatures.Count;
                index++)
            {
                CreatureDefinition candidate =
                    ecosystem.Creatures[index];

                if (!CanSpawnDefinition(
                        candidate,
                        habitat))
                {
                    continue;
                }

                lastEligible = candidate;
                roll -= candidate.SpawnWeight;

                if (roll <= 0f)
                {
                    selected = candidate;
                    return true;
                }
            }

            selected = lastEligible;
            return selected != null;
        }

        private CreatureController SpawnCreature(
            CreatureDefinition definition,
            Vector2 spawnPosition,
            Transform regionRoot)
        {
            GameObject instance =
                Instantiate(
                    definition.Prefab,
                    spawnPosition,
                    Quaternion.identity,
                    regionRoot);

            instance.name =
                definition.CreatureId +
                "_" +
                (aliveCreatures.Count + 1);

            CreatureController controller =
                instance.GetComponent<CreatureController>();

            if (controller == null)
            {
                Debug.LogError(
                    definition.CreatureId +
                    " prefab requires a CreatureController.",
                    instance);

                Destroy(instance);
                return null;
            }

            float variation =
                (float)random.NextDouble();

            int regionId =
                environment.GetRegionId(
                    spawnPosition);

            int stableSeed =
                unchecked(
                    environment.GetStableRegionSeed(
                        spawnPosition) ^
                    random.Next());

            CreatureSpawnContext spawnContext =
                new CreatureSpawnContext(
                    spawnPosition,
                    variation,
                    regionId,
                    stableSeed);

            bool initializedCreature =
                controller.Initialize(
                    definition,
                    ecosystem,
                    spawnContext,
                    environment,
                    player);

            if (!initializedCreature)
            {
                Destroy(instance);
                return null;
            }

            aliveCreatures.Add(controller);
            return controller;
        }

        private int SpawnDefinition(
            CreatureDefinition definition,
            Vector2 groupCenter,
            Transform regionRoot)
        {
            int requestedCount =
                GetSpawnCount(definition);

            int spawnedCount = 0;

            for (int index = 0;
                index < requestedCount;
                index++)
            {
                Vector2 spawnPosition =
                    index == 0
                        ? groupCenter
                        : GetGroupMemberPosition(
                            groupCenter,
                            regionRoot);

                CreatureController creature =
                    SpawnCreature(
                        definition,
                        spawnPosition,
                        regionRoot);

                if (creature != null)
                {
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        private void LogSpawnEvent(
            CreatureDefinition definition,
            int spawnedCount,
            Vector2 spawnPosition)
        {
            if (!logSpawnEvents ||
                definition == null ||
                spawnedCount <= 0)
            {
                return;
            }

            string creatureName =
                string.IsNullOrWhiteSpace(
                    definition.DisplayName)
                    ? definition.CreatureId
                    : definition.DisplayName;

            Debug.Log(
                "[CreatureSpawner] " +
                creatureName +
                " × " +
                spawnedCount +
                " | 위치: (" +
                spawnPosition.x.ToString("F1") +
                ", " +
                spawnPosition.y.ToString("F1") +
                ")" +
                " | 현재 생존: " +
                aliveCreatures.Count +
                "/" +
                ecosystem.SpawnSettings.MaxTotalAlive,
                this);
        }

        private void LogDespawnEvent(
            CreatureController creature,
            Vector2 despawnPosition)
        {
            if (!logDespawnEvents || creature == null)
            {
                return;
            }

            CreatureDefinition definition = creature.Definition;

            string creatureName =
                definition != null &&
                !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : creature.name;

            Debug.Log(
                $"[CreatureSpawner] 디스폰: {creatureName} | " +
                $"위치: ({despawnPosition.x:F1}, {despawnPosition.y:F1}) | " +
                $"현재 생존: {aliveCreatures.Count}/" +
                $"{ecosystem.SpawnSettings.MaxTotalAlive}",
                this);
        }

        private bool TryResolveReferences()
        {
            if (ecosystem == null)
            {
                Debug.LogError(
                    "CreatureSpawner requires an EcosystemDefinition.",
                    this);

                return false;
            }

            environment =
                environmentSource as ICreatureEnvironment;

            if (environment == null)
            {
                environment =
                    GetComponent<ICreatureEnvironment>();
            }

            if (environment == null)
            {
                Debug.LogError(
                    "CreatureSpawner requires an ICreatureEnvironment.",
                    this);

                return false;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogError(
                    "CreatureSpawner requires a Camera.",
                    this);

                return false;
            }

            if (player == null)
            {
                player =
                    FindFirstObjectByType<PrototypePlayer>();
            }

            if (player == null)
            {
                Debug.LogError(
                    "CreatureSpawner requires a PrototypePlayer.",
                    this);

                return false;
            }

            return true;
        }
    }
}