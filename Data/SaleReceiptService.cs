using System.Diagnostics;
using System.Globalization;
using System.IO;
using CamperoDesktop.Models;
using CamperoDesktop.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CamperoDesktop.Data;

public static class SaleReceiptService
{
    static SaleReceiptService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string GenerateReceiptPdf(SaleReceiptDocument receipt)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campero", "NotasVenta");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"Nota_{SanitizeFileName(receipt.NroNota)}_{receipt.Fecha:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(90).AlignMiddle().Element(container =>
                        {
                            string? logoPath = BusinessBranding.GetLogoPath();
                            if (!string.IsNullOrWhiteSpace(logoPath))
                            {
                                container.Height(60).Image(logoPath).FitArea();
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
                    });

                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(text =>
                            {
                                text.Span("Fecha : ").Bold();
                                text.Span(receipt.Fecha.ToString("dd/MM/yyyy hh:mm tt", CultureInfo.InvariantCulture));
                            });
                            left.Item().PaddingTop(4).Text(text =>
                            {
                                text.Span("Nombre/Razon Social: ").Bold();
                                text.Span(string.IsNullOrWhiteSpace(receipt.NombreCliente) ? "S/N" : receipt.NombreCliente);
                            });
                        });

                        row.ConstantItem(190).Column(right =>
                        {
                            right.Item().AlignRight().Text(text =>
                            {
                                text.Span("NIT/CI/CEX: ").Bold();
                                text.Span(string.IsNullOrWhiteSpace(receipt.CiNit) ? "S/N" : receipt.CiNit);
                            });
                            right.Item().PaddingTop(4).AlignRight().Text(text =>
                            {
                                text.Span("Cod. Cliente : ").Bold();
                                text.Span(string.IsNullOrWhiteSpace(receipt.CodigoCliente) ? "0" : receipt.CodigoCliente);
                            });
                            right.Item().PaddingTop(4).AlignRight().Text(text =>
                            {
                                text.Span("Nota : ").Bold();
                                text.Span(receipt.NroNota);
                            });
                        });
                    });

                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(0.85f);
                            columns.RelativeColumn(0.95f);
                            columns.RelativeColumn(1.95f);
                            columns.RelativeColumn(1.05f);
                            columns.RelativeColumn(1.05f);
                            columns.RelativeColumn(0.95f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("CODIGO\nPRODUCTO /\nSERVICIO").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("CANTIDAD").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("UNIDAD DE\nMEDIDA").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("DESCRIPCION").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("PRECIO\nUNITARIO").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("DESCUENTO").Bold().AlignCenter();
                            header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignMiddle().Text("SUBTOTAL").Bold().AlignCenter();
                        });

                        foreach (var item in receipt.Items)
                        {
                            BodyCell(table, item.Codigo, "center");
                            BodyCell(table, item.Cantidad.ToString("N2"), "center");
                            BodyCell(table, FormatUnit(item.UnidadMedida), "center");
                            BodyCell(table, item.Descripcion, "center");
                            BodyCell(table, item.PrecioUnitario.ToString("N2"), "right");
                            BodyCell(table, item.Descuento.ToString("N2"), "right");
                            BodyCell(table, item.Subtotal.ToString("N2"), "right");
                        }
                    });

                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().PaddingTop(50).Text(text =>
                        {
                            text.Span("Son : ").Bold();
                            text.Span($"{ConvertAmountToWords(receipt.Total)} Bolivianos");
                        });

                        row.ConstantItem(250).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.8f);
                                columns.RelativeColumn(1f);
                            });

                            SummaryRow(table, "SUBTOTAL Bs", receipt.Subtotal);
                            SummaryRow(table, "DESCUENTO Bs", receipt.Descuento);
                            SummaryRow(table, "TOTAL Bs", receipt.Total, true);
                            SummaryRow(table, "MONTO RECIBIDO Bs", receipt.MontoRecibido, true);
                            SummaryRow(table, "CAMBIO Bs", receipt.Cambio, true);
                            SummaryRow(table, "MONTO GIFT CARDBs", receipt.MontoGiftCard);
                            SummaryRow(table, "MONTO A PAGAR Bs", receipt.Total - receipt.MontoGiftCard, true);
                            SummaryRow(table, "IMPORTE BASE CREDITO FISCAL Bs", receipt.Total, true);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(BusinessBranding.FooterMessage))
                    {
                        column.Item().PaddingTop(8).AlignCenter().Text(BusinessBranding.FooterMessage).FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    public static bool TryPrintReceipt(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void OpenReceipt(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static void BodyCell(TableDescriptor table, string text, string align)
    {
        var cell = table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(6).PaddingHorizontal(5).AlignMiddle();
        var descriptor = cell.Text(text);

        if (align == "right")
        {
            descriptor.AlignRight();
        }
        else if (align == "center")
        {
            descriptor.AlignCenter();
        }
        else
        {
            descriptor.AlignLeft();
        }
    }

    private static void SummaryRow(TableDescriptor table, string label, decimal value, bool bold = false)
    {
        var labelCell = table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignRight().Text(label);
        var valueCell = table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Padding(5).AlignRight().Text(value.ToString("N2"));

        if (bold)
        {
            labelCell.Bold();
            valueCell.Bold();
        }
    }

    private static string FormatUnit(string unit)
    {
        string normalized = string.IsNullOrWhiteSpace(unit) ? "UNIDAD" : unit.Trim().ToUpperInvariant();
        return $"{normalized}\n(BIENES)";
    }

    private static string ConvertAmountToWords(decimal amount)
    {
        long integerPart = (long)Math.Floor(amount);
        int cents = (int)Math.Round((amount - integerPart) * 100m, MidpointRounding.AwayFromZero);
        if (cents == 100)
        {
            integerPart += 1;
            cents = 0;
        }

        return $"{ConvertNumber(integerPart)} {cents:00}/100";
    }

    private static string ConvertNumber(long number)
    {
        if (number == 0) return "Cero";
        if (number < 0) return "Menos " + ConvertNumber(Math.Abs(number));
        if (number < 30) return Units[number];
        if (number < 100) return ConvertTens(number);
        if (number < 1000) return ConvertHundreds(number);
        if (number < 1000000) return ConvertThousands(number);
        if (number < 1000000000000) return ConvertMillions(number);

        return number.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string ConvertTens(long number)
    {
        if (number < 30) return Units[number];

        long tens = number / 10;
        long units = number % 10;
        string tensText = Tens[tens];
        if (units == 0) return tensText;
        if (tens == 2) return "Veinti" + Units[units].ToLowerInvariant();
        return tensText + " y " + Units[units].ToLowerInvariant();
    }

    private static string ConvertHundreds(long number)
    {
        if (number == 100) return "Cien";

        long hundreds = number / 100;
        long remainder = number % 100;
        string hundredsText = Hundreds[hundreds];
        if (remainder == 0) return hundredsText;
        return hundredsText + " " + ConvertNumber(remainder).ToLowerInvariant();
    }

    private static string ConvertThousands(long number)
    {
        long thousands = number / 1000;
        long remainder = number % 1000;
        string thousandsText = thousands == 1 ? "Mil" : ConvertNumber(thousands) + " mil";
        if (remainder == 0) return thousandsText;
        return thousandsText + " " + ConvertNumber(remainder).ToLowerInvariant();
    }

    private static string ConvertMillions(long number)
    {
        long millions = number / 1000000;
        long remainder = number % 1000000;
        string millionsText = millions == 1 ? "Un millón" : ConvertNumber(millions) + " millones";
        if (remainder == 0) return millionsText;
        return millionsText + " " + ConvertNumber(remainder).ToLowerInvariant();
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value.Replace(' ', '_');
    }

    private static readonly Dictionary<long, string> Units = new()
    {
        [0] = "Cero",
        [1] = "Uno",
        [2] = "Dos",
        [3] = "Tres",
        [4] = "Cuatro",
        [5] = "Cinco",
        [6] = "Seis",
        [7] = "Siete",
        [8] = "Ocho",
        [9] = "Nueve",
        [10] = "Diez",
        [11] = "Once",
        [12] = "Doce",
        [13] = "Trece",
        [14] = "Catorce",
        [15] = "Quince",
        [16] = "Dieciséis",
        [17] = "Diecisiete",
        [18] = "Dieciocho",
        [19] = "Diecinueve",
        [20] = "Veinte",
        [21] = "Veintiuno",
        [22] = "Veintidós",
        [23] = "Veintitrés",
        [24] = "Veinticuatro",
        [25] = "Veinticinco",
        [26] = "Veintiséis",
        [27] = "Veintisiete",
        [28] = "Veintiocho",
        [29] = "Veintinueve"
    };

    private static readonly Dictionary<long, string> Tens = new()
    {
        [3] = "Treinta",
        [4] = "Cuarenta",
        [5] = "Cincuenta",
        [6] = "Sesenta",
        [7] = "Setenta",
        [8] = "Ochenta",
        [9] = "Noventa"
    };

    private static readonly Dictionary<long, string> Hundreds = new()
    {
        [1] = "Ciento",
        [2] = "Doscientos",
        [3] = "Trescientos",
        [4] = "Cuatrocientos",
        [5] = "Quinientos",
        [6] = "Seiscientos",
        [7] = "Setecientos",
        [8] = "Ochocientos",
        [9] = "Novecientos"
    };
}
