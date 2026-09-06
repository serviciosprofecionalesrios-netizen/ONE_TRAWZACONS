using System;
using System.Globalization;
using System.IO;
using ITServiceDeskApp.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITServiceDeskApp.Services
{
    public static class FinanceInvoicePdfReportService
    {
        public static byte[] GeneratePdf(FinanceInvoice invoice, string webRootPath, string? signUrl)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var logoBytes = TryLoadLogo(webRootPath);
            var qrBytes = string.IsNullOrWhiteSpace(signUrl) ? null : BuildQrCode(signUrl);
            var signatureBytes = ExtractSignaturePng(invoice.SignatureDataUrl);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(h => ComposeHeader(h, invoice, logoBytes));
                    page.Content().Element(c => ComposeContent(c, invoice, signUrl, qrBytes, signatureBytes));
                    page.Footer().AlignRight().Text($"Generado {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, FinanceInvoice invoice, byte[]? logoBytes)
        {
            container.BorderBottom(2)
                .BorderColor("#11439A")
                .PaddingBottom(8)
                .Row(row =>
                {
                    row.ConstantItem(90).Height(58).AlignMiddle().AlignCenter().Element(slot =>
                    {
                        if (logoBytes != null)
                        {
                            slot.Image(logoBytes).FitArea();
                        }
                        else
                        {
                            slot.Text("SIN LOGO").FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.RelativeItem().PaddingLeft(10).Column(col =>
                    {
                        col.Item().Text("Finanzas - Formulario de Factura").SemiBold().FontSize(14).FontColor("#11439A");
                        col.Item().Text($"No. Factura: {invoice.InvoiceNumber}").FontSize(10).FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().Text("Control interno").FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"Creado por: {invoice.CreatedBy}").FontSize(9);
                        col.Item().Text($"Fecha creación: {invoice.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}").FontSize(9);
                    });
                });
        }

        private static void ComposeContent(
            IContainer container,
            FinanceInvoice invoice,
            string? signUrl,
            byte[]? qrBytes,
            byte[]? signatureBytes)
        {
            container.PaddingTop(14).Column(col =>
            {
                col.Spacing(10);

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                    });

                    AddRow(table, "No. Factura", invoice.InvoiceNumber);
                    AddRow(table, "Sitio / Sede", invoice.Site);
                    AddRow(table, "Cliente / Proveedor", invoice.CounterpartyName);
                    AddRow(table, "Monto", $"{invoice.Currency} {invoice.Amount.ToString("N2", CultureInfo.InvariantCulture)}");
                    AddRow(table, "Fecha factura", invoice.InvoiceDateUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                });

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(block =>
                {
                    block.Item().Text("Concepto").SemiBold().FontColor(Colors.Grey.Darken4);
                    block.Item().PaddingTop(6).Text(invoice.Concept);
                });

                if (!string.IsNullOrWhiteSpace(invoice.Notes))
                {
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(block =>
                    {
                        block.Item().Text("Notas").SemiBold().FontColor(Colors.Grey.Darken4);
                        block.Item().PaddingTop(6).Text(invoice.Notes!);
                    });
                }

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(block =>
                {
                    block.Item().Text("Firma del responsable").SemiBold().FontColor(Colors.Grey.Darken4);

                    if (signatureBytes != null)
                    {
                        block.Item().PaddingTop(8).Height(110).AlignLeft().Image(signatureBytes).FitHeight();
                        block.Item().PaddingTop(4).Text($"Firmado por {invoice.SignedByName} el {invoice.SignedAtUtc?.ToLocalTime():dd/MM/yyyy HH:mm}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    }
                    else
                    {
                        block.Item().PaddingTop(8).Text("Pendiente de firma electronica.").FontColor(Colors.Orange.Darken2);
                    }
                });

                if (!invoice.SignedAtUtc.HasValue && !string.IsNullOrWhiteSpace(signUrl))
                {
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(linkCol =>
                        {
                            linkCol.Item().Text("Firma electronica remota").SemiBold().FontColor(Colors.Grey.Darken4);
                            linkCol.Item().PaddingTop(6).Text("Abre este enlace en celular, laptop o cualquier dispositivo:");
                            linkCol.Item().PaddingTop(4).Text(signUrl).FontSize(9).FontColor(Colors.Blue.Darken2);
                        });

                        row.ConstantItem(108).Height(108).AlignMiddle().AlignCenter().Element(slot =>
                        {
                            if (qrBytes != null)
                            {
                                slot.Image(qrBytes).FitArea();
                            }
                        });
                    });
                }
            });
        }

        private static void AddRow(TableDescriptor table, string label, string value)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingRight(8)
                .Text(label).SemiBold().FontColor(Colors.Grey.Darken2);
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5)
                .Text(value).FontColor(Colors.Grey.Darken4);
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

        private static byte[]? BuildQrCode(string content)
        {
            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(data);
                return qrCode.GetGraphic(8);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ExtractSignaturePng(string? signatureDataUrl)
        {
            if (string.IsNullOrWhiteSpace(signatureDataUrl))
            {
                return null;
            }

            const string marker = "base64,";
            var index = signatureDataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var base64 = signatureDataUrl[(index + marker.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }
        }
    }
}
