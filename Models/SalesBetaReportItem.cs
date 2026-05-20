namespace CamperoDesktop.Models;

public class SalesBetaReportItem
{
    public string Codigo { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public string Almacen { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal PrecioVentaPromedio { get; set; }
    public decimal PrecioCompraReferencia { get; set; }
    public decimal TotalVendido { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal GananciaUnitaria => PrecioVentaPromedio - PrecioCompraReferencia;
    public decimal GananciaTotal => TotalVendido - CostoTotal;
}
