namespace CamperoDesktop.Models;

public class InventoryAdjustmentRequest
{
    public int IdProducto { get; set; }
    public int IdAlmacen { get; set; }
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
}
