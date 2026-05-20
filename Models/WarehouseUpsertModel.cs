namespace CamperoDesktop.Models;

public class WarehouseUpsertModel
{
    public int IdAlmacen { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
