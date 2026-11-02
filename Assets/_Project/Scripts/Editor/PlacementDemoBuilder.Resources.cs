#if UNITY_EDITOR
using ProjectoRTS.Resources;
using UnityEngine;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private static readonly Vector3[] WoodClusterCenters =
        {
            new Vector3(-34f, 0f, 14f),
            new Vector3(40f, 0f, 46f),
            new Vector3(-70f, 0f, 82f),
            new Vector3(-120f, 0f, -96f),
            new Vector3(96f, 0f, 128f),
            new Vector3(-28f, 0f, 116f),
            new Vector3(136f, 0f, -18f),
            new Vector3(-132f, 0f, 34f)
        };

        private static readonly Vector3[] BronzeClusterCenters =
        {
            new Vector3(-46f, 0f, -40f),
            new Vector3(62f, 0f, -30f),
            new Vector3(92f, 0f, 74f),
            new Vector3(104f, 0f, -108f),
            new Vector3(-88f, 0f, 118f),
            new Vector3(-128f, 0f, -24f),
            new Vector3(30f, 0f, -128f),
            new Vector3(126f, 0f, 26f)
        };

        private static void CreateResourceNodes(VisualPalette palette)
        {
            GameObject root = new GameObject("Resource Nodes");
            root.transform.position = Vector3.zero;

            Material leafMaterial = GetOrCreateMaterial(
                "ResourceLeaves",
                new Color(0.22f, 0.42f, 0.18f, 1f));

            for (int i = 0; i < WoodClusterCenters.Length; i++)
            {
                CreateResourceCluster(
                    root.transform,
                    WoodClusterCenters[i],
                    ResourceType.Wood,
                    3,
                    400,
                    palette,
                    leafMaterial);
            }

            for (int i = 0; i < BronzeClusterCenters.Length; i++)
            {
                CreateResourceCluster(
                    root.transform,
                    BronzeClusterCenters[i],
                    ResourceType.Bronze,
                    2,
                    400,
                    palette,
                    leafMaterial);
            }
        }

        private static void CreateResourceCluster(
            Transform parent,
            Vector3 center,
            ResourceType type,
            int count,
            int amount,
            VisualPalette palette,
            Material leafMaterial)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 137.508f + (type == ResourceType.Wood ? 17f : 61f)) * Mathf.Deg2Rad;
                float radius = i == 0 ? 0f : 2.2f + (i % 3) * 0.9f;
                Vector3 position = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                if (type == ResourceType.Wood)
                {
                    CreateWoodNode(parent, position, amount, palette, leafMaterial);
                }
                else
                {
                    CreateBronzeNode(parent, position, amount, palette);
                }
            }
        }

        private static void CreateWoodNode(
            Transform parent,
            Vector3 position,
            int amount,
            VisualPalette palette,
            Material leafMaterial)
        {
            GameObject root = new GameObject("Wood Node");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, position.x * 3f + position.z * 7f, 0f);

            ResourceNode node = root.AddComponent<ResourceNode>();
            node.Configure(ResourceType.Wood, amount);

            CreatePart(
                PrimitiveType.Cylinder,
                "Trunk",
                root.transform,
                new Vector3(0f, 1.2f, 0f),
                new Vector3(0.34f, 1.2f, 0.34f),
                palette.Wood);

            CreatePart(
                PrimitiveType.Sphere,
                "Canopy",
                root.transform,
                new Vector3(0f, 2.75f, 0f),
                new Vector3(1.8f, 1.5f, 1.8f),
                leafMaterial);

            DisableShadows(root);
        }

        private static void CreateBronzeNode(
            Transform parent,
            Vector3 position,
            int amount,
            VisualPalette palette)
        {
            GameObject root = new GameObject("Bronze Node");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, position.x * 5f - position.z * 2f, 0f);

            ResourceNode node = root.AddComponent<ResourceNode>();
            node.Configure(ResourceType.Bronze, amount);

            CreatePart(
                PrimitiveType.Sphere,
                "Rock",
                root.transform,
                new Vector3(0f, 0.55f, 0f),
                new Vector3(1.7f, 0.9f, 1.45f),
                palette.Stone);

            CreatePart(
                PrimitiveType.Sphere,
                "Ore",
                root.transform,
                new Vector3(0.5f, 0.9f, 0.15f),
                new Vector3(0.65f, 0.45f, 0.55f),
                palette.Bronze);

            CreatePart(
                PrimitiveType.Cube,
                "Ore Vein",
                root.transform,
                new Vector3(-0.42f, 0.7f, -0.18f),
                new Vector3(0.45f, 0.35f, 0.45f),
                Quaternion.Euler(18f, 24f, 12f),
                palette.Bronze);

            DisableShadows(root);
        }

        private static void DisableShadows(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }
}
#endif
