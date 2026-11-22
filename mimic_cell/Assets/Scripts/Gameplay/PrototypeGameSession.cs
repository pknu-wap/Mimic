using System;
using System.Collections;
using MimicCell.Player;
using UnityEngine;

namespace MimicCell.Gameplay
{
    public enum PrototypeSessionState
    {
        Playing,
        PlayerDefeated
    }

    [DisallowMultipleComponent]
    public sealed class PrototypeGameSession : MonoBehaviour
    {
        [SerializeField] private PrototypePlayer player;
        [SerializeField, Min(0f)] private float respawnDelay = 1.25f;

        private Vector3 playerSpawnPosition;

        public PrototypeSessionState State { get; private set; } = PrototypeSessionState.Playing;

        public event Action<PrototypeSessionState> StateChanged;

        private void Awake()
        {
            if (player == null)
            {
                Debug.LogError("Assign the scene player to the game session.", this);
                return;
            }

            playerSpawnPosition = player.transform.position;
            player.Defeated += HandlePlayerDefeated;
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.Defeated -= HandlePlayerDefeated;
            }
        }

        private void HandlePlayerDefeated(PrototypePlayer defeatedPlayer)
        {
            if (State == PrototypeSessionState.PlayerDefeated)
            {
                return;
            }

            defeatedPlayer.SetControlsEnabled(false);
            SetState(PrototypeSessionState.PlayerDefeated);
            StartCoroutine(RespawnPlayer());
        }

        private IEnumerator RespawnPlayer()
        {
            yield return new WaitForSeconds(respawnDelay);

            if (player != null)
            {
                player.ResetAt(playerSpawnPosition);
            }

            SetState(PrototypeSessionState.Playing);
        }

        private void SetState(PrototypeSessionState newState)
        {
            if (State == newState)
            {
                return;
            }

            State = newState;
            StateChanged?.Invoke(State);
        }
    }
}
