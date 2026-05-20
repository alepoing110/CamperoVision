namespace CamperoDesktop.Models;

public class TopProductReportItem
{
    public string Codigo { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal TotalVendido { get; set; }
}
