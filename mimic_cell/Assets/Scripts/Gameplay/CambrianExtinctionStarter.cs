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

        public void StartExtinction()
        {
            if (extinction == null || extinction.gameObject.activeSelf)
                return;

            extinction.gameObject.SetActive(true);
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