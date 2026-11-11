using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Player;
using MimicCell.Prototype;
using MimicCell.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimicCell.EditorTools
{
    public static class MimicPrototypeSceneBuilder
    {
        private const string PrototypePrefabFolder = "Assets/Prefabs/Prototype";
        private const string PlayerPrefabPath = PrototypePrefabFolder + "/PlayerPrototype.prefab";
        private const string PlayerSpritePath = "Assets/Sprites/Player/플레이어 (초기 버전).psd";
        private const string AimMaterialPath = PrototypePrefabFolder + "/PrototypeAimLine.mat";
        private const float PixelsPerUnit = 16f;
        private const float PlayerVisualScale = 0.5f;

        private sealed class CreatureSpec
        {
            public string Name;
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

        [MenuItem("Mimic/Build Prototype Assets", priority = 10)]
        public static void BuildPrototypeSceneObjects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before building Mimic prototype scene objects.");
                return;
            }

            EnsureAssetFolders();
            EnsureAimMaterial();
            CreatePlayerPrefab();

            for (int i = 0; i < CambrianCreatures.Length; i++)
            {
                CreateCreaturePrefab(CambrianCreatures[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mimic prototype prefabs and scene objects were built successfully.");
        }

        [MenuItem("Mimic/Rebuild Player Prototype", priority = 11)]
        public static void RebuildPlayerPrototype()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before rebuilding the Mimic player prototype.");
                return;
            }

            EnsureAssetFolders();
            GameObject playerPrefab = CreatePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = playerPrefab;
            Debug.Log("Mimic player sprite prefab was rebuilt successfully.");
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
                playerCollider.radius = 0.5f;

                GameObject visual = CreatePlayerSpriteVisual(player.transform);

                player.AddComponent<WasdSwimMovement>();
                player.AddComponent<MouseHoldMovement>();
                PlayerMovementController movement = player.AddComponent<PlayerMovementController>();
                movement.Configure(null, visual.transform, null);
                movement.SetMovementMode(PlayerMovementMode.WasdSwim);

                player.AddComponent<ActorHealth>().Configure(20);
                player.AddComponent<StatusEffectController>();
                player.AddComponent<PrototypePlayer>();
                player.AddComponent<PlayerAttack>();

                return PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayerSpriteVisual(Transform parent)
        {
            UnityEngine.Object[] spriteAssets = AssetDatabase.LoadAllAssetsAtPath(PlayerSpritePath);
            Sprite bodySprite = RequirePlayerLayerSprite(spriteAssets, 7);
            Sprite coreSprite = RequirePlayerLayerSprite(spriteAssets, 11);
            Sprite extractionSprite = RequirePlayerLayerSprite(spriteAssets, 9);
            Sprite legOneSprite = RequirePlayerLayerSprite(spriteAssets, 1);
            Sprite legThreeSprite = RequirePlayerLayerSprite(spriteAssets, 3);
            Sprite legFiveSprite = RequirePlayerLayerSprite(spriteAssets, 5);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * PlayerVisualScale;

            SortingGroup sortingGroup = visual.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = 10;

            Transform aimPivot = new GameObject("Tentacle Aim").transform;
            aimPivot.SetParent(visual.transform, false);
            aimPivot.gameObject.AddComponent<PlayerTentacleAim>();

            Vector2 bodyLowerLeft = new Vector2(282f, 495f);
            Vector2 bodyCenter = bodyLowerLeft + (bodySprite.rect.size * 0.5f);

            GameObject bodyRoot = new GameObject("Body Jelly");
            bodyRoot.transform.SetParent(aimPivot, false);
            SpriteRenderer bodyRenderer = CreatePlayerLayer(
                bodyRoot.transform,
                "Layer 7 - Body",
                bodySprite,
                Vector2.zero,
                7);
            SpriteRenderer coreRenderer = CreatePlayerLayer(
                bodyRoot.transform,
                "Layer 11 - Core",
                coreSprite,
                CalculatePlayerLayerOffset(coreSprite, new Vector2(344f, 525f), bodyCenter),
                11);

            SpriteRenderer[] legs =
            {
                CreatePlayerLayer(
                    aimPivot,
                    "Layer 1 - Leg",
                    legOneSprite,
                    CalculatePlayerLayerOffset(legOneSprite, new Vector2(339f, 83f), bodyCenter),
                    1),
                CreatePlayerLayer(
                    aimPivot,
                    "Layer 3 - Leg",
                    legThreeSprite,
                    CalculatePlayerLayerOffset(legThreeSprite, new Vector2(174f, 71f), bodyCenter),
                    3),
                CreatePlayerLayer(
                    aimPivot,
                    "Layer 5 - Leg",
                    legFiveSprite,
                    CalculatePlayerLayerOffset(legFiveSprite, new Vector2(391f, 83f), bodyCenter),
                    5)
            };

            SpriteRenderer extractionTentacle = CreatePlayerLayer(
                aimPivot,
                "Layer 9 - Extraction Tentacle",
                extractionSprite,
                CalculatePlayerLayerOffset(extractionSprite, new Vector2(376f, 146f), bodyCenter),
                9);

            PlayerJellyVisual jellyVisual = parent.gameObject.AddComponent<PlayerJellyVisual>();
            jellyVisual.Configure(
                aimPivot,
                bodyRoot.transform,
                bodyRenderer,
                coreRenderer.transform,
                legs,
                extractionTentacle);
            return visual;
        }

        private static Sprite RequirePlayerLayerSprite(UnityEngine.Object[] assets, int layerNumber)
        {
            string layerName = "레이어 " + layerNumber;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == layerName)
                {
                    return sprite;
                }
            }

            throw new System.InvalidOperationException(
                "Player PSD is missing the required sprite layer '" + layerName + "'.");
        }

        private static SpriteRenderer CreatePlayerLayer(
            Transform parent,
            string objectName,
            Sprite sprite,
            Vector2 localPosition,
            int layerOrder)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = layerOrder;
            return renderer;
        }

        private static Vector2 CalculatePlayerLayerOffset(
            Sprite sprite,
            Vector2 sourceLowerLeft,
            Vector2 bodyCenter)
        {
            Vector2 layerCenter = sourceLowerLeft + (sprite.rect.size * 0.5f);
            return (layerCenter - bodyCenter) / sprite.pixelsPerUnit;
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
