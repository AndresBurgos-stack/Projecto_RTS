using System.Collections.Generic;
using System.Text;
using ProjectoRTS.Buildings;

namespace ProjectoRTS.UI
{
    /// <summary>Spanish description and stat block for the building tooltips.</summary>
    public static class BuildingTooltipText
    {
        public static string GetTitle(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            switch (definition.name)
            {
                case "GreekBarracks": return "Cuartel";
                case "GreekGate": return "Puerta";
                case "GreekHouse": return "Casa";
                case "GreekSmelter": return "Fundición";
                case "GreekStorage": return "Almacén";
                case "GreekTower": return "Torre de piedra";
                case "GreekTowerWood": return "Torre de madera";
                case "GreekTownCenter": return "Choza de obreros";
                case "GreekWall": return "Muro de madera";
                default: return definition.DisplayName;
            }
        }

        public static string GetBody(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(180);
            builder.Append(GetDescription(definition));
            builder.Append("\n\nCoste: ");
            builder.Append(FormatCost(definition));
            builder.Append("\nVida: ");
            builder.Append(definition.MaxHealth.ToString("0"));

            if (definition.AttackDamage > 0f)
            {
                builder.Append("  ·  Ataque: ");
                builder.Append(definition.AttackDamage.ToString("0.#"));
                builder.Append("  ·  Alcance: ");
                builder.Append(definition.AttackRange.ToString("0.#"));
            }

            if (definition.RangeBonus > 0f)
            {
                builder.Append("  ·  Alcance +");
                builder.Append(definition.RangeBonus.ToString("0.#"));
                builder.Append(" (arqueros)");
            }

            if (definition.PopulationBonus > 0)
            {
                builder.Append("  ·  Población: +");
                builder.Append(definition.PopulationBonus);
            }

            builder.Append("\nTamaño: ");
            builder.Append(definition.Width);
            builder.Append('×');
            builder.Append(definition.Depth);
            return builder.ToString();
        }

        private static string GetDescription(BuildingDefinition definition)
        {
            switch (definition.name)
            {
                case "GreekBarracks":
                    return "Cuartel. Recluta hoplitas, psiloi, xifistas y peltastas para defender la aldea.";
                case "GreekGate":
                    return "Puerta de la muralla. Cierra el paso y protege la aldea de los enemigos.";
                case "GreekHouse":
                    return "Casa. Aumenta la población máxima de la aldea.";
                case "GreekSmelter":
                    return "Fundición. Funde cobre y estaño para producir bronce.";
                case "GreekStorage":
                    return "Almacén. Los trabajadores depositan aquí los recursos recogidos.";
                case "GreekTower":
                    return "Torre de piedra. Da mucho más alcance a los arqueros montados y resiste más que la de madera.";
                case "GreekTowerWood":
                    return "Torre de madera. Caben hasta 4 arqueros con más alcance; más barata y frágil que la de piedra.";
                case "GreekTownCenter":
                    return "Choza de obreros. Punto de recogida de recursos y centro de la aldea.";
                case "GreekWall":
                    return "Muro de madera. Bloquea el paso y protege la aldea.";
                default:
                    return GetCategoryDescription(definition.Category);
            }
        }

        private static string GetCategoryDescription(BuildingCategory category)
        {
            switch (category)
            {
                case BuildingCategory.Wall: return "Estructura defensiva que bloquea el paso.";
                case BuildingCategory.Tower: return "Torre que aumenta el alcance de los arqueros montados.";
                case BuildingCategory.Production: return "Edificio de producción.";
                case BuildingCategory.Economy: return "Edificio económico.";
                case BuildingCategory.Defense: return "Edificio defensivo.";
                default: return "Edificio de la aldea.";
            }
        }

        private static string FormatCost(BuildingDefinition definition)
        {
            List<string> parts = new List<string>(3);

            if (definition.WoodCost > 0)
            {
                parts.Add(definition.WoodCost + " madera");
            }

            if (definition.BronzeCost > 0)
            {
                parts.Add(definition.BronzeCost + " bronce");
            }

            if (definition.GoldCost > 0)
            {
                parts.Add(definition.GoldCost + " oro");
            }

            return parts.Count == 0 ? "Gratis" : string.Join(", ", parts);
        }
    }
}
