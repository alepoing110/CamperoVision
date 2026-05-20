namespace CamperoDesktop.Models;

public class ClientListItem
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CiNit { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
