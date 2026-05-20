namespace CamperoDesktop.Models;

public class CategoryUpsertModel
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
