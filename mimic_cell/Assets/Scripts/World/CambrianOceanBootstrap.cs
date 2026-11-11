using MimicCell.CameraSystem;
using UnityEngine;

namespace MimicCell.World
{
    [DisallowMultipleComponent]
    public sealed class CambrianOceanBootstrap : MonoBehaviour
    {
        [SerializeField] private CambrianOceanWorld ocean;
        [SerializeField] private SeabedTerrainWorld terrain;
        [SerializeField] private CameraFollow2D cameraFollow;

        private void Awake()
        {
            if (ocean == null || terrain == null || cameraFollow == null)
            {
                Debug.LogError("Assign the ocean, seabed terrain, and camera follow in the scene.", this);
                return;
            }

            Camera sceneCamera = cameraFollow.GetComponent<Camera>();
            ocean.Configure(sceneCamera, null);
            terrain.Configure(ocean, sceneCamera, null);
            ocean.RegisterTerrain(terrain);
            ocean.GetCameraBounds(sceneCamera, out Vector2 minBounds, out Vector2 maxBounds);
            cameraFollow.SetBounds(minBounds, maxBounds);
        }
    }
}
