namespace CamperoDesktop.Models;

public class InventoryMovementReportItem
{
    public DateTime Fecha { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string Almacen { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

