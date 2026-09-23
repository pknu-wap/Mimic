using MimicCell.CameraSystem;
using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Player;
using MimicCell.Prototype;
using MimicCell.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MimicCell.Core
{
    public static class MimicGameBootstrap
    {
        private const string RuntimeRootName = "[Mimic Runtime]";
        private const float PixelsPerUnit = 16f;

        private static bool registeredSceneCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            if (registeredSceneCallback)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            registeredSceneCallback = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCurrentScene()
        {
            BuildScene(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BuildScene(scene.name);
        }

        private static void BuildScene(string sceneName)
        {
            if (sceneName == MimicSceneNames.StartMenu)
            {
                EnsureStartMenuScene();
                return;
            }

            if (MimicSceneNames.IsGameplayScene(sceneName))
            {
                EnsureGameplayScene(sceneName);
            }
        }

        private static void EnsureStartMenuScene()
        {
            GameObject root = EnsureRuntimeRoot();
            Camera sceneCamera = EnsureCamera(null, new Color(0.04f, 0.09f, 0.14f, 1f), 5.5f);
            sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
            EnsureGlobalLight();

            if (root.GetComponent<StartMenuController>() == null)
            {
                root.AddComponent<StartMenuController>();
            }
        }

        private static void EnsureGameplayScene(string sceneName)
        {
            GameObject root = EnsureRuntimeRoot();
            EnsureGlobalLight();

            if (sceneName == MimicSceneNames.Modern)
            {
                EnsureModernBackdrop(root.transform);
            }
            else
            {
                EnsureCambrianBackdrop(root.transform);
            }

            PrototypePlayer player = EnsurePlayer(root.transform);
            PlayerMovementController playerMovement = player.GetComponent<PlayerMovementController>();
            Camera sceneCamera = EnsureCamera(player.transform, GetGameplayBackground(sceneName), sceneName == MimicSceneNames.Modern ? 6f : 5.5f);
            CameraFollow2D follow = sceneCamera.GetComponent<CameraFollow2D>();
            follow.SetTarget(player.transform);
            follow.SetOffset(new Vector3(0f, 0f, -10f));
            follow.SetBounds(new Vector2(-22f, -10f), new Vector2(22f, 10f));

            Transform visual = player.transform.Find("Visual");
            LineRenderer aimLine = EnsureAimLine(player.transform);
            playerMovement.Configure(sceneCamera, visual, aimLine);
            EnsureGameSession(root, player);

            if (sceneName == MimicSceneNames.Modern)
            {
                EnsureModernMarkers(root.transform);
            }
            else
            {
                EnsureCambrianCreatureMarkers(root.transform, player);
            }
        }

        private static GameObject EnsureRuntimeRoot()
        {
            GameObject root = GameObject.Find(RuntimeRootName);
            if (root != null)
            {
                return root;
            }

            return new GameObject(RuntimeRootName);
        }

        private static PrototypePlayer EnsurePlayer(Transform root)
        {
            PrototypePlayer existingPlayer = Object.FindFirstObjectByType<PrototypePlayer>();
            if (existingPlayer != null)
            {
                existingPlayer.Configure(20);
                return existingPlayer;
            }

            PlayerMovementController existingMovement = Object.FindFirstObjectByType<PlayerMovementController>();
            if (existingMovement != null)
            {
                GameObject existingObject = existingMovement.gameObject;
                if (existingObject.GetComponent<ActorHealth>() == null)
                {
                    existingObject.AddComponent<ActorHealth>();
                }

                PrototypePlayer upgradedPlayer = existingObject.GetComponent<PrototypePlayer>();
                if (upgradedPlayer == null)
                {
                    upgradedPlayer = existingObject.AddComponent<PrototypePlayer>();
                }

                upgradedPlayer.Configure(20);
                return upgradedPlayer;
            }

            GameObject playerObject = new GameObject("Player_MimicCell");
            playerObject.transform.SetParent(root, false);
            playerObject.transform.position = Vector3.zero;
            TrySetTag(playerObject, "Player");

            Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.freezeRotation = true;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D playerCollider = playerObject.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.42f;

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(playerObject.transform, false);
            SpriteRenderer visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualRenderer.sortingOrder = 10;
            PlaceholderSprite visualPlaceholder = visualObject.AddComponent<PlaceholderSprite>();
            visualPlaceholder.Apply(
                new Vector2Int(16, 16),
                new Color(0.26f, 0.95f, 1f, 0.72f),
                new Color(0.9f, 1f, 1f, 0.95f),
                PixelsPerUnit);

            playerObject.AddComponent<WasdSwimMovement>();
            playerObject.AddComponent<MouseHoldMovement>();
            playerObject.AddComponent<PlayerMovementController>();
            playerObject.AddComponent<ActorHealth>();

            PrototypePlayer player = playerObject.AddComponent<PrototypePlayer>();
            player.Configure(20);
            return player;
        }

        private static void EnsureGameSession(GameObject root, PrototypePlayer player)
        {
            PrototypeGameSession session = root.GetComponent<PrototypeGameSession>();
            if (session == null)
            {
                session = root.AddComponent<PrototypeGameSession>();
            }

            session.Configure(player, Vector3.zero);
        }

        private static Camera EnsureCamera(Transform target, Color backgroundColor, float orthographicSize)
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                TrySetTag(cameraObject, "MainCamera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = orthographicSize;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = backgroundColor;

            if (sceneCamera.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                sceneCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            CameraFollow2D follow = sceneCamera.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                follow = sceneCamera.gameObject.AddComponent<CameraFollow2D>();
            }

            if (target != null)
            {
                follow.SetTarget(target);
            }

            return sceneCamera;
        }

        private static void EnsureGlobalLight()
        {
            Light2D existingLight = Object.FindFirstObjectByType<Light2D>();
            if (existingLight != null)
            {
                existingLight.lightType = Light2D.LightType.Global;
                existingLight.intensity = 1f;
                existingLight.color = Color.white;
                return;
            }

            GameObject lightObject = new GameObject("Global Light 2D");
            Light2D globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;
            globalLight.color = Color.white;
        }

        private static LineRenderer EnsureAimLine(Transform player)
        {
            Transform existingLine = player.Find("Aim Line");
            if (existingLine != null)
            {
                return existingLine.GetComponent<LineRenderer>();
            }

            GameObject lineObject = new GameObject("Aim Line");
            lineObject.transform.SetParent(player, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.035f;
            line.endWidth = 0.01f;
            line.startColor = new Color(0.72f, 1f, 1f, 0.75f);
            line.endColor = new Color(0.72f, 1f, 1f, 0.05f);
            line.sortingOrder = 30;
            line.material = new Material(Shader.Find("Sprites/Default"));
            return line;
        }

        private static void EnsureCambrianBackdrop(Transform root)
        {
            if (root.Find("Cambrian Depth Zones") != null)
            {
                return;
            }

            Transform zones = new GameObject("Cambrian Depth Zones").transform;
            zones.SetParent(root, false);

            CreateRuntimeBlock(zones, "A Surface Zone", new Vector2(0f, 6f), new Vector2(44f, 4f), new Color(0.13f, 0.58f, 0.75f, 0.42f), -100);
            CreateRuntimeBlock(zones, "AB Buffer Zone", new Vector2(0f, 3.5f), new Vector2(44f, 1f), new Color(0.10f, 0.48f, 0.66f, 0.38f), -99);
            CreateRuntimeBlock(zones, "B Middle Zone", new Vector2(0f, 0f), new Vector2(44f, 6f), new Color(0.06f, 0.32f, 0.52f, 0.46f), -100);
            CreateRuntimeBlock(zones, "BC Buffer Zone", new Vector2(0f, -3.5f), new Vector2(44f, 1f), new Color(0.04f, 0.24f, 0.42f, 0.42f), -99);
            CreateRuntimeBlock(zones, "C Deep Zone", new Vector2(0f, -6f), new Vector2(44f, 4f), new Color(0.02f, 0.11f, 0.24f, 0.58f), -100);
        }

        private static void EnsureModernBackdrop(Transform root)
        {
            if (root.Find("Modern Prototype Backdrop") != null)
            {
                return;
            }

            Transform backdrop = new GameObject("Modern Prototype Backdrop").transform;
            backdrop.SetParent(root, false);
            CreateRuntimeBlock(backdrop, "Sky Band", new Vector2(0f, 3.5f), new Vector2(44f, 9f), new Color(0.38f, 0.62f, 0.76f, 0.34f), -100);
            CreateRuntimeBlock(backdrop, "Ground Band", new Vector2(0f, -4.8f), new Vector2(44f, 1.6f), new Color(0.18f, 0.30f, 0.20f, 0.72f), -98);
        }

        private static void EnsureCambrianCreatureMarkers(Transform root, PrototypePlayer player)
        {
            if (root.Find("Cambrian Creature Placeholders") != null)
            {
                return;
            }

            Transform creatures = new GameObject("Cambrian Creature Placeholders").transform;
            creatures.SetParent(root, false);

            CreateCreature(
                creatures,
                player,
                "Pikaia",
                new Vector2(-4.5f, 2f),
                new Vector2Int(24, 8),
                new Color(0.77f, 0.86f, 0.64f, 0.86f),
                CreatureDisposition.Timid,
                8,
                1.8f,
                3.5f,
                4f,
                0);

            CreateCreature(
                creatures,
                player,
                "Myllokunmingia",
                new Vector2(3.8f, 1.2f),
                new Vector2Int(20, 10),
                new Color(0.58f, 0.78f, 0.94f, 0.86f),
                CreatureDisposition.Timid,
                10,
                1.7f,
                3.2f,
                4.5f,
                0);

            CreateCreature(
                creatures,
                player,
                "Isoxys",
                new Vector2(-2.6f, -1.6f),
                new Vector2Int(24, 16),
                new Color(0.91f, 0.62f, 0.48f, 0.86f),
                CreatureDisposition.Passive,
                14,
                1.25f,
                3f,
                3.5f,
                0);

            CreateCreature(
                creatures,
                player,
                "Marrella",
                new Vector2(2.4f, -3.1f),
                new Vector2Int(16, 16),
                new Color(0.83f, 0.74f, 0.94f, 0.86f),
                CreatureDisposition.Timid,
                8,
                1.4f,
                2.8f,
                3f,
                0);

            CreateCreature(
                creatures,
                player,
                "Anomalocaris",
                new Vector2(6.2f, -0.5f),
                new Vector2Int(64, 32),
                new Color(0.92f, 0.37f, 0.28f, 0.84f),
                CreatureDisposition.Aggressive,
                40,
                2.2f,
                5.8f,
                7f,
                4);
        }

        private static void EnsureModernMarkers(Transform root)
        {
            if (root.Find("Modern Movement Placeholders") != null)
            {
                return;
            }

            Transform markers = new GameObject("Modern Movement Placeholders").transform;
            markers.SetParent(root, false);
            CreateRuntimeBlock(markers, "Future Air Movement Area", new Vector2(-5f, 3f), new Vector2(5f, 2f), new Color(0.76f, 0.89f, 1f, 0.22f), -80);
            CreateRuntimeBlock(markers, "Future Land Movement Area", new Vector2(4f, -3.2f), new Vector2(6f, 1.2f), new Color(0.42f, 0.66f, 0.42f, 0.32f), -80);
        }

        private static void CreateCreature(
            Transform parent,
            PrototypePlayer player,
            string speciesName,
            Vector2 position,
            Vector2Int pixelSize,
            Color fillColor,
            CreatureDisposition disposition,
            int maxHealth,
            float moveSpeed,
            float perceptionRadius,
            float wanderRadius,
            int contactDamage)
        {
            GameObject creature = new GameObject(speciesName + " Placeholder");
            creature.transform.SetParent(parent, false);
            creature.transform.position = position;

            Rigidbody2D creatureBody = creature.AddComponent<Rigidbody2D>();
            creatureBody.gravityScale = 0f;
            creatureBody.freezeRotation = true;
            creatureBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D trigger = creature.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(pixelSize.x / PixelsPerUnit, pixelSize.y / PixelsPerUnit);

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(creature.transform, false);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3;

            PlaceholderSprite placeholder = visualObject.AddComponent<PlaceholderSprite>();
            placeholder.Apply(pixelSize, fillColor, Color.white, PixelsPerUnit);

            creature.AddComponent<ActorHealth>();
            CreatureController controller = creature.AddComponent<CreatureController>();
            controller.Configure(
                speciesName,
                disposition,
                player,
                visualObject.transform,
                maxHealth,
                moveSpeed,
                perceptionRadius,
                wanderRadius,
                contactDamage);
        }

        private static void CreateRuntimeBlock(Transform parent, string objectName, Vector2 position, Vector2 scale, Color fillColor, int sortingOrder)
        {
            GameObject block = new GameObject(objectName);
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            PlaceholderSprite placeholder = block.AddComponent<PlaceholderSprite>();
            placeholder.Apply(new Vector2Int(16, 16), fillColor, new Color(1f, 1f, 1f, 0f), PixelsPerUnit);
        }

        private static Color GetGameplayBackground(string sceneName)
        {
            if (sceneName == MimicSceneNames.Modern)
            {
                return new Color(0.53f, 0.74f, 0.82f, 1f);
            }

            return new Color(0.02f, 0.12f, 0.24f, 1f);
        }

        private static void TrySetTag(GameObject target, string tagName)
        {
            try
            {
                target.tag = tagName;
            }
            catch (UnityException)
            {
                Debug.LogWarning("Unity tag '" + tagName + "' is not available yet.");
            }
        }
    }
}
