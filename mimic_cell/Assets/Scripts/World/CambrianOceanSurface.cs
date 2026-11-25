using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CambrianOceanWorld))]
    [DefaultExecutionOrder(100)]
    public class CambrianOceanSurface : MonoBehaviour
    {
        private const int Segments = 96;
        private const int VerticesPerColumn = 5;

        [SerializeField] private Color waterHighlight = new Color(0.67f, 0.9f, 0.94f, 1f);
        [SerializeField] private Color skyHorizon = new Color(0.88f, 0.96f, 0.97f, 1f);
        [SerializeField] private Color skyHigh = new Color(0.32f, 0.65f, 0.84f, 1f);

        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * VerticesPerColumn];
        private CambrianOceanWorld ocean;
        private Camera sceneCamera;
        private Mesh mesh;
        private MeshRenderer surfaceRenderer;

        private void Start()
        {
            ocean = GetComponent<CambrianOceanWorld>();
            sceneCamera = ocean.StreamingCamera != null ? ocean.StreamingCamera : Camera.main;
            if (sceneCamera == null || !sceneCamera.orthographic || ocean.WaterMaterial == null)
            {
                enabled = false;
                return;
            }

            var surface = new GameObject("Waves and Sky");
            surface.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Cambrian Waves and Sky" };
            mesh.MarkDynamic();
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = ocean.WaterMaterial;
            surfaceRenderer.sortingOrder = -101;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = false;
            InitializeMesh();
        }

        private void InitializeMesh()
        {
            var colors = new Color[vertices.Length];
            var triangles = new int[Segments * 18];
            for (int i = 0; i <= Segments; i++)
            {
                int column = i * VerticesPerColumn;
                colors[column] = ocean.SurfaceTint;
                colors[column + 1] = Color.Lerp(ocean.SurfaceTint, waterHighlight, 0.35f);
                colors[column + 2] = waterHighlight;
                colors[column + 3] = skyHorizon;
                colors[column + 4] = skyHigh;
                if (i < Segments)
                {
                    WriteStrip(triangles, i * 18, column);
                    WriteStrip(triangles, i * 18 + 6, column + 1);
                    WriteStrip(triangles, i * 18 + 12, column + 3);
                }
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = new Vector2[vertices.Length];
            mesh.triangles = triangles;
        }

        private static void WriteStrip(int[] triangles, int index, int lowerLeft)
        {
            int lowerRight = lowerLeft + VerticesPerColumn;
            triangles[index] = lowerLeft;
            triangles[index + 1] = lowerLeft + 1;
            triangles[index + 2] = lowerRight;
            triangles[index + 3] = lowerLeft + 1;
            triangles[index + 4] = lowerRight + 1;
            triangles[index + 5] = lowerRight;
        }

        private void LateUpdate()
        {
            if (sceneCamera == null)
            {
                return;
            }

            Vector3 cameraPosition = sceneCamera.transform.position;
            float cameraTop = cameraPosition.y + sceneCamera.orthographicSize;
            float surfaceY = ocean.SurfaceY;
            surfaceRenderer.enabled = cameraTop >= surfaceY - 0.02f;
            if (!surfaceRenderer.enabled)
            {
                return;
            }

            float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect + 1f;
            float left = Mathf.Max(ocean.WorldMinX, cameraPosition.x - halfWidth);
            float right = Mathf.Min(ocean.WorldMaxX, cameraPosition.x + halfWidth);
            float skyTop = Mathf.Max(surfaceY + 14f, cameraTop + 1f);
            float time = Time.time;
            for (int i = 0; i <= Segments; i++)
            {
                float x = Mathf.Lerp(left, right, i / (float)Segments);
                float wave = surfaceY + 0.58f + 0.30f * Mathf.Sin(x * 0.55f - time * 1.65f) +
                    0.14f * Mathf.Sin(x * 1.42f + time * 2.2f);
                int column = i * VerticesPerColumn;
                vertices[column] = new Vector3(x, surfaceY - 0.02f);
                vertices[column + 1] = new Vector3(x, wave - 0.12f);
                vertices[column + 2] = new Vector3(x, wave);
                vertices[column + 3] = vertices[column + 2];
                vertices[column + 4] = new Vector3(x, skyTop);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (surfaceRenderer != null)
            {
                surfaceRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (surfaceRenderer != null)
            {
                Destroy(surfaceRenderer.gameObject);
            }
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }
}
