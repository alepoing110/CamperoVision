namespace CamperoDesktop.Models;

public class KitListItem
{
    public int IdKit { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioVenta { get; set; }
    public bool Activo { get; set; }
    public int Componentes { get; set; }
    public int StockDisponible { get; set; }
}
