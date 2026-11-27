using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitlePlayButton : MonoBehaviour
{
    [SerializeField] private string prologueSceneName = "Prologue";

    public void Play()
    {
        if (!Application.CanStreamedLevelBeLoaded(prologueSceneName))
        {
            Debug.LogError(
                $"씬 목록에서 {prologueSceneName}을 확인해 주세요.",
                this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(prologueSceneName);
    }
}