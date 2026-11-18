using System.Collections;
using System.Collections.Generic;
using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorHealth), typeof(PlayerMovementController))]
    public class PlayerDnaExtraction : MonoBehaviour
    {
        private const float DiscardSeconds = 2f;

        [SerializeField, Min(0.1f)] private float extractionRange = 6f;

        private readonly DnaSample sample = new DnaSample();
        private readonly List<Collider2D> clickHits = new List<Collider2D>(8);
        private ActorHealth health;
        private PlayerMovementController movement;
        private PlayerTentacleStrike tentacle;
        private Coroutine strike;
        private float discardTime;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            movement = GetComponent<PlayerMovementController>();
            tentacle = GetComponentInChildren<PlayerTentacleStrike>(true);
        }

        private void OnEnable() => health.Died += OnDeath;

        private void OnDisable()
        {
            health.Died -= OnDeath;
            CancelStrike();
        }

        private void Update()
        {
            if (!health.IsAlive || !movement.ControlsEnabled || Time.deltaTime <= 0f)
            {
                discardTime = 0f;
                return;
            }

            if (UpdateDiscard())
            {
                return;
            }

            if (strike != null || sample.IsComplete || tentacle == null || !tentacle.isActiveAndEnabled ||
                !TryReadClick(out Vector2 screenPosition) ||
                !TryGetWorldPoint(screenPosition, out Vector2 point) || !IsInRange(point))
            {
                return;
            }

            Collider2D hit = FindTarget(point, out CreatureDefinition species, out ActorHealth targetHealth);
            if (hit != null && tentacle.Play(point))
            {
                Vector2 localPoint = hit.transform.InverseTransformPoint(point);
                strike = StartCoroutine(Extract(hit, species, targetHealth, localPoint));
            }
        }

        private IEnumerator Extract(Collider2D hit, CreatureDefinition species, ActorHealth targetHealth, Vector2 localPoint)
        {
            float hitAt = Time.time + tentacle.ImpactTime;
            do
            {
                yield return null;
                if (IsTargetAlive(hit, targetHealth))
                {
                    Vector2 point = hit.transform.TransformPoint(localPoint);
                    Vector2 origin = transform.position;
                    tentacle.Track(origin + Vector2.ClampMagnitude(point - origin, extractionRange));
                }
            }
            while (Time.time < hitAt);

            if (movement.ControlsEnabled && IsTargetAlive(hit, targetHealth) &&
                IsInRange(hit.transform.TransformPoint(localPoint)))
            {
                sample.AddHit(species, targetHealth.MaxHealth);
                Debug.Log($"[DNA] {species.DisplayName} 적중 ({sample.Hits}/{sample.RequiredHits})", this);
                if (sample.IsComplete)
                {
                    Debug.Log($"[DNA] {species.DisplayName} 추출 완료. 복제 가능.", this);
                }
            }

            yield return new WaitForSeconds(tentacle.RecoveryTime);
            strike = null;
        }

        private Collider2D FindTarget(Vector2 point, out CreatureDefinition species, out ActorHealth targetHealth)
        {
            Physics2D.OverlapPoint(point, new ContactFilter2D { useTriggers = true }, clickHits);
            Collider2D bestHit = null;
            float bestDistance = float.PositiveInfinity;
            species = null;
            targetHealth = null;

            foreach (Collider2D hit in clickHits)
            {
                CreatureController creature = hit.GetComponentInParent<CreatureController>();
                CreatureDefinition definition = creature != null ? creature.Definition : null;
                if (!sample.Accepts(definition))
                {
                    continue;
                }

                ActorHealth candidateHealth = creature.GetComponent<ActorHealth>();
                float distance = (point - (Vector2)creature.transform.position).sqrMagnitude;
                if (distance < bestDistance && IsTargetAlive(hit, candidateHealth))
                {
                    bestHit = hit;
                    bestDistance = distance;
                    species = definition;
                    targetHealth = candidateHealth;
                }
            }

            return bestHit;
        }

        private bool IsInRange(Vector2 point)
        {
            return (point - (Vector2)transform.position).sqrMagnitude <= extractionRange * extractionRange;
        }

        private static bool IsTargetAlive(Collider2D hit, ActorHealth targetHealth)
        {
            return hit != null && hit.enabled && hit.gameObject.activeInHierarchy &&
                targetHealth != null && targetHealth.IsAlive;
        }

        private bool UpdateDiscard()
        {
            if (sample.Species == null || !IsDiscardHeld())
            {
                discardTime = 0f;
                return false;
            }

            discardTime += Time.deltaTime;
            if (discardTime >= DiscardSeconds)
            {
                CancelStrike();
                sample.Clear();
                Debug.Log("[DNA] 샘플 폐기.", this);
            }
            return true;
        }

        private void OnDeath(ActorHealth actor) => CancelStrike();

        private void CancelStrike()
        {
            discardTime = 0f;
            if (strike != null)
            {
                StopCoroutine(strike);
                strike = null;
            }
            if (tentacle != null)
            {
                tentacle.Cancel();
            }
        }

        private bool TryGetWorldPoint(Vector2 screenPosition, out Vector2 point)
        {
            point = default;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            Plane swimPlane = new Plane(Vector3.forward, transform.position);
            if (!swimPlane.Raycast(ray, out float distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        private static bool TryReadClick(out Vector2 point)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                point = Mouse.current.position.ReadValue();
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            point = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
#else
            point = default;
            return false;
#endif
        }

        private static bool IsDiscardHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.rKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.R);
#else
            return false;
#endif
        }
    }
}
