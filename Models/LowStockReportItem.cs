namespace CamperoDesktop.Models;

public class LowStockReportItem
{
    public string Almacen { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int StockMinimo { get; set; }
}
