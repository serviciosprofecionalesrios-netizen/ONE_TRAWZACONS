using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ITServiceDeskApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class MaintenanceOrderPdfReportService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp"
        };

        public static byte[] GeneratePdf(Ticket ticket, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var generatedAt = DateTime.Now;

            var orderNumber = Safe(ticket.TicketNumber) ?? $"TK-{ticket.Id:D4}";
            var createdLocal = ticket.CreatedDate.ToLocalTime();
            var closedLocal = ticket.ClosedDate?.ToLocalTime();
            var slaLocal = ticket.SLADeadline.ToLocalTime();
            var statusLabel = GetStatusLabel(ticket.Status);
            var priorityLabel = GetPriorityLabel(ticket.Priority);
            var slaLabel = CalculateSlaCompliance(ticket, generatedAt);
            var resolutionTime = closedLocal.HasValue
                ? FormatDuration(closedLocal.Value - createdLocal)
                : FormatDuration(generatedAt - createdLocal);

            var kmIngreso = FormatDecimal(ticket.FailureOdometerKm, " km");
            var kmSalida = FormatDecimal(ticket.ExitOdometerKm, " km");
            var kmPostReparacion = (ticket.ExitOdometerKm.HasValue && ticket.FailureOdometerKm.HasValue)
                ? $"{Math.Max(0, ticket.ExitOdometerKm.Value - ticket.FailureOdometerKm.Value):N2} km"
                : "No aplica";

            var generalRows = new List<RowItem>
            {
                new("Numero de orden", orderNumber),
                new("Fecha de creacion", createdLocal.ToString("dd/MM/yyyy HH:mm")),
                new("Departamento", "Mantenimiento"),
                new("Solicitante", Safe(ticket.RequestingUser) ?? "No especificado"),
                new("Sitio / Ubicacion", Safe(ticket.Site) ?? "No especificado"),
                new("Tenencia", Safe(ticket.Tenencia) ?? "No especificado"),
                new("Tipo de orden", Safe(ticket.IncidentType) ?? "No especificado"),
                new("Etapa de taller", Safe(ticket.MaintenanceStage) ?? "Recibida"),
                new("Prioridad", priorityLabel),
                new("Fecha compromiso (SLA)", slaLocal.ToString("dd/MM/yyyy HH:mm")),
                new("Tecnico asignado", Safe(ticket.AssignedTechnician) ?? "Sin asignar"),
                new("Estado", statusLabel),
                new("Cumplimiento SLA", slaLabel)
            };

            var failureRows = new List<RowItem>
            {
                new("Unidad / Equipo", Safe(ticket.UnitCode) ?? "No especificado"),
                new("Categoria de falla", Safe(ticket.FailureCategory) ?? "No especificado"),
                new("Elemento danado", Safe(ticket.DamagedElement) ?? "No especificado"),
                new("KM al ingreso por falla", kmIngreso),
                new("KM de salida de taller", kmSalida),
                new("KM recorridos post-reparacion", kmPostReparacion)
            };

            var technicalRows = new List<RowItem>
            {
                new("Condicion inicial del equipo", Safe(ticket.InitialConditionNotes) ?? "Sin detalle"),
                new("Reparaciones realizadas", Safe(ticket.RepairActionsPerformed) ?? "Sin detalle"),
                new("Causa raiz detectada", Safe(ticket.RootCause) ?? "Sin detalle"),
                new("Pruebas tecnicas realizadas", Safe(ticket.TechnicalTestsPerformed) ?? "Sin detalle"),
                new("Se requirio repuesto", ticket.SparePartRequired ? "Si" : "No"),
                new("Detalle de repuesto", Safe(ticket.SparePartDetails) ?? "No aplica"),
                new("Se realizo cambio de componente", ticket.ComponentChanged ? "Si" : "No"),
                new("Usuario confirma solucion aplicada", ticket.UserConformityConfirmed ? "Si" : "No"),
                new("Recomendaciones preventivas", Safe(ticket.PreventiveRecommendations) ?? "Sin recomendaciones"),
                new("Observaciones", Safe(ticket.Observations) ?? "Sin observaciones")
            };

            var dispatchRows = (ticket.SparePartDispatches ?? Enumerable.Empty<TicketSparePartDispatch>())
                .OrderBy(x => x.PartName)
                .Select(x => new SpareDispatchRow(
                    PartLabel: $"{x.PartCode} - {x.PartName}",
                    Quantity: x.QuantityDispatched,
                    UnitCostCordoba: x.UnitCostCordoba ?? 0m,
                    TotalCostCordoba: x.TotalCostCordoba ?? 0m,
                    UnitCostUsd: x.UnitCostUsd ?? 0m,
                    TotalCostUsd: x.TotalCostUsd ?? 0m))
                .ToList();

            var totalSpareCordoba = dispatchRows.Sum(x => x.TotalCostCordoba);
            var totalSpareUsd = dispatchRows.Sum(x => x.TotalCostUsd);
            var totalCostCordoba = totalSpareCordoba +
                                   (ticket.ChangedComponentCostCordoba ?? 0m) +
                                   (ticket.LaborCostCordoba ?? 0m) +
                                   (ticket.ExternalCostCordoba ?? 0m);
            var totalCostUsd = totalSpareUsd +
                               (ticket.ChangedComponentCostUsd ?? 0m) +
                               (ticket.LaborCostUsd ?? 0m) +
                               (ticket.ExternalCostUsd ?? 0m);

            var costRows = new List<RowItem>
            {
                new("Repuestos despachados (C$)", $"C$ {totalSpareCordoba:N2}"),
                new("Repuestos despachados ($)", $"$ {totalSpareUsd:N2}"),
                new("Costo componente (C$)", $"C$ {(ticket.ChangedComponentCostCordoba ?? 0m):N2}"),
                new("Costo componente ($)", $"$ {(ticket.ChangedComponentCostUsd ?? 0m):N2}"),
                new("Mano de obra (C$)", $"C$ {(ticket.LaborCostCordoba ?? 0m):N2}"),
                new("Mano de obra ($)", $"$ {(ticket.LaborCostUsd ?? 0m):N2}"),
                new("Servicio externo (C$)", $"C$ {(ticket.ExternalCostCordoba ?? 0m):N2}"),
                new("Servicio externo ($)", $"$ {(ticket.ExternalCostUsd ?? 0m):N2}"),
                new("Total estimado (C$)", $"C$ {totalCostCordoba:N2}"),
                new("Total estimado ($)", $"$ {totalCostUsd:N2}"),
                new("Requiere aprobacion", ticket.RequiresCostApproval ? "Si" : "No"),
                new("Costo aprobado", ticket.CostApproved ? "Si" : "No"),
                new("Aprobado por", Safe(ticket.CostApprovedBy) ?? "No aplica"),
                new("Fecha aprobacion", ticket.CostApprovedAtUtc.HasValue
                    ? ticket.CostApprovedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : "No aplica"),
                new("Notas aprobacion", Safe(ticket.CostApprovalNotes) ?? "Sin notas")
            };

            var evidences = new List<EvidenceData>
            {
                BuildEvidence("Evidencia inicial", ticket.BeforeEvidencePath, webRootPath),
                BuildEvidence("Evidencia final", ticket.AfterEvidencePath, webRootPath),
                BuildEvidence("Evidencia adjunta", ticket.AttachmentPath, webRootPath),
                BuildEvidence("Ficha tecnica", ticket.TechnicalSheetPath, webRootPath),
                BuildEvidence("Orden de salida", ticket.ExitOrderPath, webRootPath)
            };

            var historyRows = (ticket.HistoryEntries ?? Enumerable.Empty<TicketHistory>())
                .OrderBy(h => h.ChangeDate)
                .Select(h => new HistoryRow(
                    h.ChangeDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Safe(h.FieldChanged) ?? "Campo",
                    Safe(h.OldValue) ?? "(vacio)",
                    Safe(h.NewValue) ?? "(vacio)",
                    Safe(h.ChangedBy) ?? "Sistema"))
                .ToList();

            var logoBytes = TryLoadLogo(webRootPath);
            var executiveSummary =
                $"Orden {orderNumber} creada el {createdLocal:dd/MM/yyyy HH:mm}. " +
                $"Estado actual: {statusLabel}. SLA: {slaLabel} (limite {slaLocal:dd/MM/yyyy HH:mm}). " +
                $"Tecnico responsable: {Safe(ticket.AssignedTechnician) ?? "Sin asignar"}.";

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header =>
                        ComposeHeader(header, logoBytes, generatedAt, orderNumber));
                    page.Content().Element(content =>
                        ComposeContent(
                            content,
                            executiveSummary,
                            statusLabel,
                            priorityLabel,
                            slaLabel,
                            resolutionTime,
                            ticket.Description,
                            generalRows,
                            failureRows,
                            technicalRows,
                            costRows,
                            dispatchRows,
                            totalSpareCordoba,
                            totalSpareUsd,
                            evidences,
                            historyRows));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(
            IContainer container,
            byte[]? logoBytes,
            DateTime generatedAt,
            string orderNumber)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(64).AlignMiddle().AlignCenter().Element(slot =>
                    {
                        if (logoBytes != null)
                        {
                            slot.Image(logoBytes).FitArea();
                        }
                        else
                        {
                            slot.Border(1).BorderColor(Colors.Grey.Lighten2)
                                .AlignCenter()
                                .AlignMiddle()
                                .Text("SIN LOGO")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(column =>
                    {
                        column.Item().Text("TRAWZACONS")
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Service Desk - Orden de Mantenimiento")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text($"Orden {orderNumber}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(170).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("FORMULARIO ORDEN DE MANTENIMIENTO").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            string executiveSummary,
            string statusLabel,
            string priorityLabel,
            string slaLabel,
            string resolutionTime,
            string? description,
            IReadOnlyList<RowItem> generalRows,
            IReadOnlyList<RowItem> failureRows,
            IReadOnlyList<RowItem> technicalRows,
            IReadOnlyList<RowItem> costRows,
            IReadOnlyList<SpareDispatchRow> dispatchRows,
            decimal totalSpareCordoba,
            decimal totalSpareUsd,
            IReadOnlyList<EvidenceData> evidences,
            IReadOnlyList<HistoryRow> historyRows)
        {
            container.PaddingTop(12).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Formulario de Nueva Orden de Mantenimiento",
                    body =>
                    {
                        body.Column(inner =>
                        {
                            inner.Spacing(8);
                            inner.Item().Text(executiveSummary).FontSize(10).FontColor(Colors.Grey.Darken3);
                            inner.Item().Row(row =>
                            {
                                row.Spacing(8);
                                row.RelativeItem().Element(card => ComposeMetricCard(card, "Estado", statusLabel, "#0B6E4F"));
                                row.RelativeItem().Element(card => ComposeMetricCard(card, "Cumplimiento SLA", slaLabel, slaLabel == "Vencido" ? "#11439A" : "#0B6E4F"));
                                row.RelativeItem().Element(card => ComposeMetricCard(card, "Prioridad", priorityLabel, "#D97706"));
                                row.RelativeItem().Element(card => ComposeMetricCard(card, "Tiempo de atencion", resolutionTime, "#2563EB"));
                            });
                        });
                    }));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Datos Generales de la Orden",
                    body => ComposeKeyValueTable(body, generalRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Detalle de Falla y Unidad",
                    body => ComposeKeyValueTable(body, failureRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Descripcion del trabajo",
                    body => body.Text(Safe(description) ?? "Sin detalle de falla reportada.").FontSize(10)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Repuestos despachados para esta orden",
                    body => ComposeSparePartsTable(body, dispatchRows, totalSpareCordoba, totalSpareUsd)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Bloque Tecnico",
                    body => ComposeKeyValueTable(body, technicalRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Control de costos y aprobacion",
                    body => ComposeKeyValueTable(body, costRows)));

                column.Item().Element(section => ComposeSectionCard(
                    section,
                    "Evidencia Tecnica",
                    body => ComposeEvidenceSection(body, evidences)));

                if (historyRows.Count > 0)
                {
                    column.Item().Element(section => ComposeSectionCard(
                        section,
                        "Historial de cambios",
                        body => ComposeHistoryTable(body, historyRows)));
                }
            });
        }

        private static void ComposeSectionCard(IContainer container, string title, Action<IContainer> bodyComposer)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.White)
                .Padding(10)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(4).Height(16).Background("#11439A");
                        row.RelativeItem().PaddingLeft(8).Text(title)
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);
                    });

                    column.Item().PaddingTop(8).Element(bodyComposer);
                });
        }

        private static void ComposeMetricCard(IContainer container, string label, string value, string accentColor)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background("#FAFAFA")
                .Padding(8)
                .Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(value).FontSize(12).SemiBold().FontColor(accentColor);
                });
        }

        private static void ComposeKeyValueTable(IContainer container, IReadOnlyList<RowItem> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(CellLabel).Text(row.Label);
                    table.Cell().Element(CellValue).Text(row.Value);
                }
            });
        }

        private static void ComposeSparePartsTable(
            IContainer container,
            IReadOnlyList<SpareDispatchRow> rows,
            decimal totalSpareCordoba,
            decimal totalSpareUsd)
        {
            if (rows.Count == 0)
            {
                container.Text("Sin repuestos despachados en esta orden.")
                    .FontColor(Colors.Grey.Darken1)
                    .FontSize(9);
                return;
            }

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Articulo");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Cant.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Unit C$");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total C$");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Unit $");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Total $");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.PartLabel);
                    table.Cell().Element(BodyCell).AlignRight().Text(row.Quantity.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCostCordoba.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCostCordoba.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCostUsd.ToString("N2", CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCostUsd.ToString("N2", CultureInfo.InvariantCulture));
                }

                table.Cell().ColumnSpan(3).Element(BodyCell).AlignRight().Text("Total orden").SemiBold();
                table.Cell().Element(BodyCell).AlignRight().Text($"C$ {totalSpareCordoba:N2}").SemiBold();
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).AlignRight().Text($"$ {totalSpareUsd:N2}").SemiBold();
            });
        }

        private static void ComposeEvidenceSection(IContainer container, IReadOnlyList<EvidenceData> evidences)
        {
            if (evidences.Count == 0)
            {
                container.Text("No hay evidencias adjuntas para esta orden.")
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            container.Column(column =>
            {
                column.Spacing(8);

                foreach (var evidence in evidences)
                {
                    column.Item().Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(8)
                        .Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text(evidence.Title).SemiBold().FontSize(10);

                            if (evidence.ImageBytes != null)
                            {
                                card.Item()
                                    .Height(180)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten3)
                                    .Padding(3)
                                    .Image(evidence.ImageBytes)
                                    .FitArea();
                            }
                            else
                            {
                                card.Item().Text("No hay imagen disponible para esta evidencia.")
                                    .FontColor(Colors.Grey.Darken1)
                                    .FontSize(9);
                            }

                            card.Item().Text(evidence.Description)
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        });
                }
            });
        }

        private static void ComposeHistoryTable(IContainer container, IReadOnlyList<HistoryRow> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Fecha");
                    header.Cell().Element(HeaderCell).Text("Campo");
                    header.Cell().Element(HeaderCell).Text("Valor anterior");
                    header.Cell().Element(HeaderCell).Text("Valor nuevo");
                    header.Cell().Element(HeaderCell).Text("Usuario");
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(BodyCell).Text(row.ChangeDate);
                    table.Cell().Element(BodyCell).Text(row.Field);
                    table.Cell().Element(BodyCell).Text(row.OldValue);
                    table.Cell().Element(BodyCell).Text(row.NewValue);
                    table.Cell().Element(BodyCell).Text(row.ChangedBy);
                }
            });
        }

        private static void ComposeFooter(IContainer container, DateTime generatedAt)
        {
            container.BorderTop(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingTop(5)
                .Row(row =>
                {
                    row.RelativeItem().Text($"Confidencial | {generatedAt:dd/MM/yyyy HH:mm}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);

                    row.ConstantItem(120).AlignRight().Text(text =>
                    {
                        text.Span("Pagina ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).SemiBold();
                        text.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).SemiBold();
                    });
                });
        }

        private static IContainer CellLabel(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .PaddingRight(8)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor(Colors.Grey.Darken2));
        }

        private static IContainer CellValue(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .DefaultTextStyle(TextStyle.Default.FontColor(Colors.Grey.Darken4));
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#11439A")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#11439A"));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(9));
        }

        private static EvidenceData BuildEvidence(string title, string? webPath, string webRootPath)
        {
            if (string.IsNullOrWhiteSpace(webPath))
            {
                return new EvidenceData(title, null, "No se adjunto archivo.");
            }

            var fullPath = ResolvePhysicalPath(webRootPath, webPath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                return new EvidenceData(title, null, $"Archivo no encontrado: {webPath}");
            }

            if (!ImageExtensions.Contains(Path.GetExtension(fullPath)))
            {
                return new EvidenceData(title, null, $"Archivo adjunto no es imagen: {webPath}");
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                return new EvidenceData(title, bytes, $"Archivo: {webPath}");
            }
            catch
            {
                return new EvidenceData(title, null, $"No se pudo leer el archivo: {webPath}");
            }
        }

        private static byte[]? TryLoadLogo(string webRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(webRootPath, "images", "logo-trawzacons-corporativo.png"),
                Path.Combine(webRootPath, "images", "logo-trawzacons.png")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return File.ReadAllBytes(path);
                }
                catch
                {
                    // Ignore and keep trying.
                }
            }

            return null;
        }

        private static string? ResolvePhysicalPath(string webRootPath, string webPath)
        {
            if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(webPath))
            {
                return null;
            }

            var relative = webPath
                .Trim()
                .TrimStart('~')
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(webRootPath, relative);
        }

        private static string CalculateSlaCompliance(Ticket ticket, DateTime nowLocal)
        {
            var sla = ticket.SLADeadline.ToLocalTime();

            if (ticket.Status == TicketStatus.Closed)
            {
                if (!ticket.ClosedDate.HasValue)
                {
                    return "No definido";
                }

                return ticket.ClosedDate.Value.ToLocalTime() <= sla ? "Cumplido" : "Vencido";
            }

            return nowLocal <= sla ? "En curso" : "Vencido";
        }

        private static string GetPriorityLabel(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => "Baja",
                PriorityLevel.Medium => "Media",
                PriorityLevel.High => "Alta",
                PriorityLevel.Critical => "Critica",
                _ => priority.ToString()
            };
        }

        private static string GetStatusLabel(TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Open => "Abierto",
                TicketStatus.InProgress => "En progreso",
                TicketStatus.Resolved => "Resuelto",
                TicketStatus.Closed => "Cerrado",
                _ => status.ToString()
            };
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 0)
            {
                span = TimeSpan.Zero;
            }

            var days = (int)Math.Floor(span.TotalDays);
            var hours = span.Hours;
            var minutes = span.Minutes;

            if (days > 0)
            {
                return $"{days}d {hours}h {minutes}m";
            }

            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }

            return $"{minutes}m";
        }

        private static string? Safe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string FormatCurrency(decimal? value, string symbol)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", symbol, value.Value)
                : "No aplica";
        }

        private static string FormatDecimal(decimal? value, string suffix = "")
        {
            return value.HasValue
                ? $"{value.Value:N2}{suffix}"
                : "No especificado";
        }

        private sealed record RowItem(string Label, string Value);

        private sealed record SpareDispatchRow(
            string PartLabel,
            int Quantity,
            decimal UnitCostCordoba,
            decimal TotalCostCordoba,
            decimal UnitCostUsd,
            decimal TotalCostUsd);

        private sealed record EvidenceData(string Title, byte[]? ImageBytes, string Description);

        private sealed record HistoryRow(
            string ChangeDate,
            string Field,
            string OldValue,
            string NewValue,
            string ChangedBy);
    }
}



