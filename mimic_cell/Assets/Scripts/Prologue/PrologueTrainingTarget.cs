using MimicCell.Gameplay;
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
        [SerializeField] private bool runLessonOnStart = true;
[SerializeField] private PrologueTrainingTarget replacementTarget;
[SerializeField] private PrologueTrainingTarget attackTargetPoint;
[SerializeField] private CreatureDefinition replacementDefinition;        [SerializeField] private PrologueMoveGoal moveGoal;
[SerializeField] private PrologueDialogue dialogue;
[SerializeField] private PrologueControlHint controlHint;
[SerializeField] private PrologueDeparture departure;

[SerializeField, Min(0.1f)]
private float trainingAssimilationPerSecond = 10f;

private PlayerAssimilationController assimilation;
private bool ownsTrainingLimit;

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
    if (!runLessonOnStart)
        yield break;

    if (spawnOnStart)
    {
        SpawnTarget();
        yield break;
    }

    if (player == null)
        player = FindFirstObjectByType<PrototypePlayer>();

    PlayerDnaExtraction dna = player != null
        ? player.GetComponent<PlayerDnaExtraction>()
        : null;

        assimilation = player != null
    ? player.GetComponent<PlayerAssimilationController>()
    : null;

   if (moveGoal == null ||
    dialogue == null ||
    controlHint == null ||
    dna == null ||
    assimilation == null ||
    trainingDefinition == null ||
    replacementTarget == null ||
    replacementDefinition == null)
    {
        Debug.LogError(
            "[프롤로그] 훈련 진행에 필요한 연결을 확인해 주세요.",
            this);
        yield break;
    }

    if (trainingDefinition.CreatureId ==
            replacementDefinition.CreatureId ||
        replacementTarget.trainingDefinition != replacementDefinition)
    {
        Debug.LogError(
            "[프롤로그] A·B의 Creature Id와 B 생성 지점의 데이터를 확인해 주세요.",
            this);
        yield break;
    }

    yield return new WaitUntil(() =>
        moveGoal.IsCompleted && !dialogue.IsSequencePlaying);

    SpawnTarget();

    if (target == null)
        yield break;
dna.ExtractionAllowed = false;
    dialogue.ShowText(
        "추출 기능 확인. 지정된 생물체의 유전 정보 확보를 개시하십시오.");

    controlHint.ShowHint("좌클릭 — DNA 추출");
    dna.ExtractionAllowed = true;

    // A의 DNA가 처음 확보될 때까지만 추출을 허용한다.
    yield return new WaitUntil(() => dna.HasStoredDna);

    dna.ExtractionAllowed = false;

    dialogue.ShowSequence(
        "유전 정보 확보 확인. 추출 대상을 변경합니다.",
        "보유 가능한 유전자는 하나입니다. 확보 중인 유전 정보를 폐기하십시오.");

    controlHint.ShowHint("Enter — 계속");

    yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

    controlHint.ShowHint("R 2초 누르기 — DNA 폐기");

    // R키로 실제 DNA를 비웠는지 확인한다.
    yield return new WaitUntil(() => !dna.HasStoredDna);

    dialogue.ShowText(
        "유전 정보 폐기 정상. 새로운 훈련 개체를 배치합니다.");

    controlHint.ShowHint("Enter — 계속");

    yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

    replacementTarget.SpawnTarget();

    if (replacementTarget.target == null)
        yield break;

    dialogue.ShowText(
        "추출·복제 기능 확인. 새로 배치된 생물체의 유전자를 추출한 뒤, 해당 생물체로 복제하십시오.");

    controlHint.ShowHint("왼쪽 생물 좌클릭 — DNA 추출");
    dna.ExtractionAllowed = true;

    // DNA는 복제 시 소비되므로 현재 복제한 종으로 성공을 판단한다.
    yield return new WaitUntil(() =>
        player != null &&
        player.CurrentCreatureDefinition == replacementDefinition);

    dna.ExtractionAllowed = false;

dialogue.ShowSequence(
    "복제 상태 정상.",
    "복제한 종의 외형과 이동·공격 능력, 고유 특성이 현재 신체에 적용완료.",
    "이제 복제한 신체의 공격 기능을 점검합니다.");
    controlHint.ShowHint("Enter — 계속");

    Debug.Log(
        "[프롤로그] 추출·폐기·첫 복제 훈련 완료!",
        this);
        yield return new WaitUntil(() => !dialogue.IsSequencePlaying);
        if (attackTargetPoint == null || player.CurrentAttackDamage <= 0)
{
    Debug.LogError(
        "[프롤로그] 공격 표적 연결과 B 데이터의 공격력을 확인해 주세요.",
        this);
    yield break;
}

attackTargetPoint.SpawnTarget();

CreatureController practiceTarget = attackTargetPoint.target;

if (practiceTarget == null || practiceTarget.Health == null)
    yield break;

ActorHealth practiceHealth = practiceTarget.Health;

// 다른 훈련 개체와 구분하기 위해 회색으로 표시한다.
Transform practiceVisual = practiceTarget.transform.Find("Visual");

if (practiceVisual != null &&
    practiceVisual.TryGetComponent<SpriteRenderer>(out var renderer))
{
    renderer.color = new Color(0.65f, 0.65f, 0.65f, 1f);
}

bool practiceHit = false;

// 플레이어가 실제 피해를 준 경우에만 성공으로 처리한다.
Action<ActorHealth, ActorDamageContext, int> onPracticeDamage =
    (actor, context, amount) =>
    {
        if (context.Source == player.Health && amount > 0)
            practiceHit = true;
    };

practiceHealth.DamageApplied += onPracticeDamage;

dialogue.ShowText(
    "전투 기능 확인. 배치된 연습 표적을 공격하십시오.");

controlHint.ShowHint("마우스 우클릭 — 회색 표적 공격");
try
{
    yield return new WaitUntil(() => practiceHit);
}
finally
{
    if (practiceHealth != null)
        practiceHealth.DamageApplied -= onPracticeDamage;
}

if (practiceTarget != null)
    Destroy(practiceTarget.gameObject);

dialogue.ShowText(
    "공격 기능 정상. 복제한 생물체의 공격 능력이 확인되었습니다.");

controlHint.ShowHint("Enter — 계속");

yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

dialogue.ShowText(
    "유전자 교체 기능 확인. 다른 생물체의 유전자를 확보하여 복제 상태를 전환하십시오.");

controlHint.ShowHint("오른쪽 생물 좌클릭 — DNA 추출");
dna.ExtractionAllowed = true;

// 처음 배치했던 A로 실제 복제했는지 확인한다.
yield return new WaitUntil(() =>
    player != null &&
    player.CurrentCreatureDefinition == trainingDefinition);

dna.ExtractionAllowed = false;

// A 복제 이후부터 훈련용 상한을 적용한다.
assimilation.SetTrainingLimit(true);
ownsTrainingLimit = true;

dialogue.ShowText(
    "유전자 교체 정상. 동화율 점검으로 전환합니다.");

controlHint.ShowHint("Enter — 계속");

yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

dialogue.ShowSequence(
    "복제한 신체를 유지하면 동화율이 상승합니다.",
    "동화율이 80%에 도달하면 복제한 생물체와의 동기화로 이상 증상이 발생합니다.",
    "현재 신체를 유지하며 동화율 변화를 관찰하십시오.");

controlHint.ShowHint("Enter — 계속");

yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

controlHint.ShowHint("복제 상태 유지 — 동화율 80% 도달");

// 훈련 중에만 빠르게 올린다.
while (assimilation.Percent < 80f)
{
    if (player == null || !player.Health.IsAlive)
        yield break;

    assimilation.AddTrainingAssimilation(
        trainingAssimilationPerSecond * Time.deltaTime);

    yield return null;
}

// 이후 B로 복제할 때까지 80% 상한은 계속 유지된다.
dialogue.ShowSequence(
    "임계치 도달. 이상 반응이 관찰됩니다.",
    "즉시 다른 생물체로 복제하십시오. 필요한 경우, 보유 중인 유전자를 먼저 폐기하십시오.");

controlHint.ShowHint("Enter — 계속");

yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

// B를 다시 추출할 수 있도록 준비한다.
replacementTarget.SpawnTarget();

if (replacementTarget.target == null)
{
    assimilation.SetTrainingLimit(false);
    ownsTrainingLimit = false;
    yield break;
}

replacementTarget.target.gameObject.SetActive(true);

controlHint.ShowHint(
    "왼쪽 생물 좌클릭 — DNA 추출·복제");

dna.ExtractionAllowed = true;

// B로 실제 복제했는지 확인한다.
yield return new WaitUntil(() =>
    player != null &&
    player.CurrentCreatureDefinition == replacementDefinition);

dna.ExtractionAllowed = false;

// 복제에 따른 동화율과 상태이상 갱신을 기다린다.
yield return null;

if (assimilation.Percent >= 80f)
{
    assimilation.SetTrainingLimit(false);
    ownsTrainingLimit = false;

    Debug.LogError(
        "[프롤로그] B 복제 후 동화율이 초기화되지 않았습니다. A·B의 Creature Id를 확인해 주세요.",
        this);

    yield break;
}

// B 복제 성공 후 일반 동화율 규칙으로 돌려놓는다.
assimilation.SetTrainingLimit(false);
ownsTrainingLimit = false;

dialogue.ShowText(
    "이상 반응 해소 확인. 파견 후에도 동화율을 지속적으로 확인하십시오.");

controlHint.ShowHint("Enter — 계속");

yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

controlHint.HideHint();

Debug.Log("[프롤로그] 동화율 훈련 완료!", this);

if (departure == null)
{
    Debug.LogError(
        "[프롤로그] Departure 연결을 확인해 주세요.", this);
    yield break;
}

yield return departure.Play(dialogue, controlHint, player);
}

private void OnDisable()
{
    StopAllCoroutines();

    if (ownsTrainingLimit && assimilation != null)
        assimilation.SetTrainingLimit(false);

    ownsTrainingLimit = false;
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