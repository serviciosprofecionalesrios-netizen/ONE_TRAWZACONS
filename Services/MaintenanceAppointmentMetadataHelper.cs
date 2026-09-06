using System;
using System.Text.Json;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Services
{
    public static class MaintenanceAppointmentMetadataHelper
    {
        private const string MetaStart = "[MNT_META]";
        private const string MetaEnd = "[/MNT_META]";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public static (MaintenanceAppointmentMetadata Meta, string UserNotes) Parse(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return (new MaintenanceAppointmentMetadata(), string.Empty);
            }

            var trimmed = notes.Trim();
            var startIndex = trimmed.IndexOf(MetaStart, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return (new MaintenanceAppointmentMetadata(), notes.Trim());
            }

            var jsonStart = startIndex + MetaStart.Length;
            var endIndex = trimmed.IndexOf(MetaEnd, jsonStart, StringComparison.Ordinal);
            if (endIndex <= jsonStart)
            {
                return (new MaintenanceAppointmentMetadata(), notes.Trim());
            }

            var jsonLength = endIndex - jsonStart;
            var json = trimmed.Substring(jsonStart, jsonLength).Trim();
            var prefixNotes = trimmed[..startIndex].Trim();
            var suffixNotes = trimmed[(endIndex + MetaEnd.Length)..].Trim();
            var remainder = string.IsNullOrWhiteSpace(prefixNotes)
                ? suffixNotes
                : string.IsNullOrWhiteSpace(suffixNotes)
                    ? prefixNotes
                    : $"{prefixNotes}{Environment.NewLine}{Environment.NewLine}{suffixNotes}";

            try
            {
                var meta = JsonSerializer.Deserialize<MaintenanceAppointmentMetadata>(json, JsonOptions)
                           ?? new MaintenanceAppointmentMetadata();

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Compatibilidad con payloads legacy de checklist.
                meta.ChecklistInspectionCompleted = meta.ChecklistInspectionCompleted ||
                                                    TryReadBooleanAlias(root, "checklistInspection", "inspectionCompleted");
                meta.ChecklistSparePartsCompleted = meta.ChecklistSparePartsCompleted ||
                                                    TryReadBooleanAlias(root, "checklistPartsValidated", "checklistSparePartsValidated", "sparePartsCompleted");
                meta.ChecklistFinalTestCompleted = meta.ChecklistFinalTestCompleted ||
                                                   TryReadBooleanAlias(root, "checklistFinalTest", "finalTestCompleted");
                meta.ChecklistUserConformityCompleted = meta.ChecklistUserConformityCompleted ||
                                                        TryReadBooleanAlias(root, "checklistUserConformity", "userConformityCompleted");

                if (TryReadChecklistContainer(root, out var checklistContainer))
                {
                    meta.ChecklistInspectionCompleted = meta.ChecklistInspectionCompleted ||
                                                        TryReadBooleanAlias(checklistContainer, "inspection", "inspectionCompleted", "checklistInspection");
                    meta.ChecklistSparePartsCompleted = meta.ChecklistSparePartsCompleted ||
                                                        TryReadBooleanAlias(checklistContainer, "parts", "partsValidated", "spareParts", "sparePartsCompleted");
                    meta.ChecklistFinalTestCompleted = meta.ChecklistFinalTestCompleted ||
                                                       TryReadBooleanAlias(checklistContainer, "finalTest", "finalTestCompleted");
                    meta.ChecklistUserConformityCompleted = meta.ChecklistUserConformityCompleted ||
                                                            TryReadBooleanAlias(checklistContainer, "userConformity", "userConformityCompleted");
                }

                return (meta, remainder);
            }
            catch
            {
                return (new MaintenanceAppointmentMetadata(), notes.Trim());
            }
        }

        public static string Build(MaintenanceAppointmentMetadata meta, string? userNotes)
        {
            var safeMeta = meta ?? new MaintenanceAppointmentMetadata();
            var json = JsonSerializer.Serialize(safeMeta, JsonOptions);
            var cleanNotes = string.IsNullOrWhiteSpace(userNotes) ? string.Empty : userNotes.Trim();

            return string.IsNullOrWhiteSpace(cleanNotes)
                ? $"{MetaStart}{json}{MetaEnd}"
                : $"{MetaStart}{json}{MetaEnd}{Environment.NewLine}{Environment.NewLine}{cleanNotes}";
        }

        private static bool TryReadBooleanAlias(JsonElement root, params string[] aliases)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var alias in aliases)
            {
                if (TryReadBooleanProperty(root, alias, out var value) && value)
                    return true;
            }

            return false;
        }

        private static bool TryReadBooleanProperty(JsonElement root, string propertyName, out bool value)
        {
            value = false;

            foreach (var property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.True:
                        value = true;
                        return true;
                    case JsonValueKind.False:
                        value = false;
                        return true;
                    case JsonValueKind.String:
                    {
                        var raw = property.Value.GetString();
                        if (string.IsNullOrWhiteSpace(raw))
                            return false;

                        if (bool.TryParse(raw, out var parsedBool))
                        {
                            value = parsedBool;
                            return true;
                        }

                        if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(raw, "si", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
                        {
                            value = true;
                            return true;
                        }

                        if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase))
                        {
                            value = false;
                            return true;
                        }

                        return false;
                    }
                    case JsonValueKind.Number:
                        if (property.Value.TryGetInt32(out var number))
                        {
                            value = number != 0;
                            return true;
                        }

                        return false;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool TryReadChecklistContainer(JsonElement root, out JsonElement checklist)
        {
            checklist = default;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (string.Equals(property.Name, "checklist", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "maintenanceChecklist", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "tecnicoChecklist", StringComparison.OrdinalIgnoreCase))
                {
                    checklist = property.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
