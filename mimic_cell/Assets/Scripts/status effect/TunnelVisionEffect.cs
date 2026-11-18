
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
private const float RecoveryDuration = 0.8f;
private static readonly int BlurStrengthId = Shader.PropertyToID("_BlurStrength");

private Material runtimeBlurMaterial;
private float fullBlurStrength;
private float visionWeight;
[SerializeField] private StatusEffectDefinition tunnelVisionDefinition;

private StatusEffectController statusController;


private readonly Dictionary<SpriteRenderer, Material> originalMaterials
    = new Dictionary<SpriteRenderer, Material>();


private void Awake()
{
    statusController = GetComponent<StatusEffectController>();
    if (blurMaterial != null)
{
    runtimeBlurMaterial = new Material(blurMaterial);
    fullBlurStrength = runtimeBlurMaterial.GetFloat(BlurStrengthId);
}
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
        case CreatureFoodTier.Tier3:
    effectMaterial = runtimeBlurMaterial;
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

 

private void LateUpdate()
{
    bool active = statusController != null &&
                  tunnelVisionDefinition != null &&
                  statusController.HasEffect(tunnelVisionDefinition);

    visionWeight = active
        ? 1f
        : Mathf.MoveTowards(visionWeight, 0f, Time.deltaTime / RecoveryDuration);

    if (runtimeBlurMaterial != null)
        runtimeBlurMaterial.SetFloat(BlurStrengthId, fullBlurStrength * visionWeight);

   
}

private void OnDisable()
{
    visionWeight = 0f;
    RestoreMaterials();
}

private void OnDestroy()
{
    if (runtimeBlurMaterial != null)
        Destroy(runtimeBlurMaterial);
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