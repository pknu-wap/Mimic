using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PrologueMoveGoal : MonoBehaviour
    {
        private bool completed;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed)
                return;

            PrototypePlayer player = other.GetComponentInParent<PrototypePlayer>();
            if (player == null)
                return;

            completed = true;
            Debug.Log("[프롤로그] 이동 훈련 완료!", this);
            gameObject.SetActive(false);
        }
    }
}