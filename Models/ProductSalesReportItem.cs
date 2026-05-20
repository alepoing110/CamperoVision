namespace CamperoDesktop.Models;

public class ProductSalesReportItem
{
    public string Codigo { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal PrecioPromedio { get; set; }
    public decimal DescuentoTotal { get; set; }
    public decimal TotalVendido { get; set; }
}
