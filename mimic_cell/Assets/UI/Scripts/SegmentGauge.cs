using UnityEngine;
using UnityEngine.UI;

public class SegmentGauge : MonoBehaviour
{
    [Header("칸 스프라이트")]
    [SerializeField] private Sprite segOn;   // 초록
    [SerializeField] private Sprite segOff;  // 회색

    [Header("같이 움직일 빨간 바 (없으면 비워두기)")]
    [SerializeField] private Image redFill;  // GaugeFill

    private Image[] segments;

    void Awake()
    {
        segments = new Image[transform.childCount];
        for (int i = 0; i < segments.Length; i++)
            segments[i] = transform.GetChild(i).GetComponent<Image>();
    }

    // percent: 0 ~ 100
    public void SetPercent(float percent)
    {
        float ratio = Mathf.Clamp01(percent / 100f);
        int on = Mathf.CeilToInt(ratio * segments.Length);

        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = (i < on) ? segOn : segOff;

        if (redFill != null)
            redFill.fillAmount = ratio;
    }
}