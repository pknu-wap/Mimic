using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.Core
{
    internal static class RenderingDebugSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableRuntimeUI()
        {
            DebugManager.instance.enableRuntimeUI = false;
        }
    }
}
