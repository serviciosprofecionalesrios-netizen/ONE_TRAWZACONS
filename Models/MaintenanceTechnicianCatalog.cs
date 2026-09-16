using System;
using System.Collections.Generic;
using System.Linq;

namespace ITServiceDeskApp.Models
{
    public static class MaintenanceTechnicianCatalog
    {
        public sealed record InitialTechnician(
            string FullName,
            string Role,
            string Specialty,
            string? PhoneNumber = null,
            string? Email = null);

        public static readonly IReadOnlyList<InitialTechnician> InitialPersonnel =
        [
            new("Álvaro Blass", "Mecánico", "Mecanico B", "83903819"),
            new("David Canales", "Supervisor de Taller", "Mecanico A", "85012848"),
            new("Hugo Noel Flores", "Mecánico", "Mecanico A", "89857504"),
            new("Jefferson Gutierrez", "Mecánico", "Mecanico A", "57486537"),
            new("Wiston Baltodano", "Mecánico", "Mecanico B", "84044434"),
            new("Mitón Juarez", "Eléctrico", "Electrico"),
            new("Eddy Flores", "Pintor", "Pintor"),
            new("Jamil Mojica", "Soldador", "Soldador"),
            new("Ruben Gonzalez", "Mecánico", "Mecanico B"),
            new("Jonathan Mendieta", "Mecánico", "Mecanico C"),
            new("Freddy Antonio Moreno Larios", "Mecánico", "Mecanico", "87173653", "caledaed383@gmail.com"),
            new("Ernesto Herrera", "Eléctrico", "Electrico", "88175438", "gerberchino5@gmail.com"),
            new("Ricardo Joaquín Castillo Larios", "Mecánico", "Motores y rodamientos", "1", "8@gmail.com"),
            new("Jairo Gonez", "Llantero", "Llantero", "87207439", "jairo@gmail.com")
        ];

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
