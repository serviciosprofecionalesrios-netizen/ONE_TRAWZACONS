using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITServiceDeskApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class UsersPdfReportService
    {
        public static byte[] GenerateUsersReportPdf(IReadOnlyList<User> users, string webRootPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var generatedAt = DateTime.Now;
            var logo = TryLoadLogo(webRootPath);

            var total = users.Count;
            var active = users.Count(u => u.IsActive);
            var inactive = total - active;

            var byRole = users
                .GroupBy(u => RoleLabel(u.Role))
                .Select(g => new RoleCountRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Role)
                .ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(header => ComposeHeader(header, generatedAt, logo));
                    page.Content().Element(content => ComposeContent(content, users, total, active, inactive, byRole));
                    page.Footer().Element(footer => ComposeFooter(footer, generatedAt));
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, DateTime generatedAt, byte[]? logo)
        {
            container.BorderBottom(2)
                .BorderColor("#C91010")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(64).AlignMiddle().AlignCenter().Element(logoBox =>
                    {
                        if (logo != null)
                        {
                            logoBox.Image(logo).FitArea();
                        }
                        else
                        {
                            logoBox.Text("SIN LOGO").FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(column =>
                    {
                        column.Item().Text("GRUPO TRANSAN")
                            .FontSize(18)
                            .SemiBold()
                            .FontColor("#C91010");
                        column.Item().Text("Service Desk - Reporte de Usuarios")
                            .FontSize(12)
                            .SemiBold()
                            .FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(2).Text("Modulo de Usuarios")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento").FontSize(8).FontColor(Colors.Grey.Darken1);
                        column.Item().Text("REPORTE DE USUARIOS").SemiBold().FontSize(10);
                        column.Item().PaddingTop(2).Text($"Generado: {generatedAt:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            IReadOnlyList<User> users,
            int total,
            int active,
            int inactive,
            IReadOnlyList<RoleCountRow> byRole)
        {
            container.PaddingTop(12).Column(column =>
            {
                column.Spacing(10);

                column.Item().Element(card => ComposeSectionCard(card, "Resumen Corporativo", body =>
                {
                    body.Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text($"Se registran {total} usuarios en el sistema, de los cuales {active} se encuentran activos y {inactive} inactivos.");
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Total", total.ToString(), "#0B1D3A"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Activos", active.ToString(), "#0B6E4F"));
                            row.RelativeItem().Element(c => ComposeMetricCard(c, "Inactivos", inactive.ToString(), "#C91010"));
                        });
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Distribucion por Rol", body =>
                {
                    if (byRole.Count == 0)
                    {
                        body.Text("Sin datos de roles.").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    body.Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Rol");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        });

                        foreach (var row in byRole)
                        {
                            table.Cell().Element(BodyCell).Text(row.Role);
                            table.Cell().Element(BodyCell).AlignRight().Text(row.Count.ToString());
                        }
                    });
                }));

                column.Item().Element(card => ComposeSectionCard(card, "Listado de Usuarios", body =>
                {
                    if (users.Count == 0)
                    {
                        body.Text("No hay usuarios registrados.").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    body.Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(2.8f);
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Nombre");
                            header.Cell().Element(HeaderCell).Text("Correo");
                            header.Cell().Element(HeaderCell).Text("Departamento");
                            header.Cell().Element(HeaderCell).Text("Rol");
                            header.Cell().Element(HeaderCell).Text("Estado");
                            header.Cell().Element(HeaderCell).Text("Creado");
                        });

                        foreach (var user in users.OrderBy(u => u.FullName))
                        {
                            table.Cell().Element(BodyCell).Text(user.FullName);
                            table.Cell().Element(BodyCell).Text(user.Email);
                            table.Cell().Element(BodyCell).Text(user.Department);
                            table.Cell().Element(BodyCell).Text(RoleLabel(user.Role));
                            table.Cell().Element(BodyCell).Text(user.IsActive ? "Activo" : "Inactivo");
                            table.Cell().Element(BodyCell).Text(user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"));
                        }
                    });
                }));
            });
        }

        private static void ComposeFooter(IContainer container, DateTime generatedAt)
        {
            container.BorderTop(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingTop(5)
                .Row(row =>
                {
                    row.RelativeItem().Text($"Confidencial - Grupo Transan | {generatedAt:dd/MM/yyyy HH:mm}")
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
                        row.ConstantItem(4).Height(16).Background("#C91010");
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

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#C91010")
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#C91010").FontSize(9));
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .PaddingVertical(4)
                .DefaultTextStyle(TextStyle.Default.FontSize(9));
        }

        private static string RoleLabel(UserRole role)
        {
            return role switch
            {
                UserRole.Administrator => "Administrador",
                UserRole.Technician => "Tecnico IT",
                UserRole.EndUser => "Usuario Final",
                UserRole.CoordinadorIT => "Coordinador IT",
                UserRole.GerenciaGeneral => "Gerencia General",
                _ => role.ToString()
            };
        }

        private static byte[]? TryLoadLogo(string webRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(webRootPath, "images", "logo-transan-corporativo.png"),
                Path.Combine(webRootPath, "images", "logo-transan.png")
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
                    // Ignore and continue.
                }
            }

            return null;
        }

        private sealed record RoleCountRow(string Role, int Count);
    }
}
