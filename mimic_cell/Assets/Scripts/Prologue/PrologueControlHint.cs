using TMPro;
using UnityEngine;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    public sealed class PrologueControlHint : MonoBehaviour
    {
        [SerializeField] private TMP_Text hintText;

        public void ShowHint(string message)
        {
            if (hintText == null)
                return;

            hintText.text = message;
        }

        public void HideHint()
        {
            if (hintText == null)
                return;

            hintText.text = string.Empty;
        }
    }
}