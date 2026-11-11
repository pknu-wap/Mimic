using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Player;
using MimicCell.World;
using UnityEngine;
using MimicCell.CameraSystem;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class PressureMaladaptationEffect : MonoBehaviour
    {
        private const float DamageInterval = 1f;
        private const double DamageFraction = 0.10;

        [SerializeField] private CambrianOceanWorld oceanWorld;
        [SerializeField] private CameraFollow2D cameraFollow;

        [Header("Test Only")]
        [SerializeField] private bool useTestHabitat;
        [SerializeField] private CreatureHabitatMask testAllowedHabitats =
            CreatureHabitatMask.OceanAA;

        private ActorHealth health;
        private PlayerCreatureFormController formController;
        private float elapsed;
        private double damageRemainder;

        private void Start()
        {
            health = GetComponent<ActorHealth>();
            formController = GetComponent<PlayerCreatureFormController>();

            if (oceanWorld == null || health == null)
            {
                Debug.LogWarning(
                    "수압 부적응: Ocean World 연결과 플레이어의 Actor Health를 확인해 주세요.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            CreatureDefinition form = formController != null
                ? formController.CurrentDefinition
                : null;

            CreatureHabitatMask allowed = useTestHabitat
                ? testAllowedHabitats
                : form != null ? form.AllowedHabitats : CreatureHabitatMask.None;

            CreatureHabitatMask current = oceanWorld.GetHabitatAt(transform.position);

            if (!health.IsAlive ||
                allowed == CreatureHabitatMask.None ||
                current == CreatureHabitatMask.None ||
                (allowed & current) != CreatureHabitatMask.None)
            {
                elapsed = 0f;
                damageRemainder = 0;
                return;
            }

            elapsed += Time.deltaTime;

            while (elapsed >= DamageInterval && health.IsAlive)
            {
                elapsed -= DamageInterval;
                damageRemainder += health.MaxHealth * DamageFraction;

                int damage = (int)System.Math.Floor(damageRemainder);
                damageRemainder -= damage;

                if (health.TakeDamage(damage))
                {
                      cameraFollow?.PlayPressureShake();

                    Debug.Log(
                        $"수압 부적응 [{current}] 피해 {damage}. " +
                        $"체력 {health.CurrentHealth}/{health.MaxHealth}",
                        this);
                }
            }
        }

        private void OnDisable()
        {
            elapsed = 0f;
            damageRemainder = 0;
        }
    }
}