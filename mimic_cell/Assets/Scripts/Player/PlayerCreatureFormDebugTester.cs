using MimicCell.Creatures;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypePlayer))]
    [RequireComponent(typeof(PlayerCreatureFormController))]
    public sealed class PlayerCreatureFormDebugTester : MonoBehaviour
    {
        [Header("Forms applied in order whenever F6 is pressed")]
        [SerializeField]
        private CreatureDefinition[] testForms;

        private PrototypePlayer player;
        private PlayerCreatureFormController formController;
        private int nextFormIndex;

        private void Awake()
        {
            player = GetComponent<PrototypePlayer>();

            formController =
                GetComponent<PlayerCreatureFormController>();
        }

        private void Update()
        {
            if (WasNextFormKeyPressed())
            {
                ApplyNextForm();
            }

            if (WasRestoreKeyPressed())
            {
                RestoreBaseForm();
            }
        }

        [ContextMenu("Apply Next Test Form")]
        private void ApplyNextForm()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "복제 테스트는 Play Mode에서 실행해야 합니다.",
                    this);

                return;
            }

            if (testForms == null || testForms.Length == 0)
            {
                Debug.LogWarning(
                    "Test Forms 배열에 CreatureDefinition을 넣어주세요.",
                    this);

                return;
            }

            CreatureDefinition definition =
                testForms[nextFormIndex];

            nextFormIndex =
                (nextFormIndex + 1) % testForms.Length;

            if (definition == null)
            {
                Debug.LogWarning(
                    "Test Forms 배열에 비어 있는 항목이 있습니다.",
                    this);

                return;
            }

            if (!player.TryApplyCreatureForm(definition))
            {
                Debug.LogWarning(
                    definition.name + " 복제 적용에 실패했습니다.",
                    this);

                return;
            }

            Debug.Log(
                "복제 적용 성공: " +
                definition.DisplayName +
                " / 먹이층: " +
                player.CurrentFoodTier +
                " / 체력: " +
                player.Health.CurrentHealth +
                "/" +
                player.Health.MaxHealth +
                " / 공격력: " +
                player.CurrentAttackDamage +
                " / 특징: " +
                player.CurrentTraits,
                this);
        }

        [ContextMenu("Restore Base Form")]
        private void RestoreBaseForm()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "기본 형태 복구 테스트는 Play Mode에서 실행해야 합니다.",
                    this);

                return;
            }

            formController.RestoreBaseForm();

            Debug.Log(
                "미믹세포 기본 형태로 복구했습니다.",
                this);
        }

        private static bool WasNextFormKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                keyboard.f6Key.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F6);
#else
            return false;
#endif
        }

        private static bool WasRestoreKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                keyboard.f7Key.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F7);
#else
            return false;
#endif
        }
    }
}