namespace CamperoDesktop.Models;

public class QuotationLoadResult
{
    public int IdCotizacion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int? IdCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string CiNitCliente { get; set; } = string.Empty;
    public int IdAlmacen { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Descuento { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public List<SaleDetailDraft> Items { get; set; } = new();
}
