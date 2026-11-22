using UnityEngine;

namespace MimicCell.Creatures
{
    public enum CreatureColliderPurpose
    {
        Body,
        Spikes,
        Capture
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CreatureColliderRole : MonoBehaviour
    {
        [SerializeField]
        private CreatureColliderPurpose purpose = CreatureColliderPurpose.Body;

        public CreatureColliderPurpose Purpose => purpose;

        public bool IsBody => purpose == CreatureColliderPurpose.Body;

        public static bool IsBodyCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            CreatureColliderRole role = collider.GetComponent<CreatureColliderRole>();

            return role == null || role.IsBody;
        }
    }
}