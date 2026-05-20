namespace CamperoDesktop.Models;

public class PurchaseCreateRequest
{
    public int IdProveedor { get; set; }
    public int IdUsuario { get; set; }
    public int IdAlmacen { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Estado { get; set; } = "enviada";
    public string Observaciones { get; set; } = string.Empty;
    public List<DetalleOrdenCompraDraft> Items { get; set; } = new();
}
