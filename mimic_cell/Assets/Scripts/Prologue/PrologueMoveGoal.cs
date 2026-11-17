using MimicCell.Player;
using UnityEngine;


namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PrologueMoveGoal : MonoBehaviour
    {
[SerializeField] private PrologueDialogue dialogue;
        private bool completed;

        private void Start()
{
    if (dialogue != null)
    {
        dialogue.ShowText(
            "연구원: 탐사 개체, 기초 유영 훈련을 시작한다.\n" +
            "WASD로 초록색 목표 지점까지 이동하라.");
    }
}

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed)
                return;

            PrototypePlayer player = other.GetComponentInParent<PrototypePlayer>();
            if (player == null)
                return;

            completed = true;
           if (dialogue != null)
    dialogue.ShowText("연구원: 기초 유영 능력 확인. 다음 훈련을 준비한다.");
            Debug.Log("[프롤로그] 이동 훈련 완료!", this);
            gameObject.SetActive(false);
        }
    }
}