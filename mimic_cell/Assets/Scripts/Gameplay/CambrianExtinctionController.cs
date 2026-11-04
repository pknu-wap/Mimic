using MimicCell.Creatures;
using MimicCell.Player;
using MimicCell.World;
using UnityEngine;

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
        private Camera viewCamera;
        private ExtinctionPhase phase = ExtinctionPhase.Warning;
        private float phaseTime;
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
        }

        private void RefreshCreatures()
        {
            CreatureController[] creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
            bool frenzy = phase == ExtinctionPhase.Warning ||
                          phase == ExtinctionPhase.Turbidity ||
                          phase == ExtinctionPhase.Anoxia;
            bool cold = phase == ExtinctionPhase.Cold && phaseTime >= 5f;
            CreatureSpawner[] spawners = FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None);
            foreach (CreatureSpawner spawner in spawners)
            {
                spawner.SetExtinctionSpawnRateMultiplier(cold ? 0.35f : 1f);
            }

            foreach (CreatureController creature in creatures)
            {
                if (creature == null || creature.CurrentState == CreatureState.Dead)
                {
                    continue;
                }

                creature.SetExtinctionBehavior(
                    frenzy,
                    GetEscapeDirection(),
                    cold);
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
            if (!TryGetHazard(actor.transform.position, out ExtinctionStatus status))
            {
                return;
            }

            ExtinctionStatusEffects effects = actor.GetComponent<ExtinctionStatusEffects>();
            if (effects == null)
            {
                effects = actor.AddComponent<ExtinctionStatusEffects>();
            }

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

            if (phase == ExtinctionPhase.Turbidity && phaseTime >= 3f &&
                position.y <= column.SurfaceY && position.y >= TurbidityFront(column))
            {
                status = ExtinctionStatus.MudCoating;
            }
            else if (phase == ExtinctionPhase.Anoxia && phaseTime >= 5f &&
                     position.y <= AnoxiaFront(column))
            {
                status = ExtinctionStatus.Anoxia;
            }
            else if (phase == ExtinctionPhase.Cold && phaseTime >= 5f)
            {
                OceanDepthZone zone = column.GetZone(position.y);
                if (zone == OceanDepthZone.AA || zone == OceanDepthZone.CC)
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

        private float TurbidityFront(OceanColumnProfile column)
        {
            // The first three seconds are a visual warning. The moving front reaches B's floor at 10 s.
            return Mathf.Lerp(column.SurfaceY, column.BBBottomY,
                Mathf.Clamp01((phaseTime - 3f) / 7f));
        }

        private float AnoxiaFront(OceanColumnProfile column)
        {
            // Gas first fills C, then reaches the top of B at 15 s.
            return Mathf.Lerp(column.BCBottomY, column.ABBottomY,
                Mathf.Clamp01((phaseTime - 5f) / 10f));
        }

        private void OnGUI()
        {
            if (ocean == null || viewCamera == null)
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                DrawAtmosphere();
            }

            DrawHud();
        }

        private void DrawAtmosphere()
        {
            if (phase == ExtinctionPhase.Warning)
            {
                Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                    new Color(0.29f, 0.24f, 0.19f, 0.24f));
            }

            const int strips = 32;
            float width = (float)Screen.width / strips;
            for (int index = 0; index < strips; index++)
            {
                float pixelX = (index + 0.5f) * width;
                float worldX = viewCamera.ScreenToWorldPoint(new Vector3(pixelX, 0f, 10f)).x;
                OceanColumnProfile column = ocean.GetColumnProfile(worldX);

                if (phase == ExtinctionPhase.Turbidity)
                {
                    float front = phaseTime < 3f
                        ? Mathf.Lerp(column.SurfaceY, column.AABottomY, phaseTime / 3f) - 0.5f
                        : TurbidityFront(column);
                    DrawBand(index * width, width + 1f, column.SurfaceY, front,
                        new Color(0.25f, 0.20f, 0.14f, 0.48f));
                }
                else if (phase == ExtinctionPhase.Anoxia)
                {
                    float front = phaseTime < 5f
                        ? Mathf.Lerp(column.SeabedY, column.BCBottomY, phaseTime / 5f)
                        : AnoxiaFront(column);
                    DrawBand(index * width, width + 1f, front, ocean.WorldMinY,
                        new Color(0.08f, 0.19f, 0.08f, 0.43f));
                }
                else if (phase == ExtinctionPhase.Cold)
                {
                    float opacity = Mathf.Clamp01(phaseTime / 5f) * 0.31f;
                    DrawBand(index * width, width + 1f, column.SurfaceY,
                        column.AABottomY, new Color(0.87f, 0.94f, 0.95f, opacity));
                    DrawBand(index * width, width + 1f, column.BCBottomY,
                        ocean.WorldMinY, new Color(0.01f, 0.05f, 0.22f, opacity));
                    DrawBand(index * width, width + 1f, column.SurfaceY,
                        column.SurfaceY - Mathf.Lerp(0f, 2f, Mathf.Clamp01(phaseTime / 5f)),
                        new Color(0.78f, 0.93f, 1f, 0.68f));
                }
            }
        }

        private void DrawBand(float x, float width, float worldTop, float worldBottom, Color tint)
        {
            float top = Screen.height - viewCamera.WorldToScreenPoint(new Vector3(0f, worldTop, 0f)).y;
            float bottom = Screen.height - viewCamera.WorldToScreenPoint(new Vector3(0f, worldBottom, 0f)).y;
            top = Mathf.Clamp(top, 0f, Screen.height);
            bottom = Mathf.Clamp(bottom, 0f, Screen.height);
            if (bottom > top)
            {
                Fill(new Rect(x, top, width, bottom - top), tint);
            }
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
                          phase == ExtinctionPhase.Cold && phaseTime >= 5f ? "AB / B / BC" : "ALL";
            GUI.Label(new Rect(22f, 17f, 316f, 25f),
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
