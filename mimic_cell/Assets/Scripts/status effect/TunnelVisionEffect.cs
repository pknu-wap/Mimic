
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
private readonly Dictionary<CreatureController, TierOneLightPulse> preyLights = new();[SerializeField]
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

    Material effectMaterial;

    switch (creature.Definition.FoodTier)
    {
        case CreatureFoodTier.Tier1:
            ShowPreyLight(creature);
continue;
        case CreatureFoodTier.Tier3:
            effectMaterial = blurMaterial;
            break;
        default:
            continue;
    }

    if (effectMaterial == null)
        continue;

    foreach (SpriteRenderer renderer in
             creature.GetComponentsInChildren<SpriteRenderer>())
    {
        if (!originalMaterials.ContainsKey(renderer))
        {
            originalMaterials.Add(renderer, renderer.sharedMaterial);
        }

        renderer.sharedMaterial = effectMaterial;
    }
}

}

  private void ShowPreyLight(CreatureController creature)
{
    if (preyLights.TryGetValue(creature, out TierOneLightPulse existing) &&
        existing != null)
    {
        return;
    }

    SpriteRenderer body = creature.GetComponentInChildren<SpriteRenderer>();
    if (body == null || body.sprite == null)
        return;

    var lightObject = new GameObject("Tier 1 Prey Light");
    lightObject.transform.SetParent(creature.transform, false);

    var pulse = lightObject.AddComponent<TierOneLightPulse>();
    pulse.Initialize(body, Random.Range(0f, 0.35f));
    preyLights[creature] = pulse;
}

private void ClearPreyLights()
{
    foreach (TierOneLightPulse pulse in preyLights.Values)
    {
        if (pulse != null)
            Destroy(pulse.gameObject);
    }

    preyLights.Clear();
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
    ClearPreyLights();
}

    }
}