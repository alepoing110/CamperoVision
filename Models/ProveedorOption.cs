namespace CamperoDesktop.Models;

public class ProveedorOption
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Nit) ? Nombre : $"{Nombre} - {Nit}";
}
