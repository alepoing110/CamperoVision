namespace CamperoDesktop.Models;

public class InventoryMovementItem
{
    public int IdMovimiento { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public string Almacen { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}
