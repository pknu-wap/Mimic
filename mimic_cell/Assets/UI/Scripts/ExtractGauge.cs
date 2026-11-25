using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExtractGauge : MonoBehaviour
{
    [Header("연결할 오브젝트")]
    [SerializeField] private Image redFill;           // RedFill
    [SerializeField] private TMP_Text percentText;    // PercentText
    [SerializeField] private Transform segmentsParent; // Segments

    [Header("칸 스프라이트")]
    [SerializeField] private Sprite segOn;   // 켜진 칸 (청록)
    [SerializeField] private Sprite segOff;  // 꺼진 칸 (남색)

    private Image[] segments;

    void Awake()
    {
        segments = new Image[segmentsParent.childCount];
        for (int i = 0; i < segments.Length; i++)
            segments[i] = segmentsParent.GetChild(i).GetComponent<Image>();

        SetPercent(0f);
    }

    // 게임 시스템에서는 이 함수만 호출하면 됨 (0 ~ 100)
    public void SetPercent(float percent)
    {
        if (segments == null)
            return;

        percent = Mathf.Clamp(percent, 0f, 100f);
        float ratio = percent / 100f;

        // 1) 빨간 채움 (아래 -> 위)
        if (redFill != null)
            redFill.fillAmount = ratio;

        // 2) 글자
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(percent) + "%";

        // 3) 칸 (왼쪽부터 켜짐)
        int on = Mathf.CeilToInt(ratio * segments.Length);
        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = (i < on) ? segOn : segOff;
    }
}