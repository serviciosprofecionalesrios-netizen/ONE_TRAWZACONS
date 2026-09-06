using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EnvironmentOrLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AccessUrlOrHost = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Port = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Username = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceApprovalRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MinAmountCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmountCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    EscalationHours = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCostCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CostCenterNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CostCenterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MonthlyBudgetCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AlertThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    HardStopOnOverrun = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAuthorizationOnOverrun = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCostCenters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCounterparties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCounterparties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceMonthlyBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostCenter = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    BudgetLimitCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HardStopOnOverrun = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceMonthlyBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsdToCordobaRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DefaultEscalationHours = table.Column<int>(type: "integer", nullable: false),
                    DefaultAlertThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsCorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosureNotes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsCorrectiveActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EffectiveDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReportedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IncidentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImmediateAction = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    RootCause = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    ImmediateCause = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    FiveWhyAnalysis = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    Witnesses = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Investigator = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InvestigationDueDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InvestigationClosedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RiskProbability = table.Column<int>(type: "integer", nullable: false),
                    RiskSeverity = table.Column<int>(type: "integer", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResidualRiskProbability = table.Column<int>(type: "integer", nullable: false),
                    ResidualRiskSeverity = table.Column<int>(type: "integer", nullable: false),
                    ResidualRiskScore = table.Column<int>(type: "integer", nullable: false),
                    ResidualRiskLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CurrentControls = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    RecommendedControls = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    LostTime = table.Column<bool>(type: "boolean", nullable: false),
                    LostDays = table.Column<int>(type: "integer", nullable: false),
                    EvidencePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InspectionNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InspectionDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EquipmentCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Inspector = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScorePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    FindingsCount = table.Column<int>(type: "integer", nullable: false),
                    CriticalFindingsCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrainingDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Trainer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Audience = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmployeeCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Position = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AttendeeCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredTraining = table.Column<bool>(type: "boolean", nullable: false),
                    ExpirationDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EvidencePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsWorkPermits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermitNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PermitType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovalResponseDueAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Controls = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    HasRiskAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    HasAreaIsolation = table.Column<bool>(type: "boolean", nullable: false),
                    HasPpe = table.Column<bool>(type: "boolean", nullable: false),
                    HasEmergencyPlan = table.Column<bool>(type: "boolean", nullable: false),
                    HasSupervisorApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ChecklistNotes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    EvidencePaths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsWorkPermits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subcategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Brand = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TechnicalSpecifications = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Condition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Site = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AssignedToEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CorporatePhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WarrantyEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Supplier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OperatingSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OfficeVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Antivirus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastMaintenanceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextMaintenanceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RequestingUser = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tenencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaintenanceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AssetOrArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedTechnician = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TechnicianCategory = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RecipientUsers = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAppointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceInventoryParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PartName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subcategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SystemType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EquipmentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CompatibleModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tenencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    MinimumStock = table.Column<int>(type: "integer", nullable: false),
                    QuantityIssued = table.Column<int>(type: "integer", nullable: false),
                    QuantityInTransit = table.Column<int>(type: "integer", nullable: false),
                    LastIssueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StockStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Supplier = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UnitCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    UnitCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ShelfLocation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceInventoryParts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTechnicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Shift = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaxActiveOrders = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmergencyRelationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTechnicians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesIngresoEquipoGondolaSolicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Asunto = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TareaPadre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Prioridad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesIngresoEquipoGondolaSolicitudes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesIngresoEquipoGondolaTareasPadre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitanteKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TareaPadre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TipoOrigen = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UltimoTipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UltimoAsunto = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesIngresoEquipoGondolaTareasPadre", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesIngresoPersonalSolicitudes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Asunto = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TareaPadre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Prioridad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesIngresoPersonalSolicitudes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesIngresoPersonalTareasPadre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitanteKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TareaPadre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TipoOrigen = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UltimoTipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UltimoAsunto = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesIngresoPersonalTareasPadre", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesSeguimientoCargas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    FechaOperativa = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesSeguimientoCargas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacionesSeguimientoRegistros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FechaOperativa = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaEvento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Equipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Procedencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ruta = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Toneladas = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Viajes = table.Column<int>(type: "integer", nullable: false),
                    CombustibleGalones = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PesoSugerido = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Conductor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Estado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Eficiencia = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesSeguimientoRegistros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RequestingUser = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tenencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UnitCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DamagedElement = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FailureOdometerKm = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExitOdometerKm = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IncidentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedTechnician = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CostCenterCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TechnicianCategory = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    MaintenanceStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SLADeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AttachmentPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeEvidencePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AfterEvidencePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitialConditionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RepairActionsPerformed = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RootCause = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SparePartRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SparePartPurchased = table.Column<bool>(type: "boolean", nullable: false),
                    SparePartDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComponentChanged = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedComponentName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ChangedComponentCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ChangedComponentCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ChangedComponentCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LaborCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LaborCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExternalCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExternalCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RequiresCostApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CostApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CostApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CostApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CostApprovalNotes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    FinanceAccountingEntryNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FinancePurchaseOrderNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FinanceInvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FinancePaymentDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinanceFinalPaymentMethod = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FinanceReconciliationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FinanceReturnedForCorrection = table.Column<bool>(type: "boolean", nullable: false),
                    FinanceReturnedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinanceReturnedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FinanceReturnReason = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    FinanceEscalatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinanceEscalatedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FinanceErpExportedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinanceErpExportReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TechnicalTestsPerformed = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserConformityConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PreventiveRecommendations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TechnicalSheetPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExitOrderPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CounterpartyId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CounterpartyName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CounterpartyTaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Concept = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InvoiceDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SignatureToken = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SignedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignatureDeviceInfo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_FinanceCounterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "FinanceCounterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CounterpartyId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedFrom = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CounterpartyTaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Concept = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReceiptDateUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SignatureToken = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SignedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignatureDeviceInfo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceReceipts_FinanceCounterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "FinanceCounterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    ChangeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FieldChanged = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketHistories_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketSparePartDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceInventoryPartId = table.Column<int>(type: "integer", nullable: false),
                    PartCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PartName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    QuantityDispatched = table.Column<int>(type: "integer", nullable: false),
                    UnitCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalCostCordoba = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    UnitCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalCostUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSparePartDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSparePartDispatches_MaintenanceInventoryParts_Mainten~",
                        column: x => x.MaintenanceInventoryPartId,
                        principalTable: "MaintenanceInventoryParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketSparePartDispatches_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Name_Type",
                table: "Credentials",
                columns: new[] { "Name", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceApprovalRules_IsActive_RequestType_CostCenter_MinAmo~",
                table: "FinanceApprovalRules",
                columns: new[] { "IsActive", "RequestType", "CostCenter", "MinAmountCordoba", "MaxAmountCordoba" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAttachments_DocumentType_DocumentId",
                table: "FinanceAttachments",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAuditLogs_EntityName_EntityId_PerformedAtUtc",
                table: "FinanceAuditLogs",
                columns: new[] { "EntityName", "EntityId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCostCenters_Area_IsActive",
                table: "FinanceCostCenters",
                columns: new[] { "Area", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCostCenters_CostCenterNumber",
                table: "FinanceCostCenters",
                column: "CostCenterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCounterparties_Name_Type",
                table: "FinanceCounterparties",
                columns: new[] { "Name", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_ApprovalStatus_InvoiceDateUtc",
                table: "FinanceInvoices",
                columns: new[] { "ApprovalStatus", "InvoiceDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_CounterpartyId",
                table: "FinanceInvoices",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_InvoiceNumber",
                table: "FinanceInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_SignatureToken",
                table: "FinanceInvoices",
                column: "SignatureToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_Site_InvoiceDateUtc",
                table: "FinanceInvoices",
                columns: new[] { "Site", "InvoiceDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceMonthlyBudgets_CostCenter_Year_Month",
                table: "FinanceMonthlyBudgets",
                columns: new[] { "CostCenter", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_ApprovalStatus_ReceiptDateUtc",
                table: "FinanceReceipts",
                columns: new[] { "ApprovalStatus", "ReceiptDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_CounterpartyId",
                table: "FinanceReceipts",
                column: "CounterpartyId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_ReceiptNumber",
                table: "FinanceReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_SignatureToken",
                table: "FinanceReceipts",
                column: "SignatureToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_Site_ReceiptDateUtc",
                table: "FinanceReceipts",
                columns: new[] { "Site", "ReceiptDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsCorrectiveActions_Status_Priority_DueDateUtc",
                table: "HsCorrectiveActions",
                columns: new[] { "Status", "Priority", "DueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsDocuments_Code",
                table: "HsDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsDocuments_DocumentType_Status_ReviewDateUtc",
                table: "HsDocuments",
                columns: new[] { "DocumentType", "Status", "ReviewDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_IncidentNumber",
                table: "HsIncidents",
                column: "IncidentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_Status_Severity_OccurredAtUtc",
                table: "HsIncidents",
                columns: new[] { "Status", "Severity", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_InspectionNumber",
                table: "HsInspections",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_Site_Area_InspectionDateUtc",
                table: "HsInspections",
                columns: new[] { "Site", "Area", "InspectionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsTrainingRecords_Site_TrainingDateUtc",
                table: "HsTrainingRecords",
                columns: new[] { "Site", "TrainingDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HsWorkPermits_PermitNumber",
                table: "HsWorkPermits",
                column: "PermitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsWorkPermits_Status_StartAtUtc",
                table: "HsWorkPermits",
                columns: new[] { "Status", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_AssetCode",
                table: "InventoryItems",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAppointments_AppointmentNumber",
                table: "MaintenanceAppointments",
                column: "AppointmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInventoryParts_PartCode",
                table: "MaintenanceInventoryParts",
                column: "PartCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_Category_IsAvailable_IsActive",
                table: "MaintenanceTechnicians",
                columns: new[] { "Category", "IsAvailable", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_Category_Shift_IsAvailable_IsActive",
                table: "MaintenanceTechnicians",
                columns: new[] { "Category", "Shift", "IsAvailable", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTechnicians_FullName",
                table: "MaintenanceTechnicians",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoEquipoGondolaSolicitudes_UpdatedAt",
                table: "OperacionesIngresoEquipoGondolaSolicitudes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoEquipoGondolaTareasPadre_SolicitanteKey",
                table: "OperacionesIngresoEquipoGondolaTareasPadre",
                column: "SolicitanteKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoEquipoGondolaTareasPadre_TareaPadre",
                table: "OperacionesIngresoEquipoGondolaTareasPadre",
                column: "TareaPadre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoPersonalSolicitudes_UpdatedAt",
                table: "OperacionesIngresoPersonalSolicitudes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoPersonalTareasPadre_SolicitanteKey",
                table: "OperacionesIngresoPersonalTareasPadre",
                column: "SolicitanteKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesIngresoPersonalTareasPadre_TareaPadre",
                table: "OperacionesIngresoPersonalTareasPadre",
                column: "TareaPadre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesSeguimientoRegistros_FechaOperativa",
                table: "OperacionesSeguimientoRegistros",
                column: "FechaOperativa");

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_TicketId",
                table: "TicketHistories",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Department_MaintenanceStage_Status",
                table: "Tickets",
                columns: new[] { "Department", "MaintenanceStage", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketNumber",
                table: "Tickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSparePartDispatches_MaintenanceInventoryPartId",
                table: "TicketSparePartDispatches",
                column: "MaintenanceInventoryPartId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSparePartDispatches_TicketId",
                table: "TicketSparePartDispatches",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropTable(
                name: "FinanceApprovalRules");

            migrationBuilder.DropTable(
                name: "FinanceAttachments");

            migrationBuilder.DropTable(
                name: "FinanceAuditLogs");

            migrationBuilder.DropTable(
                name: "FinanceCostCenters");

            migrationBuilder.DropTable(
                name: "FinanceInvoices");

            migrationBuilder.DropTable(
                name: "FinanceMonthlyBudgets");

            migrationBuilder.DropTable(
                name: "FinanceReceipts");

            migrationBuilder.DropTable(
                name: "FinanceSettings");

            migrationBuilder.DropTable(
                name: "HsCorrectiveActions");

            migrationBuilder.DropTable(
                name: "HsDocuments");

            migrationBuilder.DropTable(
                name: "HsIncidents");

            migrationBuilder.DropTable(
                name: "HsInspections");

            migrationBuilder.DropTable(
                name: "HsTrainingRecords");

            migrationBuilder.DropTable(
                name: "HsWorkPermits");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "MaintenanceAppointments");

            migrationBuilder.DropTable(
                name: "MaintenanceTechnicians");

            migrationBuilder.DropTable(
                name: "OperacionesIngresoEquipoGondolaSolicitudes");

            migrationBuilder.DropTable(
                name: "OperacionesIngresoEquipoGondolaTareasPadre");

            migrationBuilder.DropTable(
                name: "OperacionesIngresoPersonalSolicitudes");

            migrationBuilder.DropTable(
                name: "OperacionesIngresoPersonalTareasPadre");

            migrationBuilder.DropTable(
                name: "OperacionesSeguimientoCargas");

            migrationBuilder.DropTable(
                name: "OperacionesSeguimientoRegistros");

            migrationBuilder.DropTable(
                name: "TicketHistories");

            migrationBuilder.DropTable(
                name: "TicketSparePartDispatches");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "FinanceCounterparties");

            migrationBuilder.DropTable(
                name: "MaintenanceInventoryParts");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
