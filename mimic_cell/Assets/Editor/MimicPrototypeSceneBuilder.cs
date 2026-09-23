using MimicCell.CameraSystem;
using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Player;
using MimicCell.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MimicCell.EditorTools
{
    public static class MimicPrototypeSceneBuilder
    {
        private const string RuntimeRootName = "[Mimic Runtime]";
        private const string CambrianScenePath = "Assets/Scenes/CambrianOcean.unity";
        private const string ModernScenePath = "Assets/Scenes/Modern.unity";
        private const string PrototypePrefabFolder = "Assets/Prefabs/Prototype";
        private const string PlayerPrefabPath = PrototypePrefabFolder + "/PlayerPrototype.prefab";
        private const string AimMaterialPath = PrototypePrefabFolder + "/PrototypeAimLine.mat";
        private const float PixelsPerUnit = 16f;

        private sealed class CreatureSpec
        {
            public string Name;
            public Vector2 Position;
            public Vector2Int PixelSize;
            public Color FillColor;
            public CreatureDisposition Disposition;
            public int MaxHealth;
            public float MoveSpeed;
            public float PerceptionRadius;
            public float WanderRadius;
            public int ContactDamage;

            public string PrefabPath
            {
                get { return PrototypePrefabFolder + "/" + Name + "Prototype.prefab"; }
            }
        }

        private static readonly CreatureSpec[] CambrianCreatures =
        {
            new CreatureSpec
            {
                Name = "Pikaia",
                Position = new Vector2(-4.5f, 2f),
                PixelSize = new Vector2Int(24, 8),
                FillColor = new Color(0.77f, 0.86f, 0.64f, 0.86f),
                Disposition = CreatureDisposition.Timid,
                MaxHealth = 8,
                MoveSpeed = 1.8f,
                PerceptionRadius = 3.5f,
                WanderRadius = 4f,
                ContactDamage = 0
            },
            new CreatureSpec
            {
                Name = "Myllokunmingia",
                Position = new Vector2(3.8f, 1.2f),
                PixelSize = new Vector2Int(20, 10),
                FillColor = new Color(0.58f, 0.78f, 0.94f, 0.86f),
                Disposition = CreatureDisposition.Timid,
                MaxHealth = 10,
                MoveSpeed = 1.7f,
                PerceptionRadius = 3.2f,
                WanderRadius = 4.5f,
                ContactDamage = 0
            },
            new CreatureSpec
            {
                Name = "Isoxys",
                Position = new Vector2(-2.6f, -1.6f),
                PixelSize = new Vector2Int(24, 16),
                FillColor = new Color(0.91f, 0.62f, 0.48f, 0.86f),
                Disposition = CreatureDisposition.Passive,
                MaxHealth = 14,
                MoveSpeed = 1.25f,
                PerceptionRadius = 3f,
                WanderRadius = 3.5f,
                ContactDamage = 0
            },
            new CreatureSpec
            {
                Name = "Marrella",
                Position = new Vector2(2.4f, -3.1f),
                PixelSize = new Vector2Int(16, 16),
                FillColor = new Color(0.83f, 0.74f, 0.94f, 0.86f),
                Disposition = CreatureDisposition.Timid,
                MaxHealth = 8,
                MoveSpeed = 1.4f,
                PerceptionRadius = 2.8f,
                WanderRadius = 3f,
                ContactDamage = 0
            },
            new CreatureSpec
            {
                Name = "Anomalocaris",
                Position = new Vector2(6.2f, -0.5f),
                PixelSize = new Vector2Int(64, 32),
                FillColor = new Color(0.92f, 0.37f, 0.28f, 0.84f),
                Disposition = CreatureDisposition.Aggressive,
                MaxHealth = 40,
                MoveSpeed = 2.2f,
                PerceptionRadius = 5.8f,
                WanderRadius = 7f,
                ContactDamage = 4
            }
        };

        [MenuItem("Mimic/Build Prototype Scene Objects", priority = 10)]
        public static void BuildPrototypeSceneObjects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before building Mimic prototype scene objects.");
                return;
            }

            EnsureAssetFolders();
            Material aimMaterial = EnsureAimMaterial();
            GameObject playerPrefab = CreatePlayerPrefab();

            for (int i = 0; i < CambrianCreatures.Length; i++)
            {
                CreateCreaturePrefab(CambrianCreatures[i]);
            }

            BuildGameplayScene(CambrianScenePath, playerPrefab, aimMaterial, true);
            BuildGameplayScene(ModernScenePath, playerPrefab, aimMaterial, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mimic prototype prefabs and scene objects were built successfully.");
        }

        private static void EnsureAssetFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            if (!AssetDatabase.IsValidFolder(PrototypePrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Prototype");
            }
        }

        private static Material EnsureAimMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AimMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            material = new Material(shader)
            {
                name = "PrototypeAimLine"
            };
            AssetDatabase.CreateAsset(material, AimMaterialPath);
            return material;
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject player = new GameObject("Player_MimicCell");

            try
            {
                TrySetTag(player, "Player");

                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                CircleCollider2D playerCollider = player.AddComponent<CircleCollider2D>();
                playerCollider.radius = 0.42f;

                GameObject visual = CreateVisual(
                    player.transform,
                    new Vector2Int(16, 16),
                    new Color(0.26f, 0.95f, 1f, 0.72f),
                    new Color(0.9f, 1f, 1f, 0.95f),
                    10);

                player.AddComponent<WasdSwimMovement>();
                player.AddComponent<MouseHoldMovement>();
                PlayerMovementController movement = player.AddComponent<PlayerMovementController>();
                movement.Configure(null, visual.transform, null);
                movement.SetMovementMode(PlayerMovementMode.WasdSwim);

                player.AddComponent<ActorHealth>();
                PrototypePlayer prototypePlayer = player.AddComponent<PrototypePlayer>();
                prototypePlayer.Configure(20);

                return PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static void CreateCreaturePrefab(CreatureSpec spec)
        {
            GameObject creature = new GameObject(spec.Name + " Placeholder");

            try
            {
                Rigidbody2D body = creature.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                BoxCollider2D trigger = creature.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(
                    spec.PixelSize.x / PixelsPerUnit,
                    spec.PixelSize.y / PixelsPerUnit);

                GameObject visual = CreateVisual(
                    creature.transform,
                    spec.PixelSize,
                    spec.FillColor,
                    Color.white,
                    3);

                creature.AddComponent<ActorHealth>();
                CreatureController controller = creature.AddComponent<CreatureController>();
                controller.Configure(
                    spec.Name,
                    spec.Disposition,
                    null,
                    visual.transform,
                    spec.MaxHealth,
                    spec.MoveSpeed,
                    spec.PerceptionRadius,
                    spec.WanderRadius,
                    spec.ContactDamage);

                PrefabUtility.SaveAsPrefabAsset(creature, spec.PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(creature);
            }
        }

        private static void BuildGameplayScene(
            string scenePath,
            GameObject playerPrefab,
            Material aimMaterial,
            bool includeCambrianCreatures)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasAlreadyLoaded = scene.IsValid() && scene.isLoaded;

            if (!wasAlreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                RemoveGeneratedRoot(scene);

                GameObject runtimeRoot = new GameObject(RuntimeRootName);
                SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
                runtimeRoot.AddComponent<PrototypeGameSession>();

                if (includeCambrianCreatures)
                {
                    CreateCambrianBackdrop(runtimeRoot.transform);
                }
                else
                {
                    CreateModernBackdrop(runtimeRoot.transform);
                }

                GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
                player.transform.SetParent(runtimeRoot.transform, false);
                player.transform.position = Vector3.zero;

                Camera sceneCamera = FindSceneCamera(scene);
                ConfigureCamera(
                    sceneCamera,
                    player.transform,
                    includeCambrianCreatures
                        ? new Color(0.02f, 0.12f, 0.24f, 1f)
                        : new Color(0.53f, 0.74f, 0.82f, 1f),
                    includeCambrianCreatures ? 5.5f : 6f);

                LineRenderer aimLine = CreateAimLine(player.transform, aimMaterial);
                PlayerMovementController movement = player.GetComponent<PlayerMovementController>();
                movement.Configure(sceneCamera, player.transform.Find("Visual"), aimLine);

                if (includeCambrianCreatures)
                {
                    PlaceCambrianCreatures(scene, runtimeRoot.transform);
                }
                else
                {
                    CreateModernMarkers(runtimeRoot.transform);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                if (SceneManager.GetActiveScene() == scene)
                {
                    Selection.activeGameObject = player;
                }
            }
            finally
            {
                if (!wasAlreadyLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void RemoveGeneratedRoot(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == RuntimeRootName)
                {
                    Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static void PlaceCambrianCreatures(Scene scene, Transform root)
        {
            Transform group = new GameObject("Cambrian Creature Placeholders").transform;
            group.SetParent(root, false);

            for (int i = 0; i < CambrianCreatures.Length; i++)
            {
                CreatureSpec spec = CambrianCreatures[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                GameObject creature = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                creature.transform.SetParent(group, false);
                creature.transform.position = spec.Position;
            }
        }

        private static Camera FindSceneCamera(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera camera = roots[i].GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    return camera;
                }
            }

            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            TrySetTag(cameraObject, "MainCamera");
            cameraObject.AddComponent<AudioListener>();
            return cameraObject.AddComponent<Camera>();
        }

        private static void ConfigureCamera(Camera sceneCamera, Transform target, Color background, float size)
        {
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = size;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = background;
            sceneCamera.transform.position = new Vector3(0f, 0f, -10f);

            CameraFollow2D follow = sceneCamera.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                follow = sceneCamera.gameObject.AddComponent<CameraFollow2D>();
            }

            follow.SetTarget(target);
            follow.SetOffset(new Vector3(0f, 0f, -10f));
            follow.SetBounds(new Vector2(-22f, -10f), new Vector2(22f, 10f));
            EditorUtility.SetDirty(sceneCamera);
            EditorUtility.SetDirty(follow);
        }

        private static LineRenderer CreateAimLine(Transform player, Material material)
        {
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
            line.sharedMaterial = material;
            line.enabled = false;
            return line;
        }

        private static GameObject CreateVisual(
            Transform parent,
            Vector2Int pixelSize,
            Color fillColor,
            Color outlineColor,
            int sortingOrder)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            PlaceholderSprite placeholder = visual.AddComponent<PlaceholderSprite>();
            placeholder.Apply(pixelSize, fillColor, outlineColor, PixelsPerUnit);
            return visual;
        }

        private static void CreateCambrianBackdrop(Transform root)
        {
            Transform zones = new GameObject("Cambrian Depth Zones").transform;
            zones.SetParent(root, false);

            CreateBlock(zones, "A Surface Zone", new Vector2(0f, 6f), new Vector2(44f, 4f), new Color(0.13f, 0.58f, 0.75f, 0.42f), -100);
            CreateBlock(zones, "AB Buffer Zone", new Vector2(0f, 3.5f), new Vector2(44f, 1f), new Color(0.10f, 0.48f, 0.66f, 0.38f), -99);
            CreateBlock(zones, "B Middle Zone", Vector2.zero, new Vector2(44f, 6f), new Color(0.06f, 0.32f, 0.52f, 0.46f), -100);
            CreateBlock(zones, "BC Buffer Zone", new Vector2(0f, -3.5f), new Vector2(44f, 1f), new Color(0.04f, 0.24f, 0.42f, 0.42f), -99);
            CreateBlock(zones, "C Deep Zone", new Vector2(0f, -6f), new Vector2(44f, 4f), new Color(0.02f, 0.11f, 0.24f, 0.58f), -100);
        }

        private static void CreateModernBackdrop(Transform root)
        {
            Transform backdrop = new GameObject("Modern Prototype Backdrop").transform;
            backdrop.SetParent(root, false);
            CreateBlock(backdrop, "Sky Band", new Vector2(0f, 3.5f), new Vector2(44f, 9f), new Color(0.38f, 0.62f, 0.76f, 0.34f), -100);
            CreateBlock(backdrop, "Ground Band", new Vector2(0f, -4.8f), new Vector2(44f, 1.6f), new Color(0.18f, 0.30f, 0.20f, 0.72f), -98);
        }

        private static void CreateModernMarkers(Transform root)
        {
            Transform markers = new GameObject("Modern Movement Placeholders").transform;
            markers.SetParent(root, false);
            CreateBlock(markers, "Future Air Movement Area", new Vector2(-5f, 3f), new Vector2(5f, 2f), new Color(0.76f, 0.89f, 1f, 0.22f), -80);
            CreateBlock(markers, "Future Land Movement Area", new Vector2(4f, -3.2f), new Vector2(6f, 1.2f), new Color(0.42f, 0.66f, 0.42f, 0.32f), -80);
        }

        private static void CreateBlock(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 scale,
            Color fillColor,
            int sortingOrder)
        {
            GameObject block = new GameObject(objectName);
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            PlaceholderSprite placeholder = block.AddComponent<PlaceholderSprite>();
            placeholder.Apply(
                new Vector2Int(16, 16),
                fillColor,
                new Color(1f, 1f, 1f, 0f),
                PixelsPerUnit);
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
