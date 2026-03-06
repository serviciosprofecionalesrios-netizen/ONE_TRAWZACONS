using System.Collections.Generic;

namespace ITServiceDeskApp.Models
{
    public class DashboardViewModel
    {
        // =============================
        // KPI PRINCIPALES
        // =============================

        public int TotalTickets { get; set; }
        public int OpenTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int OverdueTickets { get; set; }

        // =============================
        // DISTRIBUCIONES
        // =============================

        public Dictionary<string, int> TicketsBySite { get; set; } = new();
        public Dictionary<string, int> TicketsByDepartment { get; set; } = new();
        public Dictionary<string, int> TicketsByType { get; set; } = new();

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

        public List<Ticket> RecentTickets { get; set; } = new();
    }
}