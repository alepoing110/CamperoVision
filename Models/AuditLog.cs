namespace CamperoDesktop.Models;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Tabla { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public int? RegistroId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public string IpOrigen { get; set; } = string.Empty;
}
