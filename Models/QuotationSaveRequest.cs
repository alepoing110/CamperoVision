namespace CamperoDesktop.Models;

public class QuotationSaveRequest
{
    public string Codigo { get; set; } = string.Empty;
    public int? IdCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string CiNitCliente { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public int IdAlmacen { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public List<SaleDetailDraft> Items { get; set; } = new();
}
