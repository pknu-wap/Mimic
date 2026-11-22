using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class PlayerJellyAnimation : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fullSwimSpeed = 4f;

        private static readonly int SwimAmount = Animator.StringToHash("SwimAmount");

        private Animator animator;
        private Rigidbody2D playerBody;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            playerBody = GetComponentInParent<Rigidbody2D>();
            if (playerBody == null)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            float swimAmount = Mathf.Clamp01(playerBody.linearVelocity.magnitude / fullSwimSpeed);
            animator.SetFloat(SwimAmount, swimAmount, dampTime: 0.12f, deltaTime: Time.deltaTime);
        }
    }
}
