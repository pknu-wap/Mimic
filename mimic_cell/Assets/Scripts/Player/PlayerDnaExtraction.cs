using System;
using System.Collections;
using MimicCell.Creatures;
using MimicCell.Gameplay;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer), typeof(TentacleTargeting))]
    public class PlayerDnaExtraction : MonoBehaviour
    {
        private const float DiscardSeconds = 2f;

        private readonly DnaSample sample = new DnaSample();
        private TentacleTargeting targeting;
        private ActorHealth health;
        private PrototypePlayer player;
        private PlayerMovementController movement;
        private PlayerTentacleStrike tentacle;
        private PlayerTentacleAttack attack;
        private Coroutine strike;
        private Predicate<CreatureDefinition> canExtract;
        private float discardTime;

        internal bool IsExtracting => strike != null;
        public bool HasStoredDna => sample.Species != null;
        public bool ExtractionAllowed { get; set; } = true;

        public float ExtractionPercent =>
    sample.Species != null && sample.RequiredHits > 0
        ? (float)sample.Hits / sample.RequiredHits * 100f
        : 0f;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();
            health = GetComponent<ActorHealth>();
            movement = GetComponent<PlayerMovementController>();
            tentacle = GetComponentInChildren<PlayerTentacleStrike>(true);
            attack = GetComponent<PlayerTentacleAttack>();
            targeting = GetComponent<TentacleTargeting>();
            canExtract = CanExtract;
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
            if (!ExtractionAllowed)
    return;

            if (IsExtracting || sample.IsComplete || (attack != null && attack.IsAttacking) ||
                tentacle == null || !tentacle.isActiveAndEnabled ||
                !TentacleTargeting.TryReadClick(0, transform.position, out Vector2 point))
            {
                return;
            }

            TentacleTargeting.Target target = targeting.Find(point, canExtract);
            if (target.IsAlive && tentacle.Play(point))
            {
                strike = StartCoroutine(Extract(target));
            }
        }

        private IEnumerator Extract(TentacleTargeting.Target target)
        {
            float hitAt = Time.time + tentacle.ImpactTime;
            do
            {
                yield return null;
                if (target.IsAlive)
                {
                    tentacle.Track(targeting.ClampToRange(target.Point));
                }
            }
            while (Time.time < hitAt);

            if (movement.ControlsEnabled && target.IsAlive && targeting.IsInRange(target.Point))
            {
                CreatureDefinition species = target.Creature.Definition;
                sample.AddHit(species, target.Creature.Health.MaxHealth);
                Debug.Log($"[DNA] {species.DisplayName} 적중 ({sample.Hits}/{sample.RequiredHits})", this);
            }

            yield return new WaitForSeconds(tentacle.RecoveryTime);
            strike = null;
          if (sample.IsComplete && health.IsAlive)
{
    CreatureDefinition species = sample.Species;

    if (player.TryApplyCreatureForm(species))
    {
        sample.Clear();

        Debug.Log(
            $"[DNA] {species.DisplayName} 추출 완료. 복제했습니다.",
            this);
    }
}
        }

        private bool CanExtract(CreatureDefinition species) =>
            sample.Accepts(species) && species.PlayerForm != null && species.PlayerForm.IsConfigured;

        internal CreatureController FindAimCreature(Vector2 point) =>
            isActiveAndEnabled && !sample.IsComplete && tentacle != null && tentacle.isActiveAndEnabled
                ? targeting.Find(point, canExtract).Creature
                : null;

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
