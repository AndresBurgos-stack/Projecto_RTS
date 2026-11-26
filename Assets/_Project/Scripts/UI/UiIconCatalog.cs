using System.Collections.Generic;
using ProjectoRTS.Buildings;
using ProjectoRTS.Resources;
using ProjectoRTS.Units;
using UnityEngine;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Loads the circular medallion icons from Resources/UI/Icons and maps
    /// building/unit definitions to them. Missing icons return null so the
    /// caller can fall back to text.
    /// </summary>
    public static class UiIconCatalog
    {
        private const string Folder = "UI/Icons/";

        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(12);

        private static readonly HashSet<string> Warned = new HashSet<string>();

        public static Sprite GetBuildingIcon(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            switch (definition.name)
            {
                case "GreekBarracks": return Load("IconoBarrack");
                case "GreekGate": return Load("MurodeMadera");
                case "GreekHouse": return Load("Casa");
                case "GreekSmelter": return Load("IconoFundicion");
                case "GreekStorage": return Load("Almacen");
                case "GreekTownCenter": return Load("ChozadeObreros");
                case "GreekWall": return Load("MurodeMadera");
                default: return null;
            }
        }

        public static Sprite GetUnitIcon(UnitDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            switch (definition.name)
            {
                case "HopliteArcadio": return Load("IconoHoplita");
                case "Xifista": return Load("IconoXifista");
                case "Psiloi": return Load("IconoArquero");
                case "Peltasta": return Load("IconoArquero");
                default: return null;
            }
        }

        public static Sprite GetResourceIcon(ResourceType type)
        {
            return type == ResourceType.Wood ? Load("IconoMadera") : null;
        }

        private static Sprite Load(string iconName)
        {
            if (Cache.TryGetValue(iconName, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite imported = UnityEngine.Resources.Load<Sprite>(Folder + iconName);
            if (imported != null)
            {
                Cache[iconName] = imported;
                return imported;
            }

            Texture2D texture = UnityEngine.Resources.Load<Texture2D>(Folder + iconName);
            if (texture == null)
            {
                if (Warned.Add(iconName))
                {
                    Debug.LogWarning(
                        "[Iconos] No se encontro Resources/" + Folder + iconName + ".png");
                }

                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            sprite.name = iconName;
            Cache[iconName] = sprite;
            return sprite;
        }
    }
}
