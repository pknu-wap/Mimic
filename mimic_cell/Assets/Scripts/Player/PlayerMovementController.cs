using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Player
{
    public enum PlayerMovementMode
    {
        WasdSwim,
        MouseHold
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(WasdSwimMovement))]
    [RequireComponent(typeof(MouseHoldMovement))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movement Test")]
        [SerializeField] private PlayerMovementMode startingMode = PlayerMovementMode.WasdSwim;
        [SerializeField] private bool allowNumberKeySwitching = true;

        private WasdSwimMovement wasdMovement;
        private MouseHoldMovement mouseHoldMovement;
        private bool controlsEnabled = true;

        public PlayerMovementMode CurrentMode { get; private set; }

        private void Awake()
        {
            ResolveComponents();
            ApplyMovementMode(startingMode);
        }

        private void Update()
        {
            if (!controlsEnabled || !allowNumberKeySwitching)
            {
                return;
            }

            if (WasNumberKeyPressed(1))
            {
                SetMovementMode(PlayerMovementMode.WasdSwim);
            }
            else if (WasNumberKeyPressed(2))
            {
                SetMovementMode(PlayerMovementMode.MouseHold);
            }
        }

        public void Configure(Camera aimCamera, Transform visualRoot, LineRenderer aimLine)
        {
            ResolveComponents();
            wasdMovement.Configure(visualRoot);
            mouseHoldMovement.Configure(aimCamera, visualRoot, aimLine);
            ApplyMovementMode(CurrentMode);
        }

        public void SetMovementMode(PlayerMovementMode newMode)
        {
            if (CurrentMode == newMode && IsModeApplied(newMode))
            {
                return;
            }

            startingMode = newMode;
            ApplyMovementMode(newMode);
            Debug.Log("Player movement mode changed to " + newMode + ".");
        }

        public void SetControlEnabled(bool isEnabled)
        {
            controlsEnabled = isEnabled;
            ApplyMovementMode(CurrentMode);
        }

        private void ApplyMovementMode(PlayerMovementMode mode)
        {
            ResolveComponents();

            bool useWasd = mode == PlayerMovementMode.WasdSwim;
            wasdMovement.enabled = controlsEnabled && useWasd;
            mouseHoldMovement.enabled = controlsEnabled && !useWasd;
            CurrentMode = mode;
        }

        private bool IsModeApplied(PlayerMovementMode mode)
        {
            if (!controlsEnabled)
            {
                return !wasdMovement.enabled && !mouseHoldMovement.enabled;
            }

            return mode == PlayerMovementMode.WasdSwim
                ? wasdMovement.enabled && !mouseHoldMovement.enabled
                : !wasdMovement.enabled && mouseHoldMovement.enabled;
        }

        private void ResolveComponents()
        {
            if (wasdMovement == null)
            {
                wasdMovement = GetComponent<WasdSwimMovement>();
            }

            if (mouseHoldMovement == null)
            {
                mouseHoldMovement = GetComponent<MouseHoldMovement>();
            }
        }

        private static bool WasNumberKeyPressed(int number)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (number == 1 && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
                {
                    return true;
                }

                if (number == 2 && (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame))
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return number == 1
                ? Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)
                : Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
#else
            return false;
#endif
        }
    }
}
