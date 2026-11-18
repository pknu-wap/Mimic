using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Player;
using MimicCell.World;
using UnityEngine;

namespace MimicCell.StatusEffects
{
    [DisallowMultipleComponent]
    public sealed class PressureMaladaptationEffect : MonoBehaviour
    {
        private const float DamageInterval = 1f;
        private const double DamageFraction = 0.10;

        private const float DistortionBuildUpSeconds = 5f;
private const float DistortionRecoverySeconds = 0.6f;
private static readonly int PressureStrengthId =
    Shader.PropertyToID("_MimicCellPressureScreenStrength");

private float distortionStrength;

        [SerializeField] private CambrianOceanWorld oceanWorld;

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
                UpdateDistortion(false); // 수압이 맞는 곳에서는 서서히 복구
    
                elapsed = 0f;
                damageRemainder = 0;
                return;
            }
            UpdateDistortion(true); // 수압이 맞지 않는 곳에서는 점점 강해짐


            elapsed += Time.deltaTime;

            while (elapsed >= DamageInterval && health.IsAlive)
            {
                elapsed -= DamageInterval;
                damageRemainder += health.MaxHealth * DamageFraction;

                int damage = (int)System.Math.Floor(damageRemainder);
                damageRemainder -= damage;

                if (health.TakeDamage(damage))
                {

                    Debug.Log(
                        $"수압 부적응 [{current}] 피해 {damage}. " +
                        $"체력 {health.CurrentHealth}/{health.MaxHealth}",
                        this);
                }
            }
        }

        private void UpdateDistortion(bool exposed)
{
    float target = exposed ? 1f : 0f;
    float seconds = exposed ? DistortionBuildUpSeconds : DistortionRecoverySeconds;

    distortionStrength = Mathf.MoveTowards(
        distortionStrength, target, Time.deltaTime / seconds);

    Shader.SetGlobalFloat(PressureStrengthId, distortionStrength);
}

private void OnDisable()
{
    elapsed = 0f;
    damageRemainder = 0;
    distortionStrength = 0f;
    Shader.SetGlobalFloat(PressureStrengthId, 0f);
}
    }
}