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

    [Header("테스트")]
    [SerializeField, Range(0, 100)] private float testPercent = 65f;

    private Image[] segments;
    private float currentPercent = -1f;

    void Awake()
    {
        segments = new Image[segmentsParent.childCount];
        for (int i = 0; i < segments.Length; i++)
            segments[i] = segmentsParent.GetChild(i).GetComponent<Image>();

        SetPercent(testPercent);
    }

    // 게임 시스템에서는 이 함수만 호출하면 됨 (0 ~ 100)
    public void SetPercent(float percent)
    {
        percent = Mathf.Clamp(percent, 0f, 100f);
        currentPercent = percent;
        float ratio = percent / 100f;

        // 1) 빨간 채움 (아래 -> 위)
        redFill.fillAmount = ratio;

        // 2) 글자
        percentText.text = Mathf.RoundToInt(percent) + "%";

        // 3) 칸 (왼쪽부터 켜짐)
        int on = Mathf.CeilToInt(ratio * segments.Length);
        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = (i < on) ? segOn : segOff;
    }

#if UNITY_EDITOR
    // Play 중 Inspector 슬라이더를 움직이면 바로 반영 (테스트용)
    void Update()
    {
        if (Application.isPlaying && !Mathf.Approximately(testPercent, currentPercent))
            SetPercent(testPercent);
    }
#endif
}