using System.Collections;
using TMPro;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    public sealed class PrologueDialogue : MonoBehaviour
    {
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 30f;

        private Coroutine typing;

        public void ShowText(string message)
        {
            if (dialogueText == null || !isActiveAndEnabled)
                return;

            FinishTyping();

            dialogueText.text = message;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            typing = StartCoroutine(TypeText());
        }

        private IEnumerator TypeText()
        {
            int characterCount = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;

            while (dialogueText.maxVisibleCharacters < characterCount)
            {
                visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                dialogueText.maxVisibleCharacters =
                    Mathf.Min(characterCount, Mathf.FloorToInt(visibleCharacters));

                yield return null;
            }

            typing = null;
        }

        private void Update()
        {
            if (typing != null && WasConfirmPressed())
                FinishTyping();
        }

        private void FinishTyping()
        {
            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
            }

            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        private void OnDisable()
        {
            FinishTyping();
        }

        private static bool WasConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   Keyboard.current.enterKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return);
#else
            return false;
#endif
        }
    }
}
