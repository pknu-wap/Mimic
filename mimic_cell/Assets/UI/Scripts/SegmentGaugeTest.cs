using UnityEngine;

public class SegmentGaugeTest : MonoBehaviour
{
    [Range(0, 100)] public float percent = 100f;
    private SegmentGauge gauge;

    void Start() { gauge = GetComponent<SegmentGauge>(); }
    void Update() { gauge.SetPercent(percent); }
}