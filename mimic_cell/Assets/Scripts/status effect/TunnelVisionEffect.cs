
using UnityEngine;
using MimicCell.Creatures;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


namespace MimicCell.StatusEffects
{
    public sealed class TunnelVisionEffect : MonoBehaviour
    {
        [SerializeField]
private Material blurMaterial;

[SerializeField]
private StatusEffectDefinition tunnelVisionDefinition;

private StatusEffectController statusController;


private readonly Dictionary<SpriteRenderer, Material> originalMaterials
    = new Dictionary<SpriteRenderer, Material>();


private void Awake()
{
    statusController = GetComponent<StatusEffectController>();
}

        
private void Update()
{
    bool testPressed = false;

#if ENABLE_INPUT_SYSTEM
    if (Keyboard.current != null)
        testPressed = Keyboard.current.uKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
    testPressed = Input.GetKeyDown(KeyCode.U);
#endif

    if (!testPressed)
        return;
        Debug.Log("U키 입력 감지 성공!");

    if (statusController == null)
        statusController = GetComponent<StatusEffectController>();

    if (statusController != null && tunnelVisionDefinition != null)
    {
        statusController.ApplyEffect(tunnelVisionDefinition);
        Debug.Log("Tunnel Vision 활성화 여부: " +
            statusController.HasEffect(tunnelVisionDefinition));
    }
    else
    {
        Debug.LogWarning("Tunnel Vision: Controller 또는 Definition이 없습니다.");
    }
}



private void Start()
{
    InvokeRepeating(nameof(CheckCreatures), 1f, 1f);
}

private void CheckCreatures()
{
    
    if (statusController == null)
    {
        statusController = GetComponent<StatusEffectController>();
    }

    
if (statusController == null ||
    tunnelVisionDefinition == null ||
    !statusController.HasEffect(tunnelVisionDefinition))
{
    RestoreMaterials();
    return;
}


    CreatureController[] creatures =
        FindObjectsByType<CreatureController>(
            FindObjectsSortMode.None);

    Debug.Log($"현재 NPC 수: {creatures.Length}");

   
foreach (CreatureController creature in creatures)
{
    if (creature.Definition == null)
        continue;

    if (creature.Definition.FoodTier != CreatureFoodTier.Tier3)
        continue;

    SpriteRenderer[] renderers =
        creature.GetComponentsInChildren<SpriteRenderer>();

   
foreach (SpriteRenderer renderer in renderers)
{
    if (renderer == null || blurMaterial == null)
        continue;

    // 원래 Material을 한 번만 저장
    if (!originalMaterials.ContainsKey(renderer))
    {
        originalMaterials.Add(renderer, renderer.sharedMaterial);
    }

    // 블러 적용
    renderer.sharedMaterial = blurMaterial;
}


    Debug.Log(
        $"{creature.name}: Tier3 블러 적용 완료");
}

}

   

private void RestoreMaterials()
{
    foreach (var pair in originalMaterials)
    {
        if (pair.Key != null)
        {
            pair.Key.sharedMaterial = pair.Value;
        }
    }

    originalMaterials.Clear();
}

    }
}