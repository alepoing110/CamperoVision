namespace CamperoDesktop.Models;

public class UserSession
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}
