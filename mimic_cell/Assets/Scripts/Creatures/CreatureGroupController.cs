using System.Collections.Generic;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Creatures
{
    public enum CreatureGroupState
    {
        Roaming,
        Scattered,
        Regrouping
    }

    [DisallowMultipleComponent]
    public sealed class CreatureGroupController : MonoBehaviour
    {
        [SerializeField]
        private CreatureGroupState currentState =
            CreatureGroupState.Roaming;

        private readonly List<CreatureController> members =
            new List<CreatureController>();

        private CreatureDefinition definition;
        private EcosystemDefinition ecosystem;
        private ICreatureEnvironment environment;
        private PrototypePlayer player;

        public CreatureGroupState CurrentState => currentState;

        public int MemberCount => members.Count;

        public bool Initialize(
    CreatureDefinition newDefinition,
    EcosystemDefinition newEcosystem,
    ICreatureEnvironment newEnvironment,
    PrototypePlayer targetPlayer)
        {
            if (newDefinition == null ||
                newEcosystem == null ||
                newEnvironment == null ||
                targetPlayer == null)
            {
                Debug.LogError(
                    "CreatureGroupController requires complete initialization data.",
                    this);

                return false;
            }

            if (newDefinition.SpawnKind != CreatureSpawnKind.Group)
            {
                Debug.LogError(
                    "CreatureGroupController requires a group creature definition.",
                    this);

                return false;
            }

            definition = newDefinition;
            ecosystem = newEcosystem;
            environment = newEnvironment;
            player = targetPlayer;
            currentState = CreatureGroupState.Roaming;

            return true;
        }
    }
}