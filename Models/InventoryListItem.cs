namespace CamperoDesktop.Models;

public class InventoryListItem
{
    public int IdInventario { get; set; }
    public int IdProducto { get; set; }
    public int IdAlmacen { get; set; }
    public string Almacen { get; set; } = string.Empty;
    public string CodigoProducto { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int StockMinimo { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
