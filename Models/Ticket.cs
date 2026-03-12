using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITServiceDeskApp.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string? TicketNumber { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedDate { get; set; }

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

        [Required]
        public PriorityLevel Priority { get; set; }

        [Required]
        public TicketStatus Status { get; set; } = TicketStatus.Open;

        [MaxLength(100)]
        public string? AssignedTechnician { get; set; }

        public DateTime SLADeadline { get; set; }

        [Required]
        [MinLength(10)]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AttachmentPath { get; set; }

        [MaxLength(500)]
        public string? BeforeEvidencePath { get; set; }

        [MaxLength(500)]
        public string? AfterEvidencePath { get; set; }

        [MaxLength(1000)]
        public string? InitialConditionNotes { get; set; }

        [MaxLength(2000)]
        public string? RepairActionsPerformed { get; set; }

        [MaxLength(1000)]
        public string? RootCause { get; set; }

        public bool SparePartRequired { get; set; }

        public bool SparePartPurchased { get; set; }

        [MaxLength(500)]
        public string? SparePartDetails { get; set; }

        public bool ComponentChanged { get; set; }

        [MaxLength(250)]
        public string? ChangedComponentName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo del componente debe ser mayor o igual a 0.")]
        public decimal? ChangedComponentCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo en C$ debe ser mayor o igual a 0.")]
        public decimal? ChangedComponentCostCordoba { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "El costo en $ debe ser mayor o igual a 0.")]
        public decimal? ChangedComponentCostUsd { get; set; }

        [MaxLength(1000)]
        public string? TechnicalTestsPerformed { get; set; }

        public bool UserConformityConfirmed { get; set; }

        [MaxLength(1000)]
        public string? PreventiveRecommendations { get; set; }

        [MaxLength(500)]
        public string? TechnicalSheetPath { get; set; }

        [MaxLength(500)]
        public string? ExitOrderPath { get; set; }

        [MaxLength(2000)]
        public string? Observations { get; set; }

        public ICollection<TicketHistory> HistoryEntries { get; set; }
            = new List<TicketHistory>();

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}


