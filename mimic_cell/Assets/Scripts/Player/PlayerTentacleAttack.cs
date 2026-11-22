using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.StatusEffects;
using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(StatusEffectController))]
    [DefaultExecutionOrder(10)]
    public class PlayerTentacleAttack : MonoBehaviour
    {
        private const float Windup = 0.14f;
        private const float Lash = 0.13f;
        private const float ContactHold = 0.05f;
        private const float Recovery = 0.3f;
        private const float ImpactTime = Windup + Lash;

        [SerializeField] private PlayerJellyLeg[] blueTentacles = System.Array.Empty<PlayerJellyLeg>();
        [SerializeField] private StatusEffectDefinition dopamineRelease;
        [SerializeField, Min(0.1f)] private float attackRange = 4f;
        [SerializeField] private Vector2Int attackDamage = new Vector2Int(2, 5);

        private readonly TentacleTargeting targeting = new TentacleTargeting();
        private PrototypePlayer player;
        private PlayerMovementController movement;
        private PlayerDnaExtraction extraction;
        private StatusEffectController statusEffects;
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
            statusEffects = GetComponent<StatusEffectController>();
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
            if (best == null)
            {
                return;
            }

            // TODO(스태미너): 시도 확정 전에 2층 10 / 3층 35를 차감하고, 부족하면 취소한다. 빗나가도 차감한다.
            strikingTentacle = best;
            target = InRange(point) ? targeting.Find(point) : default;
            aimPoint = point;
            startedAt = Time.time;
            impactPending = true;
            if (dopamineRelease != null)
            {
                statusEffects.ApplyEffect(dopamineRelease);
            }
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
            strikingTentacle.SetStrikePose(origin + Vector2.ClampMagnitude(aimPoint - origin, attackRange), reach, curl);
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
            if (!health.TakeDamage(damage))
            {
                return;
            }

            // TODO(가시): 피격 대상의 가시 특성이 발동하면 반사 피해를 처리한다. 반사에는 스태미너를 차감하지 않는다.
            // TODO(엔딩 점수): 과잉 피해를 뺀 appliedDamage를 엔딩 집계에 전달한다.
            // TODO(동화율): 적중한 생물과 피해량을 동화 시스템에 전달해 공격 규칙에 따라 갱신한다.
            Debug.Log($"[공격] {creature.Definition.DisplayName} 피해 {appliedDamage}", this);
        }

        private bool InRange(Vector2 point) =>
            (point - (Vector2)transform.position).sqrMagnitude <= attackRange * attackRange;

        private void OnDisable() => EndAttack();

        private void EndAttack()
        {
            if (strikingTentacle != null)
            {
                strikingTentacle.SetStrikePose(default, 0f, 0f);
                strikingTentacle = null;
            }
        }
    }
}
