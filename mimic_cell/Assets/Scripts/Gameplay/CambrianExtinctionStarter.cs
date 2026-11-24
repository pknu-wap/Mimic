using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CambrianExtinctionStarter : MonoBehaviour
    {
        [SerializeField] private CambrianExtinctionController extinction;

        private void Update()
        {
            if (WasTestKeyPressed())
                StartExtinction();
        }

       [ContextMenu("Test Start Extinction")]
public void StartExtinction()
{
    if (!Application.isPlaying)
        return;

    if (extinction == null)
    {
        Debug.LogError("[대멸종 시작] Extinction 연결이 비어 있습니다.", this);
        return;
    }

    extinction.enabled = true;
    extinction.gameObject.SetActive(true);

    Debug.Log(
        $"[대멸종 시작] 오브젝트 활성 {extinction.gameObject.activeInHierarchy} · " +
        $"컴포넌트 활성 {extinction.isActiveAndEnabled}",
        this);
}

        private static bool WasTestKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.jKey.wasPressedThisFrame;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.J);
#else
            return false;
#endif
        }
    }
}