namespace CamperoDesktop.Models;

public class ClientOption
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CiNit { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(CiNit) ? Nombre : $"{Nombre} - {CiNit}";
}
