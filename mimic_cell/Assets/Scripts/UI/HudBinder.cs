using UnityEngine;
using UnityEngine.UI;
using MimicCell.Player;
using MimicCell.Gameplay;
using MimicCell.Creatures;

public class HudBinder : MonoBehaviour
{
    [Header("스태미너 (줄무늬 칸)")]
    [SerializeField] private SegmentGauge staminaGauge;   // GaugeStripe
    [SerializeField] private PlayerStaminaController stamina;

    [Header("HP (빨간 바)")]
    [SerializeField] private Image hpFill;                // GaugeFill

    [Header("복제율/추출량 패널")]
    [SerializeField] private ExtractGauge extractGauge;   // ExtractPanel

    private ActorHealth playerHealth;
    private PlayerDnaExtraction dna;
    private bool loggedFound;

    void Update()
    {
        // 플레이어가 없어지면(사망/재생성) 다시 찾기
        if (stamina == null)
        {
            playerHealth = null;
            dna = null;
            loggedFound = false;
            stamina = FindFirstObjectByType<PlayerStaminaController>();
        }

        if (stamina == null)
        {
            RefreshExtractionPanel();
            return;
        }

        if (!loggedFound)
        {
            Debug.Log("[HudBinder] 플레이어 찾음", this);
            loggedFound = true;
        }

        // 스태미너
        if (staminaGauge != null)
        {
            float max = stamina.MaxStamina;
            float percent = max > 0f ? stamina.CurrentStamina / max * 100f : 0f;
            staminaGauge.SetPercent(percent);
        }

        // HP
        if (playerHealth == null)
            playerHealth = stamina.GetComponent<ActorHealth>();

        if (playerHealth != null)
            SetHp(playerHealth.CurrentHealth, playerHealth.MaxHealth);

        // DNA 추출량
        if (dna == null)
            dna = stamina.GetComponent<PlayerDnaExtraction>();

        RefreshExtractionPanel();
    }

    private void RefreshExtractionPanel()
    {
        if (extractGauge == null)
            return;

        extractGauge.SetPercent(dna != null ? dna.ExtractionPercent : 0f);
        extractGauge.SetDiscardPercent(dna != null ? dna.DiscardPercent : 0f);

        CreatureDefinition species = dna != null ? dna.StoredSpecies : null;
        Sprite sprite = species != null && species.PlayerForm != null
            ? species.PlayerForm.Sprite
            : null;
        extractGauge.SetTargetSprite(sprite, species != null ? species.ExtractionPortrait : null);
    }

    public void SetHp(float current, float max)
    {
        if (hpFill != null)
            hpFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}
