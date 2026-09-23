using MimicCell.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.UI
{
    [DisallowMultipleComponent]
    public sealed class StartMenuController : MonoBehaviour
    {
        private void Update()
        {
            if (WasCambrianStartPressed())
            {
                LoadCambrian();
            }

            if (WasModernStartPressed())
            {
                LoadModern();
            }
        }

        private void OnGUI()
        {
            const float width = 420f;
            const float height = 230f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(panel, GUIContent.none);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                wordWrap = true
            };

            GUI.Label(new Rect(panel.x + 20f, panel.y + 20f, panel.width - 40f, 44f), "Mimic Cell", titleStyle);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 72f, panel.width - 64f, 48f),
                "이동 방식 비교: 1번 WASD 유영 / 2번 마우스 클릭 유지 이동",
                bodyStyle);

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 132f, panel.width - 140f, 34f), "캄브리아기 바다 시작"))
            {
                LoadCambrian();
            }

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 174f, panel.width - 140f, 34f), "현대 테스트 씬"))
            {
                LoadModern();
            }
        }

        private static void LoadCambrian()
        {
            SceneManager.LoadScene(MimicSceneNames.CambrianOcean);
        }

        private static void LoadModern()
        {
            SceneManager.LoadScene(MimicSceneNames.Modern);
        }

        private static bool WasCambrianStartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private static bool WasModernStartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.M);
#else
            return false;
#endif
        }
    }
}
