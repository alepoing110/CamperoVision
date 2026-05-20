namespace CamperoDesktop.Models;

public class UserUpsertModel
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = UserRoles.Vendedor;
    public string Password { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
