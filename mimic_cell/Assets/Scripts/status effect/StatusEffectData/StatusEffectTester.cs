
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.StatusEffects
{
    public class StatusEffectTester : MonoBehaviour
    {
        [SerializeField] private StatusEffectDefinition dopamineRelease;

        private StatusEffectController controller;

        private void Awake()
        {
            controller = GetComponent<StatusEffectController>();
        }

        private void Update()
{

    bool testPressed = false;
    #if ENABLE_INPUT_SYSTEM
if (Keyboard.current != null &&
    Keyboard.current.tKey.isPressed)
{
    Debug.Log("T키가 눌린 상태");
}
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                Keyboard.current.tKey.wasPressedThisFrame)
            {
                testPressed = true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
            {
                testPressed = true;
            }
#endif

           if (testPressed)
{
    Debug.Log("T키 입력 감지!");

    if (controller == null)
    {
        Debug.LogWarning("StatusEffectController를 찾지 못했습니다.");
    }
    else if (dopamineRelease == null)
    {
        Debug.LogWarning("DopamineRelease 데이터가 연결되지 않았습니다.");
    }
    else
    {
        controller.ApplyEffect(dopamineRelease);
    }
}
        }
    }
}
