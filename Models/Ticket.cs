using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        // =========================
        // IDENTIFICACIÓN
        // =========================

        [MaxLength(20)]
        public string? TicketNumber { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedDate { get; set; }

        // =========================
        // INFORMACIÓN DEL SOLICITANTE
        // =========================

        [Required]
        [MaxLength(100)]
        public string RequestingUser { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Site { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string IncidentType { get; set; } = string.Empty;

        // =========================
        // CONTROL OPERATIVO
        // =========================

        [Required]
        public PriorityLevel Priority { get; set; }

        [Required]
        public TicketStatus Status { get; set; } = TicketStatus.Open;

        [MaxLength(100)]
        public string? AssignedTechnician { get; set; }

        // =========================
        // SLA
        // =========================

        public DateTime SLADeadline { get; set; }

        // =========================
        // CONTENIDO
        // =========================

        [Required]
        [MinLength(10)]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AttachmentPath { get; set; }

        // =========================
        // RELACIÓN 1 → MUCHOS (Historial)
        // =========================

        public ICollection<TicketHistory> HistoryEntries { get; set; }
            = new List<TicketHistory>();

        // =========================
        // CONTROL DE CONCURRENCIA
        // =========================

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}