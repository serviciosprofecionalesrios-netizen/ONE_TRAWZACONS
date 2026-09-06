using System;
using System.Collections.Generic;
using System.Linq;

namespace ITServiceDeskApp.Models
{
    public static class MaintenanceTechnicianCatalog
    {
        public static readonly string[] BaseCategories =
        {
            "Mecanico",
            "Electrico",
            "Llantero",
            "Hidraulico",
            "Soldador",
            "Pintor",
            "Auxiliar de taller",
            "Otro"
        };

        public static readonly string[] ShiftOptions =
        {
            "Diurno",
            "Nocturno",
            "Mixto"
        };

        public static readonly string[] MaintenanceStageOptions =
        {
            "Recibida",
            "Diagnostico",
            "En reparacion",
            "Prueba tecnica",
            "Lista para entrega",
            "Cerrada"
        };

        public static List<string> BuildCategoryOptions(IEnumerable<string>? dynamicCategories = null, string? includeValue = null)
        {
            var categories = BaseCategories
                .Concat(dynamicCategories ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(includeValue) &&
                categories.All(x => !x.Equals(includeValue, StringComparison.OrdinalIgnoreCase)))
            {
                categories.Insert(0, includeValue.Trim());
            }

            return categories;
        }

        public static List<string> BuildShiftOptions(string? includeValue = null)
        {
            var shifts = ShiftOptions
                .Concat(string.IsNullOrWhiteSpace(includeValue)
                    ? Array.Empty<string>()
                    : new[] { includeValue.Trim() })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            return shifts;
        }

        public static List<string> BuildMaintenanceStageOptions(string? includeValue = null)
        {
            var stages = MaintenanceStageOptions
                .Concat(string.IsNullOrWhiteSpace(includeValue)
                    ? Array.Empty<string>()
                    : new[] { includeValue.Trim() })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return stages;
        }
    }
}
