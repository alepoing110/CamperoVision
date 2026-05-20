namespace CamperoDesktop.Models;

public class KitUpsertModel
{
    public int IdKit { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioVenta { get; set; }
    public bool Activo { get; set; } = true;
    public List<KitComponentDraft> Componentes { get; set; } = new();
}
