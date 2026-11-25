using System.Collections;
using MimicCell.Player;
using UnityEngine;


namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PrologueMoveGoal : MonoBehaviour
    {
[SerializeField] private PrologueDialogue dialogue;
[SerializeField] private PrologueControlHint controlHint;
        private bool completed;
        private bool movementReady;
        public bool IsCompleted => completed;

private IEnumerator Start()
{
    CircleCollider2D goalArea = GetComponent<CircleCollider2D>();
    SpriteRenderer goalMarker = GetComponent<SpriteRenderer>();

    goalArea.enabled = false;

    if (goalMarker != null)
        goalMarker.enabled = false;

    if (dialogue == null || controlHint == null)
    {
        Debug.LogError(
            "[프롤로그] MoveGoal의 Dialogue와 Control Hint를 연결해 주세요.",
            this);
        yield break;
    }

    dialogue.ShowText(
        "미믹세포, 활성화 확인. 지시가 들리면 반응하십시오.");

    controlHint.ShowHint("Enter — 응답");

    yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

    dialogue.ShowText(
        "파견 전 최종 점검 프로토콜을 개시합니다.");

    controlHint.ShowHint("Enter — 계속");

    yield return new WaitUntil(() => !dialogue.IsSequencePlaying);

    dialogue.ShowText(
        "운동 기능 확인. 지정된 위치로 이동하십시오.");

    controlHint.ShowHint("WASD — 이동");

    movementReady = true;

    if (goalMarker != null)
        goalMarker.enabled = true;

    goalArea.enabled = true;
}

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!movementReady || completed)
    return;

            PrototypePlayer player = other.GetComponentInParent<PrototypePlayer>();
            if (player == null)
                return;

                                   completed = true;

            if (dialogue != null)
            {
                dialogue.ShowText(
                    "운동 반응 정상. 다음 항목으로 진행합니다.");
            }

            if (controlHint != null)
            {
                controlHint.ShowHint(
                    "Enter — 다음 항목으로 진행");
            }

            Debug.Log("[프롤로그] 이동 훈련 완료!", this);
            gameObject.SetActive(false);
        }
    }
}