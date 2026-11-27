using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MimicCell.Creatures;
using MimicCell.UI;

public class ExtractGauge : MonoBehaviour
{
    [Header("연결할 오브젝트")]
    [SerializeField] private Image redFill;           // RedFill
    [SerializeField] private TMP_Text percentText;    // PercentText
    [SerializeField] private Transform segmentsParent; // Segments
    [SerializeField] private Image targetPortrait;     // 복제 대상 사진

    [Header("칸 스프라이트")]
    [SerializeField] private Sprite segOn;   // 켜진 칸 (청록)
    [SerializeField] private Sprite segOff;  // 꺼진 칸 (남색)

    private Image[] segments;
    private readonly PortraitLayout portraitLayout = new PortraitLayout();

    void Awake()
    {
        segments = new Image[segmentsParent.childCount];
        for (int i = 0; i < segments.Length; i++)
            segments[i] = segmentsParent.GetChild(i).GetComponent<Image>();

        SetPercent(0f);
        SetDiscardPercent(0f);
        SetTargetSprite(null);
    }

    public void SetTargetSprite(Sprite sprite)
    {
        SetTargetSprite(sprite, null);
    }

    public void SetTargetSprite(Sprite sprite, CreaturePortraitSettings settings)
    {
        if (targetPortrait == null)
            return;

        targetPortrait.sprite = sprite;
        targetPortrait.preserveAspect = true;
        targetPortrait.enabled = sprite != null;
        portraitLayout.Apply(targetPortrait.rectTransform, sprite != null ? settings : null);
    }

    private void OnDisable()
    {
        portraitLayout.Reset();
    }

    // DNA 폐기 진행률: 빨간 채움만 변경한다.
public void SetDiscardPercent(float percent)
{
    if (redFill != null)
        redFill.fillAmount = Mathf.Clamp01(percent / 100f);
}

// DNA 추출 진행률: 숫자와 칸을 변경한다.
    public void SetPercent(float percent)
    {
        if (segments == null)
            return;

        percent = Mathf.Clamp(percent, 0f, 100f);
        float ratio = percent / 100f;


        // 1) 글자
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(percent) + "%";

        // 2) 칸 (왼쪽부터 켜짐)
        int on = Mathf.CeilToInt(ratio * segments.Length);
        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = (i < on) ? segOn : segOff;
    }
}
