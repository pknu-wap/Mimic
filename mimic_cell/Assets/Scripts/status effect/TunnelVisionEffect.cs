
using UnityEngine;
using MimicCell.Creatures;

namespace MimicCell.StatusEffects
{
    public sealed class TunnelVisionEffect : MonoBehaviour
    {
        [SerializeField]
private Material blurMaterial;
        
private void Start()
{
    InvokeRepeating(nameof(CheckCreatures), 1f, 1f);
}

private void CheckCreatures()
{
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
        if (blurMaterial != null)
        {
            renderer.sharedMaterial = blurMaterial;
        }
    }

    Debug.Log(
        $"{creature.name}: Tier3 블러 적용 완료");
}

}

    }
}
