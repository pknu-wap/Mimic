using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerCreatureFormController))]
    public sealed class PlayerCreatureTraitController : MonoBehaviour
    {
        [Header("Sensing")]
        [SerializeField, Min(1f)]
        private float sensingCameraZoomMultiplier = 1.25f;

        private CreatureDefinition activeDefinition;
        private Camera targetCamera;
        private float baseOrthographicSize;
        private bool hasBaseCameraSize;

        private Rigidbody2D attachedRigidbody;
        private PlayerCreatureFormController formController;
        private ICreatureEnvironment environment;

        private void Awake()
        {
            attachedRigidbody = GetComponent<Rigidbody2D>();

            formController = GetComponent<PlayerCreatureFormController>();

            ResolveCamera();
            ResolveEnvironment();
        }

        private void FixedUpdate()
        {
            if (activeDefinition == null || !activeDefinition.Traits.HasTrait(CreatureTraits.Benthic))
            {
                return;
            }

            ApplyBenthicMovement();
        }

        public void ApplyTraits(CreatureDefinition definition)
        {
            activeDefinition = definition;

            ResolveCamera();
            RestoreCameraZoom();

            CreatureTraits traits = activeDefinition != null
                    ? activeDefinition.Traits
                    : CreatureTraits.None;

            if (targetCamera == null ||
                !targetCamera.orthographic ||
                !traits.HasTrait(CreatureTraits.Sensing))
            {
                return;
            }

            targetCamera.orthographicSize = baseOrthographicSize * sensingCameraZoomMultiplier;
        }

        public void ClearTraits()
        {
            activeDefinition = null;

            ResolveCamera();
            RestoreCameraZoom();
        }

        private void ApplyBenthicMovement()
        {
            if (environment == null)
            {
                ResolveEnvironment();
            }

            PolygonCollider2D bodyCollider = formController != null
                    ? formController.CreatureFormCollider
                    : null;

            if (environment == null ||
                bodyCollider == null ||
                !bodyCollider.enabled)
            {
                return;
            }

            Vector2 position = attachedRigidbody.position;

            if (!environment.TryGetHabitatBottomY(
                    activeDefinition.AllowedHabitats,
                    position,
                    out float habitatBottomY))
            {
                return;
            }

            float targetY =
                habitatBottomY +
                Mathf.Max(0.1f, bodyCollider.bounds.extents.y) +
                CreatureTraitSettings.BenthicClearance;

            float heightError = targetY - position.y;

            Vector2 velocity = attachedRigidbody.linearVelocity;

            if (Mathf.Abs(heightError) <= CreatureTraitSettings.BenthicHeightTolerance)
            {
                velocity.y = 0f;
            }
            else
            {
                float maximumCorrectionSpeed = Mathf.Max(0.5f, activeDefinition.SwimSpeed);

                velocity.y = Mathf.Clamp(
                    heightError *
                    CreatureTraitSettings
                        .BenthicCorrectionStrength,
                    -maximumCorrectionSpeed,
                    maximumCorrectionSpeed);
            }

            attachedRigidbody.linearVelocity = velocity;
        }

        private void ResolveEnvironment()
        {
            if (environment != null)
            {
                return;
            }

            MonoBehaviour[] candidates = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (MonoBehaviour candidate in candidates)
            {
                if (candidate is
                    ICreatureEnvironment
                    resolvedEnvironment)
                {
                    environment = resolvedEnvironment;

                    return;
                }
            }
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null &&
                targetCamera.orthographic &&
                !hasBaseCameraSize)
            {
                baseOrthographicSize = targetCamera.orthographicSize;

                hasBaseCameraSize = true;
            }
        }

        private void RestoreCameraZoom()
        {
            if (targetCamera != null && hasBaseCameraSize)
            {
                targetCamera.orthographicSize = baseOrthographicSize;
            }
        }

        private void OnDisable()
        {
            RestoreCameraZoom();
        }
    }
}
