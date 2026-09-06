using System;
using System.Collections.Generic;

namespace ITServiceDeskApp.Models
{
    public class DashboardViewModel
    {
        // =============================
        // FILTROS
        // =============================

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public TicketStatus? FilterStatus { get; set; }
        public PriorityLevel? FilterPriority { get; set; }
        public string? FilterSite { get; set; }
        public string? FilterDepartment { get; set; }
        public string? FilterTechnician { get; set; }

        public List<string> SiteOptions { get; set; } = new();
        public List<string> DepartmentOptions { get; set; } = new();
        public List<string> TechnicianOptions { get; set; } = new();

        // =============================
        // KPI PRINCIPALES
        // =============================

        public int TotalTickets { get; set; }
        public int ActiveTickets { get; set; }
        public int OpenTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int OverdueTickets { get; set; }
        public int UnassignedActiveTickets { get; set; }
        public int CriticalActiveTickets { get; set; }
        public int TicketsCreatedCurrentPeriod { get; set; }
        public int TicketsCreatedPreviousPeriod { get; set; }
        public double CreatedTrendPercentage { get; set; }
        public double MttrHours { get; set; }
        public double SlaComplianceRate { get; set; }

        // =============================
        // DISTRIBUCIONES
        // =============================

        public Dictionary<string, int> TicketsByStatus { get; set; } = new();
        public Dictionary<string, int> TicketsByPriority { get; set; } = new();
        public Dictionary<string, int> TicketsBySite { get; set; } = new();
        public Dictionary<string, int> TicketsByDepartment { get; set; } = new();
        public Dictionary<string, int> TicketsByType { get; set; } = new();
        public Dictionary<string, int> BacklogAgingBuckets { get; set; } = new();

        // =============================
        // INDICADOR SLA
        // =============================

        public int SlaCompliant { get; set; }
        public int SlaNearDue { get; set; }
        public int SlaBreached { get; set; }

        public double SlaCompliantPercentage { get; set; }
        public double SlaNearDuePercentage { get; set; }
        public double SlaBreachedPercentage { get; set; }

        // =============================
        // INDICADORES MENSUALES
        // =============================

        public int CurrentYear { get; set; }

        public Dictionary<int, int> TicketsByMonthCurrent { get; set; } = new();
        public Dictionary<int, int> TicketsByMonthPrevious { get; set; } = new();

        public double YearGrowthPercentage { get; set; }

        // =============================
        // LISTADO RECIENTE
        // =============================

        public List<DashboardRiskTicketItem> AtRiskTickets { get; set; } = new();
        public List<DashboardTechnicianLoadItem> TechnicianLoads { get; set; } = new();
        public List<Ticket> RecentTickets { get; set; } = new();
    }

    public class DashboardRiskTicketItem
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public PriorityLevel Priority { get; set; }
        public TicketStatus Status { get; set; }
        public string AssignedTechnician { get; set; } = "Sin asignar";
        public DateTime SlaDeadline { get; set; }
        public double HoursToSla { get; set; }
    }

    public class DashboardTechnicianLoadItem
    {
        public string Technician { get; set; } = "Sin asignar";
        public int ActiveTickets { get; set; }
        public int CriticalTickets { get; set; }
        public int OverdueTickets { get; set; }
    }
}
