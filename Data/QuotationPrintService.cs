using System.Diagnostics;
using System.Globalization;
using System.IO;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CamperoDesktop.Data;

public static class QuotationPrintService
{
    static QuotationPrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string GenerateQuotationPdf(QuotationDocument quotation)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campero", "Cotizaciones");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Cotizacion_{quotation.Numero}_{quotation.Fecha:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(90).AlignMiddle().Element(c =>
                        {
                            string? logoPath = BusinessBranding.GetLogoPath();
                            if (!string.IsNullOrWhiteSpace(logoPath))
                            {
                                c.Height(60).Image(logoPath).FitArea();
                            }
                        });

                        row.RelativeItem().PaddingLeft(8).Column(inner =>
                        {
                            inner.Item().Text(BusinessBranding.BusinessName).Bold().FontSize(18);
                            inner.Item().Text(BusinessBranding.Address).FontSize(9);
                            inner.Item().Text($"Tel: {BusinessBranding.Phone}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Nit))
                            {
                                inner.Item().Text($"NIT: {BusinessBranding.Nit}").FontSize(9);
                            }
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Email))
                            {
                                inner.Item().Text($"Email: {BusinessBranding.Email}").FontSize(9);
                            }
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Branch))
                            {
                                inner.Item().Text($"Sucursal: {BusinessBranding.Branch}").FontSize(9);
                            }
                        });

                        row.ConstantItem(180).AlignRight().Column(inner =>
                        {
                            inner.Item().AlignRight().Text("COTIZACION").Bold().FontSize(18);
                            inner.Item().AlignRight().Text($"Nro: {quotation.Numero}").FontSize(10);
                            inner.Item().AlignRight().Text($"Fecha: {quotation.Fecha:dd/MM/yyyy HH:mm}").FontSize(10);
                        });
                    });

                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Cliente: ").Bold();
                            text.Span(string.IsNullOrWhiteSpace(quotation.Cliente) ? "S/N" : quotation.Cliente);
                        });
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("CI/NIT: ").Bold();
                            text.Span(string.IsNullOrWhiteSpace(quotation.CiNit) ? "S/N" : quotation.CiNit);
                        });
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Vendedor: ").Bold();
                        text.Span(quotation.Vendedor);
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(0.9f);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Codigo");
                            HeaderCell(header.Cell(), "Descripcion");
                            HeaderCell(header.Cell(), "Unidad");
                            HeaderCell(header.Cell(), "Cant.");
                            HeaderCell(header.Cell(), "P. Unit");
                            HeaderCell(header.Cell(), "Subtotal");
                        });

                        foreach (SaleReceiptItem item in quotation.Items)
                        {
                            BodyCell(table, item.Codigo);
                            BodyCell(table, item.Descripcion);
                            BodyCell(table, item.UnidadMedida, true);
                            BodyCell(table, item.Cantidad.ToString("N2", CultureInfo.InvariantCulture), true);
                            BodyCell(table, item.PrecioUnitario.ToString("N2", CultureInfo.InvariantCulture), true);
                            BodyCell(table, item.Subtotal.ToString("N2", CultureInfo.InvariantCulture), true);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(quotation.Observaciones))
                    {
                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Text(text =>
                        {
                            text.Span("Observaciones: ").Bold();
                            text.Span(quotation.Observaciones);
                        });
                    }

                    column.Item().AlignRight().Width(220).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1f);
                        });

                        SummaryRow(table, "Subtotal Bs", quotation.Subtotal);
                        SummaryRow(table, "Descuento Bs", quotation.Descuento);
                        SummaryRow(table, "Total Bs", quotation.Total, true);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Cotizacion valida por 15 dias. ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.Span(BusinessBranding.BusinessName).SemiBold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(BusinessBranding.FooterMessage))
                    {
                        text.Span(" · ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        text.Span(BusinessBranding.FooterMessage).FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    public static void OpenPdf(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static void HeaderCell(IContainer container, string value)
    {
        container.Background(Colors.Blue.Medium).Padding(6).Text(value).FontColor(Colors.White).SemiBold().AlignCenter();
    }

    private static void BodyCell(TableDescriptor table, string value, bool right = false)
    {
        var text = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(value ?? string.Empty);
        if (right)
        {
            text.AlignRight();
        }
    }

    private static void SummaryRow(TableDescriptor table, string label, decimal value, bool bold = false)
    {
        var left = table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(label);
        var right = table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(value.ToString("N2", CultureInfo.InvariantCulture));

        if (bold)
        {
            left.Bold();
            right.Bold();
        }
    }
}
