using System;

namespace ITServiceDeskApp.Models
{
    public class MaintenanceAppointmentMetadata
    {
        public string? MaintenanceOrderNumber { get; set; }
        public decimal? EstimatedDurationHours { get; set; }
        public DateTime? WorkshopEntryAt { get; set; }
        public DateTime? WorkshopExitAt { get; set; }

        public bool ChecklistInspectionCompleted { get; set; }
        public bool ChecklistSparePartsCompleted { get; set; }
        public bool ChecklistFinalTestCompleted { get; set; }
        public bool ChecklistUserConformityCompleted { get; set; }

        public string? CompletionEvidenceNotes { get; set; }
        public DateTime? Reminder24hSentAtUtc { get; set; }
        public DateTime? Reminder2hSentAtUtc { get; set; }

        public int GetChecklistProgressPercent()
        {
            var completed = 0;
            if (ChecklistInspectionCompleted) completed++;
            if (ChecklistSparePartsCompleted) completed++;
            if (ChecklistFinalTestCompleted) completed++;
            if (ChecklistUserConformityCompleted) completed++;

            return (int)Math.Round((completed / 4.0) * 100, MidpointRounding.AwayFromZero);
        }
    }
}
