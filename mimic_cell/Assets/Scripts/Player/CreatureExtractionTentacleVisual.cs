using UnityEngine;

namespace MimicCell.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(30)]
    public sealed class CreatureExtractionTentacleVisual : MonoBehaviour
    {
        [SerializeField] private Material tentacleMaterial;
        [SerializeField] private Color tentacleColor =
            new Color(1f, 0.35f, 0.55f, 1f);

        [SerializeField, Min(0.01f)] private float width = 0.18f;
        [SerializeField, Min(0.01f)] private float impactTime = 0.4f;
        [SerializeField, Min(0.01f)] private float recoveryTime = 0.5f;

        private readonly Vector3[] points = new Vector3[25];

        private LineRenderer line;
        private PrototypePlayer player;
        private PlayerCreatureFormController form;
        private Vector2 targetPoint;
        private float startedAt;
        private bool playing;

        public float ImpactTime => impactTime;
        public float RecoveryTime => recoveryTime;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            player = GetComponentInParent<PrototypePlayer>();
            form = GetComponentInParent<PlayerCreatureFormController>();

            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.startWidth = width;
            line.endWidth = width * 0.3f;
            line.startColor = tentacleColor;
            line.endColor = tentacleColor;
            line.enabled = false;

            if (tentacleMaterial != null)
                line.sharedMaterial = tentacleMaterial;
        }

        public bool Play(Vector2 point)
        {
            if (!isActiveAndEnabled ||
                player == null ||
                !player.IsCreatureForm ||
                tentacleMaterial == null)
            {
                Debug.LogWarning(
                    "복제 상태와 Tentacle Material 연결을 확인해 주세요.",
                    this);
                return false;
            }

            targetPoint = point;
            startedAt = Time.time;
            playing = true;
            line.enabled = true;
            return true;
        }

        public void Track(Vector2 point)
        {
            targetPoint = point;
        }

        private void LateUpdate()
        {
            if (!playing)
                return;

            if (player == null ||
                !player.IsCreatureForm ||
                !player.Health.IsAlive)
            {
                Stop();
                return;
            }

            float elapsed = Time.time - startedAt;

            if (elapsed >= impactTime + recoveryTime)
            {
                Stop();
                return;
            }

            Vector2 origin = player.transform.position;
            Collider2D body = form.CreatureFormCollider;

            if (body != null && body.enabled)
            {
                Vector2 edge = body.ClosestPoint(targetPoint);

                origin = (edge - targetPoint).sqrMagnitude > 0.001f
                    ? edge
                    : (Vector2)body.bounds.center;

                SpriteRenderer sprite =
                    body.GetComponent<SpriteRenderer>();

                if (sprite != null)
                {
                    line.sortingLayerID = sprite.sortingLayerID;
                    line.sortingOrder = sprite.sortingOrder + 1;
                }
            }

            float reach = elapsed <= impactTime
                ? Mathf.SmoothStep(0f, 1f, elapsed / impactTime)
                : Mathf.SmoothStep(
                    1f, 0f,
                    (elapsed - impactTime) / recoveryTime);

            Vector2 tip = Vector2.Lerp(origin, targetPoint, reach);
            Vector2 span = tip - origin;
            Vector2 bend = Vector2.Perpendicular(span) * 0.2f;

            for (int i = 0; i < points.Length; i++)
            {
                float t = (float)i / (points.Length - 1);
                Vector2 point = Vector2.Lerp(origin, tip, t) +
                    bend * Mathf.Sin(t * Mathf.PI);

                points[i] = new Vector3(
                    point.x, point.y, player.transform.position.z);
            }

            line.SetPositions(points);
        }

        public void Stop()
        {
            playing = false;

            if (line != null)
                line.enabled = false;
        }

        private void OnDisable()
        {
            Stop();
        }

        [ContextMenu("Test Tentacle")]
        private void TestTentacle()
        {
            if (!Application.isPlaying || player == null)
                return;

            Play((Vector2)player.transform.position + Vector2.right * 6f);
        }
    }
}