using ITServiceDeskApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
        public DbSet<TicketSparePartDispatch> TicketSparePartDispatches => Set<TicketSparePartDispatch>();
        public DbSet<FinanceApprovalRule> FinanceApprovalRules => Set<FinanceApprovalRule>();
        public DbSet<FinanceMonthlyBudget> FinanceMonthlyBudgets => Set<FinanceMonthlyBudget>();
        public DbSet<FinanceCostCenter> FinanceCostCenters => Set<FinanceCostCenter>();
        public DbSet<FinanceInvoice> FinanceInvoices => Set<FinanceInvoice>();
        public DbSet<FinanceReceipt> FinanceReceipts => Set<FinanceReceipt>();
        public DbSet<FinanceCounterparty> FinanceCounterparties => Set<FinanceCounterparty>();
        public DbSet<FinanceAttachment> FinanceAttachments => Set<FinanceAttachment>();
        public DbSet<FinanceAuditLog> FinanceAuditLogs => Set<FinanceAuditLog>();
        public DbSet<FinanceSetting> FinanceSettings => Set<FinanceSetting>();
        public DbSet<MaintenanceAppointment> MaintenanceAppointments => Set<MaintenanceAppointment>();
        public DbSet<MaintenanceTechnician> MaintenanceTechnicians => Set<MaintenanceTechnician>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<MaintenanceInventoryPart> MaintenanceInventoryParts => Set<MaintenanceInventoryPart>();
        public DbSet<Credential> Credentials => Set<Credential>();
        public DbSet<OperacionesSeguimientoRegistro> OperacionesSeguimientoRegistros => Set<OperacionesSeguimientoRegistro>();
        public DbSet<OperacionesSeguimientoCarga> OperacionesSeguimientoCargas => Set<OperacionesSeguimientoCarga>();
        public DbSet<OperacionesIngresoPersonalTareaPadre> OperacionesIngresoPersonalTareasPadre => Set<OperacionesIngresoPersonalTareaPadre>();
        public DbSet<OperacionesIngresoPersonalSolicitud> OperacionesIngresoPersonalSolicitudes => Set<OperacionesIngresoPersonalSolicitud>();
        public DbSet<OperacionesIngresoEquipoGondolaTareaPadre> OperacionesIngresoEquipoGondolaTareasPadre => Set<OperacionesIngresoEquipoGondolaTareaPadre>();
        public DbSet<OperacionesIngresoEquipoGondolaSolicitud> OperacionesIngresoEquipoGondolaSolicitudes => Set<OperacionesIngresoEquipoGondolaSolicitud>();
        public DbSet<HsIncident> HsIncidents => Set<HsIncident>();
        public DbSet<HsInspection> HsInspections => Set<HsInspection>();
        public DbSet<HsCorrectiveAction> HsCorrectiveActions => Set<HsCorrectiveAction>();
        public DbSet<HsTrainingRecord> HsTrainingRecords => Set<HsTrainingRecord>();
        public DbSet<HsWorkPermit> HsWorkPermits => Set<HsWorkPermit>();
        public DbSet<HsDocument> HsDocuments => Set<HsDocument>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => t.TicketNumber)
                .IsUnique();

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => new { t.Department, t.MaintenanceStage, t.Status });

            modelBuilder.Entity<MaintenanceAppointment>()
                .HasIndex(a => a.AppointmentNumber)
                .IsUnique();

            modelBuilder.Entity<MaintenanceTechnician>()
                .HasIndex(t => t.FullName);

            modelBuilder.Entity<MaintenanceTechnician>()
                .HasIndex(t => new { t.Category, t.IsAvailable, t.IsActive });

            modelBuilder.Entity<MaintenanceTechnician>()
                .HasIndex(t => new { t.Category, t.Shift, t.IsAvailable, t.IsActive });

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.AssetCode)
                .IsUnique();

            modelBuilder.Entity<MaintenanceInventoryPart>()
                .HasIndex(i => i.PartCode)
                .IsUnique();

            modelBuilder.Entity<Credential>()
                .HasIndex(c => new { c.Name, c.Type });

            modelBuilder.Entity<TicketHistory>()
                .HasOne(th => th.Ticket)
                .WithMany(t => t.HistoryEntries)
                .HasForeignKey(th => th.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .HasOne(x => x.Ticket)
                .WithMany(t => t.SparePartDispatches)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .HasOne(x => x.MaintenanceInventoryPart)
                .WithMany()
                .HasForeignKey(x => x.MaintenanceInventoryPartId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.TicketNumber)
                .HasMaxLength(20);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.MaintenanceStage)
                .HasMaxLength(40);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.CostCenterCode)
                .HasMaxLength(40);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.CostApprovedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.CostApprovalNotes)
                .HasMaxLength(800);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceAccountingEntryNumber)
                .HasMaxLength(80);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinancePurchaseOrderNumber)
                .HasMaxLength(80);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceInvoiceNumber)
                .HasMaxLength(80);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceFinalPaymentMethod)
                .HasMaxLength(60);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceReconciliationStatus)
                .HasMaxLength(40);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceReturnedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceReturnReason)
                .HasMaxLength(700);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceEscalatedTo)
                .HasMaxLength(120);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.FinanceErpExportReference)
                .HasMaxLength(120);

            modelBuilder.Entity<MaintenanceAppointment>()
                .Property(a => a.AppointmentNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<MaintenanceTechnician>()
                .Property(t => t.FullName)
                .HasMaxLength(120);

            modelBuilder.Entity<MaintenanceTechnician>()
                .Property(t => t.Shift)
                .HasMaxLength(40);

            modelBuilder.Entity<MaintenanceTechnician>()
                .Property(t => t.MaxActiveOrders)
                .HasDefaultValue(4);

            modelBuilder.Entity<User>()
                .Property(u => u.FullName)
                .HasMaxLength(120);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.AssetCode)
                .HasMaxLength(30);

            modelBuilder.Entity<MaintenanceInventoryPart>()
                .Property(i => i.PartCode)
                .HasMaxLength(30);

            modelBuilder.Entity<MaintenanceInventoryPart>()
                .Property(i => i.ItemCode)
                .HasMaxLength(80);

            modelBuilder.Entity<Credential>()
                .Property(c => c.Name)
                .HasMaxLength(120);

            modelBuilder.Entity<Credential>()
                .Property(c => c.Type)
                .HasMaxLength(80);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .HasIndex(r => r.FechaOperativa);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Equipo)
                .HasMaxLength(80);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Procedencia)
                .HasMaxLength(120);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Ruta)
                .HasMaxLength(180);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Conductor)
                .HasMaxLength(120);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Estado)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Eficiencia)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.Toneladas)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.CombustibleGalones)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<OperacionesSeguimientoRegistro>()
                .Property(r => r.PesoSugerido)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<OperacionesSeguimientoCarga>()
                .Property(c => c.SourceFileName)
                .HasMaxLength(260);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .HasIndex(x => x.SolicitanteKey)
                .IsUnique();

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .HasIndex(x => x.TareaPadre)
                .IsUnique();

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.SolicitanteKey)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.SolicitanteNombre)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.TareaPadre)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.TipoOrigen)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.UltimoTipo)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoPersonalTareaPadre>()
                .Property(x => x.UltimoAsunto)
                .HasMaxLength(260);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .HasIndex(x => x.UpdatedAt);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.Tipo)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.Asunto)
                .HasMaxLength(260);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.SolicitanteNombre)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.TareaPadre)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.Estado)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoPersonalSolicitud>()
                .Property(x => x.Prioridad)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .HasIndex(x => x.SolicitanteKey)
                .IsUnique();

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .HasIndex(x => x.TareaPadre)
                .IsUnique();

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.SolicitanteKey)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.SolicitanteNombre)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.TareaPadre)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.TipoOrigen)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.UltimoTipo)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaTareaPadre>()
                .Property(x => x.UltimoAsunto)
                .HasMaxLength(260);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .HasIndex(x => x.UpdatedAt);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.Tipo)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.Asunto)
                .HasMaxLength(260);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.SolicitanteNombre)
                .HasMaxLength(220);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.TareaPadre)
                .HasMaxLength(40);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.Estado)
                .HasMaxLength(60);

            modelBuilder.Entity<OperacionesIngresoEquipoGondolaSolicitud>()
                .Property(x => x.Prioridad)
                .HasMaxLength(40);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.FieldChanged)
                .HasMaxLength(100);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.OldValue)
                .HasMaxLength(500);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.NewValue)
                .HasMaxLength(500);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.ChangedBy)
                .HasMaxLength(100);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.PartCode)
                .HasMaxLength(30);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.PartName)
                .HasMaxLength(180);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.UnitOfMeasure)
                .HasMaxLength(50);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.ItemCode)
                .HasMaxLength(80);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.PartNumber)
                .HasMaxLength(120);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.UnitCostCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.TotalCostCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.UnitCostUsd)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketSparePartDispatch>()
                .Property(x => x.TotalCostUsd)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketSparePartDispatch>()
                .HasIndex(x => x.TicketId);

            modelBuilder.Entity<TicketSparePartDispatch>()
                .HasIndex(x => x.MaintenanceInventoryPartId);

            modelBuilder.Entity<FinanceApprovalRule>()
                .Property(x => x.RequestType)
                .HasMaxLength(100);

            modelBuilder.Entity<FinanceApprovalRule>()
                .Property(x => x.CostCenter)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceApprovalRule>()
                .Property(x => x.MinAmountCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceApprovalRule>()
                .Property(x => x.MaxAmountCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceApprovalRule>()
                .HasIndex(x => new { x.IsActive, x.RequestType, x.CostCenter, x.MinAmountCordoba, x.MaxAmountCordoba });

            modelBuilder.Entity<FinanceMonthlyBudget>()
                .Property(x => x.CostCenter)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceMonthlyBudget>()
                .Property(x => x.BudgetLimitCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceMonthlyBudget>()
                .HasIndex(x => new { x.CostCenter, x.Year, x.Month })
                .IsUnique();

            modelBuilder.Entity<FinanceCostCenter>()
                .Property(x => x.Area)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceCostCenter>()
                .Property(x => x.CostCenterNumber)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceCostCenter>()
                .Property(x => x.CostCenterName)
                .HasMaxLength(150);

            modelBuilder.Entity<FinanceCostCenter>()
                .Property(x => x.MonthlyBudgetCordoba)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceCostCenter>()
                .HasIndex(x => x.CostCenterNumber)
                .IsUnique();

            modelBuilder.Entity<FinanceCostCenter>()
                .HasIndex(x => new { x.Area, x.IsActive });

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.InvoiceNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.CounterpartyName)
                .HasMaxLength(150);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.CounterpartyTaxId)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.Concept)
                .HasMaxLength(1200);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.Currency)
                .HasMaxLength(10);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.Notes)
                .HasMaxLength(700);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.SignatureToken)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.SignedByName)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.SignatureDeviceInfo)
                .HasMaxLength(300);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.ApprovalStatus)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.ApprovedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceInvoice>()
                .Property(x => x.ApprovalNotes)
                .HasMaxLength(700);

            modelBuilder.Entity<FinanceInvoice>()
                .HasIndex(x => x.InvoiceNumber)
                .IsUnique();

            modelBuilder.Entity<FinanceInvoice>()
                .HasIndex(x => x.SignatureToken)
                .IsUnique();

            modelBuilder.Entity<FinanceInvoice>()
                .HasIndex(x => new { x.Site, x.InvoiceDateUtc });

            modelBuilder.Entity<FinanceInvoice>()
                .HasIndex(x => new { x.ApprovalStatus, x.InvoiceDateUtc });

            modelBuilder.Entity<FinanceInvoice>()
                .HasOne(x => x.Counterparty)
                .WithMany()
                .HasForeignKey(x => x.CounterpartyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.ReceiptNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.ReceivedFrom)
                .HasMaxLength(150);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.CounterpartyTaxId)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.Concept)
                .HasMaxLength(1200);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.Currency)
                .HasMaxLength(10);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.Notes)
                .HasMaxLength(700);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.SignatureToken)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.SignedByName)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.SignatureDeviceInfo)
                .HasMaxLength(300);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.ApprovalStatus)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.ApprovedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceReceipt>()
                .Property(x => x.ApprovalNotes)
                .HasMaxLength(700);

            modelBuilder.Entity<FinanceReceipt>()
                .HasIndex(x => x.ReceiptNumber)
                .IsUnique();

            modelBuilder.Entity<FinanceReceipt>()
                .HasIndex(x => x.SignatureToken)
                .IsUnique();

            modelBuilder.Entity<FinanceReceipt>()
                .HasIndex(x => new { x.Site, x.ReceiptDateUtc });

            modelBuilder.Entity<FinanceReceipt>()
                .HasIndex(x => new { x.ApprovalStatus, x.ReceiptDateUtc });

            modelBuilder.Entity<FinanceReceipt>()
                .HasOne(x => x.Counterparty)
                .WithMany()
                .HasForeignKey(x => x.CounterpartyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.Name)
                .HasMaxLength(150);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.Type)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.TaxId)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.Phone)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.Email)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceCounterparty>()
                .Property(x => x.Address)
                .HasMaxLength(300);

            modelBuilder.Entity<FinanceCounterparty>()
                .HasIndex(x => new { x.Name, x.Type });

            modelBuilder.Entity<FinanceAttachment>()
                .Property(x => x.DocumentType)
                .HasMaxLength(40);

            modelBuilder.Entity<FinanceAttachment>()
                .Property(x => x.FileName)
                .HasMaxLength(260);

            modelBuilder.Entity<FinanceAttachment>()
                .Property(x => x.StoredPath)
                .HasMaxLength(500);

            modelBuilder.Entity<FinanceAttachment>()
                .Property(x => x.ContentType)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceAttachment>()
                .Property(x => x.UploadedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceAttachment>()
                .HasIndex(x => new { x.DocumentType, x.DocumentId });

            modelBuilder.Entity<FinanceAuditLog>()
                .Property(x => x.EntityName)
                .HasMaxLength(80);

            modelBuilder.Entity<FinanceAuditLog>()
                .Property(x => x.Action)
                .HasMaxLength(80);

            modelBuilder.Entity<FinanceAuditLog>()
                .Property(x => x.PerformedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<FinanceAuditLog>()
                .Property(x => x.Details)
                .HasMaxLength(2000);

            modelBuilder.Entity<FinanceAuditLog>()
                .HasIndex(x => new { x.EntityName, x.EntityId, x.PerformedAtUtc });

            modelBuilder.Entity<FinanceSetting>()
                .Property(x => x.UsdToCordobaRate)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<HsIncident>()
                .HasIndex(x => x.IncidentNumber)
                .IsUnique();

            modelBuilder.Entity<HsIncident>()
                .HasIndex(x => new { x.Status, x.Severity, x.OccurredAtUtc });

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.IncidentNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Area)
                .HasMaxLength(120);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.ReportedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.IncidentType)
                .HasMaxLength(60);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Severity)
                .HasMaxLength(40);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Status)
                .HasMaxLength(40);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Description)
                .HasMaxLength(2000);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.ImmediateAction)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.RootCause)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.ImmediateCause)
                .HasMaxLength(1200);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.FiveWhyAnalysis)
                .HasMaxLength(1200);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Witnesses)
                .HasMaxLength(800);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.Investigator)
                .HasMaxLength(120);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.RiskLevel)
                .HasMaxLength(40);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.ResidualRiskLevel)
                .HasMaxLength(40);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.CurrentControls)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.RecommendedControls)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.EvidencePath)
                .HasMaxLength(500);

            modelBuilder.Entity<HsIncident>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsInspection>()
                .HasIndex(x => x.InspectionNumber)
                .IsUnique();

            modelBuilder.Entity<HsInspection>()
                .HasIndex(x => new { x.Site, x.Area, x.InspectionDateUtc });

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.InspectionNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Area)
                .HasMaxLength(120);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.EquipmentCode)
                .HasMaxLength(80);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Inspector)
                .HasMaxLength(120);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Category)
                .HasMaxLength(80);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.ScorePercent)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Status)
                .HasMaxLength(40);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.Notes)
                .HasMaxLength(2000);

            modelBuilder.Entity<HsInspection>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsCorrectiveAction>()
                .HasIndex(x => new { x.Status, x.Priority, x.DueDateUtc });

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.SourceType)
                .HasMaxLength(40);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.Title)
                .HasMaxLength(180);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.Description)
                .HasMaxLength(2000);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.Owner)
                .HasMaxLength(120);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.Priority)
                .HasMaxLength(40);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.Status)
                .HasMaxLength(40);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.ClosureNotes)
                .HasMaxLength(1200);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.ReviewedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.ApprovedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.ApprovalNotes)
                .HasMaxLength(1200);

            modelBuilder.Entity<HsCorrectiveAction>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsTrainingRecord>()
                .HasIndex(x => new { x.Site, x.TrainingDateUtc });

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Topic)
                .HasMaxLength(160);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Trainer)
                .HasMaxLength(120);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Audience)
                .HasMaxLength(120);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.EmployeeName)
                .HasMaxLength(120);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.EmployeeCode)
                .HasMaxLength(60);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Position)
                .HasMaxLength(120);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Result)
                .HasMaxLength(40);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.EvidencePath)
                .HasMaxLength(500);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.Notes)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsTrainingRecord>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsWorkPermit>()
                .HasIndex(x => x.PermitNumber)
                .IsUnique();

            modelBuilder.Entity<HsWorkPermit>()
                .HasIndex(x => new { x.Status, x.StartAtUtc });

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.PermitNumber)
                .HasMaxLength(30);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.PermitType)
                .HasMaxLength(80);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.Area)
                .HasMaxLength(120);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.RequestedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.Status)
                .HasMaxLength(40);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.ApprovedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.Controls)
                .HasMaxLength(1500);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.ChecklistNotes)
                .HasMaxLength(1200);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.EvidencePaths)
                .HasMaxLength(2000);

            modelBuilder.Entity<HsWorkPermit>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            modelBuilder.Entity<HsDocument>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<HsDocument>()
                .HasIndex(x => new { x.DocumentType, x.Status, x.ReviewDateUtc });

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.Title)
                .HasMaxLength(160);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.DocumentType)
                .HasMaxLength(80);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.Code)
                .HasMaxLength(40);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.Version)
                .HasMaxLength(30);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.Site)
                .HasMaxLength(100);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.Status)
                .HasMaxLength(40);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.FilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<HsDocument>()
                .Property(x => x.CreatedBy)
                .HasMaxLength(120);

            // SQL Server stores DateTime values without a timezone and the existing
            // application intentionally mixes local and UTC timestamps. Preserve that
            // behavior on PostgreSQL instead of rejecting non-UTC DateTime values.
            if (Database.IsNpgsql())
            {
                foreach (var property in modelBuilder.Model
                    .GetEntityTypes()
                    .SelectMany(entityType => entityType.GetProperties())
                    .Where(property => property.ClrType == typeof(DateTime) ||
                                       property.ClrType == typeof(DateTime?)))
                {
                    property.SetColumnType("timestamp without time zone");
                }
            }
        }
    }
}
