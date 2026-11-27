using System.Collections;
using MimicCell.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MimicCell.Prologue
{
    [DisallowMultipleComponent]
    public sealed class PrologueDeparture : MonoBehaviour
    {
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField, Min(0.1f)] private float fadeDuration = 1.5f;
        [SerializeField] private string destinationScene = "CambrianOcean";

        private bool isDeparting;

        public IEnumerator Play(
            PrologueDialogue dialogue,
            PrologueControlHint controlHint,
            PrototypePlayer player)
        {
            if (isDeparting)
                yield break;

            if (fadeGroup == null ||
                dialogue == null ||
                controlHint == null ||
                player == null)
            {
                Debug.LogError(
                    "[프롤로그] 파견 연출 연결을 확인해 주세요.", this);
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError(
                    $"[프롤로그] 씬 목록에 {destinationScene}을 추가해 주세요.",
                    this);
                yield break;
            }

            isDeparting = true;

            // 마지막 대사 중에는 플레이어 조작을 멈춘다.
            player.SetControlsEnabled(false);

            dialogue.ShowSequence(
                "최종 점검 완료. 파견 적합 판정.",
                "현지에는 실험실에서 재현되지 않은 환경 요인과 위험이 존재합니다. 주변 환경을 확인하며 행동하십시오.",
                "목표 행성 식별 코드, SOL-3.",
                "파견 개체, 미믹세포.",
                "임무는 현지 생물의 유전 정보 확보입니다.",
                "파견을 개시합니다.");

            controlHint.ShowHint("Enter — 계속");

            yield return new WaitUntil(
                () => !dialogue.IsSequencePlaying);

            controlHint.HideHint();

            fadeGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float startAlpha = fadeGroup.alpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.SmoothStep(
                    0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));

                fadeGroup.alpha = Mathf.Lerp(
                    startAlpha, 1f, progress);

                yield return null;
            }

            fadeGroup.alpha = 1f;

// 검은 화면을 한 프레임 표시한 뒤 씬을 불러온다.
            yield return null;

            yield return SceneManager.LoadSceneAsync(
                destinationScene, LoadSceneMode.Single);
        }
    }
}