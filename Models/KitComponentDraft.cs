namespace CamperoDesktop.Models;

public class KitComponentDraft
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = "unidad";
    public int Cantidad { get; set; }
    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }
    public int StockDisponible { get; set; }
    public decimal CostoReferencial => Cantidad * PrecioCompra;
}
