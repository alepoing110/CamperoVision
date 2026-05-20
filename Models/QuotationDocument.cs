namespace CamperoDesktop.Models;

public class QuotationDocument
{
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string CiNit { get; set; } = string.Empty;
    public string Vendedor { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public List<SaleReceiptItem> Items { get; set; } = new();
}
