namespace CamperoDesktop.Models;

public class OrdenCompraListItem
{
    public int IdOrden { get; set; }
    public string Proveedor { get; set; } = "";
    public string Nit { get; set; } = "";
    public string Almacen { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Estado { get; set; } = "";
    public decimal Total { get; set; }
}
