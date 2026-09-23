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
        [SerializeField, Min(0f)] private float respawnDelay = 1.25f;

        private PrototypePlayer player;
        private Vector3 playerSpawnPosition;
        private Coroutine respawnRoutine;

        public PrototypeSessionState State { get; private set; } = PrototypeSessionState.Playing;

        public event Action<PrototypeSessionState> StateChanged;

        public void Configure(PrototypePlayer newPlayer, Vector3 spawnPosition)
        {
            if (player != null)
            {
                player.Defeated -= HandlePlayerDefeated;
            }

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

            player = newPlayer;
            playerSpawnPosition = spawnPosition;

            if (player != null)
            {
                player.Defeated += HandlePlayerDefeated;
                player.SetControlsEnabled(true);
            }

            SetState(PrototypeSessionState.Playing);
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
            respawnRoutine = StartCoroutine(RespawnPlayer());
        }

        private IEnumerator RespawnPlayer()
        {
            yield return new WaitForSeconds(respawnDelay);

            if (player != null)
            {
                player.ResetAt(playerSpawnPosition);
            }

            respawnRoutine = null;
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
