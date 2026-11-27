using MimicCell.Creatures;
using MimicCell.Player;
using MimicCell.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PlayerPortraitBinder : MonoBehaviour
{
    [Header("플레이어 초상화")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject basePortraitRoot;

    [Header("기본 미믹세포 사진 조절")]
    [SerializeField] private CreaturePortraitSettings basePortraitSettings = new CreaturePortraitSettings();

    [Header("플레이어 이름")]
    [SerializeField] private TMP_Text creatureNameText;
    [SerializeField] private string baseFormDisplayName = "미믹세포";

    private PlayerCreatureFormController formController;
    private readonly PortraitLayout portraitLayout = new PortraitLayout();

    private void OnEnable()
    {
        if (portraitImage == null)
            portraitImage = GetComponent<Image>();

        portraitImage.preserveAspect = true;
        BindPlayer();
    }

    private void Update()
    {
        // 플레이어가 나중에 생성되거나 교체되면 현재 형태부터 다시 연결한다.
        if (formController == null)
            BindPlayer();
        else
            RefreshPortrait(formController.CurrentDefinition);
    }

    private void OnDisable()
    {
        if (formController != null)
            formController.FormChanged -= RefreshPortrait;

        formController = null;
        portraitLayout.Reset();
    }

    private void OnValidate()
    {
        basePortraitSettings ??= new CreaturePortraitSettings();
        basePortraitSettings.Validate();
    }

    private void BindPlayer()
    {
        formController = FindFirstObjectByType<PlayerCreatureFormController>();

        if (formController != null)
            formController.FormChanged += RefreshPortrait;

        RefreshPortrait(formController != null ? formController.CurrentDefinition : null);
    }

    private void RefreshPortrait(CreatureDefinition definition)
    {
        if (creatureNameText != null)
        {
            string displayName = definition != null ? definition.DisplayName : baseFormDisplayName;
            if (creatureNameText.text != displayName)
                creatureNameText.text = displayName;
        }

        Sprite sprite = definition != null && definition.PlayerForm != null
            ? definition.PlayerForm.Sprite
            : null;
        bool showCreature = sprite != null;

        portraitImage.sprite = sprite;
        portraitImage.enabled = showCreature;

        CreaturePortraitSettings settings = showCreature
            ? definition.PlayerPortrait
            : basePortraitSettings;
        portraitLayout.Apply(portraitImage.rectTransform, settings);

        bool showBase = !showCreature;
        if (basePortraitRoot != null && basePortraitRoot.activeSelf != showBase)
            basePortraitRoot.SetActive(showBase);
    }
}
