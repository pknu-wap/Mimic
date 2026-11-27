using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public sealed class ProloguePlayerProtection : MonoBehaviour
    {
        private ActorHealth health;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
        }

        private void OnEnable()
        {
            health.SetDeathProtection(this, true);
        }

        private void OnDisable()
        {
            if (health != null)
                health.SetDeathProtection(this, false);
        }
    }
}