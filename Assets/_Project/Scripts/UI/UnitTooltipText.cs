using System.Text;
using ProjectoRTS.Units;

namespace ProjectoRTS.UI
{
    /// <summary>Spanish stat block for the unit recruitment tooltips.</summary>
    public static class UnitTooltipText
    {
        public static string GetBody(UnitDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(150);
            builder.Append("Rol: ");
            builder.Append(GetRoleName(definition.Role));
            builder.Append("\nCoste: ");
            builder.Append(FormatCost(definition));
            builder.Append("\nPoblación: ");
            builder.Append(definition.PopulationCost);
            builder.Append("\nVida: ");
            builder.Append(definition.MaxHealth.ToString("0"));
            builder.Append("  ·  Ataque: ");
            builder.Append(definition.AttackDamage.ToString("0.#"));
            builder.Append("  ·  Alcance: ");
            builder.Append(definition.AttackRange.ToString("0.#"));
            builder.Append("\nEntrenamiento: ");
            builder.Append(definition.TrainTime.ToString("0.#"));
            builder.Append(" s");
            return builder.ToString();
        }

        private static string GetRoleName(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Infantry: return "Infantería";
                case UnitRole.Ranged: return "A distancia";
                case UnitRole.Cavalry: return "Caballería";
                case UnitRole.Siege: return "Asedio";
                default: return "Unidad";
            }
        }

        private static string FormatCost(UnitDefinition definition)
        {
            if (definition.WoodCost <= 0 && definition.BronzeCost <= 0)
            {
                return "Gratis";
            }

            if (definition.WoodCost > 0 && definition.BronzeCost > 0)
            {
                return definition.WoodCost + " madera, " +
                    definition.BronzeCost + " bronce";
            }

            return definition.WoodCost > 0
                ? definition.WoodCost + " madera"
                : definition.BronzeCost + " bronce";
        }
    }
}
