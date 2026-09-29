using System;
using System.Collections;
using UnityEngine;

namespace MimicCell.Creatures
{
    [DisallowMultipleComponent]
    public sealed class CreatureSpawner : MonoBehaviour
    {
        private enum SpawnMode
        {
            Individual,
            Group
        }

        [Serializable]
        private sealed class SpawnEntry
        {
            public GameObject prefab;
            public SpawnMode spawnMode = SpawnMode.Individual;

            [Min(2)]
            public int minGroupSize = 3;

            [Min(2)]
            public int maxGroupSize = 5;
        }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform spawnedCreaturesParent;
        [SerializeField] private SpawnEntry[] spawnEntries;

        [Header("Spawn Timing")]
        [SerializeField, Min(0.1f)] private float minSpawnInterval = 2f;
        [SerializeField, Min(0.1f)] private float maxSpawnInterval = 5f;
        [SerializeField, Min(1)] private int maxAliveCreatures = 25;

        [Header("Spawn Area")]
        [SerializeField] private Vector2 worldBoundsMin = new Vector2(-21f, -8.5f);
        [SerializeField] private Vector2 worldBoundsMax = new Vector2(21f, 8.5f);

        [Tooltip("화면 가장자리에서 추가로 떨어뜨릴 거리입니다.")]
        [SerializeField, Min(0f)] private float outsidePadding = 2.5f;

        [Tooltip("군집 구성원이 중심점에서 흩어지는 최대 거리입니다.")]
        [SerializeField, Min(0.1f)] private float groupRadius = 1.5f;

        [SerializeField, Min(1)] private int maxPositionAttempts = 50;
        [SerializeField] private float spawnPlaneZ = 0f;

        private Coroutine spawnRoutine;

        private void Awake()
        {
            ResolveCamera();
        }

        private void OnEnable()
        {
            if (spawnRoutine == null)
            {
                spawnRoutine = StartCoroutine(SpawnLoop());
            }
        }

        private void OnDisable()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }

        private void OnValidate()
        {
            minSpawnInterval = Mathf.Max(0.1f, minSpawnInterval);
            maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
            maxAliveCreatures = Mathf.Max(1, maxAliveCreatures);
            maxPositionAttempts = Mathf.Max(1, maxPositionAttempts);

            worldBoundsMax.x =
                Mathf.Max(worldBoundsMin.x + 0.1f, worldBoundsMax.x);

            worldBoundsMax.y =
                Mathf.Max(worldBoundsMin.y + 0.1f, worldBoundsMax.y);
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                float waitTime = UnityEngine.Random.Range(
                    minSpawnInterval,
                    maxSpawnInterval);

                yield return new WaitForSeconds(waitTime);

                TrySpawnCreature();
            }
        }

        private void TrySpawnCreature()
        {
            int aliveCount = CountAliveCreatures();

            if (aliveCount >= maxAliveCreatures)
            {
                return;
            }

            SpawnEntry entry = GetRandomValidEntry();

            if (entry == null)
            {
                return;
            }

            if (entry.prefab.GetComponent<CreatureController>() == null)
            {
                Debug.LogWarning(
                    entry.prefab.name +
                    " 프리팹에 CreatureController가 없습니다.");

                return;
            }

            int remainingCapacity = maxAliveCreatures - aliveCount;

            if (entry.spawnMode == SpawnMode.Individual)
            {
                SpawnIndividual(entry);
            }
            else
            {
                SpawnGroup(entry, remainingCapacity);
            }
        }

        private void SpawnIndividual(SpawnEntry entry)
        {
            if (!TryFindSpawnPosition(0f, out Vector2 spawnPosition))
            {
                return;
            }

            SpawnPrefab(entry.prefab, spawnPosition);
        }

        private void SpawnGroup(
            SpawnEntry entry,
            int remainingCapacity)
        {
            int minimumSize = Mathf.Max(2, entry.minGroupSize);

            int maximumSize = Mathf.Max(
                minimumSize,
                entry.maxGroupSize);

            maximumSize = Mathf.Min(
                maximumSize,
                remainingCapacity);

            if (maximumSize < minimumSize)
            {
                return;
            }

            int groupSize = UnityEngine.Random.Range(
                minimumSize,
                maximumSize + 1);

            if (!TryFindSpawnPosition(
                    groupRadius,
                    out Vector2 groupCenter))
            {
                return;
            }

            for (int i = 0; i < groupSize; i++)
            {
                Vector2 offset = i == 0
                    ? Vector2.zero
                    : UnityEngine.Random.insideUnitCircle * groupRadius;

                Vector2 memberPosition = groupCenter + offset;

                SpawnPrefab(entry.prefab, memberPosition);
            }
        }

        private void SpawnPrefab(
            GameObject prefab,
            Vector2 position)
        {
            Vector3 spawnPosition = new Vector3(
                position.x,
                position.y,
                spawnPlaneZ);

            if (spawnedCreaturesParent == null)
            {
                Instantiate(
                    prefab,
                    spawnPosition,
                    Quaternion.identity);

                return;
            }

            Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity,
                spawnedCreaturesParent);
        }

        private bool TryFindSpawnPosition(
            float edgeClearance,
            out Vector2 spawnPosition)
        {
            spawnPosition = Vector2.zero;

            if (!TryGetCameraWorldRect(out Rect cameraRect))
            {
                return false;
            }

            float minimumX = worldBoundsMin.x + edgeClearance;
            float maximumX = worldBoundsMax.x - edgeClearance;
            float minimumY = worldBoundsMin.y + edgeClearance;
            float maximumY = worldBoundsMax.y - edgeClearance;

            if (minimumX >= maximumX || minimumY >= maximumY)
            {
                return false;
            }

            float requiredDistance =
                outsidePadding + edgeClearance;

            for (int attempt = 0;
                 attempt < maxPositionAttempts;
                 attempt++)
            {
                Vector2 candidate = new Vector2(
                    UnityEngine.Random.Range(minimumX, maximumX),
                    UnityEngine.Random.Range(minimumY, maximumY));

                if (IsOutsideCamera(
                        candidate,
                        cameraRect,
                        requiredDistance))
                {
                    spawnPosition = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetCameraWorldRect(
            out Rect cameraRect)
        {
            cameraRect = new Rect();

            ResolveCamera();

            if (targetCamera == null)
            {
                Debug.LogWarning(
                    "CreatureSpawner가 Main Camera를 찾지 못했습니다.");

                return false;
            }

            float cameraDepth = Mathf.Abs(
                spawnPlaneZ -
                targetCamera.transform.position.z);

            Vector3 bottomLeft =
                targetCamera.ViewportToWorldPoint(
                    new Vector3(0f, 0f, cameraDepth));

            Vector3 topRight =
                targetCamera.ViewportToWorldPoint(
                    new Vector3(1f, 1f, cameraDepth));

            cameraRect = Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));

            return true;
        }

        private static bool IsOutsideCamera(
            Vector2 position,
            Rect cameraRect,
            float padding)
        {
            return
                position.x < cameraRect.xMin - padding ||
                position.x > cameraRect.xMax + padding ||
                position.y < cameraRect.yMin - padding ||
                position.y > cameraRect.yMax + padding;
        }

        private SpawnEntry GetRandomValidEntry()
        {
            if (spawnEntries == null ||
                spawnEntries.Length == 0)
            {
                return null;
            }

            int startIndex = UnityEngine.Random.Range(
                0,
                spawnEntries.Length);

            for (int i = 0; i < spawnEntries.Length; i++)
            {
                int index =
                    (startIndex + i) % spawnEntries.Length;

                SpawnEntry entry = spawnEntries[index];

                if (entry != null && entry.prefab != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private static int CountAliveCreatures()
        {
            CreatureController[] creatures =
                FindObjectsByType<CreatureController>(
                    FindObjectsSortMode.None);

            int aliveCount = 0;

            for (int i = 0; i < creatures.Length; i++)
            {
                CreatureController creature = creatures[i];

                if (creature.gameObject.activeInHierarchy &&
                    creature.CurrentState != CreatureState.Dead)
                {
                    aliveCount++;
                }
            }

            return aliveCount;
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 center =
                (worldBoundsMin + worldBoundsMax) * 0.5f;

            Vector2 size =
                worldBoundsMax - worldBoundsMin;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
        }
    }
}