using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth))]
    public class PlayerAttack : MonoBehaviour
    {
        private ActorHealth health;
        private PlayerTentacleAim tentacleAim;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            tentacleAim = GetComponentInChildren<PlayerTentacleAim>(true);
        }

        private void OnEnable()
        {
            health.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDeath;
            }

            CancelAim();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || !health.IsAlive || tentacleAim == null ||
                !tentacleAim.isActiveAndEnabled || !TryReadClick(out Vector2 screenPosition))
            {
                return;
            }

            Camera aimCamera = Camera.main;
            if (aimCamera == null)
            {
                return;
            }

            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            Plane swimPlane = new Plane(Vector3.forward, tentacleAim.transform.position);
            if (swimPlane.Raycast(ray, out float distance))
            {
                tentacleAim.AimAt(ray.GetPoint(distance));
            }
        }

        private void OnDeath(ActorHealth actor)
        {
            CancelAim();
        }

        private void CancelAim()
        {
            if (tentacleAim != null)
            {
                tentacleAim.ClearAim();
            }
        }

        private static bool TryReadClick(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                bool clicked = mouse.leftButton.wasPressedThisFrame;
                screenPosition = clicked ? mouse.position.ReadValue() : default;
                return clicked;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            bool legacyClicked = Input.GetMouseButtonDown(0);
            screenPosition = legacyClicked ? (Vector2)Input.mousePosition : default;
            return legacyClicked;
#else
            screenPosition = default;
            return false;
#endif
        }
    }
}
