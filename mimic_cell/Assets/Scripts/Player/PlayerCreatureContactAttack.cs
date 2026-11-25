using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(PlayerAttackEffects))]
    public sealed class PlayerCreatureContactAttack : MonoBehaviour
    {
        [Header("돌진 공격")]
        [SerializeField, Min(0.1f)] private float attackRange = 8f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 10f;
[SerializeField, Min(0.01f)] private float dashDuration = 0.8f;
        [SerializeField, Min(0f)] private float contactCooldown = 0.8f;

        private TentacleTargeting targeting;

        private PrototypePlayer player;
        private PlayerAttackEffects attackEffects;
        private PlayerMovementController controls;
        private WasdSwimMovement movement;
        private PlayerCreatureFormController form;
        private PlayerDnaExtraction extraction;

        private CreatureController target;
        private Collider2D targetBody;

        private bool isDashing;
        private float dashEndsAt;
        private float nextAttackAt;
        private Vector2 dashDirection;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            attackEffects = GetComponent<PlayerAttackEffects>();
            controls = GetComponent<PlayerMovementController>();
            movement = GetComponent<WasdSwimMovement>();
            form = GetComponent<PlayerCreatureFormController>();
            extraction = GetComponent<PlayerDnaExtraction>();
            targeting = GetComponent<TentacleTargeting>();
        }

        private bool CanAttack()
        {
            return player != null &&
                   player.Health != null &&
                   player.Health.IsAlive &&
                   player.IsCreatureForm &&
                   player.CurrentAttackDamage > 0 &&
                   controls != null &&
                   controls.ControlsEnabled &&
                   movement != null &&
                   movement.isActiveAndEnabled &&
                   form != null &&
                   form.CreatureFormCollider != null &&
                   form.CreatureFormCollider.enabled;
        }

        private void Update()
        {
            if (isDashing)
            {
                if (!CanAttack() ||
                    target == null ||
                    target.Health == null ||
                    !target.Health.IsAlive ||
                    targetBody == null ||
                    !targetBody.enabled ||
                    !targetBody.gameObject.activeInHierarchy ||
                    Time.time >= dashEndsAt)
                {
                    EndDash();
                }

                return;
            }

            if (Time.timeScale <= 0f ||
                Time.time < nextAttackAt ||
                !CanAttack() ||
                (extraction != null && extraction.IsExtracting))
            {
                return;
            }

            // 1은 마우스 우클릭.
            if (!TentacleTargeting.TryReadClick(
                    1, transform.position, out Vector2 point))
            {
                return;
            }

            if (targeting == null)
                return;

            Vector3 bodyExtents = form.CreatureFormCollider.bounds.extents;
            float clickRange = attackRange + Mathf.Max(bodyExtents.x, bodyExtents.y);
            TentacleTargeting.Target clicked = targeting.Find(point, rangeOverride: clickRange);

            if (!clicked.IsAlive)
                return;

            Collider2D body = FindBody(clicked.Creature, point);

            if (body == null)
                return;

            Vector2 origin = transform.position;
            Vector2 closestPoint = body.ClosestPoint(origin);

            // 플레이어 몸집에 해당하는 반경을 추가한다.
            Vector3 extents = form.CreatureFormCollider.bounds.extents;
            float range = attackRange + Mathf.Max(extents.x, extents.y);

            if ((closestPoint - origin).sqrMagnitude > range * range)
                return;

            if (!attackEffects.TryBeginAttack())
                return;

            target = clicked.Creature;
            targetBody = body;

            Vector2 direction = closestPoint - origin;

            if (direction.sqrMagnitude < 0.001f)
                direction = (Vector2)target.transform.position - origin;

            dashDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.right;

            isDashing = true;
            dashEndsAt = Time.time + dashDuration;
            nextAttackAt = Time.time + contactCooldown;

            player.Health.SetDamageImmunity(this, true);
            movement.StartForcedMovement(dashDirection, dashSpeed);
        }

        private static Collider2D FindBody(
            CreatureController creature, Vector2 point)
        {
            Collider2D best = null;
            float nearest = float.PositiveInfinity;

            foreach (Collider2D candidate in
                     creature.GetComponentsInChildren<Collider2D>())
            {
                if (!candidate.enabled ||
                    !CreatureColliderRole.IsBodyCollider(candidate))
                {
                    continue;
                }

                float distance =
                    (candidate.ClosestPoint(point) - point).sqrMagnitude;

                if (distance < nearest)
                {
                    nearest = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private void FixedUpdate()
{
    if (!isDashing)
        return;

    if (!CanAttack() ||
        target == null ||
        target.Health == null ||
        !target.Health.IsAlive ||
        targetBody == null ||
        !targetBody.enabled ||
        !targetBody.gameObject.activeInHierarchy ||
        Time.time >= dashEndsAt)
    {
        EndDash();
        return;
    }

    ColliderDistance2D distance =
        form.CreatureFormCollider.Distance(targetBody);

    if (!distance.isValid)
    {
        EndDash();
        return;
    }

    // 닿았으면 한 번 공격하고 종료.
    if (distance.isOverlapped || distance.distance <= 0.03f)
    {
        HitTarget();
        return;
    }

    // 대상의 현재 위치를 향하도록 방향을 갱신.
    Vector2 direction = distance.pointB - distance.pointA;

    if (direction.sqrMagnitude <= 0.0001f)
    {
        EndDash();
        return;
    }

    dashDirection = direction.normalized;
    movement.StartForcedMovement(dashDirection, dashSpeed);
}

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHit(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHit(other);
        }

     private void OnCollisionEnter2D(Collision2D collision)
{
    HandleDashCollision(collision);
}

private void OnCollisionStay2D(Collision2D collision)
{
    HandleDashCollision(collision);
}

private void HandleDashCollision(Collision2D collision)
{
    if (!isDashing)
        return;

    // 선택한 생물과 닿았다면 공격 처리.
    TryHit(collision.collider);

    if (!isDashing)
        return;

    // 다른 물체가 진행 방향을 막고 있다면 종료.
    for (int i = 0; i < collision.contactCount; i++)
    {
        Vector2 normal = collision.GetContact(i).normal;

        if (Vector2.Dot(dashDirection, normal) < -0.2f)
        {
            EndDash();
            return;
        }
    }
}
        private void TryHit(Collider2D other)
        {
            if (!isDashing ||
                !CreatureColliderRole.IsBodyCollider(other))
            {
                return;
            }

            if (other.GetComponentInParent<CreatureController>() == target)
                HitTarget();
        }

        private void HitTarget()
        {
            if (!isDashing)
                return;

            // 재진입으로 중복 공격되지 않도록 먼저 종료한다.
            CreatureController victim = target;
            EndDash();

            if (victim == null ||
                victim.Health == null ||
                !victim.Health.IsAlive ||
                !player.Health.IsAlive)
            {
                return;
            }

            int damage = CreatureCombatRules.CalculateDamage(
                player.CurrentAttackDamage,
                player.CurrentFoodTier,
                victim.CurrentFoodTier,
                victim.CurrentTraits);

            if (damage <= 0)
                return;

            var context = new ActorDamageContext(
                damage,
                player.Health,
                ActorDamageType.Contact);

            if (victim.Health.TakeDamage(context))
            {
                attackEffects.NotifyHit();
                Debug.Log(
                    $"돌진 공격 적중: {victim.SpeciesName} / 피해 {damage}",
                    this);
            }
        }

        private void EndDash()
        {
            if (!isDashing)
                return;

            isDashing = false;
            nextAttackAt = Time.time + contactCooldown;

            if (movement != null)
                movement.StopForcedMovement();

            if (player != null && player.Health != null)
                player.Health.SetDamageImmunity(this, false);

            target = null;
            targetBody = null;
        }

        private void OnDisable()
        {
            EndDash();
        }
    }
}
