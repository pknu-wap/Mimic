using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(PlayerAttackEffects))]
    [DefaultExecutionOrder(10)]
    public class PlayerTentacleAttack : MonoBehaviour
    {
        private const float Windup = 0.14f;
        private const float Lash = 0.13f;
        private const float ContactHold = 0.05f;
        private const float Recovery = 0.3f;
        private const float ImpactTime = Windup + Lash;

        [SerializeField] private PlayerJellyLeg[] blueTentacles = System.Array.Empty<PlayerJellyLeg>();
        [SerializeField] private PlayerTentacleBodyAim bodyAim;
        [SerializeField, Min(0.1f)] private float attackRange = 4f;
        [SerializeField] private Vector2Int attackDamage = new Vector2Int(2, 5);

        private readonly TentacleTargeting targeting = new TentacleTargeting();
        private PrototypePlayer player;
        private PlayerMovementController movement;
        private PlayerDnaExtraction extraction;
        private PlayerAttackEffects attackEffects;
        private PlayerJellyLeg strikingTentacle;
        private TentacleTargeting.Target target;
        private Vector2 aimPoint;
        private float startedAt;
        private bool impactPending;

        internal bool IsAttacking => strikingTentacle != null;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            movement = GetComponent<PlayerMovementController>();
            extraction = GetComponent<PlayerDnaExtraction>();
            attackEffects = GetComponent<PlayerAttackEffects>();
        }

        private void Update()
        {
            if (!player.Health.IsAlive || !movement.ControlsEnabled || player.IsCreatureForm)
            {
                EndAttack();
                return;
            }

            if (IsAttacking)
            {
                UpdateAttack();
                return;
            }

            if (Time.deltaTime > 0f && (extraction == null || !extraction.IsExtracting) &&
                TentacleTargeting.TryReadClick(1, transform.position, out Vector2 point))
            {
                BeginAttack(point);
            }
        }

        private void BeginAttack(Vector2 point)
        {
            PlayerJellyLeg best = null;
            float highestAlignment = float.NegativeInfinity;
            foreach (PlayerJellyLeg leg in blueTentacles)
            {
                if (leg == null || !leg.isActiveAndEnabled)
                {
                    continue;
                }

                float alignment = leg.GetStrikeAlignment(point);
                if (alignment > highestAlignment)
                {
                    highestAlignment = alignment;
                    best = leg;
                }
            }
            if (best == null || !attackEffects.TryBeginAttack())
            {
                return;
            }

            strikingTentacle = best;
            target = InRange(point) ? targeting.Find(point) : default;
            aimPoint = point;
            startedAt = Time.time;
            impactPending = true;
        }

        private void UpdateAttack()
        {
            if (!strikingTentacle.isActiveAndEnabled)
            {
                EndAttack();
                return;
            }

            float elapsed = Time.time - startedAt;
            if (target.IsAlive && impactPending)
            {
                aimPoint = target.Point;
            }

            if (impactPending && elapsed >= ImpactTime)
            {
                impactPending = false;
                ApplyHit();
            }

            float recovery = Mathf.Clamp01((elapsed - ImpactTime - ContactHold) / Recovery);
            if (recovery >= 1f)
            {
                EndAttack();
                return;
            }

            float cast = Mathf.Clamp01((elapsed - Windup) / Lash);
            float reach = Mathf.SmoothStep(0f, 1f, cast) * (1f - Mathf.SmoothStep(0f, 1f, recovery));
            float curl = Mathf.Lerp(-0.55f, 0.13f, cast) + recovery * 0.32f;
            Vector2 origin = transform.position;
            Vector2 strikePoint = origin + Vector2.ClampMagnitude(aimPoint - origin, attackRange);
            if (bodyAim != null)
            {
                Vector2 attachment = strikingTentacle.GetJointPosition(0) - (Vector2)bodyAim.transform.position;
                bodyAim.ApplyPose(strikePoint, attachment, elapsed, Windup, cast, reach, recovery);
            }
            strikingTentacle.SetStrikePose(strikePoint, reach, curl);
        }

        private void ApplyHit()
        {
            if (!target.IsAlive || !InRange(target.Point))
            {
                return;
            }

            CreatureController creature = target.Creature;
            ActorHealth health = creature.Health;
            int min = Mathf.Max(1, attackDamage.x);
            int max = Mathf.Max(min, attackDamage.y);
            int rolledDamage = Random.Range(min - 1, max) + 1;
            int damage = CreatureCombatRules.CalculateDamage(
                rolledDamage, player.CurrentFoodTier, creature.CurrentFoodTier, creature.CurrentTraits);
            int appliedDamage = Mathf.Min(damage, health.CurrentHealth);
            if (!health.TakeDamage(new ActorDamageContext(damage, player.Health, ActorDamageType.Tentacle)))
            {
                return;
            }

            // TODO(가시): 피격 대상의 가시 특성이 발동하면 반사 피해를 처리한다. 반사에는 스태미너를 차감하지 않는다.
            // TODO(엔딩 점수): 과잉 피해를 뺀 appliedDamage를 엔딩 집계에 전달한다.
            attackEffects.NotifyHit();
            Debug.Log($"[공격] {creature.Definition.DisplayName} 피해 {appliedDamage}", this);
        }

        internal CreatureController FindAimCreature(Vector2 point) =>
            isActiveAndEnabled && !player.IsCreatureForm && InRange(point)
                ? targeting.Find(point).Creature
                : null;

        private bool InRange(Vector2 point) =>
            (point - (Vector2)transform.position).sqrMagnitude <= attackRange * attackRange;

        private void OnDisable() => EndAttack();

        private void EndAttack()
        {
            target = default;
            if (strikingTentacle != null)
            {
                strikingTentacle.SetStrikePose(default, 0f, 0f);
                strikingTentacle = null;
                if (bodyAim != null)
                {
                    bodyAim.Restore();
                }
            }
        }
    }
}
