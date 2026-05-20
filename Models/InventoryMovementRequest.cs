namespace CamperoDesktop.Models;

public class InventoryMovementRequest
{
    public int IdProducto { get; set; }
    public int IdAlmacen { get; set; }
    public int IdUsuario { get; set; }
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}
