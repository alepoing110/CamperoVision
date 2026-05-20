namespace CamperoDesktop.Models;

public class ProveedorUpsertModel
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = "";
    public string Nit { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string Direccion { get; set; } = "";
    public bool Activo { get; set; } = true;
}
