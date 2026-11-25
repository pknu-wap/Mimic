using MimicCell.Creatures;
using MimicCell.Gameplay;
using MimicCell.Prototype;
using UnityEditor;
using UnityEngine;

namespace MimicCell.EditorTools
{
    public static class MimicPrototypeSceneBuilder
    {
        private const string PrototypePrefabFolder = "Assets/Prefabs/Prototype";
        private const string PlayerPrefabPath = PrototypePrefabFolder + "/PlayerPrototype.prefab";
        private const string AimMaterialPath = PrototypePrefabFolder + "/PrototypeAimLine.mat";
        private const float PixelsPerUnit = 16f;

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

            for (int i = 0; i < CambrianCreatures.Length; i++)
            {
                CreateCreaturePrefab(CambrianCreatures[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mimic prototype creature prototype prefabs were built successfully.");
        }

        [MenuItem("Mimic/Select Player Prototype", priority = 11)]
        public static void SelectPlayerPrototype()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
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
    }
}
