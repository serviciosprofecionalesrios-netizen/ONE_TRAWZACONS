using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ITServiceDeskApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class MaintenanceAppointmentPdfReportService
    {
        public static byte[] GeneratePdf(MaintenanceAppointment appointment, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var logoBytes = TryLoadLogo(webRootPath);
            var generatedAt = DateTime.Now;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, appointment, logoBytes, generatedAt));
                    page.Content().Element(content => ComposeContent(content, appointment));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(
            IContainer container,
            MaintenanceAppointment appointment,
            byte[]? logoBytes,
            DateTime generatedAt)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(60).AlignMiddle().AlignCenter().Element(slot =>
                    {
                        if (logoBytes != null)
                        {
                            slot.Image(logoBytes).FitArea();
                        }
                        else
                        {
                            slot.Text("SIN LOGO")
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(column =>
                    {
                        column.Item().Text(string.Empty)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#11439A");
                        column.Item().Text("Service Desk - Cita de Mantenimiento")
                            .FontSize(12)
                            .SemiBold();
                        column.Item().Text($"Cita: {appointment.AppointmentNumber}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("CITA PROGRAMADA").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(IContainer container, MaintenanceAppointment appointment)
        {
            var (meta, userNotes) = MaintenanceAppointmentMetadataHelper.Parse(appointment.Notes);

            var details = new List<(string Label, string Value)>
            {
                ("Numero de cita", appointment.AppointmentNumber),
                ("Fecha y hora programada", appointment.ScheduledFor.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
                ("Solicitante", appointment.RequestingUser),
                ("Sitio / Ubicacion", appointment.Site),
                ("Tenencia", appointment.Tenencia),
                ("Tipo de mantenimiento", appointment.MaintenanceType),
                ("Activo / Area", appointment.AssetOrArea),
                ("Tecnico asignado", string.IsNullOrWhiteSpace(appointment.AssignedTechnician) ? "Sin asignar" : appointment.AssignedTechnician),
                ("Usuarios destino", string.IsNullOrWhiteSpace(appointment.RecipientUsers) ? "No definido" : appointment.RecipientUsers),
                ("Estado", appointment.Status),
                ("Orden relacionada", string.IsNullOrWhiteSpace(meta.MaintenanceOrderNumber) ? "Sin enlazar" : meta.MaintenanceOrderNumber!),
                ("Duracion estimada (h)", meta.EstimatedDurationHours?.ToString("0.##", CultureInfo.InvariantCulture) ?? "No definida"),
                ("Ingreso a taller", meta.WorkshopEntryAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "No definido"),
                ("Salida de taller", meta.WorkshopExitAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "No definida")
            };

            container.PaddingTop(14).Column(column =>
            {
                column.Spacing(12);

                column.Item().Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(10)
                    .Column(card =>
                    {
                        card.Item().Text("Detalle de la cita")
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);

                        card.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            foreach (var item in details)
                            {
                                table.Cell().Element(LabelCell).Text(item.Label);
                                table.Cell().Element(ValueCell).Text(item.Value);
                            }
                        });
                    });

                column.Item().Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(10)
                    .Column(card =>
                    {
                        card.Item().Text("Descripcion del trabajo")
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);
                        card.Item().PaddingTop(6).Text(appointment.Description);
                    });

                column.Item().Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(10)
                    .Column(card =>
                    {
                        card.Item().Text("Checklist tecnico")
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);

                        var checklistRows = new List<(string Label, string Value)>
                        {
                            ("Inspeccion inicial", meta.ChecklistInspectionCompleted ? "Completada" : "Pendiente"),
                            ("Repuestos validados", meta.ChecklistSparePartsCompleted ? "Completada" : "Pendiente"),
                            ("Prueba final", meta.ChecklistFinalTestCompleted ? "Completada" : "Pendiente"),
                            ("Conformidad usuario", meta.ChecklistUserConformityCompleted ? "Completada" : "Pendiente"),
                            ("Avance", $"{meta.GetChecklistProgressPercent()}%")
                        };

                        card.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            foreach (var item in checklistRows)
                            {
                                table.Cell().Element(LabelCell).Text(item.Label);
                                table.Cell().Element(ValueCell).Text(item.Value);
                            }
                        });
                    });

                column.Item().Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(10)
                    .Column(card =>
                    {
                        card.Item().Text("Evidencia de cierre")
                            .SemiBold()
                            .FontSize(12)
                            .FontColor(Colors.Grey.Darken4);
                        card.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(meta.CompletionEvidenceNotes) ? "Sin evidencia registrada." : meta.CompletionEvidenceNotes!);
                    });

                if (!string.IsNullOrWhiteSpace(userNotes))
                {
                    column.Item().Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(10)
                        .Column(card =>
                        {
                            card.Item().Text("Notas")
                                .SemiBold()
                                .FontSize(12)
                                .FontColor(Colors.Grey.Darken4);
                            card.Item().PaddingTop(6).Text(userNotes);
                        });
                }
            });
        }

        private static IContainer LabelCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .PaddingRight(8)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor(Colors.Grey.Darken2));
        }

        private static IContainer ValueCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .DefaultTextStyle(TextStyle.Default.FontColor(Colors.Grey.Darken4));
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
                    // Ignorar y continuar.
                }
            }

            return null;
        }
    }
}



