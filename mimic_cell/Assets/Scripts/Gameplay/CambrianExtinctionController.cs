using MimicCell.Creatures;
using MimicCell.Player;
using MimicCell.World;
using UnityEngine;
using MimicCell.StatusEffects;

namespace MimicCell.Gameplay
{
    public enum ExtinctionPhase
    {
        Warning,
        Turbidity,
        Rest,
        Anoxia,
        Cold,
        Complete
    }

    // The test scene owns the event. The ordinary Cambrian scene never creates this component.
    [DisallowMultipleComponent]
    public sealed class CambrianExtinctionController : MonoBehaviour
    {
        private static readonly float[] Durations = { 10f, 18f, 10f, 25f, 70f };
        private static readonly string[] PhaseNames =
        {
            "WARNING", "TURBIDITY", "REST", "ANOXIA", "COLD", "COMPLETE"
        };

        private CambrianOceanWorld ocean;
        private PrototypePlayer player;
        [Header("Tile Visuals")]
[SerializeField] private bool useTileVisuals;
[SerializeField] private Texture2D turbidityTile;
[SerializeField] private Texture2D anoxiaTile;
        private Camera viewCamera;
        private ExtinctionPhase phase = ExtinctionPhase.Warning;
        private float phaseTime;
        [SerializeField, Min(0f)] private float boundaryVariation = 40f;
private float turbidityOffset;
private float anoxiaOffset;
        private float creatureRefreshTime;
        private GUIStyle hudStyle;
        private BoxCollider2D iceCollider;

        public ExtinctionPhase Phase => phase;
        public float PhaseTime => phaseTime;

        private void Awake()
        {
            ocean = FindFirstObjectByType<CambrianOceanWorld>();
            player = FindFirstObjectByType<PrototypePlayer>();
            viewCamera = Camera.main;
            RollBoundaryOffsets();

               Debug.Log(
        $"[대멸종 연결] 바다 {ocean != null} · " +
        $"플레이어 {player != null} · 카메라 {viewCamera != null}",
        this);
        }

        private void Update()
        {
            if (ocean == null || player == null)
            {
                return;
            }

            if (phase != ExtinctionPhase.Complete)
            {
                phaseTime += Time.deltaTime;
                if (phaseTime >= Durations[(int)phase])
                {
                    phaseTime -= Durations[(int)phase];
                    phase++;
                    creatureRefreshTime = 0f;
                }
            }

            UpdateColdSurface();

            ApplyHazard(player.gameObject);

            creatureRefreshTime -= Time.deltaTime;
            if (creatureRefreshTime <= 0f)
            {
                creatureRefreshTime = 0.2f;
                RefreshCreatures();
            }
        }
private void OnDisable()
{
    CreatureController[] creatures =
        FindObjectsByType<CreatureController>(FindObjectsSortMode.None);

    foreach (CreatureController creature in creatures)
    {
        if (creature.TryGetComponent<ExtinctionStatusEffects>(out var effects))
            effects.SetExposure(ExtinctionStatus.None);
    }
}
        private void OnDestroy()
        {
            CreatureController[] creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
            foreach (CreatureController creature in creatures)
            {
                creature.SetExtinctionBehavior(false, Vector2.zero, false);
            }
            CreatureSpawner[] spawners = FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None);
            foreach (CreatureSpawner spawner in spawners)
            {
                spawner.SetExtinctionSpawnRateMultiplier(1f);
            }
        }

        public void SkipPhase()
        {
            if (phase < ExtinctionPhase.Complete)
            {
                phase++;
                phaseTime = 0f;
                creatureRefreshTime = 0f;
            }
        }

        public void Restart()
        {
            phase = ExtinctionPhase.Warning;
            phaseTime = 0f;
            creatureRefreshTime = 0f;
            RollBoundaryOffsets();
        }

        private void RefreshCreatures()
        {
            CreatureController[] creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
            
            bool cold = phase == ExtinctionPhase.Cold && phaseTime >= 5f;
            CreatureSpawner[] spawners = FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None);
            foreach (CreatureSpawner spawner in spawners)
            {
                spawner.SetExtinctionSpawnRateMultiplier(cold ? 0.35f : 1f);
            }

ExtinctionWarningMovement warningMovement = GetComponent<ExtinctionWarningMovement>();

            foreach (CreatureController creature in creatures)
            {
                if (creature == null || creature.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

bool isWarning = phase == ExtinctionPhase.Warning;
bool inDanger = TryGetHazard(
    creature.transform.position, out ExtinctionStatus hazard);

bool escaping = inDanger &&
    (hazard == ExtinctionStatus.MudCoating ||
     hazard == ExtinctionStatus.Anoxia);
     
Vector2 escapeDirection = escaping
    ? GetEscapeDirection() * 4f
    : Vector2.zero;

if (isWarning && warningMovement != null && warningMovement.isActiveAndEnabled)
{
    escapeDirection = warningMovement.GetScatteredDirection(creature, phaseTime);
}

creature.SetExtinctionBehavior(isWarning || escaping, escapeDirection, cold);
                ApplyHazard(creature.gameObject);
            }
        }

        private void UpdateColdSurface()
        {
            bool frozen = phase == ExtinctionPhase.Cold && phaseTime >= 5f;
            if (frozen && iceCollider == null)
            {
                GameObject ice = new GameObject("Extinction Ice Ceiling");
                ice.transform.SetParent(transform, false);
                ice.transform.position = new Vector3(0f, ocean.SurfaceY - 1.2f, 0f);
                iceCollider = ice.AddComponent<BoxCollider2D>();
                iceCollider.size = new Vector2(ocean.WorldMaxX - ocean.WorldMinX, 1.6f);
            }
            if (iceCollider != null)
            {
                iceCollider.enabled = frozen;
            }
        }

        private void ApplyHazard(GameObject actor)
{
    bool hasHazard = TryGetHazard(
        actor.transform.position, out ExtinctionStatus status);

    if (player != null && actor == player.gameObject)
    {
        bool handledByPlayerEffect = false;

        if (actor.TryGetComponent<AnoxiaEffect>(out var anoxia) &&
            anoxia.isActiveAndEnabled)
        {
            bool insideAnoxia = hasHazard && status == ExtinctionStatus.Anoxia;
            anoxia.SetExposure(insideAnoxia);
            handledByPlayerEffect |= insideAnoxia;
        }

        if (actor.TryGetComponent<FreezingEffect>(out var freezing) &&
            freezing.isActiveAndEnabled)
        {
            bool touchingIce = hasHazard && status == ExtinctionStatus.Freezing;
            freezing.SetIceContact(touchingIce);
            handledByPlayerEffect |= touchingIce;
        }
if (actor.TryGetComponent<MudCoatingEffect>(out var mud) &&
    mud.isActiveAndEnabled)
{
    bool insideMud = hasHazard && status == ExtinctionStatus.MudCoating;
    mud.SetCovered(insideMud);
    handledByPlayerEffect |= insideMud;
}
        if (handledByPlayerEffect)
            return;
    }
if (actor.GetComponent<CreatureController>() != null)
{
    ExtinctionStatusEffects npcEffects =
        actor.GetComponent<ExtinctionStatusEffects>();

    if (npcEffects == null && hasHazard)
        npcEffects = actor.AddComponent<ExtinctionStatusEffects>();

    if (npcEffects != null)
    {
        npcEffects.SetExposure(
            hasHazard ? status : ExtinctionStatus.None);
    }

    return;
}
    if (!hasHazard)
        return;

    ExtinctionStatusEffects effects = actor.GetComponent<ExtinctionStatusEffects>();
    if (effects == null)
        effects = actor.AddComponent<ExtinctionStatusEffects>();

    effects.Apply(status, 2.5f);
}

        public bool TryGetHazard(Vector2 position, out ExtinctionStatus status)
        {
            status = ExtinctionStatus.None;
            if (ocean == null)
            {
                return false;
            }

            OceanColumnProfile column = ocean.GetColumnProfile(position.x);
            if (position.y > column.SurfaceY || position.y < ocean.WorldMinY)
            {
                return false;
            }

            if (phase == ExtinctionPhase.Turbidity &&
                position.y <= column.SurfaceY && position.y >= TurbidityFront(column))
            {
                status = ExtinctionStatus.MudCoating;
            }
            else if (phase == ExtinctionPhase.Anoxia &&
         phaseTime >= 5f &&
         position.y >= column.SeabedY &&
         position.y <= AnoxiaFront(column))
{
    status = ExtinctionStatus.Anoxia;
}
            else if (phase == ExtinctionPhase.Cold && phaseTime >= 5f)
{
    OceanDepthZone zone = column.GetZone(position.y);
    if (zone == OceanDepthZone.AA ||
        zone == OceanDepthZone.BC ||
        zone == OceanDepthZone.CC)
    {
        status = ExtinctionStatus.Freezing;
    }
}

            return status != ExtinctionStatus.None;
        }

        private Vector2 GetEscapeDirection()
        {
            if (phase == ExtinctionPhase.Turbidity)
            {
                return Vector2.down;
            }

            if (phase == ExtinctionPhase.Anoxia)
            {
                return Vector2.up;
            }
            return Vector2.zero;
        }

        private void RollBoundaryOffsets()
{
    turbidityOffset = Random.Range(-boundaryVariation, boundaryVariation);
    anoxiaOffset = Random.Range(-boundaryVariation, boundaryVariation);
}

private float TurbidityTarget(OceanColumnProfile column)
{
    return Mathf.Clamp(
        column.BBBottomY + turbidityOffset,
        column.SeabedY,
        column.SurfaceY);
}

private float AnoxiaTarget(OceanColumnProfile column)
{
    return Mathf.Clamp(
        column.ABBottomY + anoxiaOffset,
        column.BCBottomY,
        column.SurfaceY);
}

private float TurbidityFront(OceanColumnProfile column)
{
    return Mathf.Lerp(
        column.SurfaceY,
        TurbidityTarget(column),
        Mathf.Clamp01((phaseTime - 3f) / 7f));
}

private float AnoxiaFront(OceanColumnProfile column)
{
    return Mathf.Lerp(
        column.BCBottomY,
        AnoxiaTarget(column),
        Mathf.Clamp01((phaseTime - 5f) / 10f));
}

        private void OnGUI()
{
    Color previousColor = GUI.color;
    Matrix4x4 previousMatrix = GUI.matrix;
    int previousDepth = GUI.depth;

    GUI.color = Color.white;

    // 탁류, 가스, 혹한기 화면 연출을 원래 크기로 그린다.
    if (ocean != null && viewCamera != null &&
        Event.current.type == EventType.Repaint)
    {
        DrawAtmosphere();
    }

    // 테스트 패널만 작게 그린다.
    GUI.depth = -100;

    const float panelScale = 0.65f;
    GUI.matrix = previousMatrix * Matrix4x4.Scale(
        new Vector3(panelScale, panelScale, 1f));

    DrawHud();

    GUI.color = previousColor;
    GUI.matrix = previousMatrix;
    GUI.depth = previousDepth;
}

        private void DrawAtmosphere()
        {
         float restFade = phase == ExtinctionPhase.Rest
    ? Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(phaseTime / 4f))
    : 0f;
    float anoxiaFade = phase == ExtinctionPhase.Cold
    ? Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(phaseTime / 4f))
    : 0f;

if (phase == ExtinctionPhase.Warning ||
    phase == ExtinctionPhase.Turbidity ||
    restFade > 0f)
{
    float opacity = phase == ExtinctionPhase.Warning
        ? Mathf.SmoothStep(0f, 0.45f, Mathf.Clamp01(phaseTime / 4f))
        : 0.45f;

    if (phase == ExtinctionPhase.Rest)
        opacity *= restFade;

    Fill(new Rect(0f, 0f, Screen.width, Screen.height),
        new Color(0.24f, 0.17f, 0.11f, opacity));
}
if (phase == ExtinctionPhase.Cold)
{
    float coldOpacity = Mathf.SmoothStep(
        0f, 0.5f, Mathf.Clamp01(phaseTime / 5f));

    Fill(new Rect(0f, 0f, Screen.width, Screen.height),
        new Color(0.20f, 0.42f, 0.67f, coldOpacity));
}

            const int strips = 32;
            float width = (float)Screen.width / strips;
            for (int index = 0; index < strips; index++)
            {
                float pixelX = (index + 0.5f) * width;
                float worldX = viewCamera.ScreenToWorldPoint(new Vector3(pixelX, 0f, 10f)).x;
                OceanColumnProfile column = ocean.GetColumnProfile(worldX);

if (phase == ExtinctionPhase.Turbidity || restFade > 0f)
{
    float front = phase == ExtinctionPhase.Rest
    ? TurbidityTarget(column)
    : TurbidityFront(column);   
        
        float left = Mathf.Round(index * width);
    float right = Mathf.Round((index + 1) * width);
    float stripWidth = right - left;

    // 흐린 가장자리의 깊이. 수면을 넘어 그리지 않도록 제한한다.
    float cloudTop = Mathf.Min(column.SurfaceY, front + 0.75f);
float opacity = 0.52f * (phase == ExtinctionPhase.Rest ? restFade : 1f);

    DrawBand(left, stripWidth, column.SurfaceY, cloudTop,
    new Color(0.29f, 0.20f, 0.12f, opacity), turbidityTile);

    const int edgeSteps = 8;
    for (int step = 0; step < edgeSteps; step++)
    {
        float top = Mathf.Lerp(cloudTop, front, (float)step / edgeSteps);
        float bottom = Mathf.Lerp(cloudTop, front, (step + 1f) / edgeSteps);
        float progress = (step + 0.5f) / edgeSteps;

        Color tint = Color.Lerp(
            new Color(0.29f, 0.20f, 0.12f),
            new Color(0.65f, 0.46f, 0.26f),
            progress);
        tint.a = opacity * Mathf.Lerp(1f, 0.25f, progress);

DrawBand(left, stripWidth, top, bottom, tint, turbidityTile);    }
}
                else if (phase == ExtinctionPhase.Anoxia || anoxiaFade > 0f)
{
    bool fadingOut = phase == ExtinctionPhase.Cold;
    bool hazardActive = phaseTime >= 5f;

    float gasTop = fadingOut
? AnoxiaTarget(column)
        : hazardActive
            ? AnoxiaFront(column)
            : Mathf.Lerp(column.SeabedY, column.BCBottomY, phaseTime / 5f);

    gasTop = Mathf.Max(gasTop, column.SeabedY);

    float left = Mathf.Round(index * width);
    float right = Mathf.Round((index + 1) * width);
    float stripWidth = right - left;

    const float softEdgeDepth = 0.9f;
    float solidTop = Mathf.Max(column.SeabedY, gasTop - softEdgeDepth);
    float opacity = fadingOut
    ? 0.58f * anoxiaFade
    : hazardActive
        ? 0.58f
        : Mathf.SmoothStep(0f, 0.58f, phaseTime / 5f);

    // 해저 쪽은 짙은 암록색으로 채운다.
   DrawBand(left, stripWidth, solidTop, column.SeabedY,
    new Color(0.04f, 0.15f, 0.09f, opacity), anoxiaTile);

    // 위쪽 경계에서는 암록색이 물에 섞이듯 사라진다.
    const int edgeSteps = 12;
    for (int step = 0; step < edgeSteps; step++)
    {
        float top = Mathf.Lerp(gasTop, solidTop, (float)step / edgeSteps);
        float bottom = Mathf.Lerp(gasTop, solidTop, (step + 1f) / edgeSteps);
        float depth = (step + 0.5f) / edgeSteps;
        float edgeOpacity = Mathf.SmoothStep(0f, opacity, depth);

       DrawBand(left, stripWidth, top, bottom,
    new Color(0.04f, 0.15f, 0.09f, edgeOpacity), anoxiaTile);
    }
}
                if (phase == ExtinctionPhase.Cold)
{
    float progress = Mathf.SmoothStep(
        0f, 1f, Mathf.Clamp01(phaseTime / 5f));

    float left = Mathf.Round(index * width);
    float right = Mathf.Round((index + 1) * width);
    float stripWidth = right - left;

    // 수면 아래와 해저 쪽에 찬 물이 퍼진다.
   // AB와 B 수층을 빙결 위험 구역으로 표시한다.
DrawBand(left, stripWidth, column.SurfaceY, column.AABottomY,
    new Color(0.67f, 0.84f, 0.94f, 0.34f * progress));

DrawBand(left, stripWidth, column.BBBottomY, column.SeabedY,
    new Color(0.07f, 0.16f, 0.32f, 0.40f * progress));

    // 수면의 얼음이 옅고 얇게 생겨 아래로 두꺼워진다.
    float iceTop = column.SurfaceY - 0.4f;
    float iceBottom = iceTop - 1.6f * progress;

    DrawBand(left, stripWidth, iceTop, iceBottom,
        new Color(0.78f, 0.93f, 1f, 0.75f * progress));
}
            }
        }

       private void DrawBand(
    float x, float width, float worldTop, float worldBottom,
    Color tint, Texture2D tile = null)
{
    float top = Screen.height -
        viewCamera.WorldToScreenPoint(new Vector3(0f, worldTop, 0f)).y;
    float bottom = Screen.height -
        viewCamera.WorldToScreenPoint(new Vector3(0f, worldBottom, 0f)).y;

    top = Mathf.Clamp(top, 0f, Screen.height);
    bottom = Mathf.Clamp(bottom, 0f, Screen.height);

    if (bottom <= top || width <= 0f)
        return;

    Rect area = new Rect(x, top, width, bottom - top);

    if (!useTileVisuals || tile == null)
    {
        Fill(area, tint);
        return;
    }

    // 타일 한 장이 게임 월드에서 차지하는 가로 길이.
    const float tileWidth = 4f;
    float tileHeight = tileWidth * tile.height / tile.width;

    float depth = Vector3.Dot(
        -viewCamera.transform.position, viewCamera.transform.forward);
    Vector3 lowerLeft = viewCamera.ScreenToWorldPoint(
        new Vector3(x, Screen.height - bottom, depth));
    Vector3 upperRight = viewCamera.ScreenToWorldPoint(
        new Vector3(x + width, Screen.height - top, depth));

    Rect uv = new Rect(
        lowerLeft.x / tileWidth,
        lowerLeft.y / tileHeight,
        (upperRight.x - lowerLeft.x) / tileWidth,
        (upperRight.y - lowerLeft.y) / tileHeight);

    Color previous = GUI.color;
    GUI.color = new Color(1f, 1f, 1f, tint.a);
    GUI.DrawTextureWithTexCoords(area, tile, uv, true);
    GUI.color = previous;
}

        private void DrawHud()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label);
                hudStyle.normal.textColor = Color.white;
                hudStyle.fontSize = 15;
            }

            Fill(new Rect(12f, 12f, 336f, 112f), new Color(0.01f, 0.04f, 0.08f, 0.78f));
            float remaining = phase == ExtinctionPhase.Complete
                ? 0f
                : Mathf.Max(0f, Durations[(int)phase] - phaseTime);
            string safe = phase == ExtinctionPhase.Turbidity ? "BC / C" :
                          phase == ExtinctionPhase.Anoxia ? "A / AB" :
phase == ExtinctionPhase.Cold && phaseTime >= 5f ? "AB / B" : "ALL";            GUI.Label(new Rect(22f, 17f, 316f, 25f),
                "EXTINCTION TEST: " + PhaseNames[(int)phase] + "  " + remaining.ToString("0.0") + "s", hudStyle);
            GUI.Label(new Rect(22f, 44f, 316f, 24f), "SAFE ZONES: " + safe, hudStyle);
            ExtinctionStatusEffects effects = player != null
                ? player.GetComponent<ExtinctionStatusEffects>()
                : null;
            GUI.Label(new Rect(22f, 70f, 316f, 24f),
                "STATUS: " + (effects != null ? effects.ActiveStatus.ToString() : "None"), hudStyle);

            if (GUI.Button(new Rect(22f, 96f, 112f, 23f), "Next phase"))
            {
                SkipPhase();
            }
            if (GUI.Button(new Rect(142f, 96f, 80f, 23f), "Restart"))
            {
                Restart();
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
