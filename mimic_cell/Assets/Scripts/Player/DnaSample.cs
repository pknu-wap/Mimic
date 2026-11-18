using MimicCell.Creatures;
using UnityEngine;

namespace MimicCell.Player
{
    internal class DnaSample
    {
        private int hits;
        private int requiredHits;

        public CreatureDefinition Species { get; private set; }
        public int Hits => hits;
        public int RequiredHits => requiredHits;
        public bool IsComplete => requiredHits > 0 && hits == requiredHits;

        public bool Accepts(CreatureDefinition species) => species != null && (Species == null || Species == species);

        public void AddHit(CreatureDefinition species, int maxHealth)
        {
            if (IsComplete || !Accepts(species))
            {
                return;
            }

            if (Species == null)
            {
                Species = species;
                float tierFactor = 1f + 0.1f * ((int)species.FoodTier - 1);
                requiredHits = Mathf.Max(1, Mathf.FloorToInt(1.6f * Mathf.Sqrt(maxHealth) * tierFactor + 0.5f));
            }

            hits++;
        }

        public void Clear()
        {
            Species = null;
            hits = 0;
            requiredHits = 0;
        }
    }
}
