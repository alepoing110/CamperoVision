namespace CamperoDesktop.Models;

public class SaleCreateRequest
{
    public int? IdCliente { get; set; }
    public string NombreComprador { get; set; } = string.Empty;
    public string CiNitComprador { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public int IdAlmacen { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public decimal DescuentoAdicional { get; set; }
    public List<SaleDetailDraft> Items { get; set; } = new();
}
