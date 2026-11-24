using System;
using System.Collections;
using MimicCell.Creatures;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    public sealed class PrologueTrainingTarget :
        MonoBehaviour, ICreatureEnvironment
    {
        [SerializeField] private CreatureDefinition trainingDefinition;
        [SerializeField] private EcosystemDefinition ecosystem;
        [SerializeField] private PrototypePlayer player;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private PrologueMoveGoal moveGoal;
[SerializeField] private PrologueDialogue dialogue;

        private CreatureController target;

        // 수조는 중심 (0, 0), 안쪽 크기 60 × 30 기준.
        private readonly Rect tankBounds =
            new Rect(-30f, -15f, 60f, 30f);

        // 고정된 수조 하나를 사용하므로 수역 활성화 이벤트는 발생시키지 않는다.
        public event Action<int, int, Transform> RegionActivated
        {
            add { }
            remove { }
        }

        public event Action<int> RegionDeactivated
        {
            add { }
            remove { }
        }

        private IEnumerator Start()
{
    // 기존처럼 즉시 생성하는 테스트도 사용할 수 있다.
    if (spawnOnStart)
    {
        SpawnTarget();
        yield break;
    }

    if (moveGoal == null || dialogue == null)
    {
        Debug.LogError(
            "[프롤로그] Move Goal과 Dialogue를 연결해 주세요.",
            this);
        yield break;
    }

    // 이동 훈련 완료 후, 연구원 대화를 끝까지 읽을 때까지 기다린다.
    yield return new WaitUntil(() =>
        moveGoal.IsCompleted && !dialogue.IsSequencePlaying);

    SpawnTarget();

    if (target == null)
        yield break;

    dialogue.ShowSequence(
        "훈련 개체 준비 완료.\n가까이 다가가 생물의 몸을 마우스 왼쪽 버튼으로 클릭하라.\n[Enter] 계속",
        "추출 동작이 끝나면 다시 클릭하라. 필요한 횟수를 채우면 그 생물의 몸을 복제한다.\n[Enter] 확인");

    // 실제로 훈련 생물의 DNA를 추출해 복제했는지 확인한다.
    yield return new WaitUntil(() =>
        player != null &&
        player.CurrentCreatureDefinition == trainingDefinition);

    // 설명 도중 성공했더라도 현재 설명을 끝까지 읽도록 기다린다.
    yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

    dialogue.ShowSequence(
        "유전 정보 확보 및 신체 복제 확인.\n추출 훈련을 통과했다.\n[Enter] 계속",
        "이제 복제한 몸으로 움직여 보아라.");

    Debug.Log("[프롤로그] DNA 추출·복제 훈련 완료!", this);
}

        public void SpawnTarget()
        {
            if (target != null)
                return;

            if (player == null)
                player = FindFirstObjectByType<PrototypePlayer>();

            if (trainingDefinition == null ||
                trainingDefinition.Prefab == null ||
                ecosystem == null ||
                player == null)
            {
                Debug.LogError(
                    "[프롤로그] Training Definition, Ecosystem, Player 연결을 확인해 주세요.",
                    this);
                return;
            }

            GameObject instance = Instantiate(
                trainingDefinition.Prefab,
                transform.position,
                Quaternion.identity,
                transform);

            CreatureController creature =
                instance.GetComponent<CreatureController>();

            if (creature == null)
            {
                Debug.LogError(
                    "[프롤로그] 훈련 생물 프리팹에 CreatureController가 없습니다.",
                    this);

                Destroy(instance);
                return;
            }

            var context = new CreatureSpawnContext(
                (Vector2)transform.position, 0.5f, 0, 1);

            if (!creature.Initialize(
                    trainingDefinition, ecosystem,
                    context, this, player))
            {
                Destroy(instance);
                return;
            }

            // 훈련 중에는 제자리에서 추출을 연습한다.
            creature.enabled = false;

            if (instance.TryGetComponent<Rigidbody2D>(out var body))
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            target = creature;

            Debug.Log(
                "[프롤로그] 훈련 개체 생성 완료. 가까이에서 몸을 좌클릭해 주세요.",
                this);
        }

        public bool TryGetHabitatBottomY(
            CreatureHabitatMask allowedHabitats,
            float worldX,
            out float bottomY)
        {
            bottomY = tankBounds.yMin;

            return worldX >= tankBounds.xMin &&
                   worldX <= tankBounds.xMax &&
                   (allowedHabitats & CreatureHabitatMask.OceanBB) != 0;
        }

        public CreatureHabitatMask GetHabitatAt(Vector2 position)
        {
            return IsInsidePlayableArea(position)
                ? CreatureHabitatMask.OceanBB
                : CreatureHabitatMask.None;
        }

        public bool IsInsidePlayableArea(Vector2 position)
        {
            return tankBounds.Contains(position);
        }

        public int GetRegionId(Vector2 position) => 0;

        public int GetStableRegionSeed(Vector2 position) => 1;

        public bool TryGetActiveRegionRoot(
            Vector2 position, out Transform regionRoot)
        {
            bool inside = IsInsidePlayableArea(position);
            regionRoot = inside ? transform : null;
            return inside;
        }
    }
}