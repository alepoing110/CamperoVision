using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Microsoft.Win32;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using System.IO;

namespace CamperoDesktop.Data;

public static class ReportExportService
{
    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public static string GetExportPath(string title)
    {
        SaveFileDialog dialog = new()
        {
            Title = $"Exportar {title} - Campero",
            Filter = "PDF|*.pdf|Excel|*.xlsx|ZIP|*.zip",
            FileName = $"{SanitizeFileName(title)}_{DateTime.Now:yyyyMMdd}"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
    }

    public static void ExportSalesByDayToPdf(List<SalesByDayReportItem> data, string path)
    {
        ExportTablePdf("Ventas por Dia", new[] { "Fecha", "Notas", "Total" }, data.Select(x => new[] { x.Fecha.ToString("dd/MM/yyyy"), x.CantidadNotas.ToString(), x.TotalVendido.ToString("N2") }).ToList(), path);
    }

    public static void ExportSalesByDayToExcel(List<SalesByDayReportItem> data, string path)
    {
        ExportTableExcel("Ventas por Dia", new[] { "Fecha", "Notas", "Total Vendido" }, data.Select(x => new object[] { x.Fecha.ToString("dd/MM/yyyy"), x.CantidadNotas, x.TotalVendido }).ToList(), path);
    }

    public static void ExportTopProductsToPdf(List<TopProductReportItem> data, string path)
    {
        ExportTablePdf("Top Productos", new[] { "Codigo", "Producto", "Cantidad", "Total" }, data.Select(x => new[] { x.Codigo, x.Producto, x.CantidadVendida.ToString(), x.TotalVendido.ToString("N2") }).ToList(), path);
    }

    public static void ExportTopProductsToExcel(List<TopProductReportItem> data, string path)
    {
        ExportTableExcel("Top Productos", new[] { "Codigo", "Producto", "Cantidad Vendida", "Total Vendido" }, data.Select(x => new object[] { x.Codigo, x.Producto, x.CantidadVendida, x.TotalVendido }).ToList(), path);
    }

    public static void ExportProductSalesToPdf(List<ProductSalesReportItem> data, string path)
    {
        ExportTablePdf("Ventas por Producto", new[] { "Codigo", "Producto", "Cantidad", "P. Promedio", "Descuento", "Total" }, data.Select(x => new[] { x.Codigo, x.Producto, x.CantidadVendida.ToString(), x.PrecioPromedio.ToString("N2"), x.DescuentoTotal.ToString("N2"), x.TotalVendido.ToString("N2") }).ToList(), path);
    }

    public static void ExportProductSalesToExcel(List<ProductSalesReportItem> data, string path)
    {
        ExportTableExcel("Ventas por Producto", new[] { "Codigo", "Producto", "Cantidad Vendida", "Precio Promedio", "Descuento Total", "Total Vendido" }, data.Select(x => new object[] { x.Codigo, x.Producto, x.CantidadVendida, x.PrecioPromedio, x.DescuentoTotal, x.TotalVendido }).ToList(), path);
    }

    public static void ExportLowStockToPdf(List<LowStockReportItem> data, string path)
    {
        ExportTablePdf("Stock Bajo", new[] { "Almacen", "Codigo", "Producto", "Cantidad", "Minimo" }, data.Select(x => new[] { x.Almacen, x.Codigo, x.Producto, x.Cantidad.ToString(), x.StockMinimo.ToString() }).ToList(), path);
    }

    public static void ExportLowStockToExcel(List<LowStockReportItem> data, string path)
    {
        ExportTableExcel("Stock Bajo", new[] { "Almacen", "Codigo", "Producto", "Cantidad", "Stock Minimo" }, data.Select(x => new object[] { x.Almacen, x.Codigo, x.Producto, x.Cantidad, x.StockMinimo }).ToList(), path);
    }

    public static void ExportSalesByClientToPdf(List<SalesByClientReportItem> data, string path)
    {
        ExportTablePdf("Ventas por Cliente", new[] { "Cliente", "CI/NIT", "Notas", "Total" }, data.Select(x => new[] { x.Cliente, x.CiNit, x.CantidadNotas.ToString(), x.TotalVendido.ToString("N2") }).ToList(), path);
    }

    public static void ExportSalesByClientToExcel(List<SalesByClientReportItem> data, string path)
    {
        ExportTableExcel("Ventas por Cliente", new[] { "Cliente", "CI/NIT", "Cantidad Notas", "Total Vendido" }, data.Select(x => new object[] { x.Cliente, x.CiNit, x.CantidadNotas, x.TotalVendido }).ToList(), path);
    }

    public static void ExportInventoryMovementsToPdf(List<InventoryMovementReportItem> data, string path)
    {
        ExportTablePdf("Movimientos de Inventario", new[] { "Fecha", "Producto", "Almacen", "Tipo", "Cantidad", "Motivo" }, data.Select(x => new[] { x.Fecha.ToString("dd/MM/yyyy HH:mm"), x.Producto, x.Almacen, x.Tipo, x.Cantidad.ToString(), x.Motivo }).ToList(), path);
    }

    public static void ExportInventoryMovementsToExcel(List<InventoryMovementReportItem> data, string path)
    {
        ExportTableExcel("Movimientos Inventario", new[] { "Fecha", "Producto", "Almacen", "Tipo", "Cantidad", "Motivo" }, data.Select(x => new object[] { x.Fecha.ToString("dd/MM/yyyy HH:mm"), x.Producto, x.Almacen, x.Tipo, x.Cantidad, x.Motivo }).ToList(), path);
    }

    private static void ExportTablePdf(string reportName, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string path)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(90).AlignMiddle().Element(container =>
                        {
                            string? logoPath = BusinessBranding.GetLogoPath();
                            if (!string.IsNullOrWhiteSpace(logoPath))
                            {
                                container.Height(52).Image(logoPath).FitArea();
                            }
                        });

                        row.RelativeItem().PaddingLeft(8).Column(inner =>
                        {
                            inner.Item().Text(BusinessBranding.BusinessName).FontSize(19).Bold().FontColor(Colors.Blue.Darken3);
                            inner.Item().Text("Sistema de Inventario y Ventas").FontSize(11).FontColor(Colors.Grey.Darken2);
                            inner.Item().Text(BusinessBranding.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                            inner.Item().Text($"Tel: {BusinessBranding.Phone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Nit))
                            {
                                inner.Item().Text($"NIT: {BusinessBranding.Nit}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            }
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Email))
                            {
                                inner.Item().Text($"Email: {BusinessBranding.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            }
                            if (!string.IsNullOrWhiteSpace(BusinessBranding.Branch))
                            {
                                inner.Item().Text($"Sucursal: {BusinessBranding.Branch}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            }
                        });
                        row.ConstantItem(220).AlignRight().Column(inner =>
                        {
                            inner.Item().AlignRight().Text($"Reporte: {reportName}").SemiBold().FontSize(15);
                            inner.Item().AlignRight().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    column.Item().LineHorizontal(1).LineColor(Colors.Blue.Lighten2);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (string _ in headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (string item in headers)
                        {
                            header.Cell().Background(Colors.Blue.Medium).Padding(6).Text(item).FontColor(Colors.White).SemiBold();
                        }
                    });

                    foreach (string[] row in rows)
                    {
                        foreach (string cell in row)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(cell ?? string.Empty);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"{BusinessBranding.BusinessName} · Reportes del sistema · Tel. {BusinessBranding.Phone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(BusinessBranding.FooterMessage))
                    {
                        text.Span($" · {BusinessBranding.FooterMessage}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
            });
        }).GeneratePdf(path);
    }

    private static void ExportTableExcel(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<object[]> rows, string path)
    {
        using ExcelPackage package = new();
        var ws = package.Workbook.Worksheets.Add(sheetName);

        ws.Cells[1, 1].Value = BusinessBranding.BusinessName;
        ws.Cells[2, 1].Value = BusinessBranding.Address;
        ws.Cells[3, 1].Value = $"Tel: {BusinessBranding.Phone}";
        ws.Cells[4, 1].Value = string.IsNullOrWhiteSpace(BusinessBranding.Nit) ? string.Empty : $"NIT: {BusinessBranding.Nit}";
        ws.Cells[5, 1].Value = string.IsNullOrWhiteSpace(BusinessBranding.Email) ? string.Empty : $"Email: {BusinessBranding.Email}";
        ws.Cells[6, 1].Value = string.IsNullOrWhiteSpace(BusinessBranding.Branch) ? string.Empty : $"Sucursal: {BusinessBranding.Branch}";
        ws.Cells[7, 1].Value = $"Reporte: {sheetName}";
        ws.Cells[8, 1].Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cells[1, 1, 1, headers.Count].Merge = true;
        ws.Cells[2, 1, 2, headers.Count].Merge = true;
        ws.Cells[3, 1, 3, headers.Count].Merge = true;
        ws.Cells[4, 1, 4, headers.Count].Merge = true;
        ws.Cells[5, 1, 5, headers.Count].Merge = true;
        ws.Cells[6, 1, 6, headers.Count].Merge = true;
        ws.Cells[7, 1, 7, headers.Count].Merge = true;
        ws.Cells[8, 1, 8, headers.Count].Merge = true;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Size = 16;
        ws.Cells[7, 1].Style.Font.Bold = true;

        for (int i = 0; i < headers.Count; i++)
        {
            ws.Cells[10, i + 1].Value = headers[i];
        }

        using (var headerRange = ws.Cells[10, 1, 10, headers.Count])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (int colIndex = 0; colIndex < headers.Count; colIndex++)
            {
                ws.Cells[rowIndex + 11, colIndex + 1].Value = rows[rowIndex][colIndex];
            }
        }

        ws.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value.Replace(' ', '_');
    }
}
